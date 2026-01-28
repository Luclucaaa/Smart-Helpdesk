using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartHelpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedIdentityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserClaims");
            migrationBuilder.DropTable(name: "UserLogins");
            migrationBuilder.DropTable(name: "UserTokens");
            migrationBuilder.DropTable(name: "RoleClaims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không cần restore lại các bảng này
        }
    }
}
