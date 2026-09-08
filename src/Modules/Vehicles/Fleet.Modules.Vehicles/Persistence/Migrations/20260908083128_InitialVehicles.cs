using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Modules.Vehicles.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vehicles");

            migrationBuilder.CreateTable(
                name: "depots",
                schema: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_depots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                schema: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    depot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_reading_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicles_depots_depot_id",
                        column: x => x.depot_id,
                        principalSchema: "vehicles",
                        principalTable: "depots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "odometer_readings",
                schema: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    km = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_odometer_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_odometer_readings_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalSchema: "vehicles",
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_depots_name",
                schema: "vehicles",
                table: "depots",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_odometer_readings_vehicle_id_recorded_at",
                schema: "vehicles",
                table: "odometer_readings",
                columns: new[] { "vehicle_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_depot_id",
                schema: "vehicles",
                table: "vehicles",
                column: "depot_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_plate",
                schema: "vehicles",
                table: "vehicles",
                column: "plate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_status",
                schema: "vehicles",
                table: "vehicles",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "odometer_readings",
                schema: "vehicles");

            migrationBuilder.DropTable(
                name: "vehicles",
                schema: "vehicles");

            migrationBuilder.DropTable(
                name: "depots",
                schema: "vehicles");
        }
    }
}
