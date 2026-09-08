using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fleet.Modules.Drivers.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDrivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "drivers");

            migrationBuilder.CreateTable(
                name: "drivers",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    user_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                schema: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    scan_blob_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificates", x => x.id);
                    table.ForeignKey(
                        name: "fk_certificates_drivers_driver_id",
                        column: x => x.driver_id,
                        principalSchema: "drivers",
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_driver_id_kind",
                schema: "drivers",
                table: "certificates",
                columns: new[] { "driver_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_expires_on_kind",
                schema: "drivers",
                table: "certificates",
                columns: new[] { "expires_on", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_drivers_employee_number",
                schema: "drivers",
                table: "drivers",
                column: "employee_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_drivers_user_id",
                schema: "drivers",
                table: "drivers",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificates",
                schema: "drivers");

            migrationBuilder.DropTable(
                name: "drivers",
                schema: "drivers");
        }
    }
}
