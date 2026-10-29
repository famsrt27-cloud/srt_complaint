using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using SRT.Complaint.Data;
using SRT.Complaint.Filters;
using SRT.Complaint.OpenApi;
using SRT.Complaint.Services;
using System.Net;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/srt-complaint-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .WriteTo.Console()
              .WriteTo.File("logs/srt-complaint-.log", rollingInterval: RollingInterval.Day));

    // ──────────── Database ────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddDbContext<CorruptionDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("GeneralOfficer", policy => policy.RequireRole("GeneralOfficer"));
        options.AddPolicy("CorruptionOfficer", policy => policy.RequireRole("CorruptionOfficer"));
        options.AddPolicy("StaffOnly", policy => policy.RequireRole("GeneralOfficer", "CorruptionOfficer", "SuperAdmin"));
        options.AddPolicy("CorruptionAccess", policy => policy.RequireRole("CorruptionOfficer", "SuperAdmin"));
        options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
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
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
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
