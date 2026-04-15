using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionSign.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueSettlementPerTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep only one settlement per transaction (earliest by CreatedAt/Id) before enforcing UNIQUE.
            migrationBuilder.Sql(
                """
                ;WITH DuplicateSettlements AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (
                            PARTITION BY TransactionId
                            ORDER BY CreatedAt ASC, Id ASC
                        ) AS rn
                    FROM Settlements
                )
                DELETE FROM Settlements
                WHERE Id IN (
                    SELECT Id
                    FROM DuplicateSettlements
                    WHERE rn > 1
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_Settlements_TransactionId",
                table: "Settlements");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_TransactionId",
                table: "Settlements",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Settlements_TransactionId",
                table: "Settlements");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_TransactionId",
                table: "Settlements",
                column: "TransactionId");
        }
    }
}
