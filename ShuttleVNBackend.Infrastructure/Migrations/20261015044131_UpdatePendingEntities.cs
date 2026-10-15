using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleVNBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePendingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingStatusHistories_Employees_EmployeeId",
                table: "BookingStatusHistories");

            migrationBuilder.RenameColumn(
                name: "EmployeeId",
                table: "BookingStatusHistories",
                newName: "ChangedByEmployee");

            migrationBuilder.RenameIndex(
                name: "IX_BookingStatusHistories_EmployeeId",
                table: "BookingStatusHistories",
                newName: "IX_BookingStatusHistories_ChangedByEmployee");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingStatusHistories_Employees_ChangedByEmployee",
                table: "BookingStatusHistories",
                column: "ChangedByEmployee",
                principalTable: "Employees",
                principalColumn: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingStatusHistories_Employees_ChangedByEmployee",
                table: "BookingStatusHistories");

            migrationBuilder.RenameColumn(
                name: "ChangedByEmployee",
                table: "BookingStatusHistories",
                newName: "EmployeeId");

            migrationBuilder.RenameIndex(
                name: "IX_BookingStatusHistories_ChangedByEmployee",
                table: "BookingStatusHistories",
                newName: "IX_BookingStatusHistories_EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookingStatusHistories_Employees_EmployeeId",
                table: "BookingStatusHistories",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "EmployeeId");
        }
    }
}
