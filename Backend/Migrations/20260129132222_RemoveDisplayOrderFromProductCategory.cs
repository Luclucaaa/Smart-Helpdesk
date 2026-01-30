using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHelpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDisplayOrderFromProductCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "ProductCategories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "ProductCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
