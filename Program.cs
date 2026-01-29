using Microsoft.EntityFrameworkCore;
using InvenTrack.Data;

var builder = WebApplication.CreateBuilder(args);



builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(
                    builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDbContext<InvenTrackContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("InvenTrackContext")));

// Add services to the container.
builder.Services.AddControllersWithViews();
var app = builder.Build();

// Configure the HTTP request pipeline.
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

InvenTrackInitializer.Seed(app);
//Helloooo
//My name is Mo and Moayed here too now :D 
//bhkrbjkgbjk
//hgrbuir
app.Run();
