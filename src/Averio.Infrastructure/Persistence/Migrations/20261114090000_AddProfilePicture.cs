using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Averio.Infrastructure.Persistence;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AverioDbContext))]
[Migration("20261114090000_AddProfilePicture")]
public sealed class AddProfilePicture : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "profile_picture",
            table: "users",
            type: "bytea",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "profile_picture", table: "users");
    }
}
