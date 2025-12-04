using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SHAPEE.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagReason",
                table: "Attendances",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Attendances",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "Attendances",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagReason",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "Attendances");
        }
    }
}
