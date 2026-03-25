using InvenTrack.Data;
using InvenTrack.Hubs;
using InvenTrack.Models;
using InvenTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("IdentityContext"),
        sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory_Identity");
            sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(30), null);
            sql.CommandTimeout(60);
        }));

builder.Services.AddDbContext<InvenTrackContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("InvenTrackContext"),
        sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory_InvenTrack");
            sql.EnableRetryOnFailure(10, TimeSpan.FromSeconds(30), null);
            sql.CommandTimeout(60);
        }));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;

        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Logout");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ConfirmEmail");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/AccessDenied");
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<AppAccessService>();
builder.Services.AddScoped<InventoryAiService>();
builder.Services.AddScoped<ChatService>();

builder.Services.AddSignalR();
builder.Services.AddScoped<TransferRequestNotificationService>();

builder.Services.Configure<SendGridSettings>(builder.Configuration.GetSection("SendGrid"));
builder.Services.AddTransient<IEmailSender, SendGridEmailSender>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", context =>
{
    if (context.User?.Identity?.IsAuthenticated ?? false)
    {
        context.Response.Redirect("/Home");
        return Task.CompletedTask;
    }

    context.Response.Redirect("/Identity/Account/Login");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

InvenTrackInitializer.Seed(app);
await IdentitySeeder.SeedAsync(app.Services);

app.MapHub<TransferRequestHub>("/hubs/transfer-requests");
app.MapHub<ChatHub>("/hubs/chat");
app.Run();
