using NotesApi.Models;

namespace NotesApi.Validators;

public static class NoteValidator
{
    public static bool IsValid(CreateNote note)
    {
        return !string.IsNullOrWhiteSpace(note.Title)
            && !string.IsNullOrWhiteSpace(note.Body);
    }
}