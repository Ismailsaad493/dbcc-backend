using System.Text.Json;
using Dbcc.Api.Contracts;
using Dbcc.Api.Data;
using Dbcc.Api.Models;
using Dbcc.Api.Repositories;

namespace Dbcc.Api.Services;

public sealed class PlayerService(IPlayerRepository repository) : IPlayerService
{
    public async Task<IReadOnlyList<PlayerModel>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ApiModelMapper.ToModel).ToList();

    public async Task<PlayerModel?> GetAsync(int id, CancellationToken ct)
    {
        var entity = await repository.FindAsync(id, ct);
        return entity is null ? null : ApiModelMapper.ToModel(entity);
    }

    public Task<bool> ReplaceAllAsync(IReadOnlyList<PlayerModel> players, CancellationToken ct)
    {
        if (!ValidPlayers(players) || players.Count != DatabaseInitializerNames.Count) return Task.FromResult(false);
        return Replace(players, ct);
    }

    private async Task<bool> Replace(IReadOnlyList<PlayerModel> players, CancellationToken ct)
    {
        await repository.ReplaceAllAsync(players.Select(ApiModelMapper.ToEntity).ToList(), ct);
        return true;
    }

    public async Task<bool> CreateAsync(PlayerModel player, CancellationToken ct)
    {
        if (player.Id <= 0 || !ValidPlayer(player) || await repository.FindAsync(player.Id, ct) is not null) return false;
        await repository.AddAsync(ApiModelMapper.ToEntity(player), ct);
        return true;
    }

    public async Task<bool> UpdateAsync(int id, PlayerModel player, CancellationToken ct)
    {
        if (id != player.Id || !ValidPlayer(player)) return false;
        var current = await repository.FindAsync(id, ct);
        if (current is null) return false;
        current.Name = player.Name;
        current.Batting = player.Batting;
        current.Bowling = player.Bowling;
        current.Fielding = player.Fielding;
        current.ExtraJson = ApiModelMapper.ExtraJson(player.Extra);
        await repository.UpdateAsync(current, ct);
        return true;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct) => repository.DeleteAsync(id, ct);

    public static bool ValidPlayers(IReadOnlyCollection<PlayerModel> values) =>
        values.All(player => player is not null && ValidPlayer(player)) && values.Select(x => x.Id).Distinct().Count() == values.Count;

    private static bool ValidPlayer(PlayerModel? player) => player is not null && player.Id > 0 && !string.IsNullOrWhiteSpace(player.Name) &&
        double.IsFinite(player.Batting) && double.IsFinite(player.Bowling) && double.IsFinite(player.Fielding);

    private static class DatabaseInitializerNames
    {
        public const int Count = 16;
    }
}

public sealed class CollectionService(ICollectionRepository repository, IPlayerRepository players) : ICollectionService
{
    public async Task<IReadOnlyList<CollectionModel>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ApiModelMapper.ToModel).ToList();

    public async Task<CollectionModel?> GetAsync(int id, CancellationToken ct)
    {
        var entity = await repository.FindAsync(id, ct);
        return entity is null ? null : ApiModelMapper.ToModel(entity);
    }

    public async Task<bool> ReplaceAllAsync(IReadOnlyList<CollectionModel> collections, CancellationToken ct)
    {
        if (collections.Count != 16 || collections.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name) || !double.IsFinite(x.AmountPaid)) ||
            collections.Select(x => x.Id).Distinct().Count() != collections.Count) return false;
        var playerIds = (await players.GetAllAsync(ct)).Select(x => x.Id).ToHashSet();
        if (collections.Any(x => !playerIds.Contains(x.Id))) return false;
        await repository.ReplaceAllAsync(collections.Select(ApiModelMapper.ToEntity).ToList(), ct);
        return true;
    }

    public async Task<bool> CreateAsync(CollectionModel collection, CancellationToken ct)
    {
        if (collection.Id <= 0 || !double.IsFinite(collection.AmountPaid) ||
            await repository.FindAsync(collection.Id, ct) is not null) return false;
        var player = await players.FindAsync(collection.Id, ct);
        if (player is null) return false;
        await repository.AddAsync(new CollectionEntity
        {
            PlayerId = collection.Id,
            AmountPaid = collection.AmountPaid,
            ExtraJson = ApiModelMapper.ExtraJson(collection.Extra),
        }, ct);
        return true;
    }

    public async Task<bool> UpdateAsync(int id, CollectionModel collection, CancellationToken ct)
    {
        if (id != collection.Id || collection.Id <= 0 || !double.IsFinite(collection.AmountPaid)) return false;
        var current = await repository.FindAsync(id, ct);
        if (current is null) return false;
        current.AmountPaid = collection.AmountPaid;
        current.ExtraJson = ApiModelMapper.ExtraJson(collection.Extra);
        await repository.UpdateAsync(current, ct);
        return true;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct) => repository.DeleteAsync(id, ct);
}

