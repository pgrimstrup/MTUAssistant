using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace MTU.Core.Data.Migrations;

public partial class SharedAnimals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<bool>(
            name: "Shared",
            table: "Animals",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "Shared", table: "Animals");
}
