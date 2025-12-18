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

            // Connection strings
            var localConnectionString = "Data Source=JESTER-PC\\SQLEXPRESS;Initial Catalog=IT13;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30";
            var onlineConnectionString = "Server=db35282.databaseasp.net;Database=db35282;User Id=db35282;Password=c@3E=4Akw#6H;Encrypt=False;MultipleActiveResultSets=True;TrustServerCertificate=True;Connection Timeout=30;";
            
            // Register LOCAL database context (scoped) - primary database
            builder.Services.AddDbContext<HestiaLinkContext>(options =>
                options.UseSqlServer(localConnectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null
                )), ServiceLifetime.Scoped);
            
            // Register LOCAL DbContextFactory for components to avoid threading issues
            builder.Services.AddDbContextFactory<HestiaLinkContext>(options =>
                options.UseSqlServer(localConnectionString,
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null
                )), ServiceLifetime.Singleton);

            // Register ONLINE database context factory
            builder.Services.AddSingleton<OnlineDbContextFactory>(sp =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<HestiaLinkContext>();
                optionsBuilder.UseSqlServer(onlineConnectionString,
                    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null
                    ));
                return new OnlineDbContextFactory(optionsBuilder.Options);
            });

            // Register NetworkService (singleton) - for connectivity checks
            builder.Services.AddSingleton<NetworkService>();

            // Register PendingChangesTracker (singleton) - for offline queue
            builder.Services.AddSingleton<PendingChangesTracker>();

            // Register SyncService (scoped) - for dual-write and sync operations
            builder.Services.AddScoped<SyncService>(sp =>
            {
                // Get local context (primary database)
                var localContext = sp.GetRequiredService<HestiaLinkContext>();
                
                // Create online context manually with online connection string
                var onlineOptions = new DbContextOptionsBuilder<HestiaLinkContext>()
                    .UseSqlServer(onlineConnectionString,
                        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null
                        ))
                    .Options;
                var onlineContext = new HestiaLinkContext(onlineOptions);
                
                var networkService = sp.GetRequiredService<NetworkService>();
                var pendingTracker = sp.GetRequiredService<PendingChangesTracker>();
                var logger = sp.GetService<ILogger<SyncService>>();
                
                return new SyncService(localContext, onlineContext, networkService, pendingTracker, logger);
            });

            // Register legacy services for backward compatibility
            builder.Services.AddSingleton<ConnectionStatusService>();
            builder.Services.AddScoped<DualWriteService>();

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

            // Start background sync service
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000); // Wait 5 seconds after startup
                while (true)
                {
                    try
                    {
                        using var scope = app.Services.CreateScope();
                        var syncService = scope.ServiceProvider.GetRequiredService<SyncService>();
                        
                        if (await syncService.IsOnlineAsync())
                        {
                            await syncService.SyncPendingAsync();
                        }
                    }
                    catch
                    {
                        // Silently handle sync errors
                    }
                    await Task.Delay(30000); // Check every 30 seconds
                }
            });

            return app;
        }
#pragma warning restore CA1416
    }
}
