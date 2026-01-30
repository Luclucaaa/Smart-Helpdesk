using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHelpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullableProductAndAddProductName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "Tickets",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "Tickets");
        }
    }
}
