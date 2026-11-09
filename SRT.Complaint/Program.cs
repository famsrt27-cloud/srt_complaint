using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using SRT.Complaint.Data;
using SRT.Complaint.Filters;
using SRT.Complaint.Middleware;
using SRT.Complaint.OpenApi;
using SRT.Complaint.Services;
using StackExchange.Redis;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;

static X509Certificate2 LoadDataProtectionCertificate(DataProtectionRuntimeOptions options)
{
    if (!Enum.TryParse<StoreLocation>(options.CertificateStoreLocation, true, out var location))
        throw new InvalidOperationException("DataProtection:CertificateStoreLocation must be CurrentUser or LocalMachine");

    using var store = new X509Store(StoreName.My, location);
    store.Open(OpenFlags.ReadOnly);
    var thumbprint = options.CertificateThumbprint.Replace(" ", "", StringComparison.Ordinal);
    var certificate = store.Certificates
        .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
        .OfType<X509Certificate2>()
        .Where(x => x.HasPrivateKey)
        .OrderByDescending(x => x.NotAfter)
        .FirstOrDefault();
    if (certificate == null)
        throw new InvalidOperationException(
            $"Data Protection certificate {thumbprint} with a private key was not found in {location}/My");
    if (DateTime.Now < certificate.NotBefore || DateTime.Now > certificate.NotAfter)
        throw new InvalidOperationException(
            $"Data Protection certificate {thumbprint} is outside its validity period");
    return certificate;
}

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/srt-complaint-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var secureCookiePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .WriteTo.Console()
              .WriteTo.File("logs/srt-complaint-.log", rollingInterval: RollingInterval.Day));

    // ──────────── Database ────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddDbContext<CorruptionDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    var dataProtectionOptions = builder.Configuration
        .GetSection(DataProtectionRuntimeOptions.SectionName)
        .Get<DataProtectionRuntimeOptions>() ?? new DataProtectionRuntimeOptions();
    if (string.IsNullOrWhiteSpace(dataProtectionOptions.ApplicationName))
    {
        if (!builder.Environment.IsDevelopment())
            throw new InvalidOperationException("DataProtection:ApplicationName is required outside Development");
        dataProtectionOptions.ApplicationName = "SRT.Complaint.Development";
    }

    var dataProtection = builder.Services.AddDataProtection()
        .SetApplicationName(dataProtectionOptions.ApplicationName)
        .PersistKeysToDbContext<AppDbContext>();
    if (!string.IsNullOrWhiteSpace(dataProtectionOptions.CertificateThumbprint))
        dataProtection.ProtectKeysWithCertificate(LoadDataProtectionCertificate(dataProtectionOptions));
    else if (builder.Environment.IsProduction())
        throw new InvalidOperationException(
            "DataProtection:CertificateThumbprint is required in Production to protect the shared key ring at rest");

    // ──────────── Authentication & Authorization ────────────
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Cookies";
        options.DefaultSignInScheme = "Cookies";
        options.DefaultChallengeScheme = "Cookies";
    })
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Staff/Login";
        options.LogoutPath = "/Staff/Logout";
        options.AccessDeniedPath = "/Staff/AccessDenied";
        options.Cookie.Name = "SRT.StaffAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = secureCookiePolicy;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userIdValue = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var authVersionValue = context.Principal?.FindFirst("AuthVersion")?.Value;
                if (!int.TryParse(userIdValue, out var userId) ||
                    !int.TryParse(authVersionValue, out var authVersion))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync("Cookies");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var staff = await db.StaffUsers.AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => new { x.IsActive, x.Role, x.AuthVersion })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                var cookieRole = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (staff is null || !staff.IsActive || staff.AuthVersion != authVersion || staff.Role != cookieRole)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync("Cookies");
                }
            }
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("GeneralOfficer", policy => policy.RequireRole("GeneralOfficer"));
        options.AddPolicy("CorruptionOfficer", policy => policy.RequireRole("CorruptionOfficer"));
        options.AddPolicy("StaffOnly", policy => policy.RequireRole("GeneralOfficer", "CorruptionOfficer", "SuperAdmin"));
        options.AddPolicy("GeneralComplaintAccess", policy => policy.RequireRole("GeneralOfficer", "SuperAdmin"));
        options.AddPolicy("CorruptionAccess", policy => policy.RequireRole("CorruptionOfficer", "SuperAdmin"));
        options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
    });

    builder.Services.Configure<CookiePolicyOptions>(options =>
    {
        // Optional cookies are denied until an explicit consent mechanism grants them.
        // The application currently uses essential cookies only.
        options.CheckConsentNeeded = _ => true;
        options.MinimumSameSitePolicy = SameSiteMode.Lax;
        options.Secure = secureCookiePolicy;
        options.ConsentCookie.Name = "SRT.OptionalCookieConsent";
        options.ConsentCookie.HttpOnly = true;
        options.ConsentCookie.IsEssential = true;
        options.ConsentCookie.SameSite = SameSiteMode.Lax;
        options.ConsentCookie.SecurePolicy = secureCookiePolicy;
        options.ConsentCookie.MaxAge = TimeSpan.FromDays(180);
    });
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "SRT.Antiforgery";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = secureCookiePolicy;
    });

    // Trust forwarded client/protocol headers only from explicitly configured proxies.
    // This keeps IP allowlists and rate limits from accepting spoofed X-Forwarded-For values.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var value in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(value, out var address))
                options.KnownProxies.Add(address);
        }
    });

    // ──────────── Rate Limiting (partitioned per client IP) ────────────
    static string ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("SubmitPolicy", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIp(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
        options.AddPolicy("TrackVerifyPolicy", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIp(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
        options.AddPolicy("LoginPolicy", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(ClientIp(httpContext), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ──────────── Memory Cache (for API rate limiting) ────────────
    var distributedRuntime = builder.Configuration
        .GetSection(DistributedRuntimeOptions.SectionName)
        .Get<DistributedRuntimeOptions>() ?? new DistributedRuntimeOptions();
    var validRuntimeModes = new[] { "SingleInstance", "MultiInstance" };
    var validRateLimitProviders = new[] { "Local", "Redis", "Gateway" };
    if (!validRuntimeModes.Contains(distributedRuntime.Mode, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("DistributedRuntime:Mode must be SingleInstance or MultiInstance");
    if (!validRateLimitProviders.Contains(distributedRuntime.RateLimitProvider, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException("DistributedRuntime:RateLimitProvider must be Local, Redis, or Gateway");
    if (distributedRuntime.UsesRedisRateLimit && string.IsNullOrWhiteSpace(distributedRuntime.RedisConnection))
        throw new InvalidOperationException("DistributedRuntime:RedisConnection is required for Redis rate limiting");
    if (distributedRuntime.IsMultiInstance && string.IsNullOrWhiteSpace(distributedRuntime.RedisConnection))
        throw new InvalidOperationException("DistributedRuntime:RedisConnection is required for shared session state in MultiInstance mode");
    if (distributedRuntime.IsMultiInstance &&
        !distributedRuntime.UsesRedisRateLimit &&
        !distributedRuntime.UsesGatewayRateLimit)
        throw new InvalidOperationException("MultiInstance mode requires Redis or Gateway rate limiting");

    builder.Services.AddOptions<DistributedRuntimeOptions>()
        .Bind(builder.Configuration.GetSection(DistributedRuntimeOptions.SectionName))
        .Validate(x => x.EmergencyReadLimitPerMin is >= 1 and <= 3600,
            "DistributedRuntime:EmergencyReadLimitPerMin must be between 1 and 3600")
        .ValidateOnStart();

    builder.Services.AddMemoryCache();
    if (!string.IsNullOrWhiteSpace(distributedRuntime.RedisConnection))
    {
        var redisConfiguration = ConfigurationOptions.Parse(distributedRuntime.RedisConnection);
        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ClientName = $"SRT.Complaint.{builder.Environment.EnvironmentName}";
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = distributedRuntime.RedisConnection;
            options.InstanceName = $"{distributedRuntime.RedisInstanceName}cache:";
        });
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
    }
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.Name = "SRT.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = secureCookiePolicy;
    });

    // ──────────── Services ────────────
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IMaskingService, MaskingService>();
    builder.Services.AddScoped<ISlaService, SlaService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<IComplaintService, ComplaintService>();
    builder.Services.AddScoped<ICorruptionService, CorruptionService>();
    builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
    builder.Services.AddScoped<IPdfExportService, PdfExportService>();
    builder.Services.AddScoped<IPdfSignerSettingsService, PdfSignerSettingsService>();
    builder.Services.AddScoped<IApiRequestLogService, ApiRequestLogService>();
    builder.Services.AddScoped<IWebhookService, WebhookService>();
    builder.Services.AddScoped<IWebhookUrlValidator, WebhookUrlValidator>();
    builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
    builder.Services.AddScoped<ITermsService, TermsService>();
    builder.Services.AddScoped<IContentBlockService, ContentBlockService>();
    builder.Services.AddScoped<ITurnstileService, TurnstileService>();
    builder.Services.AddScoped<IStatsService, StatsService>();
    builder.Services.AddScoped<ICorruptionStatsService, CorruptionStatsService>();
    builder.Services.AddScoped<IExternalSyncService, ExternalSyncService>();
    builder.Services.AddScoped<IEdocIntegrationService, EdocIntegrationService>();
    builder.Services.AddSingleton<IEdocApiClient, EdocApiClient>();
    builder.Services.AddSingleton<IValidateOptions<EdocOptions>, EdocOptionsValidator>();
    builder.Services.AddOptions<EdocOptions>()
        .Bind(builder.Configuration.GetSection(EdocOptions.SectionName))
        .ValidateOnStart();
    builder.Services.AddScoped<IStaffUserService, StaffUserService>();
    builder.Services.AddScoped<IStaffDirectoryService, StaffDirectoryService>();
    builder.Services.AddScoped<IDiscordNotifier, DiscordNotifier>();
    builder.Services.AddSingleton<IExternalSystemAdapter, SRT.Complaint.Services.Adapters.DamrongdhammaAdapter>();
    builder.Services.AddSingleton<ApiRateLimitStore>();
    if (distributedRuntime.UsesRedisRateLimit)
        builder.Services.AddSingleton<IApiRateLimitStore, RedisApiRateLimitStore>();
    else
        builder.Services.AddSingleton<IApiRateLimitStore>(sp => sp.GetRequiredService<ApiRateLimitStore>());
    builder.Services.AddScoped<ApiKeyAuthFilter>();

    // ──────────── Background Services ────────────
    builder.Services.AddHostedService<SlaBackgroundService>();
    builder.Services.AddHostedService<WebhookRetryService>();
    builder.Services.AddHostedService<ExternalSyncBackgroundService>();
    builder.Services.AddHostedService<EdocBackgroundService>();

    // ──────────── MVC & Razor Pages ────────────
    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var details = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(error =>
                            string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid value" : error.ErrorMessage).ToArray());
                return new BadRequestObjectResult(new
                {
                    error = new
                    {
                        code = "validation_error",
                        message = "Request validation failed",
                        details,
                        traceId = context.HttpContext.TraceIdentifier
                    }
                });
            };
        });
    builder.Services.AddRazorPages();
    builder.Services.AddOpenApi("v1", options =>
    {
        options.ShouldInclude = description => description.GroupName == "v1";
        options.AddDocumentTransformer<ApiOpenApiTransformer>();
    });

    // ──────────── HTTP Clients ────────────
    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient("Webhook", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    }).ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        WebhookHttpMessageHandlerFactory.Create(serviceProvider.GetRequiredService<IConfiguration>()));
    builder.Services.AddHttpClient("Edoc", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }).ConfigurePrimaryHttpMessageHandler(EdocHttpMessageHandlerFactory.Create);

    // ──────────── File Upload ────────────
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        // Public complaint form accepts up to 50 MB of files; keep a small allowance
        // for the remaining multipart form fields and boundaries.
        options.MultipartBodyLengthLimit = 55L * 1024 * 1024;
    });

    var apiEnvironment = builder.Configuration["ApiManagement:Environment"];
    if (!builder.Environment.IsDevelopment() &&
        (apiEnvironment == null || !ApiIntegrationPolicy.AllowedEnvironments.Contains(apiEnvironment)))
    {
        throw new InvalidOperationException(
            "ApiManagement:Environment must be configured as Sandbox or Production outside Development");
    }

    var app = builder.Build();

    // ──────────── Seed SuperAdmin (first-run only) ────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.StaffUsers.Any(u => u.Role == "SuperAdmin"))
        {
            var bootstrapPassword = builder.Configuration["BootstrapAdmin:Password"];
            if (string.IsNullOrWhiteSpace(bootstrapPassword))
            {
                if (!app.Environment.IsDevelopment())
                    throw new InvalidOperationException("BootstrapAdmin:Password must be configured for the first production startup");
                Log.Warning("No SuperAdmin exists. Configure BootstrapAdmin:Password in user-secrets to create the first administrator");
            }
            else
            {
                db.StaffUsers.Add(new SRT.Complaint.Models.StaffUser
                {
                    EmployeeCode = "0000001",
                    FullName = "Super Administrator",
                    Email = "admin@railway.co.th",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapPassword, 12),
                    Role = "SuperAdmin",
                    IsActive = true,
                    MustChangePassword = true,
                    CreatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
                Log.Information("Seeded default SuperAdmin (EmployeeCode=0000001)");
            }
        }
    }

    // ──────────── Middleware Pipeline ────────────
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseMiddleware<ApiExceptionMiddleware>();
    app.UseStatusCodePages(async statusContext =>
    {
        var http = statusContext.HttpContext;
        if (!http.Request.Path.StartsWithSegments("/api"))
            return;

        var (code, message) = http.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => ("not_found", "API endpoint not found"),
            StatusCodes.Status405MethodNotAllowed => ("method_not_allowed", "HTTP method not allowed"),
            StatusCodes.Status415UnsupportedMediaType => ("unsupported_media_type", "Content-Type must be application/json"),
            _ => ("request_failed", "Request failed")
        };
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsJsonAsync(new
        {
            error = new { code, message, traceId = http.TraceIdentifier }
        });
    });

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseCookiePolicy();
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            if (context.Request.Path.StartsWithSegments("/api"))
                context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        await next(context);
    });
    app.UseStaticFiles();

    app.UseRouting();
    app.UseRateLimiter();
    app.UseSession();
    app.UseAuthentication();

    // Redirect to password change page if MustChangePassword claim is set
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim("MustChangePassword", "true"))
        {
            var path = context.Request.Path.Value ?? "";
            var isAllowed = path.StartsWith("/Staff/ChangePassword", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/Staff/Logout", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase);
            if (!isAllowed)
            {
                context.Response.Redirect("/Staff/ChangePassword");
                return;
            }
        }
        await next(context);
    });

    app.UseAuthorization();

    app.MapControllers();
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapStaticAssets();
    app.MapRazorPages().WithStaticAssets();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
