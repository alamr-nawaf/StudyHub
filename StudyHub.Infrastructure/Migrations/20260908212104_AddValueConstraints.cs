using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddValueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Item_PriorityValue",
                table: "Items",
                sql: "\"Priority\" IS NULL OR \"Priority\" BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Item_StatusValue",
                table: "Items",
                sql: "\"Status\" IS NULL OR \"Status\" BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Course_Title",
                table: "Courses",
                sql: "\"Title\" ~ '\\S'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Item_PriorityValue",
                table: "Items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Item_StatusValue",
                table: "Items");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Course_Title",
                table: "Courses");
        }
    }
}
