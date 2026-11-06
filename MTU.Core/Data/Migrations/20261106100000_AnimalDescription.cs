using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

public partial class AnimalDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "Animals",
            type: "TEXT",
            nullable: false,
            defaultValue: ""
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE Animals DROP COLUMN Description;");
    }
}
