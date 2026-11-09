using Dbcc.Api.Data;
using Dbcc.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Dbcc.Api.Repositories;

public sealed class PlayerRepository(DbccDbContext db) : IPlayerRepository
{
    public Task<List<PlayerEntity>> GetAllAsync(CancellationToken ct) => db.Players.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
    public Task<PlayerEntity?> FindAsync(int id, CancellationToken ct) => db.Players.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task ReplaceAllAsync(IReadOnlyCollection<PlayerEntity> players, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.Players.ToDictionaryAsync(x => x.Id, ct);
        var ids = players.Select(x => x.Id).ToHashSet();
        db.Players.RemoveRange(existing.Values.Where(x => !ids.Contains(x.Id)));
        foreach (var player in players)
        {
            if (existing.TryGetValue(player.Id, out var current))
            {
                current.Name = player.Name;
                current.Batting = player.Batting;
                current.Bowling = player.Bowling;
                current.Fielding = player.Fielding;
                current.ExtraJson = player.ExtraJson;
            }
            else db.Players.Add(player);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task AddAsync(PlayerEntity player, CancellationToken ct) { db.Players.Add(player); await db.SaveChangesAsync(ct); }
    public async Task UpdateAsync(PlayerEntity player, CancellationToken ct) { db.Players.Update(player); await db.SaveChangesAsync(ct); }
    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var player = await db.Players.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (player is null) return false;
        db.Players.Remove(player);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class CollectionRepository(DbccDbContext db) : ICollectionRepository
{
    public Task<List<CollectionEntity>> GetAllAsync(CancellationToken ct) => db.Collections.AsNoTracking().Include(x => x.Player).OrderBy(x => x.PlayerId).ToListAsync(ct);
    public Task<CollectionEntity?> FindAsync(int playerId, CancellationToken ct) => db.Collections.Include(x => x.Player).SingleOrDefaultAsync(x => x.PlayerId == playerId, ct);

    public async Task ReplaceAllAsync(IReadOnlyCollection<CollectionEntity> collections, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.Collections.ToDictionaryAsync(x => x.PlayerId, ct);
        var ids = collections.Select(x => x.PlayerId).ToHashSet();
        db.Collections.RemoveRange(existing.Values.Where(x => !ids.Contains(x.PlayerId)));
        foreach (var collection in collections)
        {
            if (existing.TryGetValue(collection.PlayerId, out var current))
            {
                current.AmountPaid = collection.AmountPaid;
                current.ExtraJson = collection.ExtraJson;
            }
            else db.Collections.Add(collection);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task AddAsync(CollectionEntity collection, CancellationToken ct) { db.Collections.Add(collection); await db.SaveChangesAsync(ct); }
    public async Task UpdateAsync(CollectionEntity collection, CancellationToken ct) { db.Collections.Update(collection); await db.SaveChangesAsync(ct); }
    public async Task<bool> DeleteAsync(int playerId, CancellationToken ct)
    {
        var collection = await db.Collections.SingleOrDefaultAsync(x => x.PlayerId == playerId, ct);
        if (collection is null) return false;
        db.Collections.Remove(collection);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class MatchRepository(DbccDbContext db) : IMatchRepository
{
    private static IQueryable<MatchEntity> WithChildren(IQueryable<MatchEntity> query) => query
        .Include(x => x.Points).Include(x => x.Expenses).AsSplitQuery();

    public async Task<List<MatchEntity>> GetAllAsync(CancellationToken ct) =>
        await WithChildren(db.Matches.AsNoTracking()).OrderBy(x => x.SortOrder).ToListAsync(ct);

    public Task<MatchEntity?> FindAsync(string id, CancellationToken ct) =>
        WithChildren(db.Matches).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task ReplaceAllAsync(IReadOnlyCollection<MatchEntity> matches, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Matches.RemoveRange(await db.Matches.ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        db.Matches.AddRange(matches);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task AddAsync(MatchEntity match, CancellationToken ct) { db.Matches.Add(match); await db.SaveChangesAsync(ct); }

    public async Task UpdateAsync(MatchEntity match, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await WithChildren(db.Matches).SingleOrDefaultAsync(x => x.Id == match.Id, ct);
        if (current is null) { db.Matches.Add(match); }
        else
        {
            current.Date = match.Date;
            current.MatchNumber = match.MatchNumber;
            current.SortOrder = match.SortOrder;
            current.ExtraJson = match.ExtraJson;
            db.MatchPlayerScores.RemoveRange(current.Points);
            db.MatchExpenses.RemoveRange(current.Expenses);
            await db.SaveChangesAsync(ct);
            current.Points = match.Points;
            current.Expenses = match.Expenses;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct)
    {
        var match = await db.Matches.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (match is null) return false;
        db.Matches.Remove(match);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class ExpenseRepository(DbccDbContext db) : IExpenseRepository
{
    public Task<ExpenseTrackerEntity?> GetStateAsync(CancellationToken ct) => db.ExpenseTrackers.AsNoTracking()
        .Include(x => x.Periods).ThenInclude(x => x.Items).AsSplitQuery()
        .SingleOrDefaultAsync(x => x.Id == 1, ct);

    public Task<ExpensePeriodEntity?> FindPeriodAsync(string id, CancellationToken ct) => db.ExpensePeriods
        .Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task ReplaceStateAsync(ExpenseTrackerEntity state, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.ExpenseTrackers.Include(x => x.Periods).ThenInclude(x => x.Items).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (current is null)
        {
            state.Id = 1;
            db.ExpenseTrackers.Add(state);
        }
        else
        {
            current.ActiveId = state.ActiveId;
            db.ExpensePeriods.RemoveRange(current.Periods);
            await db.SaveChangesAsync(ct);
            current.Periods = state.Periods;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

public sealed class ReportSettingsRepository(DbccDbContext db) : IReportSettingsRepository
{
    public async Task<string?> GetJsonAsync(CancellationToken ct) =>
        await db.ReportSettings.AsNoTracking().Where(x => x.Id == 1).Select(x => x.Json).SingleOrDefaultAsync(ct);

    public async Task SaveJsonAsync(string json, CancellationToken ct)
    {
        var current = await db.ReportSettings.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (current is null) db.ReportSettings.Add(new ReportSettingsEntity { Id = 1, Json = json });
        else current.Json = json;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetAsync(string defaultsJson, CancellationToken ct) => await SaveJsonAsync(defaultsJson, ct);
}
