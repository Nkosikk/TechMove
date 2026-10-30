using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechMove.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedById",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(
                """
                UPDATE Clients SET OwnerId = NULL
                WHERE OwnerId IN (
                    SELECT U.Id FROM AspNetUsers U
                    WHERE NOT EXISTS (
                        SELECT 1 FROM AspNetUserRoles UR
                        INNER JOIN AspNetRoles R ON R.Id = UR.RoleId
                        WHERE UR.UserId = U.Id AND R.Name = 'Admin'));

                UPDATE Contracts SET OwnerId = NULL
                WHERE OwnerId IN (
                    SELECT U.Id FROM AspNetUsers U
                    WHERE NOT EXISTS (
                        SELECT 1 FROM AspNetUserRoles UR
                        INNER JOIN AspNetRoles R ON R.Id = UR.RoleId
                        WHERE UR.UserId = U.Id AND R.Name = 'Admin'));

                UPDATE Contracts SET ApprovedById = NULL
                WHERE ApprovedById IN (
                    SELECT U.Id FROM AspNetUsers U
                    WHERE NOT EXISTS (
                        SELECT 1 FROM AspNetUserRoles UR
                        INNER JOIN AspNetRoles R ON R.Id = UR.RoleId
                        WHERE UR.UserId = U.Id AND R.Name = 'Admin'));

                UPDATE ServiceRequests SET OwnerId = NULL
                WHERE OwnerId IN (
                    SELECT U.Id FROM AspNetUsers U
                    WHERE NOT EXISTS (
                        SELECT 1 FROM AspNetUserRoles UR
                        INNER JOIN AspNetRoles R ON R.Id = UR.RoleId
                        WHERE UR.UserId = U.Id AND R.Name = 'Admin'));

                UPDATE ServiceRequests SET ApprovedById = NULL
                WHERE ApprovedById IN (
                    SELECT U.Id FROM AspNetUsers U
                    WHERE NOT EXISTS (
                        SELECT 1 FROM AspNetUserRoles UR
                        INNER JOIN AspNetRoles R ON R.Id = UR.RoleId
                        WHERE UR.UserId = U.Id AND R.Name = 'Admin'));

                DELETE U FROM AspNetUsers U
                WHERE NOT EXISTS (
                    SELECT 1 FROM AspNetUserRoles UR
                    INNER JOIN AspNetRoles R ON R.Id = UR.RoleId
                    WHERE UR.UserId = U.Id AND R.Name = 'Admin');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RegisteredAtUtc",
                table: "AspNetUsers");
        }
    }
}
