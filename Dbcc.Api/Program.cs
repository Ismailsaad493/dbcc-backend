using Dbcc.Api.Data;
using Dbcc.Api.Middleware;
using Dbcc.Api.Repositories;
using Dbcc.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

static string? ReadMongoUri(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        ? value
        : null;
}

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

// Render currently has an existing DBCC secret. Accept it when it contains
// an Atlas URI, while preferring the standard connection-string variables.
var connectionString = ReadMongoUri("ConnectionStrings__MongoDb")
    ?? ReadMongoUri("MONGODB_URI")
    ?? ReadMongoUri("DBCC")
    ?? builder.Configuration.GetConnectionString("MongoDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:MongoDb is not configured.");
if (string.IsNullOrWhiteSpace(builder.Configuration["MongoDb:Database"]))
    throw new InvalidOperationException("MongoDb:Database is not configured.");

builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddSingleton<MongoDbContext>();

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
app.MapGet("/health/ready", async (MongoDbContext db, DatabaseStatus status, CancellationToken ct) =>
{
    try
    {
        await db.PingAsync(ct);
        status.SetReady(true);
        return Results.Ok(new { ok = true, database = "connected" });
    }
    catch (Exception ex) when (ApiExceptionMiddleware.IsDatabaseFailure(ex))
    {
        status.SetReady(false);
        return Results.Json(new { ok = false, error = "Database is unavailable." }, statusCode: 503);
    }
});

// Test-only cleanup is enabled by the Node integration harness and restricted
// to its DBCC_Test_* databases. It is never registered for a normal app run.
if (string.Equals(Environment.GetEnvironmentVariable("DBCC_TEST_MODE"), "true", StringComparison.OrdinalIgnoreCase))
{
    app.MapPost("/__test/shutdown", async (HttpContext context, MongoDbContext db, IHostApplicationLifetime lifetime) =>
    {
        var databaseName = db.Database.DatabaseNamespace.DatabaseName;
        if (!databaseName.StartsWith("DBCC_Test_", StringComparison.Ordinal))
            return Results.NotFound();

        if (!string.Equals(context.Request.Query["keepDatabase"], "true", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await db.Database.Client.DropDatabaseAsync(databaseName, context.RequestAborted);
            }
            catch (MongoException)
            {
                // The connection-failure test cannot reach a server to clean up.
            }
        }
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        await context.Response.CompleteAsync();
        lifetime.StopApplication();
        return Results.Empty;
    });
}

app.MapControllers();
app.MapFallback("/api/{**path}", (HttpContext context) => Results.Json(
    new { error = $"No route for {context.Request.Method} {context.Request.Path}" },
    statusCode: StatusCodes.Status404NotFound));

// Keep the API process alive when MongoDB is unavailable so health checks and
// actionable 503 responses remain available while the database is repaired.
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        await db.PingAsync(CancellationToken.None);
        await DatabaseInitializer.InitializeAsync(db, app.Environment.ContentRootPath, logger);
        scope.ServiceProvider.GetRequiredService<DatabaseStatus>().SetReady(true);
    }
    catch (Exception ex) when (ApiExceptionMiddleware.IsDatabaseFailure(ex))
    {
        scope.ServiceProvider.GetRequiredService<DatabaseStatus>().SetReady(false);
        logger.LogError(ex, "Database initialization failed. Check the MongoDB connection string and ensure the server is reachable.");
    }
}

await app.RunAsync();

public partial class Program;
