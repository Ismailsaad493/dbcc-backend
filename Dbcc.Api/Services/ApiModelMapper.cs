using System.Text.Json;
using Dbcc.Api.Contracts;
using Dbcc.Api.Models;

namespace Dbcc.Api.Services;

internal static class ApiModelMapper
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static PlayerEntity ToEntity(PlayerModel model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Batting = model.Batting,
        Bowling = model.Bowling,
        Fielding = model.Fielding,
        ExtraJson = ExtraJson(model.Extra),
    };

    public static PlayerModel ToModel(PlayerEntity entity)
    {
        var extra = ParseExtras(entity.ExtraJson);
        return new PlayerModel { Id = entity.Id, Name = entity.Name, Batting = entity.Batting, Bowling = entity.Bowling, Fielding = entity.Fielding, Extra = extra };
    }

    public static CollectionEntity ToEntity(CollectionModel model) => new()
    {
        PlayerId = model.Id,
        AmountPaid = model.AmountPaid,
        ExtraJson = ExtraJson(model.Extra),
    };

    public static CollectionModel ToModel(CollectionEntity entity) => new()
    {
        Id = entity.PlayerId,
        Name = entity.Player?.Name ?? "",
        AmountPaid = entity.AmountPaid,
        Extra = ParseExtras(entity.ExtraJson),
    };

    public static MatchEntity ToEntity(MatchModel model, int sortOrder) => new()
    {
        Id = model.Id,
        Date = model.Date,
        MatchNumber = model.MatchNumber,
        SortOrder = sortOrder,
        ExtraJson = ExtraJson(model.Extra),
        Points = model.Points.Select((point, index) => new MatchPlayerScoreEntity
        {
            MatchId = model.Id,
            PlayerId = point.PlayerId,
            PlayerName = point.PlayerName,
            Batting = point.Batting,
            Bowling = point.Bowling,
            Fielding = point.Fielding,
            SortOrder = index,
            ExtraJson = ExtraJson(point.Extra),
        }).ToList(),
        Expenses = model.Expenses.Select((expense, index) => new MatchExpenseEntity
        {
            MatchId = model.Id,
            Label = expense.Label,
            Amount = expense.Amount,
            SortOrder = index,
            ExtraJson = ExtraJson(expense.Extra),
        }).ToList(),
    };

    public static MatchModel ToModel(MatchEntity entity) => new()
    {
        Id = entity.Id,
        Date = entity.Date,
        MatchNumber = entity.MatchNumber,
        Extra = ParseExtras(entity.ExtraJson),
        Points = entity.Points.OrderBy(x => x.SortOrder).Select(point => new MatchPointModel
        {
            PlayerId = point.PlayerId,
            PlayerName = point.PlayerName,
            Batting = point.Batting,
            Bowling = point.Bowling,
            Fielding = point.Fielding,
            Extra = ParseExtras(point.ExtraJson),
        }).ToList(),
        Expenses = entity.Expenses.OrderBy(x => x.SortOrder).Select(expense => new MatchExpenseModel
        {
            Label = expense.Label,
            Amount = expense.Amount,
            Extra = ParseExtras(expense.ExtraJson),
        }).ToList(),
    };

    public static ExpenseTrackerEntity ToEntity(ExpenseStateModel model) => new()
    {
        Id = 1,
        ActiveId = model.ActiveId,
        Periods = model.Periods.Select((period, index) => ToEntity(period, index)).ToList(),
    };

    public static ExpenseStateModel ToModel(ExpenseTrackerEntity entity) => new()
    {
        ActiveId = entity.ActiveId,
        Periods = entity.Periods.OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
    };

    public static ExpensePeriodEntity ToEntity(ExpensePeriodModel model, int sortOrder) => new()
    {
        Id = model.Id,
        TrackerId = 1,
        SortOrder = sortOrder,
        Label = model.Label,
        OpeningBalanceLabel = model.OpeningBalanceLabel,
        OpeningBalance = model.OpeningBalance,
        EditableOpening = model.EditableOpening,
        CollectionLabel = model.CollectionLabel,
        CollectionAmountJson = Raw(model.CollectionAmount, "\"\""),
        ExtraJson = ExtraJson(model.Extra),
        Items = model.Income.Select((item, index) => ToEntity(model.Id, "Income", item, index))
            .Concat(model.Expenses.Select((item, index) => ToEntity(model.Id, "Expense", item, index))).ToList(),
    };

    public static ExpensePeriodModel ToModel(ExpensePeriodEntity entity) => new()
    {
        Id = entity.Id,
        Label = entity.Label,
        OpeningBalanceLabel = entity.OpeningBalanceLabel,
        OpeningBalance = entity.OpeningBalance,
        EditableOpening = entity.EditableOpening,
        CollectionLabel = entity.CollectionLabel,
        CollectionAmount = Element(entity.CollectionAmountJson, "\"\""),
        Extra = ParseExtras(entity.ExtraJson),
        Income = entity.Items.Where(x => x.Kind == "Income").OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
        Expenses = entity.Items.Where(x => x.Kind == "Expense").OrderBy(x => x.SortOrder).Select(ToModel).ToList(),
    };

    public static ExpenseLineItemEntity ToEntity(string periodId, string kind, ExpenseLineItemModel model, int order) => new()
    {
        PeriodId = periodId,
        ExternalId = string.IsNullOrWhiteSpace(model.Id) ? Guid.NewGuid().ToString("N") : model.Id,
        Kind = kind,
        SortOrder = order,
        Label = model.Label,
        AmountJson = Raw(model.Amount, "\"\""),
        Date = model.Date,
        ExtraJson = ExtraJson(model.Extra),
    };

    public static ExpenseLineItemModel ToModel(ExpenseLineItemEntity entity) => new()
    {
        Id = entity.ExternalId,
        Label = entity.Label,
        Amount = Element(entity.AmountJson, "\"\""),
        Date = entity.Date,
        Extra = ParseExtras(entity.ExtraJson),
    };

    public static string Raw(JsonElement element, string fallback) => element.ValueKind == JsonValueKind.Undefined ? fallback : element.GetRawText();
    public static string ExtraJson(Dictionary<string, JsonElement>? values) => values is null ? "{}" : JsonSerializer.Serialize(values, Options);
    public static Dictionary<string, JsonElement>? ParseExtras(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(string.IsNullOrWhiteSpace(json) ? "{}" : json, Options);

    public static JsonElement Element(string raw, string fallback)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? fallback : raw);
        return document.RootElement.Clone();
    }
}
