using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventora_Project.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalCapacity",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE e
SET TotalCapacity = e.AvailableSeats + COALESCE(r.ActiveCount, 0)
FROM Events e
LEFT JOIN (
    SELECT EventId, COUNT(*) AS ActiveCount
    FROM Registrations
    WHERE Status = 'Confirmed'
    GROUP BY EventId
) r ON e.Id = r.EventId;");

            migrationBuilder.AlterColumn<int>(
                name: "TotalCapacity",
                table: "Events",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Events_Capacity",
                table: "Events",
                sql: "[AvailableSeats] >= 0 AND [AvailableSeats] <= [TotalCapacity] AND [TotalCapacity] >= 1 AND [TotalCapacity] <= 5000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Events_Capacity",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TotalCapacity",
                table: "Events");
        }
    }
}
