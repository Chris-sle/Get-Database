using Microsoft.Data.Sqlite;
using Dapper;
using NotesApi.Models;
using NotesApi.Validators;

namespace NotesApi.Endpoints;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/notes", async () =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = "SELECT id, title, body, createdUtc FROM notes ORDER BY id";
                var notes = await connection.QueryAsync<Note>(sql);

                return Results.Ok(notes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /notes: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        app.MapPost("/notes", async (CreateNote input) =>
        {
            if (!NoteValidator.IsValid(input))
            {
                return Results.BadRequest(new
                {
                    error = "Ugyldig input",
                    details = "Title og Body kan ikke være tomme"
                });
            }

            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var createdUtc = DateTime.UtcNow.ToString("O");

                var sql = @"
                    INSERT INTO notes (title, body, createdUtc)
                    VALUES (@Title, @Body, @CreatedUtc);
                    SELECT last_insert_rowid();";

                var id = await connection.ExecuteScalarAsync<long>(sql, new
                {
                    input.Title,
                    input.Body,
                    CreatedUtc = createdUtc
                });

                return Results.Created($"/notes/{id}", new { id, title = input.Title });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in POST /notes: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });
    }
}