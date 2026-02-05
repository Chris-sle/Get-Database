using Microsoft.Data.Sqlite;
using Dapper;

namespace NotesApi.Data;

public static class DatabaseSetup
{
    public static void EnsureNotesTable(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS notes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                body TEXT NOT NULL,
                createdUtc TEXT NOT NULL
            );
        ");
    }

    public static void EnsureTodosTable(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS todos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task TEXT NOT NULL,
                isCompleted INTEGER NOT NULL DEFAULT 0,
                createdUtc TEXT NOT NULL
            );
        ");
    }

    public static void EnsureCounterTables(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS counter (
                id INTEGER PRIMARY KEY,
                value INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS counter_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                who TEXT NOT NULL,
                value INTEGER NOT NULL,
                createdUtc TEXT NOT NULL
            );
        ");

        // Sørg for at telleren har én rad
        connection.Execute(@"
            INSERT INTO counter (id, value)
            SELECT 1, 0
            WHERE NOT EXISTS (SELECT 1 FROM counter WHERE id = 1);
        ");
    }

    public static void EnsureUsersTable(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Execute(@"
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            email TEXT NOT NULL,
            country TEXT NOT NULL,
            createdUtc TEXT NOT NULL
        );
    ");
    }
}