using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Inventory_Management_System.Models.Webhooks;
using Inventory_Management_System.Services.Webhooks;
using Inventory_Management_System.Services.Recovery;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Localization Services ────────────────────────────────────────────────
// Resource files live under /Resources/ and are named SharedResource.{culture}.resx
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ── 2. MVC + View/DataAnnotations Localization ─────────────────────────────
var mvcBuilder = builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(Inventory_Management_System.SharedResource));
    });

// Enable Razor runtime compilation in Development so .cshtml changes
// are picked up without a full server restart.
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── 3. ASP.NET Core Identity & Security Baseline ───────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings (Enforced strong entropy standards: PBKDF2)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Cookie Authentication for MVC Web Portal
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.Name = "IMS.AuthSession";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Configure JWT Bearer Authentication for API Consumers
var jwtKey = builder.Configuration["Jwt:Key"] ?? "IMS_SECRET_KEY_FOR_LOCAL_ENTERPRISE_JWT_TOKEN_SIGNING_2026_SECURITY_SHA256!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "InventoryManagementSystem";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "InventoryManagementSystemClient";

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization policies supporting dual authentication (Cookie + JWT Bearer)
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
        IdentityConstants.ApplicationScheme,
        JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("RequireSuperAdmin", policy => policy.RequireRole(AppRoles.SuperAdmin));
    options.AddPolicy("RequireWarehouseManager", policy => policy.RequireRole(AppRoles.SuperAdmin, AppRoles.WarehouseManager));
    options.AddPolicy("RequireCashier", policy => policy.RequireRole(AppRoles.SuperAdmin, AppRoles.Cashier));
    options.AddPolicy("RequireProcurement", policy => policy.RequireRole(AppRoles.SuperAdmin, AppRoles.WarehouseManager, AppRoles.ProcurementSpecialist));
    options.AddPolicy("RequireAuditor", policy => policy.RequireRole(AppRoles.SuperAdmin, AppRoles.Auditor));
});

// Register Core & Domain Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ISupplierProductService, SupplierProductService>();
builder.Services.AddHttpClient<IAIService, AIService>();

// ── 3.1 Webhook Dispatcher & Cryptographic Signer ──────────────────────────
builder.Services.Configure<WebhookOptions>(builder.Configuration.GetSection(WebhookOptions.SectionName));
builder.Services.AddSingleton<IWebhookSigner, WebhookSigner>();
builder.Services.AddHttpClient(WebhookDispatcher.HttpClientName);
builder.Services.AddScoped<IWebhookDispatcher, WebhookDispatcher>();

// ── 3.2 Master Recovery Key & Authentication Pipeline ──────────────────────
builder.Services.AddScoped<IMasterRecoveryKeyService, MasterRecoveryKeyService>();

// ── 4. RequestLocalizationOptions ──────────────────────────────────────────
var supportedCultures = new[] { "en", "ar", "fr", "es", "it", "de" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

// Provider priority: Cookie (persisted preference) → QueryString → Accept-Language header
locOptions.RequestCultureProviders.Clear();
locOptions.RequestCultureProviders.Add(new CookieRequestCultureProvider());
locOptions.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
locOptions.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

var app = builder.Build();

// ── 5. Automatic Database Migration & Identity Seeding on Startup ──────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await IdentitySeeder.SeedAsync(userManager, roleManager, app.Configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration or identity seeding on startup.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ── 6. Localization Middleware (must come BEFORE routing) ───────────────────
app.UseRequestLocalization(locOptions);

app.UseRouting();

// ── 7. Authentication & Authorization Middleware ───────────────────────────
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Analytics}/{id?}")
    .WithStaticAssets();

app.Run();
