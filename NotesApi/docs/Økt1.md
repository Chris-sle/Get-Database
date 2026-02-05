# Økt 1 - Database som fundament

## Hva jeg lærte

### Konsepter
- **Backend forvalter tilstand over tid** - data må overleve restart og fungere for flere brukere samtidig
- **De tre stedene data kan befinne seg:**
  - Klient (forsvinner ved refresh)
  - Serverminne/API (forsvinner ved restart)  
  - Database (overlever restart) ✅
- **Databasen er systemets sannhetskilde** - hvis noe skal være sant i morgen, må det ligge i databasen

### Teknisk
- Sette opp ASP.NET Core Web API
- Koble til SQLite-database med Dapper
- Opprette tabeller med `CREATE TABLE`
- Bruke `record` for API-modeller (input/output)
- Forskjellen på `record` og `class`
- SQLite INTEGER mapper til `long` (Int64) i C#, ikke `int`
- WAL mode for bedre concurrency i SQLite
- Testing med curl og HTTP-filer

***

## 📁 Prosjektstruktur

```
NotesApi/
├── Program.cs
├── test.http (HTTP-fil for testing)
├── data/
│   ├── app.db (SQLite database - ikke commit til Git!)
│   ├── app.db-shm
│   └── app.db-wal
└── .gitignore
```

**.gitignore innhold:**
```
/data/*.db
/data/*.db-shm
/data/*.db-wal
```

***

## 🗄️ Database-tabeller

### Notes
```sql
CREATE TABLE IF NOT EXISTS notes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    body TEXT NOT NULL,
    createdUtc TEXT NOT NULL
);
```

### Todos
```sql
CREATE TABLE IF NOT EXISTS todos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task TEXT NOT NULL,
    isCompleted INTEGER NOT NULL DEFAULT 0,
    createdUtc TEXT NOT NULL
);
```

***

## 🔌 API Endepunkter

### Notes
- `GET /notes` - Hent alle notater
- `POST /notes` - Opprett nytt notat

### Todos
- `GET /todos` - Hent alle todos
- `POST /todos` - Opprett ny todo

### Health
- `GET /health` - Test at API-et kjører

***

## curl-kommandoer (Command Prompt / CMD)

### GET requests
```cmd
curl http://localhost:5108/health
curl http://localhost:5108/notes
curl http://localhost:5108/todos
```

### POST requests - Notes
```cmd
curl -X POST http://localhost:5108/notes -H "Content-Type: application/json" -d "{\"title\":\"Min note\",\"body\":\"Innhold her\"}"
```

### POST requests - Todos
```cmd
curl -X POST http://localhost:5108/todos -H "Content-Type: application/json" -d "{\"task\":\"Min oppgave\",\"isCompleted\":false}"
```

**Viktig for CMD:**
- Bruk `\"` for å escape anførselstegn inne i JSON
- Booleans skrives uten anførselstegn: `false` eller `true`
- Property-navn må være små bokstaver (camelCase): `task`, `isCompleted`

***

## HTTP-fil testing (test.http)

```http
@baseUrl = http://localhost:5108

### Health check
GET {{baseUrl}}/health

### Get all notes
GET {{baseUrl}}/notes

### Create new note
POST {{baseUrl}}/notes
Content-Type: application/json

{
  "title": "Test note",
  "body": "Created from HTTP file"
}

### Get all Todos
GET {{baseUrl}}/todos

### Create new Todo
POST {{baseUrl}}/todos
Content-Type: application/json

{
  "task": "Add Todo from HTTP file",
  "isCompleted": false
}
```

**Hvordan bruke:**
1. Opprett fil `test.http` i Visual Studio
2. Klikk på "Send request" som vises over hver HTTP-request
3. Se resultatet i høyre panel

**Fordeler med HTTP-filer:**
- Ingen problemer med anførselstegn (som i curl på Windows)
- Enklere å lese og vedlikeholde
- Resultatet vises direkte i Visual Studio
- Kan lagres i Git for dokumentasjon

***

## Vanlige problemer og løsninger

### Problem: "Database is locked"
**Årsak:** DB Browser for SQLite har databasen åpen  
**Løsning:** Lukk DB Browser før du tester API-et

### Problem: curl JSON-feil i PowerShell
**Årsak:** PowerShell behandler anførselstegn annerledes enn CMD  
**Løsning:** Bruk HTTP-filer i stedet, eller bruk Command Prompt (CMD)

### Problem: "Cannot convert INTEGER to bool"
**Årsak:** SQLite INTEGER mapper til `long` i C#, ikke `bool` eller `int`  
**Løsning:** Bruk `long` for INTEGER-kolonner i record-definisjoner

***

## NuGet-pakker brukt

```
Dapper - For SQL queries
Microsoft.Data.Sqlite - SQLite provider
```

Installasjon:
```bash
dotnet add package Dapper
dotnet add package Microsoft.Data.Sqlite
```

***

## Oppgaver fullført

- [x] **Del 1:** Fått `GET /notes` til å fungere og verifisert persistens
- [x] **Del 2:** Laget `todos`-tabell med GET-endepunkt
- [x] **Del 3 (Bonus):** Implementert POST-endepunkter for både notes og todos

***

## Viktige takeaways

1. **Connection string:** Bruk `Mode=ReadWriteCreate;Cache=Shared` for bedre concurrency
2. **WAL mode:** `PRAGMA journal_mode=WAL;` gjør SQLite bedre for samtidig lesing/skriving
3. **Records for data:** Perfekt for API input/output - automatiske properties og value equality
4. **Datatypekonvertering:** SQLite INTEGER → C# `long`, ikke `int` eller `bool`
5. **Testing:** HTTP-filer er mest pålitelig på Windows, curl fungerer best i CMD
6. **Database-fil:** Må **ikke** committes til Git - legg til i `.gitignore`