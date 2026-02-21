using Microsoft.EntityFrameworkCore;
using InvenTrack.Data;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------
// Database (Azure SQL)
// -------------------------------
// Azure SQL (serverless) can be paused. Retries help during resume.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
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

builder.Services.AddScoped<InvenTrackFinalProject.Services.StockService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply migrations + seed
InvenTrackInitializer.Seed(app);

app.Run();