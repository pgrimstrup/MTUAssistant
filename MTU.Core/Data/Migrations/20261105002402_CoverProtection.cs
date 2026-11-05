using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class CoverProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoverProtections",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(
                        type: "TEXT",
                        maxLength: 120,
                        nullable: false,
                        collation: "NOCASE"
                    ),
                    Protection = table.Column<int>(type: "INTEGER", nullable: false),
                    Hits = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverProtections", x => x.Id);
                    table.CheckConstraint("CK_CoverProtections_Hits", "Hits > 0");
                    table.CheckConstraint("CK_CoverProtections_Protection", "Protection >= 0");
                }
            );

            migrationBuilder.InsertData(
                table: "CoverProtections",
                columns: new[] { "Id", "Hits", "Name", "Protection" },
                values: new object[,]
                {
                    { 1, 1, "Vegetation", 2 },
                    { 2, 8, "Tree Trunk", 6 },
                    { 3, 24, "Stone Wall", 8 },
                    { 4, 50, "Civilian Vehicle", 10 },
                    { 5, 150, "Armoured Vehicle", 15 },
                    { 6, 250, "Fortifications", 20 },
                    { 7, 4, "Small Kitchen Appliance", 0 },
                    { 8, 6, "Personal Computer", 0 },
                    { 9, 8, "Wooden Chair", 2 },
                    { 10, 12, "Bar Table", 2 },
                    { 11, 6, "Wooden Door", 1 },
                    { 12, 8, "Reinforced Wooden Door", 2 },
                    { 13, 8, "Wooden Fence", 1 },
                    { 14, 5, "Plastic Door", 2 },
                    { 15, 16, "Brick Wall", 4 },
                    { 16, 24, "Concrete Wall", 8 },
                    { 17, 28, "Reinforced Concrete Wall", 10 },
                    { 18, 40, "Small Boulder", 10 },
                    { 19, 60, "Large Boulder", 10 },
                    { 20, 50, "Steel Hatch", 8 },
                    { 21, 150, "Steel Blast Door", 12 },
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CoverProtections_Name",
                table: "CoverProtections",
                column: "Name",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CoverProtections");
        }
    }
}
