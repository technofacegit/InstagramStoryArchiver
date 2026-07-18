using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InstagramStoryArchiver.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoredInstagramUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextCheckAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConsecutiveFailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredInstagramUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedInstagramStories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MonitoredUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StoryKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    InstagramStoryId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalMediaUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StoredRelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DownloadedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedInstagramStories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedInstagramStories_MonitoredInstagramUsers_MonitoredUserId",
                        column: x => x.MonitoredUserId,
                        principalTable: "MonitoredInstagramUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedInstagramStories_MonitoredUserId",
                table: "ArchivedInstagramStories",
                column: "MonitoredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedInstagramStories_Username_StoryKey",
                table: "ArchivedInstagramStories",
                columns: new[] { "Username", "StoryKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredInstagramUsers_IsActive_NextCheckAt",
                table: "MonitoredInstagramUsers",
                columns: new[] { "IsActive", "NextCheckAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredInstagramUsers_Username",
                table: "MonitoredInstagramUsers",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedInstagramStories");

            migrationBuilder.DropTable(
                name: "MonitoredInstagramUsers");
        }
    }
}
