using NotesApi.Models;

namespace NotesApi.Validators;

public static class CounterValidator
{
    public static bool IsValid(CounterIncrement increment)
    {
        return !string.IsNullOrWhiteSpace(increment.Who);
    }
}