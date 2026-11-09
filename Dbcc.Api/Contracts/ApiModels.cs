using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbcc.Api.Contracts;

public sealed class PlayerModel
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public double Batting { get; set; }
    public double Bowling { get; set; }
    public double Fielding { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class CollectionModel
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public double AmountPaid { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class MatchModel
{
    [Required, StringLength(128)] public string Id { get; set; } = "";
    [Required, StringLength(40)] public string Date { get; set; } = "";
    [Required, StringLength(40)] public string MatchNumber { get; set; } = "";
    public List<MatchPointModel> Points { get; set; } = [];
    public List<MatchExpenseModel> Expenses { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class MatchPointModel
{
    [Range(1, int.MaxValue)] public int PlayerId { get; set; }
    [StringLength(100)] public string PlayerName { get; set; } = "";
    public double Batting { get; set; }
    public double Bowling { get; set; }
    public double Fielding { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class MatchExpenseModel
{
    [Required, StringLength(300)] public string Label { get; set; } = "";
    public double Amount { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class ExpenseStateModel
{
    public List<ExpensePeriodModel> Periods { get; set; } = [];
    public string? ActiveId { get; set; }
}

public sealed class ExpensePeriodModel
{
    [Required, StringLength(64)] public string Id { get; set; } = "";
    [StringLength(200)] public string Label { get; set; } = "";
    [StringLength(200)] public string OpeningBalanceLabel { get; set; } = "";
    public double OpeningBalance { get; set; }
    public bool EditableOpening { get; set; }
    [StringLength(200)] public string CollectionLabel { get; set; } = "";
    public JsonElement CollectionAmount { get; set; }
    public List<ExpenseLineItemModel> Income { get; set; } = [];
    public List<ExpenseLineItemModel> Expenses { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class ExpenseLineItemModel
{
    [StringLength(64)] public string Id { get; set; } = "";
    [StringLength(300)] public string Label { get; set; } = "";
    public JsonElement Amount { get; set; }
    [StringLength(40), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Date { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class OkResultModel
{
    public bool Ok { get; set; } = true;
}

public sealed class ErrorResultModel(string error)
{
    public string Error { get; } = error;
}
