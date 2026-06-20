using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebMTB.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingExpiresAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpireAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpireAt",
                table: "Bookings");
        }
    }
}
