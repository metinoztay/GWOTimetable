using GWOTimetable.Models;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
