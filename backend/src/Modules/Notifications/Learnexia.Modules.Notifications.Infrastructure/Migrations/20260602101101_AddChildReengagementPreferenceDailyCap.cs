using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learnexia.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChildReengagementPreferenceDailyCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyCap",
                schema: "notifications",
                table: "ChildReengagementPreferences",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyCap",
                schema: "notifications",
                table: "ChildReengagementPreferences");
        }
    }
}
