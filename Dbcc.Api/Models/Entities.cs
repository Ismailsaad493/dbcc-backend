namespace Dbcc.Api.Models;

public sealed class PlayerEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Batting { get; set; }
    public double Bowling { get; set; }
    public double Fielding { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public CollectionEntity? Collection { get; set; }
    public ICollection<MatchPlayerScoreEntity> MatchScores { get; set; } = new List<MatchPlayerScoreEntity>();
}

public sealed class CollectionEntity
{
    public int PlayerId { get; set; }
    public double AmountPaid { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public PlayerEntity Player { get; set; } = null!;
}

public sealed class MatchEntity
{
    public string Id { get; set; } = "";
    public string Date { get; set; } = "";
    public string MatchNumber { get; set; } = "";
    public int SortOrder { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public ICollection<MatchPlayerScoreEntity> Points { get; set; } = new List<MatchPlayerScoreEntity>();
    public ICollection<MatchExpenseEntity> Expenses { get; set; } = new List<MatchExpenseEntity>();
}

public sealed class MatchPlayerScoreEntity
{
    public string MatchId { get; set; } = "";
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public double Batting { get; set; }
    public double Bowling { get; set; }
    public double Fielding { get; set; }
    public int SortOrder { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public MatchEntity Match { get; set; } = null!;
    public PlayerEntity Player { get; set; } = null!;
}

public sealed class MatchExpenseEntity
{
    public int Id { get; set; }
    public string MatchId { get; set; } = "";
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public double Amount { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public MatchEntity Match { get; set; } = null!;
}

public sealed class ExpenseTrackerEntity
{
    public int Id { get; set; } = 1;
    public string? ActiveId { get; set; }
    public ICollection<ExpensePeriodEntity> Periods { get; set; } = new List<ExpensePeriodEntity>();
}

public sealed class ExpensePeriodEntity
{
    public string Id { get; set; } = "";
    public int TrackerId { get; set; } = 1;
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public string OpeningBalanceLabel { get; set; } = "";
    public double OpeningBalance { get; set; }
    public bool EditableOpening { get; set; }
    public string CollectionLabel { get; set; } = "";
    public string CollectionAmountJson { get; set; } = "\"\"";
    public string ExtraJson { get; set; } = "{}";
    public ExpenseTrackerEntity Tracker { get; set; } = null!;
    public ICollection<ExpenseLineItemEntity> Items { get; set; } = new List<ExpenseLineItemEntity>();
}

public sealed class ExpenseLineItemEntity
{
    public int Id { get; set; }
    public string PeriodId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Kind { get; set; } = "Income";
    public int SortOrder { get; set; }
    public string Label { get; set; } = "";
    public string AmountJson { get; set; } = "\"\"";
    public string? Date { get; set; }
    public string ExtraJson { get; set; } = "{}";
    public ExpensePeriodEntity Period { get; set; } = null!;
}

public sealed class ReportSettingsEntity
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = "{}";
}

public sealed class InitializationRecordEntity
{
    public int Id { get; set; } = 1;
    public DateTime CompletedAtUtc { get; set; }
}
