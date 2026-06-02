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
                name: "payment_event_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payment_event_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_event_records_order_id",
                table: "payment_event_records",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_event_records_order_id_attempt_number_sequence_numb",
                table: "payment_event_records",
                columns: new[] { "order_id", "attempt_number", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_event_records_payment_id_sequence_number",
                table: "payment_event_records",
                columns: new[] { "payment_id", "sequence_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_event_records");
        }
    }
}
