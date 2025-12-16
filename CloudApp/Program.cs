using Microsoft.EntityFrameworkCore;
using HestiaLink.Data;
using CloudApp.Services;
using CloudApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Connection strings
var localConnectionString = builder.Configuration.GetConnectionString("LocalDatabase") 
    ?? "Data Source=MSI\\SQLEXPRESS;Initial Catalog=IT13;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30";

var onlineConnectionString = builder.Configuration.GetConnectionString("OnlineDatabase")
    ?? "Server=db35282.databaseasp.net;Database=db35282;User Id=db35282;Password=c@3E=4Akw#6H;Encrypt=False;MultipleActiveResultSets=True;TrustServerCertificate=True;Connection Timeout=30;";

// Register LOCAL database context
builder.Services.AddDbContext<HestiaLinkContext>(options =>
    options.UseSqlServer(localConnectionString,
    sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorNumbersToAdd: null
    )), ServiceLifetime.Scoped);

// Register LOCAL DbContextFactory
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

// Register dual-write services
builder.Services.AddSingleton<ConnectionStatusService>();
builder.Services.AddSingleton<PendingChangesTracker>();
builder.Services.AddScoped<DualWriteService>();
builder.Services.AddScoped<SyncService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

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
            var connectionStatus = scope.ServiceProvider.GetRequiredService<ConnectionStatusService>();
            
            if (await connectionStatus.IsOnlineAsync())
            {
                await syncService.SyncPendingChangesAsync();
            }
        }
        catch
        {
            // Silently handle sync errors
        }
        await Task.Delay(30000); // Check every 30 seconds
    }
});

app.Run();
