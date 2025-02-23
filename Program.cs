using GWOTimetable.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Db12026Context>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


using (var context = new Db12026Context())
{
    try
    {
        if (context.Database.CanConnect())
        {
            Console.WriteLine("Connection successful!");
        }
        else
        {
            Console.WriteLine("Connection failed!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error occurred: {ex.Message}");
    }
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Kimlik doğrulama middleware'i
app.UseAuthorization();  // Yetkilendirme middleware'i

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Dashboard}");
//pattern: "{controller=Account}/{action=Login}/{id?}");
app.Run();
