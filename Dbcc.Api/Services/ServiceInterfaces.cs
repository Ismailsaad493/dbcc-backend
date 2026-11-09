using System.Text.Json;
using Dbcc.Api.Contracts;

namespace Dbcc.Api.Services;

public interface IPlayerService
{
    Task<IReadOnlyList<PlayerModel>> GetAllAsync(CancellationToken ct);
    Task<PlayerModel?> GetAsync(int id, CancellationToken ct);
    Task<bool> ReplaceAllAsync(IReadOnlyList<PlayerModel> players, CancellationToken ct);
    Task<bool> CreateAsync(PlayerModel player, CancellationToken ct);
    Task<bool> UpdateAsync(int id, PlayerModel player, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

public interface ICollectionService
{
    Task<IReadOnlyList<CollectionModel>> GetAllAsync(CancellationToken ct);
    Task<CollectionModel?> GetAsync(int id, CancellationToken ct);
    Task<bool> ReplaceAllAsync(IReadOnlyList<CollectionModel> collections, CancellationToken ct);
    Task<bool> CreateAsync(CollectionModel collection, CancellationToken ct);
    Task<bool> UpdateAsync(int id, CollectionModel collection, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}

public interface IMatchService
{
    Task<IReadOnlyList<MatchModel>> GetAllAsync(CancellationToken ct);
    Task<MatchModel?> GetAsync(string id, CancellationToken ct);
    Task<bool> ReplaceAllAsync(IReadOnlyList<MatchModel> matches, CancellationToken ct);
    Task<bool> CreateAsync(MatchModel match, CancellationToken ct);
    Task<bool> UpdateAsync(string id, MatchModel match, CancellationToken ct);
    Task<bool> DeleteAsync(string id, CancellationToken ct);
}

public interface IExpenseService
{
    Task<ExpenseStateModel> GetStateAsync(CancellationToken ct);
    Task<bool> ReplaceStateAsync(ExpenseStateModel state, CancellationToken ct);
    Task<ExpensePeriodModel?> GetPeriodAsync(string id, CancellationToken ct);
    Task<bool> CreatePeriodAsync(ExpensePeriodModel period, CancellationToken ct);
    Task<bool> UpdatePeriodAsync(string id, ExpensePeriodModel period, CancellationToken ct);
    Task<bool> DeletePeriodAsync(string id, CancellationToken ct);
    Task<bool> AddLineItemAsync(string periodId, string kind, ExpenseLineItemModel item, CancellationToken ct);
    Task<bool> UpdateLineItemAsync(string periodId, string itemId, string kind, ExpenseLineItemModel item, CancellationToken ct);
    Task<bool> DeleteLineItemAsync(string periodId, string itemId, string kind, CancellationToken ct);
}

public interface IReportSettingsService
{
    Task<JsonElement> GetAsync(CancellationToken ct);
    Task<bool> SaveAsync(JsonElement settings, CancellationToken ct);
    Task ResetAsync(CancellationToken ct);
}
