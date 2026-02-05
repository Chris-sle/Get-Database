using Microsoft.Data.Sqlite;
using Dapper;
using NotesApi.Data;
using NotesApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Connection string
var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "app.db");
var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";

Console.WriteLine($"Database path: {dbPath}");

// Opprett database og tabeller
try
{
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

    using (var connection = new SqliteConnection(connectionString))
    {
        await connection.OpenAsync();
        await connection.ExecuteAsync("PRAGMA journal_mode=WAL;");
    }

    DatabaseSetup.EnsureNotesTable(connectionString);
    DatabaseSetup.EnsureTodosTable(connectionString);
    DatabaseSetup.EnsureCounterTables(connectionString);
    DatabaseSetup.EnsureUsersTable(connectionString);

    Console.WriteLine("Database initialized successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Error initializing database: {ex.Message}");
    throw;
}

// Health check
app.MapGet("/health", () => "API is running!");

// Map all endpoints
app.MapNoteEndpoints(connectionString);
app.MapTodoEndpoints(connectionString);
app.MapCounterEndpoints(connectionString);
app.MapUserEndpoints(connectionString);

app.Run();