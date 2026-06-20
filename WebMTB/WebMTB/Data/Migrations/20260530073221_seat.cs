using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMTB.Data.Migrations
{
    /// <inheritdoc />
    public partial class seat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GridColumn",
                table: "Seats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GridRow",
                table: "Seats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GridColumn",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "GridRow",
                table: "Seats");
        }
    }
}
