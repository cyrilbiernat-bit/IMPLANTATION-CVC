using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace BimMep.Services.ProjectManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "families",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_families", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "family_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_family_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_family_types_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    lod_target = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_projects_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "levels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    elevation_m = table.Column<double>(type: "double precision", nullable: false),
                    height_m = table.Column<double>(type: "double precision", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_levels", x => x.id);
                    table.ForeignKey(
                        name: "fk_levels_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mep_networks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    design_flow = table.Column<double>(type: "double precision", nullable: true),
                    design_pressure_loss = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mep_networks", x => x.id);
                    table.ForeignKey(
                        name: "fk_mep_networks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bim_elements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ifc_guid = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: true),
                    family_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true),
                    lod = table.Column<short>(type: "smallint", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    placement = table.Column<Point>(type: "geometry(PointZ,0)", nullable: true),
                    bbox = table.Column<Polygon>(type: "geometry(PolygonZ,0)", nullable: true),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bim_elements", x => x.id);
                    table.ForeignKey(
                        name: "fk_bim_elements_family_types_family_type_id",
                        column: x => x.family_type_id,
                        principalTable: "family_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bim_elements_levels_level_id",
                        column: x => x.level_id,
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bim_elements_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bim_elements_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<Polygon>(type: "geometry(PolygonZ,0)", nullable: false),
                    area_m2 = table.Column<double>(type: "double precision", nullable: true),
                    volume_m3 = table.Column<double>(type: "double precision", nullable: true),
                    heating_load_w = table.Column<double>(type: "double precision", nullable: true),
                    cooling_load_w = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                    table.ForeignKey(
                        name: "fk_rooms_levels_level_id",
                        column: x => x.level_id,
                        principalTable: "levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rooms_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clashes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_a_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_b_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clash_type = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<Point>(type: "geometry(PointZ,0)", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    suggested_resolution_json = table.Column<string>(type: "text", nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clashes", x => x.id);
                    table.ForeignKey(
                        name: "fk_clashes_bim_elements_element_a_id",
                        column: x => x.element_a_id,
                        principalTable: "bim_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clashes_bim_elements_element_b_id",
                        column: x => x.element_b_id,
                        principalTable: "bim_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_clashes_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mep_connectors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    element_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_type = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<Point>(type: "geometry(PointZ,0)", nullable: false),
                    direction_x = table.Column<double>(type: "double precision", nullable: false),
                    direction_y = table.Column<double>(type: "double precision", nullable: false),
                    direction_z = table.Column<double>(type: "double precision", nullable: false),
                    size_primary = table.Column<double>(type: "double precision", nullable: false),
                    size_secondary = table.Column<double>(type: "double precision", nullable: false),
                    connected_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    system_id = table.Column<Guid>(type: "uuid", nullable: true),
                    system_classification = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mep_connectors", x => x.id);
                    table.ForeignKey(
                        name: "fk_mep_connectors_bim_elements_element_id",
                        column: x => x.element_id,
                        principalTable: "bim_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mep_connectors_mep_connectors_connected_to_id",
                        column: x => x.connected_to_id,
                        principalTable: "mep_connectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_mep_connectors_mep_networks_system_id",
                        column: x => x.system_id,
                        principalTable: "mep_networks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bim_elements_created_by",
                table: "bim_elements",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_bim_elements_family_type_id",
                table: "bim_elements",
                column: "family_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_bim_elements_ifc_guid",
                table: "bim_elements",
                column: "ifc_guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bim_elements_level_id",
                table: "bim_elements",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_bim_elements_project_id",
                table: "bim_elements",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_clashes_element_a_id",
                table: "clashes",
                column: "element_a_id");

            migrationBuilder.CreateIndex(
                name: "ix_clashes_element_b_id",
                table: "clashes",
                column: "element_b_id");

            migrationBuilder.CreateIndex(
                name: "ix_clashes_project_id",
                table: "clashes",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_family_types_family_id",
                table: "family_types",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_levels_project_id",
                table: "levels",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_mep_connectors_connected_to_id",
                table: "mep_connectors",
                column: "connected_to_id");

            migrationBuilder.CreateIndex(
                name: "ix_mep_connectors_element_id",
                table: "mep_connectors",
                column: "element_id");

            migrationBuilder.CreateIndex(
                name: "ix_mep_connectors_system_id",
                table: "mep_connectors",
                column: "system_id");

            migrationBuilder.CreateIndex(
                name: "ix_mep_networks_project_id",
                table: "mep_networks",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_organization_id",
                table: "projects",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_level_id",
                table: "rooms",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_project_id",
                table: "rooms",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clashes");

            migrationBuilder.DropTable(
                name: "mep_connectors");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "bim_elements");

            migrationBuilder.DropTable(
                name: "mep_networks");

            migrationBuilder.DropTable(
                name: "family_types");

            migrationBuilder.DropTable(
                name: "levels");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "families");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
