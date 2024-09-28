using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using service.Data;
using System;
using System.Data.SQLite;
using System.IO;
using Serilog;
using service.Services;

internal class Program
{
    private static void Main(string[] args)
    {
        try
        {
            string dbDirectory = "/app/sqlite";
            string dbFileName = Path.Combine(dbDirectory, "app.db");

            // Ensure the directory exists
            if (!Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                Console.WriteLine($"Directory {dbDirectory} created.");
            }
            else
            {
                Console.WriteLine($"Directory {dbDirectory} already exists.");
            }

            // Check if the database file exists
            if (!File.Exists(dbFileName))
            {
                // Create the database file
                SQLiteConnection.CreateFile(dbFileName);
                Console.WriteLine($"Database file {dbFileName} created.");
            }
            else
            {
                Console.WriteLine($"Database file {dbFileName} already exists.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }


        var builder = WebApplication.CreateBuilder(args);
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration) 
            .CreateLogger();
        builder.Logging.ClearProviders(); 
        builder.Host.UseSerilog();
        
        // Add services to the container.
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        FilterManager.connectionString = connectionString;
        // builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false).AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddControllersWithViews();

        var app = builder.Build();
        
        // Apply migrations on startup
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                // var logger = services.GetRequiredService<ILogger<Program>>();
                Log.Error(ex, "An error occurred while migrating or initializing the database.");
            }
        }
        


        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();

        app.Run();
    }
}