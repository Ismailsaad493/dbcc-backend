using Dbcc.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dbcc.Api.Data;

/// <summary>
/// MongoDB collection access for the repository layer. Related match and
/// expense data are embedded in their parent documents.
/// </summary>
public sealed class MongoDbContext(IMongoClient client, IConfiguration configuration)
{
    public IMongoDatabase Database { get; } = client.GetDatabase(
        configuration["MongoDb:Database"]
        ?? throw new InvalidOperationException("MongoDb:Database is not configured."));

    public IMongoCollection<PlayerEntity> Players => Database.GetCollection<PlayerEntity>("players");
    public IMongoCollection<CollectionEntity> Collections => Database.GetCollection<CollectionEntity>("collections");
    public IMongoCollection<MatchEntity> Matches => Database.GetCollection<MatchEntity>("matches");
    public IMongoCollection<ExpenseTrackerEntity> ExpenseTrackers => Database.GetCollection<ExpenseTrackerEntity>("expenseTracker");
    public IMongoCollection<ReportSettingsEntity> ReportSettings => Database.GetCollection<ReportSettingsEntity>("reportSettings");
    public IMongoCollection<InitializationRecordEntity> InitializationRecords => Database.GetCollection<InitializationRecordEntity>("databaseInitialization");

    public async Task PingAsync(CancellationToken cancellationToken)
    {
        await Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
    }
}
