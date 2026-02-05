namespace NotesApi.Models;

public record Todo(long Id, string Task, long IsCompleted, string CreatedUtc);
public record CreateTodo(string Task, bool IsCompleted);