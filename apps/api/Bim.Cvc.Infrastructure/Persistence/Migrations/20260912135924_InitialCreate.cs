using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bim.Cvc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "drawings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NbPages = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vector_entities = table.Column<string>(type: "jsonb", nullable: true),
                    calibration = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_drawings_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "layers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DrawingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    Locked = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_layers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_layers_drawings_DrawingId",
                        column: x => x.DrawingId,
                        principalTable: "drawings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cvc_objects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DrawingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    start_x = table.Column<double>(type: "double precision", nullable: true),
                    start_y = table.Column<double>(type: "double precision", nullable: true),
                    end_x = table.Column<double>(type: "double precision", nullable: true),
                    end_y = table.Column<double>(type: "double precision", nullable: true),
                    WidthMm = table.Column<double>(type: "double precision", nullable: true),
                    HeightMm = table.Column<double>(type: "double precision", nullable: true),
                    DiameterMm = table.Column<double>(type: "double precision", nullable: true),
                    position_x = table.Column<double>(type: "double precision", nullable: true),
                    position_y = table.Column<double>(type: "double precision", nullable: true),
                    RotationRad = table.Column<double>(type: "double precision", nullable: false),
                    DebitM3h = table.Column<double>(type: "double precision", nullable: true),
                    VitesseMs = table.Column<double>(type: "double precision", nullable: true),
                    PressionPa = table.Column<double>(type: "double precision", nullable: true),
                    connected_object_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cvc_objects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cvc_objects_drawings_DrawingId",
                        column: x => x.DrawingId,
                        principalTable: "drawings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cvc_objects_layers_LayerId",
                        column: x => x.LayerId,
                        principalTable: "layers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cvc_objects_DrawingId",
                table: "cvc_objects",
                column: "DrawingId");

            migrationBuilder.CreateIndex(
                name: "IX_cvc_objects_LayerId",
                table: "cvc_objects",
                column: "LayerId");

            migrationBuilder.CreateIndex(
                name: "IX_drawings_ProjectId",
                table: "drawings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_layers_DrawingId",
                table: "layers",
                column: "DrawingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cvc_objects");

            migrationBuilder.DropTable(
                name: "layers");

            migrationBuilder.DropTable(
                name: "drawings");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
