using NotesApi.Models;

namespace NotesApi.Validators;

public static class TodoValidator
{
    public static bool IsValid(CreateTodo todo)
    {
        return !string.IsNullOrWhiteSpace(todo.Task);
    }
}