public sealed class MatchService(IMatchRepository repository, IPlayerRepository players) : IMatchService
{
    public async Task<IReadOnlyList<MatchModel>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ApiModelMapper.ToModel).ToList();

    public async Task<MatchModel?> GetAsync(string id, CancellationToken ct)
    {
        var entity = await repository.FindAsync(id, ct);
        return entity is null ? null : ApiModelMapper.ToModel(entity);
    }

    public async Task<bool> ReplaceAllAsync(IReadOnlyList<MatchModel> matches, CancellationToken ct)
    {
        if (matches.Any(x => !ValidMatch(x)) || matches.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != matches.Count)
            return false;
        if (!await ValidPlayerReferencesAsync(matches, ct)) return false;
        await repository.ReplaceAllAsync(matches.Select((x, index) => ApiModelMapper.ToEntity(x, index)).ToList(), ct);
        return true;
    }

    public async Task<bool> CreateAsync(MatchModel match, CancellationToken ct)
    {
        if (!ValidMatch(match) || await repository.FindAsync(match.Id, ct) is not null ||
            !await ValidPlayerReferencesAsync([match], ct)) return false;
        var current = await repository.GetAllAsync(ct);
        await repository.AddAsync(ApiModelMapper.ToEntity(match, current.Count), ct);
        return true;
    }

    public async Task<bool> UpdateAsync(string id, MatchModel match, CancellationToken ct)
    {
        if (!string.Equals(id, match.Id, StringComparison.Ordinal) || !ValidMatch(match) ||
            !await ValidPlayerReferencesAsync([match], ct)) return false;
        var current = await repository.FindAsync(id, ct);
        if (current is null) return false;
        await repository.UpdateAsync(ApiModelMapper.ToEntity(match, current.SortOrder), ct);
        return true;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct) => repository.DeleteAsync(id, ct);

    private static bool ValidMatch(MatchModel? match) => match is not null && !string.IsNullOrWhiteSpace(match.Id) &&
        !string.IsNullOrWhiteSpace(match.Date) && !string.IsNullOrWhiteSpace(match.MatchNumber) &&
        match.Points is not null && match.Expenses is not null &&
        match.Points.All(x => x is not null && x.PlayerId > 0 && x.PlayerName is not null && x.PlayerName.Length <= 100 && double.IsFinite(x.Batting) && double.IsFinite(x.Bowling) && double.IsFinite(x.Fielding)) &&
        match.Points.Select(x => x.PlayerId).Distinct().Count() == match.Points.Count &&
        match.Expenses.All(x => x is not null && x.Label is not null && x.Label.Length <= 300 && double.IsFinite(x.Amount));

    private async Task<bool> ValidPlayerReferencesAsync(IEnumerable<MatchModel> matches, CancellationToken ct)
    {
        var ids = (await players.GetAllAsync(ct)).Select(x => x.Id).ToHashSet();
        return matches.SelectMany(x => x.Points).All(x => ids.Contains(x.PlayerId));
    }
}

public sealed class ExpenseService(IExpenseRepository repository) : IExpenseService
{
    public async Task<ExpenseStateModel> GetStateAsync(CancellationToken ct)
    {
        var state = await repository.GetStateAsync(ct);
        return state is null ? new ExpenseStateModel() : ApiModelMapper.ToModel(state);
    }

    public async Task<bool> ReplaceStateAsync(ExpenseStateModel state, CancellationToken ct)
    {
        if (!ValidState(state)) return false;
        await repository.ReplaceStateAsync(ApiModelMapper.ToEntity(state), ct);
        return true;
    }

    public async Task<ExpensePeriodModel?> GetPeriodAsync(string id, CancellationToken ct)
    {
        var entity = await repository.FindPeriodAsync(id, ct);
        return entity is null ? null : ApiModelMapper.ToModel(entity);
    }

    public async Task<bool> CreatePeriodAsync(ExpensePeriodModel period, CancellationToken ct)
    {
        if (!ValidPeriod(period)) return false;
        var state = await GetStateAsync(ct);
        if (state.Periods.Any(x => x.Id == period.Id)) return false;
        state.Periods.Add(period);
        await ReplaceStateAsync(state, ct);
        return true;
    }

