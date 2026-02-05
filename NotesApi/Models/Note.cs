namespace NotesApi.Models;

public record Note(long Id, string Title, string Body, string CreatedUtc);
public record CreateNote(string Title, string Body);
