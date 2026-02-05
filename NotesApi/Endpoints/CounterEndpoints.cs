using Microsoft.Data.Sqlite;
using Dapper;
using NotesApi.Models;
using NotesApi.Validators;

namespace NotesApi.Endpoints;

public static class CounterEndpoints
{
    public static void MapCounterEndpoints(this WebApplication app, string connectionString)
    {
        app.MapGet("/counter", async () =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var value = await connection.ExecuteScalarAsync<long>(
                    "SELECT value FROM counter WHERE id = 1;"
                );

                var history = (await connection.QueryAsync(@"
                    SELECT who, value, createdUtc
                    FROM counter_history
                    ORDER BY id DESC
                    LIMIT 20;
                ")).ToList();

                return Results.Ok(new { value, history });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /counter: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        app.MapPost("/counter/increment", async (CounterIncrement input) =>
        {
            if (!CounterValidator.IsValid(input))
            {
                return Results.BadRequest(new { error = "Who kan ikke være tom" });
            }

            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                // 1) Les nåværende verdi
                var current = await connection.ExecuteScalarAsync<long>(
                    "SELECT value FROM counter WHERE id = 1;"
                );

                // 2) Regn ut ny verdi
                var next = current + 1;

                // 3) BEVISST PAUSE - gjør lost update lett å demonstrere!
                await Task.Delay(250);

                // 4) Lagre historikk
                await connection.ExecuteAsync(@"
                    INSERT INTO counter_history (who, value, createdUtc)
                    VALUES (@who, @value, @createdUtc);
                ", new
                {
                    who = input.Who,
                    value = next,
                    createdUtc = DateTime.UtcNow.ToString("O")
                });

                // 5) Oppdater telleren (lost update kan skje her!)
                await connection.ExecuteAsync(@"
                    UPDATE counter SET value = @value WHERE id = 1;
                ", new { value = next });

                return Results.Ok(new { value = next, who = input.Who });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in POST /counter/increment: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });
    }
}