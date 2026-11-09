using System.Globalization;
using System.Text.Json;
using Dbcc.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Dbcc.Api.Data;

public static class DatabaseInitializer
{
    private static readonly string[] PlayerNames =
    [
        "Mujahid", "Nabeel", "Tahir", "Nathiq", "Fouzan", "Bilal", "Danish", "Sanaulla",
        "Pk", "Sharu", "Muzammil", "Faizal", "Eathu", "Saif", "Firdose", "Irfan",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task InitializeAsync(DbccDbContext db, string contentRoot, ILogger logger)
    {
        if (await db.InitializationRecords.AnyAsync(x => x.Id == 1)) return;

        await using var transaction = await db.Database.BeginTransactionAsync();
        var dataDirectory = Environment.GetEnvironmentVariable("DBCC_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = Path.GetFullPath(Path.Combine(contentRoot, "..", "data"));
        }

        var playersPath = Path.Combine(dataDirectory, "players.json");
        if (File.Exists(playersPath))
        {
            using var document = await ReadJsonAsync(playersPath);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy players.json must contain an array.");

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var id = Int(item, "id");
                if (id <= 0) continue;
                db.Players.Add(new PlayerEntity
                {
                    Id = id,
                    Name = String(item, "name"),
                    Batting = Number(item, "batting"),
                    Bowling = Number(item, "bowling"),
                    Fielding = Number(item, "fielding"),
                    ExtraJson = Extras(item, "id", "name", "batting", "bowling", "fielding"),
                });
            }
        }
        else
        {
            db.Players.AddRange(PlayerNames.Select((name, index) => new PlayerEntity { Id = index + 1, Name = name }));
        }

        await db.SaveChangesAsync();
        var playerIds = (await db.Players.Select(x => x.Id).ToListAsync()).ToHashSet();

        var collectionsPath = Path.Combine(dataDirectory, "collections.json");
        if (File.Exists(collectionsPath))
        {
            using var document = await ReadJsonAsync(collectionsPath);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy collections.json must contain an array.");

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var id = Int(item, "id");
                if (!playerIds.Contains(id)) continue;
                db.Collections.Add(new CollectionEntity
                {
                    PlayerId = id,
                    AmountPaid = Number(item, "amountPaid"),
                    ExtraJson = Extras(item, "id", "name", "amountPaid"),
                });
            }
        }
        else
        {
            db.Collections.AddRange(playerIds.Select(id => new CollectionEntity { PlayerId = id }));
        }

        db.ExpenseTrackers.Add(new ExpenseTrackerEntity { Id = 1, ActiveId = null });
        db.ReportSettings.Add(new ReportSettingsEntity { Id = 1, Json = DefaultReportSettings() });
        await db.SaveChangesAsync();

