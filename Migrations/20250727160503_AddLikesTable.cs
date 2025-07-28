using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddLikesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
         
            migrationBuilder.DropColumn(
                name: "Likes",
                table: "events");


            migrationBuilder.CreateTable(
                name: "likes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_likes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_likes_events_EventId",
                        column: x => x.EventId,
                        principalTable: "events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_likes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_likes_CreatedAt",
                table: "likes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_likes_EventId_UserId",
                table: "likes",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_likes_UserId",
                table: "likes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "likes");

            migrationBuilder.AddColumn<int>(
                name: "CurrentAttendees",
                table: "events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Likes",
                table: "events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttendees",
                table: "events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "events",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
