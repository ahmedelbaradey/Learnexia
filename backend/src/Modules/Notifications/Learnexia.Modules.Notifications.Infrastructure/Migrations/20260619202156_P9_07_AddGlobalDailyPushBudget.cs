using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P9_07_AddGlobalDailyPushBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GlobalDailyPushBudget",
                schema: "notifications",
                table: "ChildReengagementPreferences",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlobalDailyPushBudget",
                schema: "notifications",
                table: "ChildReengagementPreferences");
        }
    }
}
