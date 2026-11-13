using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessObjects.Migrations;

public partial class BillingPharmacyVnpayWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ConsultationFee",
            table: "Appointments",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CashierId",
            table: "Payments",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DispensedAt",
            table: "PrescriptionDetails",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DispensedByUserId",
            table: "PrescriptionDetails",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Payments_CashierId",
            table: "Payments",
            column: "CashierId");

        migrationBuilder.CreateIndex(
            name: "IX_PrescriptionDetails_DispensedByUserId",
            table: "PrescriptionDetails",
            column: "DispensedByUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Payments_Users_CashierId",
            table: "Payments",
            column: "CashierId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PrescriptionDetails_Users_DispensedByUserId",
            table: "PrescriptionDetails",
            column: "DispensedByUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Payments_Users_CashierId", table: "Payments");
        migrationBuilder.DropForeignKey(name: "FK_PrescriptionDetails_Users_DispensedByUserId", table: "PrescriptionDetails");
        migrationBuilder.DropIndex(name: "IX_Payments_CashierId", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_PrescriptionDetails_DispensedByUserId", table: "PrescriptionDetails");
        migrationBuilder.DropColumn(name: "ConsultationFee", table: "Appointments");
        migrationBuilder.DropColumn(name: "CashierId", table: "Payments");
        migrationBuilder.DropColumn(name: "DispensedAt", table: "PrescriptionDetails");
        migrationBuilder.DropColumn(name: "DispensedByUserId", table: "PrescriptionDetails");
    }

}
