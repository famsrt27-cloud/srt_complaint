using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SRT.Complaint.Data;
using SRT.Complaint.Filters;
using SRT.Complaint.Services;
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
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwtSecret = builder.Configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("Jwt:Secret is not configured");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("GeneralOfficer", policy => policy.RequireRole("GeneralOfficer"));
        options.AddPolicy("CorruptionOfficer", policy => policy.RequireRole("CorruptionOfficer"));
        options.AddPolicy("StaffOnly", policy => policy.RequireRole("GeneralOfficer", "CorruptionOfficer", "SuperAdmin"));
        options.AddPolicy("CorruptionAccess", policy => policy.RequireRole("CorruptionOfficer", "SuperAdmin"));
        options.AddPolicy("SuperAdmin", policy => policy.RequireRole("SuperAdmin"));
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
    builder.Services.AddScoped<IApiRequestLogService, ApiRequestLogService>();
    builder.Services.AddScoped<IWebhookService, WebhookService>();
    builder.Services.AddScoped<ITermsService, TermsService>();
    builder.Services.AddScoped<IContentBlockService, ContentBlockService>();
    builder.Services.AddScoped<ITurnstileService, TurnstileService>();
    builder.Services.AddScoped<IStatsService, StatsService>();
    builder.Services.AddScoped<ICorruptionStatsService, CorruptionStatsService>();
    builder.Services.AddScoped<IExternalSyncService, ExternalSyncService>();
    builder.Services.AddScoped<ITraffySettingsService, TraffySettingsService>();
    builder.Services.AddScoped<IStaffUserService, StaffUserService>();
    builder.Services.AddScoped<IStaffDirectoryService, StaffDirectoryService>();
    builder.Services.AddScoped<IDiscordNotifier, DiscordNotifier>();
    // Registered as its own singleton so IExternalSystemAdapter and IExternalStatusResolver
    // share the same instance (and therefore the same cached token/status-list state).
    builder.Services.AddSingleton<SRT.Complaint.Services.Adapters.TraffyFonduAdapter>();
    builder.Services.AddSingleton<IExternalSystemAdapter>(sp =>
        sp.GetRequiredService<SRT.Complaint.Services.Adapters.TraffyFonduAdapter>());
    builder.Services.AddSingleton<IExternalStatusResolver>(sp =>
        sp.GetRequiredService<SRT.Complaint.Services.Adapters.TraffyFonduAdapter>());
    builder.Services.AddSingleton<ITraffyConnectionTester>(sp =>
        sp.GetRequiredService<SRT.Complaint.Services.Adapters.TraffyFonduAdapter>());
    builder.Services.AddSingleton<IExternalSystemAdapter, SRT.Complaint.Services.Adapters.DamrongdhammaAdapter>();
    builder.Services.AddScoped<ApiKeyAuthFilter>();

    // ──────────── Background Services ────────────
    builder.Services.AddHostedService<SlaBackgroundService>();
    builder.Services.AddHostedService<WebhookRetryService>();
    builder.Services.AddHostedService<ExternalSyncBackgroundService>();

    // ──────────── MVC & Razor Pages ────────────
    builder.Services.AddControllers();
    builder.Services.AddRazorPages();

    // ──────────── HTTP Clients ────────────
    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient("Webhook", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });

    // ──────────── File Upload ────────────
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10 * 1024 * 1024 * 5; // 5 files × 10 MB
    });

    var app = builder.Build();

    // ──────────── Seed SuperAdmin (first-run only) ────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.StaffUsers.Any(u => u.Role == "SuperAdmin"))
        {
            db.StaffUsers.Add(new SRT.Complaint.Models.StaffUser
            {
                EmployeeCode = "0000001",
                FullName = "Super Administrator",
                Email = "admin@railway.co.th",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@1234", 12),
                Role = "SuperAdmin",
                IsActive = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            Log.Information("Seeded default SuperAdmin (EmployeeCode=0000001)");
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

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
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
    app.MapStaticAssets();
    app.MapRazorPages().WithStaticAssets();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
