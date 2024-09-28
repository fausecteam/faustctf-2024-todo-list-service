using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using service.Data;
using service.Models;

namespace service.Services;

public class FilterManager
{
    public static String connectionString { get; set; }

    private static ApplicationDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite(connectionString); 
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    public static void AddFilter(Filter filter)
    {
        using (var context = CreateDbContext())
        {
            var existingObject = context.Filters.FirstOrDefault(f =>
                f.Name == filter.Name && f.User == filter.User && f.QueryString == filter.QueryString);
            if (existingObject != null)
            {
                return;
            }
            context.Filters.Add(filter); 
            context.SaveChanges();
        }
    }

    public static bool FilterExists(string name, string user)
    {
        using (var context = CreateDbContext())
        {
            return context.Filters.Any(f => f.Name == name && f.User == user);
        }
    }
}