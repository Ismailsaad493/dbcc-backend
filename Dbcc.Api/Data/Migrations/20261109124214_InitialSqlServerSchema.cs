using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dbcc.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DatabaseInitialization",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseInitialization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseTrackerState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ActiveId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseTrackerState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Date = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MatchNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Batting = table.Column<double>(type: "float", nullable: false),
                    Bowling = table.Column<double>(type: "float", nullable: false),
                    Fielding = table.Column<double>(type: "float", nullable: false),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpensePeriods",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TrackerId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OpeningBalanceLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OpeningBalance = table.Column<double>(type: "float", nullable: false),
                    EditableOpening = table.Column<bool>(type: "bit", nullable: false),
                    CollectionLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CollectionAmountJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpensePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpensePeriods_ExpenseTrackerState_TrackerId",
                        column: x => x.TrackerId,
                        principalTable: "ExpenseTrackerState",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<double>(type: "float", nullable: false),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchExpenses_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    AmountPaid = table.Column<double>(type: "float", nullable: false),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_Collections_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayerScores",
                columns: table => new
                {
                    MatchId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Batting = table.Column<double>(type: "float", nullable: false),
                    Bowling = table.Column<double>(type: "float", nullable: false),
                    Fielding = table.Column<double>(type: "float", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerScores", x => new { x.MatchId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_MatchPlayerScores_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchPlayerScores_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseLineItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PeriodId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AmountJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtraJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseLineItems_ExpensePeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "ExpensePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseLineItems_PeriodId_Kind_SortOrder",
                table: "ExpenseLineItems",
                columns: new[] { "PeriodId", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpensePeriods_TrackerId",
                table: "ExpensePeriods",
                column: "TrackerId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchExpenses_MatchId",
                table: "MatchExpenses",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerScores_PlayerId",
                table: "MatchPlayerScores",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "DatabaseInitialization");

            migrationBuilder.DropTable(
                name: "ExpenseLineItems");

            migrationBuilder.DropTable(
                name: "MatchExpenses");

            migrationBuilder.DropTable(
                name: "MatchPlayerScores");

            migrationBuilder.DropTable(
                name: "ReportSettings");

            migrationBuilder.DropTable(
                name: "ExpensePeriods");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "ExpenseTrackerState");
        }
    }
}
