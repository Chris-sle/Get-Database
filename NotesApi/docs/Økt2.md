# Økt 2 - Skriving til databasen, konsekvenser og ansvar

## Mål for økten

- Forstå hvorfor **skriving er vanskeligere enn lesing**
- Implementere **validering** av input før data lagres
- Se hvordan **samtidighet** kan skape problemer (lost updates)
- Lære å **strukturere kode** i en ryddig arkitektur

***

## Hva jeg lærte

### Konsepter

**Skriving endrer alt:**
- Lesing er relativt trygt - vi kan ikke ødelegge noe
- Skriving endrer systemets tilstand permanent
- Feil blir varige
- Backend må ta ansvar for hva som er lov

**Lost Update-problemet:**
- Når to forespørsler leser samme verdi samtidig
- Begge regner ut ny verdi basert på samme utgangspunkt
- Den siste som skriver overskriver den første
- **Resultat:** Én oppdatering forsvinner!

**Arkitektur og separasjon av ansvar:**
- Models - Datastrukturer
- Validators - Forretningsregler
- Endpoints - HTTP-logikk
- Data - Database-oppsett
- Program.cs - Kun orkestrering

***

## Prosjektstruktur (før og etter)

### Før (Økt 1):
```
NotesApi/
├── Program.cs (alt i én fil!)
└── data/
    └── app.db
```

### Etter (Økt 2):
```
NotesApi/
├── Program.cs                    (kun setup og routing)
├── Data/
│   └── DatabaseSetup.cs          (database-initialisering)
├── Models/
│   ├── Note.cs                   (data models)
│   ├── Todo.cs
│   └── Counter.cs
├── Validators/
│   ├── NoteValidator.cs          (validering)
│   ├── TodoValidator.cs
│   └── CounterValidator.cs
├── Endpoints/
│   ├── NoteEndpoints.cs          (endpoint-logikk)
│   ├── TodoEndpoints.cs
│   └── CounterEndpoints.cs
└── data/
    └── app.db
```

***

## Hva vi implementerte

### 1. Validering av input

**NoteValidator.cs:**
```csharp
public static class NoteValidator
{
    public static bool IsValid(CreateNote note)
    {
        return !string.IsNullOrWhiteSpace(note.Title)
            && !string.IsNullOrWhiteSpace(note.Body);
    }
}
```

**Hvorfor viktig:**
- Databasen er siste forsvarslinje, ikke første
- API-et må bestemme hva som er gyldig
- Bedre feilmeldinger til brukere

### 2. Counter med Lost Update-problem

**Naiv implementasjon (med bevisst feil):**
```csharp
// 1) Les nåværende verdi
var current = await connection.ExecuteScalarAsync<long>(
    "SELECT value FROM counter WHERE id = 1;"
);

// 2) Regn ut ny verdi
var next = current + 1;

// 3) BEVISST PAUSE - gjør lost update lett å demonstrere!
await Task.Delay(250);

// 4) Skriv tilbake (PROBLEM: andre kan ha skrevet i mellomtiden!)
await connection.ExecuteAsync(@"
    UPDATE counter SET value = @value WHERE id = 1;
", new { value = next });
```

**Hva som skjer ved samtidighet:**
```
Tid  | Alice                  | Bob
-----|------------------------|------------------------
T0   | Les: 5                | 
T1   | Regn: 5 + 1 = 6       | Les: 5
T2   | Venter...             | Regn: 5 + 1 = 6
T3   | Skriv: 6              | Venter...
T4   |                       | Skriv: 6  ❌ (overskriver!)

Resultat: Counter er 6, ikke 7! Én oppdatering forsvant.
```

## Testing og demonstrasjon

### Test validering

**Gyldig input (fungerer):**
```cmd
curl -X POST http://localhost:5108/notes -H "Content-Type: application/json" -d "{\"title\":\"Valid\",\"body\":\"OK\"}"
```

**Ugyldig input (feiler som forventet):**
```cmd
curl -X POST http://localhost:5108/notes -H "Content-Type: application/json" -d "{\"title\":\"\",\"body\":\"Test\"}"
```

Forventet svar:
```json
{
  "error": "Ugyldig input",
  "details": "Title og Body kan ikke være tomme"
}
```

### Demonstrere Lost Update

**Terminal 1:**
```cmd
curl -X POST http://localhost:5108/counter/increment -H "Content-Type: application/json" -d "{\"who\":\"Alice\"}"
```

**Terminal 2 (kjør umiddelbart etter):**
```cmd
curl -X POST http://localhost:5108/counter/increment -H "Content-Type: application/json" -d "{\"who\":\"Bob\"}"
```

**Sjekk resultatet:**
```cmd
curl http://localhost:5108/counter
```

**Observasjon:**
```json
{
  "value": 2,
  "history": [
    {"who": "Bob", "value": 2},      // Bob tror han satte til 2
    {"who": "Alice", "value": 2}     // Alice tror også hun satte til 2!
  ]
}
```

Begge trodde de skulle sette verdien til 2, men én oppdatering gikk tapt!

***

## Komplett test.http fil

```http
@baseUrl = http://localhost:5108

### Health check
GET {{baseUrl}}/health

### ==================== NOTES ====================

### Get all notes
GET {{baseUrl}}/notes

### Create new note
POST {{baseUrl}}/notes
Content-Type: application/json

{
  "title": "Test note",
  "body": "Created from HTTP file"
}

### Create invalid note (should fail)
POST {{baseUrl}}/notes
Content-Type: application/json

{
  "title": "",
  "body": "Empty title"
}

### ==================== TODOS ====================

### Get all Todos
GET {{baseUrl}}/todos

### Create new Todo
POST {{baseUrl}}/todos
Content-Type: application/json

{
  "task": "Learn about lost updates",
  "isCompleted": false
}

### ==================== COUNTER ====================

### Get Counter Status
GET {{baseUrl}}/counter

### Increment Counter
POST {{baseUrl}}/counter/increment
Content-Type: application/json

{
  "who": "Chris"
}
```

***

## Viktige takeaways

### 1. Validering er kritisk
- Alltid valider input før skriving
- Gi tydelige feilmeldinger
- Valideringen hører hjemme i API-et, ikke bare databasen

### 2. Samtidighet er vanskelig
- "Read-Modify-Write" er en race condition
- Lost updates er et reelt problem
- Vi løser det IKKE ennå - vi observerer bare problemet

### 3. Arkitektur betyr noe
- Separasjon av ansvar gjør koden lettere å forstå
- Extension methods gir ren routing
- Små, fokuserte filer > én stor fil

### 4. Backend må ta ansvar
- Databasen lagrer data, men kjenner ikke forretningsregler
- API-et må bestemme hva som er gyldig
- Skriving krever mer tanke enn lesing

***

## Uløste problemer (bevisst!)

Vi har **ikke** løst disse problemene ennå:

1. **Lost updates** - Counter har fortsatt race condition
2. **Transaksjoner** - Hva skjer hvis noe feiler midt i?
3. **Samtidig skriving** - Hvordan håndtere flere brukere?
4. **Rollback** - Hvordan angre hvis noe går galt?

**Dette er OK!** Målet med Økt 2 var å **se** problemene, ikke løse dem.