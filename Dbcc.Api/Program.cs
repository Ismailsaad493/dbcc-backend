using Dbcc.Api.Data;
using Dbcc.Api.Middleware;
using Dbcc.Api.Repositories;
using Dbcc.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(port) ? "http://0.0.0.0:4000" : $"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values.SelectMany(x => x.Errors)
            .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Request body is invalid." : x.ErrorMessage)
            .FirstOrDefault() ?? "Request body is invalid.";
        return new BadRequestObjectResult(new { error = message });
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
}

builder.Services.AddDbContext<DbccDbContext>(options =>
{
    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        throw new InvalidOperationException($"Unsupported database provider '{provider}'. Use SqlServer or Sqlite.");
    }
});

builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
builder.Services.AddScoped<IReportSettingsRepository, ReportSettingsRepository>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IReportSettingsService, ReportSettingsService>();
builder.Services.AddSingleton<DatabaseStatus>();

var app = builder.Build();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Json(new { ok = true, service = "dbcc-api" }));
app.MapGet("/health/live", () => Results.Ok(new { ok = true }));
app.MapGet("/health/ready", async (DbccDbContext db, DatabaseStatus status, CancellationToken ct) =>
{
    try
    {
        var connected = await db.Database.CanConnectAsync(ct);
        status.SetReady(connected);
        return connected
            ? Results.Ok(new { ok = true, database = "connected" })
            : Results.Json(new { ok = false, error = "Database is unavailable." }, statusCode: 503);
    }
    catch (Exception ex) when (ApiExceptionMiddleware.IsDatabaseFailure(ex))
    {
        status.SetReady(false);
        return Results.Json(new { ok = false, error = "Database is unavailable." }, statusCode: 503);
    }
});

app.MapControllers();
app.MapFallback("/api/{**path}", (HttpContext context) => Results.Json(
    new { error = $"No route for {context.Request.Method} {context.Request.Path}" },
    statusCode: StatusCodes.Status404NotFound));

// Keep the API process alive when SQL Server is unavailable so health checks and
// actionable 503 responses remain available while the database is repaired.
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<DbccDbContext>();
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        await DatabaseInitializer.InitializeAsync(db, app.Environment.ContentRootPath, logger);
        scope.ServiceProvider.GetRequiredService<DatabaseStatus>().SetReady(true);
    }
    catch (Exception ex) when (ApiExceptionMiddleware.IsDatabaseFailure(ex))
    {
        scope.ServiceProvider.GetRequiredService<DatabaseStatus>().SetReady(false);
        logger.LogError(ex, "Database initialization failed. Check the SQL Server connection string and ensure the database is reachable.");
    }
}

await app.RunAsync();

public partial class Program;
