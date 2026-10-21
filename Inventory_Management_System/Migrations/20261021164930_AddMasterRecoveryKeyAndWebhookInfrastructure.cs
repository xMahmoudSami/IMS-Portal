using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory_Management_System.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterRecoveryKeyAndWebhookInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RecoveryKeyGeneratedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryKeyHash",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecoveryOtpCode",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecoveryOtpExpiresAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecoveryKeyGeneratedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RecoveryKeyHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RecoveryOtpCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RecoveryOtpExpiresAt",
                table: "AspNetUsers");
        }
    }
}
