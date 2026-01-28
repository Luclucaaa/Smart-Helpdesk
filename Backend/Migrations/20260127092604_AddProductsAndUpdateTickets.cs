using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHelpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProductsAndUpdateTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Tickets",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<float>(
                name: "SentimentScore",
                table: "Tickets",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ProductId",
                table: "Tickets",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Products_ProductId",
                table: "Tickets",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Products_ProductId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_ProductId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SentimentScore",
                table: "Tickets");
        }
    }
}
