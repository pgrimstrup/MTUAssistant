using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Equipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Protection = table.Column<int>(type: "INTEGER", nullable: false),
                    Tl = table.Column<int>(type: "INTEGER", nullable: false),
                    Rad = table.Column<int>(type: "INTEGER", nullable: false),
                    Kg = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false),
                    RequiredSkill = table.Column<string>(type: "TEXT", nullable: false),
                    Range = table.Column<string>(type: "TEXT", nullable: false),
                    Damage = table.Column<string>(type: "TEXT", nullable: false),
                    Magazine = table.Column<string>(type: "TEXT", nullable: false),
                    MagCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    Traits = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "TravellerEquipment",
                columns: table => new
                {
                    TravellerId = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentId = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_TravellerEquipment",
                        x => new { x.TravellerId, x.EquipmentId }
                    );
                    table.ForeignKey(
                        name: "FK_TravellerEquipment_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_TravellerEquipment_Travellers_TravellerId",
                        column: x => x.TravellerId,
                        principalTable: "Travellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Kind_Name",
                table: "Equipment",
                columns: new[] { "Kind", "Name" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TravellerEquipment_EquipmentId",
                table: "TravellerEquipment",
                column: "EquipmentId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TravellerEquipment");

            migrationBuilder.DropTable(name: "Equipment");
        }
    }
}
