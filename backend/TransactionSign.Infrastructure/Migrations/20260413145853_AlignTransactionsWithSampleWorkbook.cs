using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionSign.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignTransactionsWithSampleWorkbook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Transactions",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "LastModifiedDate",
                table: "Transactions",
                newName: "LastModifyDate");

            migrationBuilder.RenameColumn(
                name: "Counterparty",
                table: "Transactions",
                newName: "BeneficiaryName");

            migrationBuilder.RenameColumn(
                name: "Company",
                table: "Transactions",
                newName: "SenderName");

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "SiteSettings",
                newName: "Name");

            migrationBuilder.RenameIndex(
                name: "IX_SiteSettings_Key",
                table: "SiteSettings",
                newName: "IX_SiteSettings_Name");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.Sql(
                """
                UPDATE Transactions
                SET Source = CASE
                    WHEN TRY_CONVERT(int, Source) IS NOT NULL THEN Source
                    WHEN Source = 'Fee' THEN '5'
                    ELSE '4'
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Source",
                table: "Transactions",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "AgentId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "Transactions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CurrencyId",
                table: "Transactions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDebit",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE Transactions
                SET CreateDate = LastModifyDate
                WHERE CreateDate = '0001-01-01T00:00:00.0000000';

                UPDATE Transactions
                SET CurrencyId = 'EUR'
                WHERE CurrencyId = '';
                """);

            migrationBuilder.UpdateData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Name", "Value" },
                values: new object[] { "NumberOfRequiredAmSignatures", "2" });

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "AgentId", "CreateDate", "CurrencyId", "IsDebit", "Note", "ParentId", "Source" },
                values: new object[] { null, new DateTime(2024, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), "EUR", false, null, null, 4 });

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AgentId", "CreateDate", "CurrencyId", "IsDebit", "Note", "ParentId", "Source" },
                values: new object[] { null, new DateTime(2024, 1, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), "EUR", false, null, null, 4 });

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "AgentId", "CreateDate", "CurrencyId", "IsDebit", "Note", "ParentId", "Source" },
                values: new object[] { null, new DateTime(2024, 1, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "EUR", false, null, null, 4 });

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "AgentId", "CreateDate", "CurrencyId", "IsDebit", "Note", "ParentId", "Source" },
                values: new object[] { null, new DateTime(2024, 1, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), "EUR", false, null, null, 4 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsDebit",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Transactions");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Transactions",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "SenderName",
                table: "Transactions",
                newName: "Company");

            migrationBuilder.RenameColumn(
                name: "LastModifyDate",
                table: "Transactions",
                newName: "LastModifiedDate");

            migrationBuilder.RenameColumn(
                name: "BeneficiaryName",
                table: "Transactions",
                newName: "Counterparty");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "SiteSettings",
                newName: "Key");

            migrationBuilder.RenameIndex(
                name: "IX_SiteSettings_Name",
                table: "SiteSettings",
                newName: "IX_SiteSettings_Key");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Transactions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.UpdateData(
                table: "SiteSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Key", "Value" },
                values: new object[] { "RequiredSignatures", "3" });

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 1,
                column: "Type",
                value: "Payment");

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 2,
                column: "Type",
                value: "Transfer");

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 3,
                column: "Type",
                value: "Payment");

            migrationBuilder.UpdateData(
                table: "Transactions",
                keyColumn: "Id",
                keyValue: 4,
                column: "Type",
                value: "Transfer");
        }
    }
}
