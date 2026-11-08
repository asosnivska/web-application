using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slae.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MaxConcurrentTasks = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_MaxConcurrentTasks_Positive", "\"MaxConcurrentTasks\" >= 1");
                    table.CheckConstraint("CK_Users_Username_NotBlank", "char_length(btrim(\"Username\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "SlaeTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    ParamsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    IsCanceled = table.Column<bool>(type: "boolean", nullable: false),
                    ServerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WorkerLastReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaeTasks", x => x.Id);
                    table.CheckConstraint("CK_SlaeTasks_Attempts_NonNegative", "\"Attempts\" >= 0");
                    table.CheckConstraint("CK_SlaeTasks_Dimension_Positive", "\"Dimension\" > 0");
                    table.CheckConstraint("CK_SlaeTasks_Finished_HasCompletedAt", "\"Status\" NOT IN (2, 3, 4) OR \"CompletedAt\" IS NOT NULL");
                    table.CheckConstraint("CK_SlaeTasks_InProgress_HasServer", "\"Status\" <> 1 OR \"ServerId\" IS NOT NULL");
                    table.CheckConstraint("CK_SlaeTasks_Progress_Range", "\"Progress\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_SlaeTasks_Status_Valid", "\"Status\" IN (0, 1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_SlaeTasks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaeTasks_InProgress_Heartbeat",
                table: "SlaeTasks",
                column: "WorkerLastReportedAt",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SlaeTasks_Queue",
                table: "SlaeTasks",
                column: "CreatedAt",
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SlaeTasks_UserId_CreatedAt",
                table: "SlaeTasks",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SlaeTasks_UserId_Status",
                table: "SlaeTasks",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlaeTasks");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