        var expensesPath = Path.Combine(dataDirectory, "expenses.json");
        if (File.Exists(expensesPath))
        {
            using var document = await ReadJsonAsync(expensesPath);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("periods", out var periods) || periods.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy expenses.json must contain a periods array.");

            var tracker = await db.ExpenseTrackers.SingleAsync(x => x.Id == 1);
            if (root.TryGetProperty("activeId", out var activeId) && activeId.ValueKind == JsonValueKind.String)
                tracker.ActiveId = activeId.GetString();

            var order = 0;
            foreach (var period in periods.EnumerateArray())
            {
                if (period.ValueKind != JsonValueKind.Object) continue;
                var id = String(period, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var entity = new ExpensePeriodEntity
                {
                    Id = id,
                    TrackerId = 1,
                    SortOrder = order++,
                    Label = String(period, "label"),
                    OpeningBalanceLabel = String(period, "openingBalanceLabel"),
                    OpeningBalance = Number(period, "openingBalance"),
                    EditableOpening = Bool(period, "editableOpening"),
                    CollectionLabel = String(period, "collectionLabel"),
                    CollectionAmountJson = Raw(period, "collectionAmount", "\"\""),
                    ExtraJson = Extras(period, "id", "label", "openingBalanceLabel", "openingBalance", "editableOpening", "collectionLabel", "collectionAmount", "income", "expenses"),
                };
                AddItems(entity, period, "income", "Income");
                AddItems(entity, period, "expenses", "Expense");
                db.ExpensePeriods.Add(entity);
            }
            await db.SaveChangesAsync();
        }

        var matchesPath = Path.Combine(dataDirectory, "matches.json");
        if (File.Exists(matchesPath))
        {
            using var document = await ReadJsonAsync(matchesPath);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy matches.json must contain an array.");

            var order = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = String(item, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var match = new MatchEntity
                {
                    Id = id,
                    Date = String(item, "date"),
                    MatchNumber = String(item, "matchNumber"),
                    SortOrder = order++,
                    ExtraJson = Extras(item, "id", "date", "matchNumber", "points", "expenses"),
                };

                if (item.TryGetProperty("points", out var points) && points.ValueKind == JsonValueKind.Array)
                {
                    var pointOrder = 0;
                    foreach (var point in points.EnumerateArray())
                    {
                        var playerId = Int(point, "playerId");
                        if (!playerIds.Contains(playerId))
                        {
                            logger.LogWarning("Skipping legacy match point for missing player {PlayerId} in match {MatchId}.", playerId, id);
                            continue;
                        }
                        match.Points.Add(new MatchPlayerScoreEntity
                        {
                            MatchId = id,
                            PlayerId = playerId,
                            PlayerName = String(point, "playerName"),
                            Batting = Number(point, "batting"),
                            Bowling = Number(point, "bowling"),
                            Fielding = Number(point, "fielding"),
                            SortOrder = pointOrder++,
                            ExtraJson = Extras(point, "playerId", "playerName", "batting", "bowling", "fielding"),
                        });
                    }
                }

                if (item.TryGetProperty("expenses", out var matchExpenses) && matchExpenses.ValueKind == JsonValueKind.Array)
                {
                    var expenseOrder = 0;
                    foreach (var expense in matchExpenses.EnumerateArray())
                    {
                        match.Expenses.Add(new MatchExpenseEntity
                        {
                            MatchId = id,
                            SortOrder = expenseOrder++,
                            Label = String(expense, "label"),
                            Amount = Number(expense, "amount"),
                            ExtraJson = Extras(expense, "label", "amount"),
                        });
                    }
                }
                db.Matches.Add(match);
            }
            await db.SaveChangesAsync();
        }

        var settingsPath = Path.Combine(dataDirectory, "reportSettings.json");
        if (File.Exists(settingsPath))
        {
            var raw = await File.ReadAllTextAsync(settingsPath);
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Legacy reportSettings.json must contain an object.");
            var settings = await db.ReportSettings.SingleAsync(x => x.Id == 1);
            settings.Json = raw;
            await db.SaveChangesAsync();
        }

        db.InitializationRecords.Add(new InitializationRecordEntity { Id = 1, CompletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public static string DefaultReportSettings()
    {
        var today = DateTime.Now;
        var month = today.ToString("MMMM", CultureInfo.GetCultureInfo("en-US"));
        var year = (today.Year % 100).ToString("00", CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(new
        {
            monthLabel = $"{month} '{year}",
            tillDate = $"{today.Day:00}/{today.Month:00}/{year}",
            matchesPlayed = 1,
            wins = "",
            losses = "",
            showWinsLosses = false,
            customMessage = "",
        });
    }

    private static async Task<JsonDocument> ReadJsonAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { AllowTrailingCommas = false });
    }

    private static void AddItems(ExpensePeriodEntity period, JsonElement source, string property, string kind)
    {
        if (!source.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array) return;
        var order = 0;
        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            period.Items.Add(new ExpenseLineItemEntity
            {
                PeriodId = period.Id,
                ExternalId = String(item, "id") is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N"),
                Kind = kind,
                SortOrder = order++,
                Label = String(item, "label"),
                AmountJson = Raw(item, "amount", "\"\""),
                Date = item.TryGetProperty("date", out var date) && date.ValueKind == JsonValueKind.String ? date.GetString() : null,
                ExtraJson = Extras(item, "id", "label", "amount", "date"),
            });
        }
    }

    internal static string Raw(JsonElement source, string name, string fallback) =>
        source.TryGetProperty(name, out var property) ? property.GetRawText() : fallback;

    internal static string String(JsonElement source, string name) =>
        source.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    internal static double Number(JsonElement source, string name) =>
        source.TryGetProperty(name, out var property) && property.TryGetDouble(out var number) ? number : 0;

    internal static int Int(JsonElement source, string name) =>
        source.TryGetProperty(name, out var property) && property.TryGetInt32(out var number) ? number : 0;

    internal static bool Bool(JsonElement source, string name) =>
        source.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;

    internal static string Extras(JsonElement source, params string[] knownNames)
    {
        if (source.ValueKind != JsonValueKind.Object) return "{}";
        var known = knownNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extras = source.EnumerateObject().Where(property => !known.Contains(property.Name))
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        return JsonSerializer.Serialize(extras, JsonOptions);
    }
}
