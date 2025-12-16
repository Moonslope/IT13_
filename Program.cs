using HestiaIT13Final;
using HestiaLink.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace HestiaLink;

public static class MauiProgram
{
    public static MauiAppBuilder CreateMauiAppBuilder()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Add DbContext with SQL Server connection
        builder.Services.AddDbContext<HestiaLinkContext>(options =>
            options.UseSqlServer(GetConnectionString()));
        
        // Register DbContextFactory for components to avoid threading issues
        builder.Services.AddDbContextFactory<HestiaLinkContext>(options =>
            options.UseSqlServer(GetConnectionString()));

        return builder;
    }

    private static string GetConnectionString()
    {
        // Connection string for MAUI app
        return "Data Source=MSI\\SQLEXPRESS;Initial Catalog=IT13;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30";
    }
}