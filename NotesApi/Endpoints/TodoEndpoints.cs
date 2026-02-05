using Microsoft.Data.Sqlite;
using Dapper;
using NotesApi.Models;
using NotesApi.Validators;

namespace NotesApi.Endpoints;

public static class TodoEndpoints
{
    public static void MapTodoEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/todos", async () =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = "SELECT id, task, isCompleted, createdUtc FROM todos ORDER BY id";
                var todos = await connection.QueryAsync<Todo>(sql);

                return Results.Ok(todos);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /todos: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        app.MapPost("/todos", async (CreateTodo input) =>
        {
            if (!TodoValidator.IsValid(input))
            {
                return Results.BadRequest(new
                {
                    error = "Ugyldig input",
                    details = "Task kan ikke være tom"
                });
            }

            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var createdUtc = DateTime.UtcNow.ToString("O");

                var sql = @"
                    INSERT INTO todos (task, isCompleted, createdUtc)
                    VALUES (@Task, @IsCompleted, @CreatedUtc);
                    SELECT last_insert_rowid();";

                var id = await connection.ExecuteScalarAsync<long>(sql, new
                {
                    input.Task,
                    IsCompleted = input.IsCompleted ? 1 : 0,
                    CreatedUtc = createdUtc
                });

                return Results.Created($"/todos/{id}", new { id, task = input.Task });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in POST /todos: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });
    }
}