using Invest.Application.EventListener;
using Invest.Application.EventService;
using InvestDapp.Application.AdminAnalytics;
using InvestDapp.Application.AdminDashboard;
using InvestDapp.Application.AuthService;
using InvestDapp.Application.AuthService.Admin;
using InvestDapp.Application.AuthService.Roles;
using InvestDapp.Application.AuthService.Staff;
using InvestDapp.Shared.Enums;
using InvestDapp.Application.CampaignService;
using InvestDapp.Application.KycService;
using InvestDapp.Application.MessageService;
using InvestDapp.Application.NotificationService;
using InvestDapp.Application.Services.Trading;
using InvestDapp.Application.SupportService;
using InvestDapp.Application.UserService;
using InvestDapp.Application.TradingServices.Admin;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Infrastructure.Data.Repository;
using InvestDapp.Infrastructure.Services.Binance;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using QuestPDF.Infrastructure;
using InvestDapp.Shared.Security;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// =======================
// 1. CẤU HÌNH BLOCKCHAIN
// =======================
builder.Services.Configure<BlockchainConfig>(builder.Configuration.GetSection("Blockchain"));

// =======================
// 2. CẤU HÌNH TRADING CONFIGS
// =======================
builder.Services.Configure<BinanceConfig>(builder.Configuration.GetSection("Binance"));
builder.Services.Configure<TradingConfig>(builder.Configuration.GetSection("Trading"));

// =======================
// 3. CẤU HÌNH DATABASE (DbContext)
// =======================
builder.Services.AddDbContext<InvestDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================
// 4. ADD MEMORY CACHE AS FALLBACK
// =======================
builder.Services.AddMemoryCache();

// =======================
// 5. CẤU HÌNH REDIS WITH RESILIENCE (OPTIONAL)
// =======================

// =======================
// 6. ĐĂNG KÝ WEB3 LÀ SINGLETON
// =======================
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IOptions<BlockchainConfig>>().Value;
    return new Web3(config.RpcUrl);
});

// =======================
// 7. ĐĂNG KÝ IHttpContextAccessor
// =======================
builder.Services.AddHttpContextAccessor();

// =======================
// 8. ĐĂNG KÝ TRADING SERVICES
// =======================
builder.Services.AddScoped<IBinanceRestService, BinanceRestService>();
builder.Services.AddScoped<IBinanceWebSocketService, BinanceWebSocketService>();
builder.Services.AddScoped<IInternalOrderService, InternalOrderService>();
builder.Services.AddScoped<IMarketPriceService, MarketPriceService>();
builder.Services.AddScoped<ITradingRepository, TradingRepository>();

// Register hosted services
builder.Services.AddHostedService<MarketDataWorker>();
builder.Services.AddHostedService<TradingEngine>();
builder.Services.AddHostedService<CampaignEventListener>();
// =======================
// 9. ĐĂNG KÝ REPOSITORY VÀ SERVICE
// =======================
builder.Services.AddScoped<ICampaignEventRepository, CampaignEventRepository>();
builder.Services.AddScoped<IUser, UserRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoleService, SmartContractRoleService>();
builder.Services.AddScoped<IAdminLoginService, AdminLoginService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IStaffManagementService, StaffManagementService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserConnectionManager, UserConnectionManager>();
builder.Services.AddScoped<IKyc, KycRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<ICampaignPostRepository, CampaignPostRepository>();
builder.Services.AddScoped<ICampaignPostService, CampaignPostService>();
builder.Services.AddScoped<ICampaign, CampaignRepository>();
builder.Services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<CampaignEventService>();
builder.Services.AddScoped<ITransactionReportService, TransactionReportService>();
builder.Services.AddScoped<ITransactionReportPdfService, TransactionReportPdfService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAdminTradingService, AdminTradingService>();

// =======================
// 10. CẤU HÌNH SIGNALR
// =======================
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// =======================
// 11. CẤU HÌNH CORS
// =======================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// =======================
// 12. ĐĂNG KÝ HTTP CLIENT
// =======================
builder.Services.AddHttpClient();

// =======================
// 13. CẤU HÌNH AUTHENTICATION COOKIE
// =======================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/auth/login";
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.RequireSuperAdmin, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified)
              .RequireRole(RoleType.SuperAdmin.ToString()));
    options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified)
              .RequireRole(RoleType.Admin.ToString(), RoleType.SuperAdmin.ToString()));
    options.AddPolicy(AuthorizationPolicies.RequireModerator, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified)
              .RequireRole(RoleType.Moderator.ToString(), RoleType.Admin.ToString(), RoleType.SuperAdmin.ToString()));
    options.AddPolicy(AuthorizationPolicies.RequireSupportAgent, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified)
              .RequireRole(RoleType.SupportAgent.ToString(), RoleType.Admin.ToString(), RoleType.SuperAdmin.ToString()));
    options.AddPolicy(AuthorizationPolicies.RequireFundraiser, policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(RoleType.Fundraiser.ToString(), RoleType.Admin.ToString(), RoleType.SuperAdmin.ToString()));
    
    // NEW: Staff Access - allows all staff roles (SuperAdmin, Admin, Moderator, SupportAgent, Fundraiser)
    options.AddPolicy(AuthorizationPolicies.RequireStaffAccess, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.AdminSessionClaim, AuthorizationPolicies.AdminSessionVerified)
              .RequireRole(
                  RoleType.SuperAdmin.ToString(), 
                  RoleType.Admin.ToString(), 
                  RoleType.Moderator.ToString(), 
                  RoleType.SupportAgent.ToString(), 
                  RoleType.Fundraiser.ToString()));
});

// =======================
// 14. ĐĂNG KÝ CONTROLLERS VỚI VIEWS
// =======================
builder.Services.AddControllersWithViews();

var app = builder.Build();

// =======================
// 15. CẤU HÌNH MIDDLEWARE PIPELINE
// =======================

// Force InvariantCulture for decimal parsing (fix decimal number binding from forms)
var cultureInfo = new CultureInfo("en-US");
cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseRouting();
app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

// =======================
// 16. MAP SIGNALR HUBS
// =======================
app.MapHub<ChatHub>("/chathub");
app.MapHub<TradingHub>("/tradingHub");
app.MapHub<InvestDapp.Application.NotificationService.NotificationHub>("/notificationHub");

// =======================
// 17. MAP ROUTES
// =======================
app.MapAreaControllerRoute(
        name: "admin",
        areaName: "Admin",
        pattern: "admin/{controller=Dashboard}/{action=Index}/{id?}")
   .RequireAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
