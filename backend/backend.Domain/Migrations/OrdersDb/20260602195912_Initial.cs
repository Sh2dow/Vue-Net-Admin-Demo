using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Domain.Migrations.OrdersDb
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumed_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumed_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_saga_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_payment_requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_payment_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_dispatched_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_failed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_saga_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    order_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    download_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    shipping_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tracking_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    routing_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    publish_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consumed_messages_consumer_message_id",
                table: "consumed_messages",
                columns: new[] { "consumer", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_saga_states_order_id",
                table: "order_saga_states",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_id",
                table: "orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_at_utc",
                table: "outbox_messages",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_published_at_utc",
                table: "outbox_messages",
                column: "published_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumed_messages");

            migrationBuilder.DropTable(
                name: "order_saga_states");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
