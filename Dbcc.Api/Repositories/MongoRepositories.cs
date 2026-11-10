using System.Linq.Expressions;
using Dbcc.Api.Data;
using Dbcc.Api.Models;
using MongoDB.Driver;

namespace Dbcc.Api.Repositories;

internal static class MongoRepositoryOperations
{
    public static async Task UpsertAllAsync<T, TKey>(
        IMongoCollection<T> collection,
        IReadOnlyCollection<T> documents,
        Expression<Func<T, TKey>> key,
        CancellationToken ct)
    {
        var keyValue = key.Compile();
        var writes = documents.Select(document => (WriteModel<T>)new ReplaceOneModel<T>(
            Builders<T>.Filter.Eq(key, keyValue(document)), document) { IsUpsert = true }).ToList();
        if (writes.Count > 0) await collection.BulkWriteAsync(writes, cancellationToken: ct);

        var filter = documents.Count == 0
            ? Builders<T>.Filter.Empty
            : Builders<T>.Filter.Nin(key, documents.Select(keyValue));
        await collection.DeleteManyAsync(filter, ct);
    }
}

public sealed class PlayerRepository(MongoDbContext db) : IPlayerRepository
{
    public Task<List<PlayerEntity>> GetAllAsync(CancellationToken ct) =>
        db.Players.Find(Builders<PlayerEntity>.Filter.Empty).SortBy(x => x.Id).ToListAsync(ct);

    public async Task<PlayerEntity?> FindAsync(int id, CancellationToken ct) =>
        await db.Players.Find(x => x.Id == id).FirstOrDefaultAsync(ct);

    public Task ReplaceAllAsync(IReadOnlyCollection<PlayerEntity> players, CancellationToken ct) =>
        MongoRepositoryOperations.UpsertAllAsync(db.Players, players, x => x.Id, ct);

    public Task AddAsync(PlayerEntity player, CancellationToken ct) => db.Players.InsertOneAsync(player, cancellationToken: ct);

    public async Task UpdateAsync(PlayerEntity player, CancellationToken ct) =>
        await db.Players.ReplaceOneAsync(x => x.Id == player.Id, player, cancellationToken: ct);

    public async Task<bool> DeleteAsync(int id, CancellationToken ct) =>
        (await db.Players.DeleteOneAsync(x => x.Id == id, ct)).DeletedCount > 0;
}

public sealed class CollectionRepository(MongoDbContext db) : ICollectionRepository
{
    public async Task<List<CollectionEntity>> GetAllAsync(CancellationToken ct)
    {
        var collections = await db.Collections.Find(Builders<CollectionEntity>.Filter.Empty).SortBy(x => x.PlayerId).ToListAsync(ct);
        var players = await db.Players.Find(Builders<PlayerEntity>.Filter.Empty).ToListAsync(ct);
        var names = players.ToDictionary(x => x.Id, x => x.Name);
        foreach (var collection in collections)
        {
            if (names.TryGetValue(collection.PlayerId, out var name)) collection.Player = new PlayerEntity { Id = collection.PlayerId, Name = name };
        }
        return collections;
    }

    public async Task<CollectionEntity?> FindAsync(int playerId, CancellationToken ct)
    {
        var collection = await db.Collections.Find(x => x.PlayerId == playerId).FirstOrDefaultAsync(ct);
        if (collection is null) return null;
        var player = await db.Players.Find(x => x.Id == playerId).FirstOrDefaultAsync(ct);
        if (player is not null) collection.Player = player;
        return collection;
    }

    public Task ReplaceAllAsync(IReadOnlyCollection<CollectionEntity> collections, CancellationToken ct) =>
        MongoRepositoryOperations.UpsertAllAsync(db.Collections, collections, x => x.PlayerId, ct);

    public Task AddAsync(CollectionEntity collection, CancellationToken ct) => db.Collections.InsertOneAsync(collection, cancellationToken: ct);

    public async Task UpdateAsync(CollectionEntity collection, CancellationToken ct) =>
        await db.Collections.ReplaceOneAsync(x => x.PlayerId == collection.PlayerId, collection, cancellationToken: ct);

    public async Task<bool> DeleteAsync(int playerId, CancellationToken ct) =>
        (await db.Collections.DeleteOneAsync(x => x.PlayerId == playerId, ct)).DeletedCount > 0;
}

public sealed class MatchRepository(MongoDbContext db) : IMatchRepository
{
    public Task<List<MatchEntity>> GetAllAsync(CancellationToken ct) =>
        db.Matches.Find(Builders<MatchEntity>.Filter.Empty).SortBy(x => x.SortOrder).ToListAsync(ct);

    public async Task<MatchEntity?> FindAsync(string id, CancellationToken ct) =>
        await db.Matches.Find(x => x.Id == id).FirstOrDefaultAsync(ct);

    public Task ReplaceAllAsync(IReadOnlyCollection<MatchEntity> matches, CancellationToken ct) =>
        MongoRepositoryOperations.UpsertAllAsync(db.Matches, matches, x => x.Id, ct);

    public Task AddAsync(MatchEntity match, CancellationToken ct) => db.Matches.InsertOneAsync(match, cancellationToken: ct);

    public async Task UpdateAsync(MatchEntity match, CancellationToken ct) =>
        await db.Matches.ReplaceOneAsync(x => x.Id == match.Id, match, cancellationToken: ct);

    public async Task<bool> DeleteAsync(string id, CancellationToken ct) =>
        (await db.Matches.DeleteOneAsync(x => x.Id == id, ct)).DeletedCount > 0;
}

public sealed class ExpenseRepository(MongoDbContext db) : IExpenseRepository
{
    public async Task<ExpenseTrackerEntity?> GetStateAsync(CancellationToken ct) =>
        await db.ExpenseTrackers.Find(x => x.Id == 1).FirstOrDefaultAsync(ct);

    public async Task<ExpensePeriodEntity?> FindPeriodAsync(string id, CancellationToken ct)
    {
        var state = await GetStateAsync(ct);
        return state?.Periods.FirstOrDefault(x => x.Id == id);
    }

    public async Task ReplaceStateAsync(ExpenseTrackerEntity state, CancellationToken ct)
    {
        state.Id = 1;
        await db.ExpenseTrackers.ReplaceOneAsync(x => x.Id == 1, state, new ReplaceOptions { IsUpsert = true }, ct);
    }
}

public sealed class ReportSettingsRepository(MongoDbContext db) : IReportSettingsRepository
{
    public async Task<string?> GetJsonAsync(CancellationToken ct) =>
        (await db.ReportSettings.Find(x => x.Id == 1).FirstOrDefaultAsync(ct))?.Json;

    public Task SaveJsonAsync(string json, CancellationToken ct) =>
        db.ReportSettings.ReplaceOneAsync(x => x.Id == 1, new ReportSettingsEntity { Id = 1, Json = json },
            new ReplaceOptions { IsUpsert = true }, ct);

    public Task ResetAsync(string defaultsJson, CancellationToken ct) => SaveJsonAsync(defaultsJson, ct);
}
