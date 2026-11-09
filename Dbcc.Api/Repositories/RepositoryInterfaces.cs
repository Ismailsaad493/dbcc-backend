using Dbcc.Api.Models;

namespace Dbcc.Api.Repositories;

public interface IPlayerRepository
{
    Task<List<PlayerEntity>> GetAllAsync(CancellationToken ct);
    Task<PlayerEntity?> FindAsync(int id, CancellationToken ct);
    Task ReplaceAllAsync(IReadOnlyCollection<PlayerEntity> players, CancellationToken ct);
    Task AddAsync(PlayerEntity player, CancellationToken ct);
    Task UpdateAsync(PlayerEntity player, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

public interface ICollectionRepository
{
    Task<List<CollectionEntity>> GetAllAsync(CancellationToken ct);
    Task<CollectionEntity?> FindAsync(int playerId, CancellationToken ct);
    Task ReplaceAllAsync(IReadOnlyCollection<CollectionEntity> collections, CancellationToken ct);
    Task AddAsync(CollectionEntity collection, CancellationToken ct);
    Task UpdateAsync(CollectionEntity collection, CancellationToken ct);
    Task<bool> DeleteAsync(int playerId, CancellationToken ct);
}

public interface IMatchRepository
{
    Task<List<MatchEntity>> GetAllAsync(CancellationToken ct);
    Task<MatchEntity?> FindAsync(string id, CancellationToken ct);
    Task ReplaceAllAsync(IReadOnlyCollection<MatchEntity> matches, CancellationToken ct);
    Task AddAsync(MatchEntity match, CancellationToken ct);
    Task UpdateAsync(MatchEntity match, CancellationToken ct);
    Task<bool> DeleteAsync(string id, CancellationToken ct);
}

public interface IExpenseRepository
{
    Task<ExpenseTrackerEntity?> GetStateAsync(CancellationToken ct);
    Task<ExpensePeriodEntity?> FindPeriodAsync(string id, CancellationToken ct);
    Task ReplaceStateAsync(ExpenseTrackerEntity state, CancellationToken ct);
}

public interface IReportSettingsRepository
{
    Task<string?> GetJsonAsync(CancellationToken ct);
    Task SaveJsonAsync(string json, CancellationToken ct);
    Task ResetAsync(string defaultsJson, CancellationToken ct);
}
