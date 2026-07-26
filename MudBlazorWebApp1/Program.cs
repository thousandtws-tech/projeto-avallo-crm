using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MudBlazor.Services;
using MudBlazorWebApp1.Components;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Application;
using MudBlazorWebApp1.Infrastructure;
using MudBlazorWebApp1.Features.Auth;
using QuestPDF.Infrastructure;
using MudBlazorWebApp1.Features.Notifications;
using MudBlazorWebApp1.Features.Connectors;
using BraSeller.Connector.MercadoLivre;
using MudBlazorWebApp1.Client.Services;
using MudBlazorWebApp1.Hosting;
using MudBlazorWebApp1.Features.Expenses;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();
builder.Services.AddMemoryCache();
// Dynamic HttpClient for server-side prerendering with cookie forwarding and SSL validation bypass
builder.Services.AddScoped(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    var context = accessor.HttpContext;
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    var httpClient = new HttpClient(handler);
    if (context is not null)
    {
        var request = context.Request;
        httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}{request.PathBase}/");
        if (request.Headers.TryGetValue("Cookie", out var cookies))
        {
            httpClient.DefaultRequestHeaders.Add("Cookie", cookies.ToString());
        }
    }
    return httpClient;
});

// Register all client-side services in the server DI container for Prerendering
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserAccessClient>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ConnectorService>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<FiscalService>();
builder.Services.AddScoped<GeoLocationService>();
builder.Services.AddScoped<PeriodClosingClient>();
builder.Services.AddScoped<ReconciliationClient>();
builder.Services.AddScoped<WebPushNotificationService>();
builder.Services.AddSingleton<AppLocalizer>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.MaxRequestBodySize = 12 * 1024 * 1024;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/wasm"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddDataProtection().SetApplicationName("BraSeller");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddConnectorLayer(builder.Configuration, builder.Environment);
new MercadoLivreModule().Register(builder.Services, builder.Configuration);

// Clean Architecture Layer Registrations
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

builder.Services.AddScoped<NotificationDispatchService>();
builder.Services.AddScoped<NotificationScheduler>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddHostedService<NotificationWorker>();
QuestPDF.Settings.License = Enum.TryParse<LicenseType>(
    builder.Configuration["Reports:QuestPdfLicense"], ignoreCase: true, out var questPdfLicense)
    ? questPdfLicense
    : LicenseType.Community;
builder.Services.AddOptions<EmailOptions>()
    .BindConfiguration(EmailOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(options => !options.Enabled ||
        (!string.IsNullOrWhiteSpace(options.Host) && !string.IsNullOrWhiteSpace(options.FromEmail)),
        "Email host and sender are required when email delivery is enabled.")
    .ValidateOnStart();
builder.Services.AddOptions<NotificationWorkerOptions>()
    .BindConfiguration(NotificationWorkerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<ObjectStorageOptions>()
    .BindConfiguration(ObjectStorageOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(options => !options.Enabled ||
        (!string.IsNullOrWhiteSpace(options.ServiceUrl) && !string.IsNullOrWhiteSpace(options.Bucket) &&
         !string.IsNullOrWhiteSpace(options.AccessKey) && !string.IsNullOrWhiteSpace(options.SecretKey)),
        "S3 endpoint, bucket and credentials are required when object storage is enabled.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?.FindFirstValue("security_stamp");
                var user = userId is null ? null : await userManager.FindByIdAsync(userId);
                if (user is null || !user.IsActive || user.SecurityStamp != stamp)
                    context.Fail("Token is no longer valid.");
            }
        };
    })
    .AddCookie(IdentityConstants.ExternalScheme, options =>
    {
        options.Cookie.Name = "nucleo-external-auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "not-configured";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "not-configured";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.SaveTokens = false;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.TenantMember, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .RequireClaim("password_change_required", "false")
        .RequireRole(Roles.All))
    .AddPolicy(Policies.CanWrite, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .RequireClaim("password_change_required", "false")
        .RequireRole(Roles.Writers))
    .AddPolicy(Policies.CanManageUsers, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .RequireClaim("password_change_required", "false")
        .RequireRole(Roles.Admin))
    .AddPolicy(Policies.CanReviewAccounting, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("tenant_id")
        .RequireClaim("password_change_required", "false")
        .RequireRole(Roles.AccountingManagers));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("auth-v1", new OpenApiInfo
    {
        Title = "MudBlazorWebApp1 Authentication API",
        Version = "v1",
        Description = "Cadastro, login, OAuth Google, renovacao de sessao e usuarios multi-tenant."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe somente o access token JWT."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseSwagger(options => options.RouteTemplate = "swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/auth-v1/swagger.json", "Authentication API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Authentication API";
        options.DisplayRequestDuration();
        options.EnableTryItOutByDefault();
    });
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "braseller-web"
})).AllowAnonymous();

app.MapStaticAssets();
app.MapBusinessModules();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MudBlazorWebApp1.Client._Imports).Assembly);

await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    if (builder.Configuration.GetValue("Database:ApplyMigrations", app.Environment.IsDevelopment()))
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        try
        {
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_AccountingPostings_AccountingEntryId\" ON \"AccountingPostings\" (\"AccountingEntryId\");");
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_InventoryReconciliationIssues_MarketplaceOrderId\" ON \"InventoryReconciliationIssues\" (\"MarketplaceOrderId\");");
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_InventoryReconciliationIssues_MarketplaceOrderItemId\" ON \"InventoryReconciliationIssues\" (\"MarketplaceOrderItemId\");");
                await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_TaxReconciliationIssues_MarketplaceOrderId\" ON \"TaxReconciliationIssues\" (\"MarketplaceOrderId\");");
            }
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<AppDbContext>>();
            logger.LogWarning(ex, "Failed to dynamically ensure indexes exist on database startup.");
        }
    }

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in Roles.All)
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
}

app.Run();
