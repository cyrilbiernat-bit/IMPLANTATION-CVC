using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bim.Cvc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingModelAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OffsetXMeters",
                table: "building_models",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OffsetZMeters",
                table: "building_models",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OffsetXMeters",
                table: "building_models");

            migrationBuilder.DropColumn(
                name: "OffsetZMeters",
                table: "building_models");
        }
    }
}
