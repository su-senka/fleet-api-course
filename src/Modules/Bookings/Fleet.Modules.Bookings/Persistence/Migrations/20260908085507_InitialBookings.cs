using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Modules.Bookings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bookings");

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_driver_id_starts_at",
                schema: "bookings",
                table: "bookings",
                columns: new[] { "driver_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_starts_at",
                schema: "bookings",
                table: "bookings",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_vehicle_id_starts_at",
                schema: "bookings",
                table: "bookings",
                columns: new[] { "vehicle_id", "starts_at" });

            // ---------------------------------------------------------------------------------
            // The rule, in the database.
            //
            // BookingService checks for an overlap before it inserts, but that check and the
            // insert are two statements: another transaction can commit a clashing booking in
            // between, and under load it will. This constraint is what makes "one vehicle, one
            // booking at a time" actually true.
            //
            //   vehicle_id WITH =        only rows for the same vehicle can conflict
            //   tstzrange(..., '[)')     half-open, so a booking ending at 12:00 and one
            //                            starting at 12:00 do not overlap - the same rule as
            //                            BookingWindow.Overlaps in C#
            //   WITH &&                  the range-overlap operator
            //   WHERE (status <> 2)      partial: cancelled bookings hold nothing. 2 is
            //                            BookingStatus.Cancelled; SQL cannot see the enum, so
            //                            reordering that enum means revisiting this line.
            //
            // EXCLUDE USING gist needs btree_gist to put a uuid equality and a range overlap in
            // the same index. docker-compose creates the extension on first start; creating it
            // here too keeps this migration runnable against a database we did not initialise.
            // ---------------------------------------------------------------------------------
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql("""
                ALTER TABLE bookings.bookings
                ADD CONSTRAINT ex_bookings_no_overlapping_windows
                EXCLUDE USING gist (
                    vehicle_id WITH =,
                    tstzrange(starts_at, ends_at, '[)') WITH &&
                ) WHERE (status <> 2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE bookings.bookings DROP CONSTRAINT IF EXISTS ex_bookings_no_overlapping_windows;");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "bookings");
        }
    }
}
