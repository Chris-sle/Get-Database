using Microsoft.Data.Sqlite;
using Dapper;
using NotesApi.Models;

namespace NotesApi.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app, string connectionString)
    {
        // GET /users - Hent alle brukere (med paging for å unngå for mye data)
        app.MapGet("/users", async (int skip = 0, int take = 100) =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT id, email, country, createdUtc 
                    FROM users 
                    ORDER BY id 
                    LIMIT @Take OFFSET @Skip";

                var users = await connection.QueryAsync<User>(sql, new { Skip = skip, Take = take });

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /users: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        // GET /users/search/email?q=user123 - Søk på email (eksakt match)
        app.MapGet("/users/search/email", async (string q) =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = "SELECT id, email, country, createdUtc FROM users WHERE email = @Email";
                var users = await connection.QueryAsync<User>(sql, new { Email = q });

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /users/search/email: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        // GET /users/search/email-prefix?q=user1 - Søk på email med prefix (LIKE 'user1%')
        app.MapGet("/users/search/email-prefix", async (string q) =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = "SELECT id, email, country, createdUtc FROM users WHERE email LIKE @Pattern";
                var users = await connection.QueryAsync<User>(sql, new { Pattern = q + "%" });

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /users/search/email-prefix: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        // GET /users/search/email-contains?q=gmail - Søk midt i email (LIKE '%gmail%')
        app.MapGet("/users/search/email-contains", async (string q) =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = "SELECT id, email, country, createdUtc FROM users WHERE email LIKE @Pattern";
                var users = await connection.QueryAsync<User>(sql, new { Pattern = "%" + q + "%" });

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /users/search/email-contains: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        // GET /users/country/NO - Hent brukere per land
        app.MapGet("/users/country/{countryCode}", async (string countryCode) =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = "SELECT id, email, country, createdUtc FROM users WHERE country = @Country LIMIT 100";
                var users = await connection.QueryAsync<User>(sql, new { Country = countryCode });

                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /users/country: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });

        // GET /users/stats - Statistikk per land
        app.MapGet("/users/stats", async () =>
        {
            try
            {
                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT country, COUNT(*) as count 
                    FROM users 
                    GROUP BY country 
                    ORDER BY count DESC";

                var stats = await connection.QueryAsync(sql);

                return Results.Ok(stats);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GET /users/stats: {ex.Message}");
                return Results.Problem($"Error: {ex.Message}");
            }
        });
    }
}