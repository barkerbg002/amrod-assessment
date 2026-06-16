using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SadcOMS.Infrastructure.Migrations
{
    [Migration("20240604000000_ScopeIdempotencyByRequestPath")]
    /// <inheritdoc />
    public partial class ScopeIdempotencyByRequestPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyKeys_Key",
                table: "IdempotencyKeys");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_Key_RequestPath",
                table: "IdempotencyKeys",
                columns: new[] { "Key", "RequestPath" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyKeys_Key_RequestPath",
                table: "IdempotencyKeys");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_Key",
                table: "IdempotencyKeys",
                column: "Key",
                unique: true);
        }
    }
}
