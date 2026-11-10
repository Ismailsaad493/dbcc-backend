using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Dbcc.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dbcc.Api.Data;

public static class DatabaseInitializer
{
    private static readonly string[] PlayerNames =
    [
        "Mujahid", "Nabeel", "Tahir", "Nathiq", "Fouzan", "Bilal", "Danish", "Sanaulla",
        "Pk", "Sharu", "Muzammil", "Faizal", "Eathu", "Saif", "Firdose", "Irfan",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task InitializeAsync(MongoDbContext db, string contentRoot, ILogger logger, CancellationToken ct = default)
    {
        await EnsureCollectionsAsync(db.Database, ct);
        if (await db.InitializationRecords.Find(x => x.Id == 1).AnyAsync(ct)) return;

        var dataDirectory = Environment.GetEnvironmentVariable("DBCC_DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDirectory))
            dataDirectory = Path.GetFullPath(Path.Combine(contentRoot, "..", "data"));

        var playersPath = Path.Combine(dataDirectory, "players.json");
        List<PlayerEntity> players;
        if (File.Exists(playersPath))
        {
            using var document = await ReadJsonAsync(playersPath, ct);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy players.json must contain an array.");
            players = document.RootElement.EnumerateArray().Select(item => new PlayerEntity
            {
                Id = Int(item, "id"), Name = String(item, "name"), Batting = Number(item, "batting"),
                Bowling = Number(item, "bowling"), Fielding = Number(item, "fielding"),
                ExtraJson = Extras(item, "id", "name", "batting", "bowling", "fielding"),
            }).Where(player => player.Id > 0).ToList();
        }
        else
        {
            players = PlayerNames.Select((name, index) => new PlayerEntity { Id = index + 1, Name = name }).ToList();
        }
        await UpsertManyAsync(db.Players, players, x => x.Id, ct);
        var playerIds = (await db.Players.Find(Builders<PlayerEntity>.Filter.Empty).Project(x => x.Id).ToListAsync(ct)).ToHashSet();

        var collectionsPath = Path.Combine(dataDirectory, "collections.json");
        List<CollectionEntity> collections;
        if (File.Exists(collectionsPath))
        {
            using var document = await ReadJsonAsync(collectionsPath, ct);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy collections.json must contain an array.");
            collections = document.RootElement.EnumerateArray().Select(item => new CollectionEntity
            {
                PlayerId = Int(item, "id"), AmountPaid = Number(item, "amountPaid"),
                ExtraJson = Extras(item, "id", "name", "amountPaid"),
            }).Where(item => playerIds.Contains(item.PlayerId)).ToList();
        }
        else
        {
            collections = playerIds.Select(id => new CollectionEntity { PlayerId = id }).ToList();
        }
        await UpsertManyAsync(db.Collections, collections, x => x.PlayerId, ct);

        var tracker = await db.ExpenseTrackers.Find(x => x.Id == 1).FirstOrDefaultAsync(ct)
            ?? new ExpenseTrackerEntity { Id = 1, ActiveId = null };
        var expensesPath = Path.Combine(dataDirectory, "expenses.json");
        if (File.Exists(expensesPath))
        {
            using var document = await ReadJsonAsync(expensesPath, ct);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("periods", out var periods) || periods.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy expenses.json must contain a periods array.");

            if (root.TryGetProperty("activeId", out var activeId) && activeId.ValueKind == JsonValueKind.String)
                tracker.ActiveId = activeId.GetString();
            var order = 0;
            tracker.Periods = periods.EnumerateArray().Where(period => period.ValueKind == JsonValueKind.Object)
                .Select(period => ReadPeriod(period, order++)).Where(period => !string.IsNullOrWhiteSpace(period.Id)).ToList();
        }
        await db.ExpenseTrackers.ReplaceOneAsync(x => x.Id == 1, tracker, new ReplaceOptions { IsUpsert = true }, ct);

        var matchesPath = Path.Combine(dataDirectory, "matches.json");
        if (File.Exists(matchesPath))
        {
            using var document = await ReadJsonAsync(matchesPath, ct);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Legacy matches.json must contain an array.");

            var order = 0;
            var matches = new List<MatchEntity>();
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
                            MatchId = id, PlayerId = playerId, PlayerName = String(point, "playerName"),
                            Batting = Number(point, "batting"), Bowling = Number(point, "bowling"), Fielding = Number(point, "fielding"),
                            SortOrder = pointOrder++, ExtraJson = Extras(point, "playerId", "playerName", "batting", "bowling", "fielding"),
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
                            MatchId = id, SortOrder = expenseOrder++, Label = String(expense, "label"), Amount = Number(expense, "amount"),
                            ExtraJson = Extras(expense, "label", "amount"),
                        });
                    }
                }
                matches.Add(match);
            }
            await UpsertManyAsync(db.Matches, matches, x => x.Id, ct);
        }

        var settingsPath = Path.Combine(dataDirectory, "reportSettings.json");
        var settings = await db.ReportSettings.Find(x => x.Id == 1).FirstOrDefaultAsync(ct);
        if (File.Exists(settingsPath))
        {
            var raw = await File.ReadAllTextAsync(settingsPath, ct);
            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Legacy reportSettings.json must contain an object.");
            settings = new ReportSettingsEntity { Id = 1, Json = raw };
        }
        settings ??= new ReportSettingsEntity { Id = 1, Json = DefaultReportSettings() };
        await db.ReportSettings.ReplaceOneAsync(x => x.Id == 1, settings, new ReplaceOptions { IsUpsert = true }, ct);

        await db.InitializationRecords.ReplaceOneAsync(x => x.Id == 1,
            new InitializationRecordEntity { Id = 1, CompletedAtUtc = DateTime.UtcNow }, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public static string DefaultReportSettings()
    {
        var today = DateTime.Now;
        var month = today.ToString("MMMM", CultureInfo.GetCultureInfo("en-US"));
        var year = (today.Year % 100).ToString("00", CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(new
        {
            monthLabel = $"{month} '{year}", tillDate = $"{today.Day:00}/{today.Month:00}/{year}",
            matchesPlayed = 1, wins = "", losses = "", showWinsLosses = false, customMessage = "",
        });
    }

    private static ExpensePeriodEntity ReadPeriod(JsonElement period, int order)
    {
        var entity = new ExpensePeriodEntity
        {
            Id = String(period, "id"), TrackerId = 1, SortOrder = order,
            Label = String(period, "label"), OpeningBalanceLabel = String(period, "openingBalanceLabel"),
            OpeningBalance = Number(period, "openingBalance"), EditableOpening = Bool(period, "editableOpening"),
            CollectionLabel = String(period, "collectionLabel"), CollectionAmountJson = Raw(period, "collectionAmount", "\"\""),
            ExtraJson = Extras(period, "id", "label", "openingBalanceLabel", "openingBalance", "editableOpening", "collectionLabel", "collectionAmount", "income", "expenses"),
        };
        AddItems(entity, period, "income", "Income");
        AddItems(entity, period, "expenses", "Expense");
        return entity;
    }

    private static async Task UpsertManyAsync<T, TKey>(IMongoCollection<T> collection, IEnumerable<T> documents,
        Expression<Func<T, TKey>> key, CancellationToken ct)
    {
        var keyValue = key.Compile();
        var writes = documents.Select(document => (WriteModel<T>)new ReplaceOneModel<T>(
            Builders<T>.Filter.Eq(key, keyValue(document)), document) { IsUpsert = true }).ToList();
        if (writes.Count > 0) await collection.BulkWriteAsync(writes, cancellationToken: ct);
    }

    private static async Task EnsureCollectionsAsync(IMongoDatabase database, CancellationToken ct)
    {
        foreach (var name in new[] { "players", "collections", "matches", "expenseTracker", "reportSettings", "databaseInitialization" })
        {
            try
            {
                await database.CreateCollectionAsync(name, cancellationToken: ct);
            }
            catch (MongoCommandException exception) when (exception.Code == 48)
            {
                // The collection already exists.
            }
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
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
                Kind = kind, SortOrder = order++, Label = String(item, "label"), AmountJson = Raw(item, "amount", "\"\""),
                Date = item.TryGetProperty("date", out var date) && date.ValueKind == JsonValueKind.String ? date.GetString() : null,
                ExtraJson = Extras(item, "id", "label", "amount", "date"),
            });
        }
    }

    internal static string Raw(JsonElement source, string name, string fallback) =>
        source.TryGetProperty(name, out var property) ? property.GetRawText() : fallback;

    internal static string String(JsonElement source, string name) =>
        source.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : "";

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
