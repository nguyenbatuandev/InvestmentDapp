using Invest.Application.EventService;
using InvestDapp.Application.AuthService;
using InvestDapp.Application.KycService;
using InvestDapp.Application.MessageService;
using InvestDapp.Application.UserService;
using InvestDapp.Infrastructure.Data;
using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Infrastructure.Data.interfaces;
using InvestDapp.Infrastructure.Data.Repository;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nethereum.Web3;

var builder = WebApplication.CreateBuilder(args);

// =======================
// 1. CẤU HÌNH BLOCKCHAIN
// =======================
builder.Services.Configure<BlockchainConfig>(builder.Configuration.GetSection("Blockchain"));

// =======================
// 2. CẤU HÌNH DATABASE (DbContext)
// =======================
builder.Services.AddDbContext<InvestDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================
// 3. ĐĂNG KÝ WEB3 LÀ SINGLETON
// =======================
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IOptions<BlockchainConfig>>().Value;
    return new Web3(config.RpcUrl);
});

// =======================
// 4. ĐĂNG KÝ IHttpContextAccessor
// Để có thể truy cập HttpContext trong AuthService
// =======================
builder.Services.AddHttpContextAccessor();

// =======================
// 5. ĐĂNG KÝ REPOSITORY VÀ SERVICE
// =======================
builder.Services.AddScoped<ICampaignEventRepository, CampaignEventRepository>();
builder.Services.AddScoped<IUser, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserConnectionManager, UserConnectionManager>();
builder.Services.AddScoped<IKyc, KycRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IConversationService, ConversationService>();
// Đăng ký CampaignEventService
builder.Services.AddScoped<CampaignEventService>();
builder.Services.AddSignalR(options =>
{
    // Bật tính năng này để server gửi lỗi chi tiết về client khi đang phát triển
    options.EnableDetailedErrors = true;
});
// Đăng ký CampaignEventListener như một Hosted Service (chạy ngầm)
//builder.Services.AddHostedService<CampaignEventListener>();

// =======================
// 6. CẤU HÌNH CORS
// Cho phép mọi origin, method, header (phục vụ phát triển hoặc API mở)
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
builder.Services.AddHttpClient(); // Thêm HttpClient để sử dụng trong các service

// =======================
// 7. CẤU HÌNH AUTHENTICATION COOKIE
// =======================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Index"; // Đường dẫn đăng nhập

        // Trả về 403 Forbidden nếu không đủ quyền truy cập
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        };
    });

// =======================
// 8. ĐĂNG KÝ CONTROLLERS VỚI VIEWS
// =======================

builder.Services.AddControllersWithViews();

var app = builder.Build();

// =======================
// 9. CẤU HÌNH MIDDLEWARE PIPELINE
// =======================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error"); // Xử lý lỗi chung
    app.UseHsts(); // Bảo mật HTTPS Strict Transport Security
}
app.MapHub<ChatHub>("/chathub");
app.UseHttpsRedirection(); // Chuyển hướng HTTP sang HTTPS
app.UseStaticFiles(); // Cho phép phục vụ các file tĩnh như css, js, hình ảnh

app.UseCors("AllowAll"); // Sử dụng cấu hình CORS đã định nghĩa

app.UseRouting(); // Xác định routing
app.UseCookiePolicy(); // 🧩 thêm dòng này

app.UseAuthentication(); // Thêm middleware xác thực (thường thêm trước Authorization)
app.UseAuthorization();  // Thêm middleware phân quyền

// Định nghĩa route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run(); // Chạy ứng dụng
