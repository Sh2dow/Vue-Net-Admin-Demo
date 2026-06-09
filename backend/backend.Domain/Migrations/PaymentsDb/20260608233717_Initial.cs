using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Domain.Migrations.PaymentsDb
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentEventRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEventRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventRecords_OrderId",
                table: "PaymentEventRecords",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventRecords_OrderId_AttemptNumber_SequenceNumber",
                table: "PaymentEventRecords",
                columns: new[] { "OrderId", "AttemptNumber", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEventRecords_PaymentId_SequenceNumber",
                table: "PaymentEventRecords",
                columns: new[] { "PaymentId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEventRecords");
        }
    }
}
