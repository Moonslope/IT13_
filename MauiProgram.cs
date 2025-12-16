using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using HestiaLink.Data;
using HestiaLink.Services;

namespace HestiaIT13Final
{
    public static class MauiProgram
    {
#pragma warning disable CA1416
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Add UserSession service for authentication
            builder.Services.AddScoped<UserSession>();
            
            // Add Authentication Service
            builder.Services.AddScoped<AuthenticationService>();

            // Add Attendance Service
            builder.Services.AddScoped<AttendanceService>();

            // Add Inventory Service
            builder.Services.AddScoped<InventoryService>();

            // Add Housekeeping Service
            builder.Services.AddScoped<HousekeepingService>();

            // Add database context with connection string
            var connectionString = "Data Source=MSI\\SQLEXPRESS;Initial Catalog=IT13;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30";
            
            // Register DbContext for services that need scoped instances
            builder.Services.AddDbContext<HestiaLinkContext>(options =>
                options.UseSqlServer(connectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null
                )));
            
            // Register DbContextFactory for components to avoid threading issues
            // This ensures each operation gets its own context instance
            builder.Services.AddDbContextFactory<HestiaLinkContext>(options =>
                options.UseSqlServer(connectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null
                )));

            builder.Services.AddScoped<DbSeeder>();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Seed Database
            // Task.Run(async () =>
            // {
            //     using (var scope = app.Services.CreateScope())
            //     {
            //         var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            //         await seeder.SeedAsync();
            //     }
            // });

            return app;
        }
#pragma warning restore CA1416
    }
}
