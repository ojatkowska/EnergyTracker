using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnergyTracker.Server.Data.Migrations
{
    public partial class AddedIsWorkingDayColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWorkingDay",
                table: "Kses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsWorkingDay",
                table: "Kses");
        }
    }
}
