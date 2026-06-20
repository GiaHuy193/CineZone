using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMTB.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameExpireAtToExpiresAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExpireAt",
                table: "Bookings",
                newName: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "Bookings",
                newName: "ExpireAt");
        }
    }
}
