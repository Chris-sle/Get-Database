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


        // POST /counter/increment - Med transaksjon (men fortsatt sårbar for lost updates!)
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

                // Start transaksjon
                using var transaction = connection.BeginTransaction();

                try
                {
                    // 1) Les nåværende verdi
                    var current = await connection.ExecuteScalarAsync<long>(
                        "SELECT value FROM counter WHERE id = 1;",
                        transaction: transaction
                    );

                    // 2) Regn ut ny verdi
                    var next = current + 1;

                    // 3) BEVISST PAUSE - gjør lost update lett å demonstrere!
                    await Task.Delay(250);

                    // 4) Oppdater telleren
                    await connection.ExecuteAsync(@"
                UPDATE counter SET value = @value WHERE id = 1;
            ", new { value = next }, transaction: transaction);

                    // 5) Lagre historikk
                    await connection.ExecuteAsync(@"
                INSERT INTO counter_history (who, value, createdUtc)
                VALUES (@who, @value, @createdUtc);
            ", new
                    {
                        who = input.Who,
                        value = next,
                        createdUtc = DateTime.UtcNow.ToString("O")
                    }, transaction: transaction);

                    // Commit - gjør endringene permanente
                    transaction.Commit();

                    return Results.Ok(new { value = next, who = input.Who, message = "Med transaksjon" });
                }
                catch (Exception ex)
                {
                    // Rollback hvis noe går galt
                    try
                    {
                        transaction.Rollback();
                    }
                    catch { /* Rollback kan feile hvis DB er ødelagt */ }

                    throw; // Re-throw for outer catch
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in POST /counter/increment: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        // POST /counter/increment-safe - Med transaksjon OG optimistic concurrency
        app.MapPost("/counter/increment-safe", async (CounterIncrement input) =>
        {
            if (!CounterValidator.IsValid(input))
            {
                return Results.BadRequest(new { error = "Who kan ikke være tom" });
            }

            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                    using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();

                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        // 1) Les nåværende verdi
                        var oldValue = await connection.ExecuteScalarAsync<long>(
                            "SELECT value FROM counter WHERE id = 1;",
                            transaction: transaction
                        );

                        // 2) Regn ut ny verdi
                        var newValue = oldValue + 1;

                        // 3) Simuler forsinkelse
                        await Task.Delay(250);

                        // 4) COMPARE-AND-SET: Oppdater bare hvis verdien fortsatt er oldValue
                        var rowsAffected = await connection.ExecuteAsync(@"
                    UPDATE counter 
                    SET value = @newValue 
                    WHERE id = 1 AND value = @oldValue;
                ", new { oldValue, newValue }, transaction: transaction);

                        // Sjekk om vi fikk oppdatert
                        if (rowsAffected == 0)
                        {
                            // Concurrency conflict! Noen andre endret verdien
                            transaction.Rollback();

                            if (attempt < maxRetries)
                            {
                                Console.WriteLine($"Concurrency conflict for {input.Who}, retry {attempt}/{maxRetries}");
                                await Task.Delay(50 * attempt); // Exponential backoff
                                continue; // Prøv igjen
                            }
                            else
                            {
                                return Results.Conflict(new
                                {
                                    error = "Concurrency conflict",
                                    message = $"Kunne ikke oppdatere etter {maxRetries} forsøk. Prøv igjen.",
                                    attempts = attempt
                                });
                            }
                        }

                        // 5) Lagre historikk
                        await connection.ExecuteAsync(@"
                    INSERT INTO counter_history (who, value, createdUtc)
                    VALUES (@who, @value, @createdUtc);
                ", new
                        {
                            who = input.Who,
                            value = newValue,
                            createdUtc = DateTime.UtcNow.ToString("O")
                        }, transaction: transaction);

                        // Commit - suksess!
                        transaction.Commit();

                        return Results.Ok(new
                        {
                            value = newValue,
                            who = input.Who,
                            message = "Med transaksjon og concurrency control",
                            attempts = attempt
                        });
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in attempt {attempt}: {ex.Message}");
                    if (attempt >= maxRetries)
                    {
                        return Results.Problem($"Error after {maxRetries} attempts: {ex.Message}");
                    }
                }
            }

            return Results.Problem("Unexpected error");
        });
    }
}