    public async Task<bool> UpdatePeriodAsync(string id, ExpensePeriodModel period, CancellationToken ct)
    {
        if (!string.Equals(id, period.Id, StringComparison.Ordinal) || !ValidPeriod(period)) return false;
        var state = await GetStateAsync(ct);
        var index = state.Periods.FindIndex(x => x.Id == id);
        if (index < 0) return false;
        state.Periods[index] = period;
        await ReplaceStateAsync(state, ct);
        return true;
    }

    public async Task<bool> DeletePeriodAsync(string id, CancellationToken ct)
    {
        var state = await GetStateAsync(ct);
        var removed = state.Periods.RemoveAll(x => x.Id == id) > 0;
        if (!removed) return false;
        if (state.ActiveId == id) state.ActiveId = state.Periods.LastOrDefault()?.Id;
        await ReplaceStateAsync(state, ct);
        return true;
    }

    public async Task<bool> AddLineItemAsync(string periodId, string kind, ExpenseLineItemModel item, CancellationToken ct)
    {
        if (!ValidLineItem(item)) return false;
        var period = await GetPeriodAsync(periodId, ct);
        if (period is null) return false;
        var items = Items(period, kind);
        if (items is null) return false;
        if (string.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
        if (items.Any(x => x.Id == item.Id)) return false;
        items.Add(item);
        return await UpdatePeriodAsync(periodId, period, ct);
    }

    public async Task<bool> UpdateLineItemAsync(string periodId, string itemId, string kind, ExpenseLineItemModel item, CancellationToken ct)
    {
        if (!ValidLineItem(item)) return false;
        var period = await GetPeriodAsync(periodId, ct);
        var items = period is null ? null : Items(period, kind);
        if (period is null || items is null) return false;
        var index = items.FindIndex(x => x.Id == itemId);
        if (index < 0) return false;
        item.Id = itemId;
        items[index] = item;
        return await UpdatePeriodAsync(periodId, period, ct);
    }

    public async Task<bool> DeleteLineItemAsync(string periodId, string itemId, string kind, CancellationToken ct)
    {
        var period = await GetPeriodAsync(periodId, ct);
        var items = period is null ? null : Items(period, kind);
        if (items is null || items.RemoveAll(x => x.Id == itemId) == 0) return false;
        return await UpdatePeriodAsync(periodId, period!, ct);
    }

    public static bool ValidState(ExpenseStateModel? state) => state is not null && state.Periods is not null &&
        state.Periods.All(ValidPeriod) && state.Periods.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == state.Periods.Count;

    private static bool ValidPeriod(ExpensePeriodModel? period) => period is not null && !string.IsNullOrWhiteSpace(period.Id) &&
        period.Label is not null && period.Label.Length <= 200 && period.OpeningBalanceLabel is not null && period.OpeningBalanceLabel.Length <= 200 &&
        period.CollectionLabel is not null && period.CollectionLabel.Length <= 200 && double.IsFinite(period.OpeningBalance) &&
        period.Income is not null && period.Expenses is not null && period.Income.All(ValidLineItem) && period.Expenses.All(ValidLineItem);

    private static bool ValidLineItem(ExpenseLineItemModel? item) => item is not null && item.Id is not null && item.Label is not null && item.Id.Length <= 64 &&
        item.Label.Length <= 300 && (item.Date is null || item.Date.Length <= 40);

    private static List<ExpenseLineItemModel>? Items(ExpensePeriodModel period, string kind) => kind.ToLowerInvariant() switch
    {
        "income" => period.Income,
        "expense" or "expenses" => period.Expenses,
        _ => null,
    };
}

public sealed class ReportSettingsService(IReportSettingsRepository repository) : IReportSettingsService
{
    public async Task<JsonElement> GetAsync(CancellationToken ct)
    {
        var json = await repository.GetJsonAsync(ct) ?? DatabaseInitializer.DefaultReportSettings();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public async Task<bool> SaveAsync(JsonElement settings, CancellationToken ct)
    {
        if (settings.ValueKind != JsonValueKind.Object || !ValidKnownTypes(settings)) return false;
        await repository.SaveJsonAsync(settings.GetRawText(), ct);
        return true;
    }

    public Task ResetAsync(CancellationToken ct) => repository.ResetAsync(DatabaseInitializer.DefaultReportSettings(), ct);

    private static bool ValidKnownTypes(JsonElement settings)
    {
        foreach (var field in new[] { "monthLabel", "tillDate", "customMessage" })
            if (settings.TryGetProperty(field, out var text) && text.ValueKind != JsonValueKind.String) return false;
        foreach (var field in new[] { "matchesPlayed", "wins", "losses" })
            if (settings.TryGetProperty(field, out var count) && count.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) return false;
        return !settings.TryGetProperty("showWinsLosses", out var flag) || flag.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }
}
