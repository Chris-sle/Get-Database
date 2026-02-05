# Økt 3 - Hva databasen faktisk gjør

## Mål for økten

- Forstå at **samme SQL kan ha helt forskjellig kostnad**
- Lære hvordan **indekser** gjør søk raskere
- Bruke **EXPLAIN QUERY PLAN** for å se hva databasen gjør
- Identifisere når indekser **ikke kan brukes**
- Forstå **trade-off** mellom lese- og skrivehastighet

***

## Hva jeg lærte

### Konsepter

**SQL beskriver hva, ikke hvordan:**
- SQL sier *hva* vi vil ha, ikke *hvordan* vi skal finne det
- Databasen velger strategi (full scan eller indeks)
- To identiske spørringer kan ha helt forskjellig kostnad

**Full Table Scan:**
- Databasen leser alle rader én for én
- Kompleksitet: O(n) - lineær tid
- Fungerer OK for små tabeller (< 1000 rader)
- Skalerer dårlig når data vokser

**Indeks:**
- Ekstra datastruktur (B-tree) som fungerer som "register i en bok"
- Kompleksitet: O(log n) - logaritmisk tid
- Gjør søk eksponentielt raskere
- Må vedlikeholdes ved skriving (trade-off)

**Skalerbarhet:**

| Antall rader | Full scan | Med indeks | Forhold |
|-------------:|-----------:|-----------:|--------:|
| 1            | 1          | 1          | 1×      |
| 8            | 8          | 3          | 2.7×    |
| 64           | 64         | 6          | 10.7×   |
| 512          | 512        | 9          | 56.9×   |
| 4,096        | 4,096      | 12         | 341×    |
| 32,768       | 32,768     | 15         | 2,184×  |
| 262,144      | 262,144    | 18         | 14,563× |

Når data 8-dobles: full scan øker 8×, indeks bare +3 sammenligninger! 🤯

***

## Database-struktur

### Users-tabell (testdata)

```sql
CREATE TABLE users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    email TEXT NOT NULL,
    country TEXT NOT NULL,
    createdUtc TEXT NOT NULL
);
```

**Testdata:** 50,000 brukere
- Email: `user1@example.com` til `user50000@example.com`
- Land: NO, SE, DK, FI, DE, US (fordelt)
- Datoer: Siste 10 år (varierende)

```sql
-- Fyll med 50,000 brukere
WITH RECURSIVE seq(n) AS (
  SELECT 1
  UNION ALL
  SELECT n + 1 FROM seq WHERE n < 50000
)
INSERT INTO users (email, country, createdUtc)
SELECT
  'user' || n || '@example.com' AS email,
  CASE (n % 6)
    WHEN 0 THEN 'NO'
    WHEN 1 THEN 'SE'
    WHEN 2 THEN 'DK'
    WHEN 3 THEN 'FI'
    WHEN 4 THEN 'DE'
    ELSE 'US'
  END AS country,
  datetime('now', '-' || (n % 3650) || ' days') AS createdUtc
FROM seq;
```

### Indekser opprettet

```sql
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_country ON users(country);
CREATE INDEX idx_users_createdUtc ON users(createdUtc);
```

***

## EXPLAIN QUERY PLAN - Se hva databasen gjør

### Eksempel 1: Med indeks (rask)

```sql
EXPLAIN QUERY PLAN
SELECT * FROM users WHERE email = 'user12345@example.com';
```

**Resultat:**
```
SEARCH users USING INDEX idx_users_email (email=?)
```

**Bruker indeks** - Rask oppslag!

### Eksempel 2: Uten indeks (treg)

```sql
EXPLAIN QUERY PLAN
SELECT * FROM users WHERE email LIKE '%gmail.com';
```

**Resultat:**
```
SCAN users
```

**Full table scan** - Må lese alle 50,000 rader!

***

## Når indekser fungerer vs ikke fungerer

### Indekser FUNGERER godt på:

| Spørring | Hvorfor |
|----------|---------|
| `WHERE email = 'test@example.com'` | Eksakt match |
| `WHERE email LIKE 'user1%'` | Kjenner starten av teksten |
| `WHERE country = 'NO'` | Eksakt match |
| `WHERE country IN ('NO', 'SE')` | Eksakte verdier |
| `WHERE createdUtc > '2025-01-01'` | Range med indeks |
| `WHERE id = 123` | Primærnøkkel (auto-indeksert) |

### Indekser fungerer IKKE på:

| Spørring | Hvorfor | Løsning |
|----------|---------|---------|
| `WHERE email LIKE '%@gmail.com'` | Kjenner ikke starten | Endre til `LIKE 'user%'` hvis mulig |
| `WHERE email LIKE '%test%'` | Mønster i midten | Full-text search eller lagre invertert indeks |
| `WHERE LOWER(email) = 'test'` | Funksjon på kolonnen | Lag function-based index |
| `WHERE id + 1 = 100` | Beregning på kolonnen | Skriv om til `WHERE id = 99` |
| `WHERE LENGTH(email) > 20` | Funksjon endrer verdien | Lagre lengde i egen kolonne |

***

## API Endepunkter for testing

### GET /users
Hent brukere med paging
```http
GET {{baseUrl}}/users?skip=0&take=100
```

### GET /users/search/email (RASK - bruker indeks)
Eksakt match på email
```http
GET {{baseUrl}}/users/search/email?q=user12345@example.com
```

### GET /users/search/email-prefix (RASK - bruker indeks)
Søk på start av email
```http
GET {{baseUrl}}/users/search/email-prefix?q=user1
```

### GET /users/search/email-contains (TREG - full scan)
Søk midt i email
```http
GET {{baseUrl}}/users/search/email-contains?q=example
```

### GET /users/country/{code} (RASK - bruker indeks)
Hent brukere per land
```http
GET {{baseUrl}}/users/country/NO
```

### GET /users/stats
Statistikk per land (GROUP BY)
```http
GET {{baseUrl}}/users/stats
```

***

## test.http for Users

```http
@baseUrl = http://localhost:5108

### ==================== USERS - PERFORMANCE TESTING ====================

### Get first 100 users
GET {{baseUrl}}/users

### Get users with paging
GET {{baseUrl}}/users?skip=100&take=50

### ========== RASK: Eksakt match (bruker indeks) ==========
GET {{baseUrl}}/users/search/email?q=user12345@example.com

### ========== RASK: Prefix search (bruker indeks) ==========
GET {{baseUrl}}/users/search/email-prefix?q=user1

### ========== TREG: Contains search (full table scan!) ==========
GET {{baseUrl}}/users/search/email-contains?q=example

### ========== RASK: Country lookup (bruker indeks) ==========
GET {{baseUrl}}/users/country/NO

### Get users from Sweden
GET {{baseUrl}}/users/country/SE

### Get statistics per country (GROUP BY)
GET {{baseUrl}}/users/stats
```

***

## Kostnad ved skriving - Trade-off

### Test: INSERT med og uten indekser

**Uten indekser:**
```sql
DROP INDEX IF EXISTS idx_users_email;
DROP INDEX IF EXISTS idx_users_country;
DROP INDEX IF EXISTS idx_users_createdUtc;

-- Mål tiden
INSERT INTO users (email, country, createdUtc)
VALUES (...); -- 10,000 rader
```

**Med indekser:**
```sql
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_country ON users(country);
CREATE INDEX idx_users_createdUtc ON users(createdUtc);

-- Mål tiden igjen
INSERT INTO users (email, country, createdUtc)
VALUES (...); -- 10,000 rader
```

**Observasjon:**
- INSERT blir **tregere** med indekser
- Databasen må oppdatere både tabell OG alle indekser
- Trade-off: Rask lesing vs tregere skriving

**Når er dette OK?**
- Les-intensive systemer (f.eks. rapporter, søk)
- Data som sjelden endres
- Kritiske søk som må være raske

**Når er det problematisk?**
- Skriv-intensive systemer (f.eks. logging, analytics)
- Mange indekser på samme tabell
- Store batch-inserts

***

## Viktige takeaways

### 1. Indekser er kritiske for ytelse
- Forskjellen mellom millisekunder og sekunder
- Må tenkes inn fra starten i store tabeller
- Men ikke lag indekser "bare i tilfelle"

### 2. EXPLAIN QUERY PLAN er din venn
- Alltid test hvordan databasen faktisk utfører spørringen
- "Det føles tregt" → Start med EXPLAIN
- Optimalisering uten måling er gambling

### 3. Ikke alle søk kan bruke indeks
- `LIKE '%tekst'` → Full scan
- Funksjoner på kolonner → Full scan
- Skriv SQL som kan bruke indekser

### 4. Trade-off: Lesing vs Skriving
- Indekser gjør lesing rask, skriving tregere
- Tenk på bruksmønster: Leses det mer enn det skrives?
- For mange indekser = unødvendig kostnad

### 5. Skalerbarhet betyr noe
- 100 rader: Indeks kanskje ikke nødvendig
- 100,000 rader: Indeks kritisk
- 10,000,000 rader: Indeks kan være forskjellen mellom sekunder og timer

***

## Praktisk eksempel: Før og etter

### Før (uten indeks)
```sql
SELECT * FROM users WHERE email = 'user25000@example.com';
-- SCAN users
-- Leser 50,000 rader
-- Tid: ~50-100ms
```

### Etter (med indeks)
```sql
CREATE INDEX idx_users_email ON users(email);
SELECT * FROM users WHERE email = 'user25000@example.com';
-- SEARCH users USING INDEX idx_users_email
-- Leser ~17 sammenligninger (log₂(50000) ≈ 16)
-- Tid: ~1-2ms
```

**50× raskere!** ⚡

***

## Læringsmål oppnådd

- [x] Forstått forskjellen mellom full scan og indeks-oppslag
- [x] Laget indekser og testet effekten
- [x] Brukt EXPLAIN QUERY PLAN for å se databasens strategi
- [x] Identifisert spørringer som ikke kan bruke indeks
- [x] Testet kostnad ved skriving med/uten indekser
- [x] Bygget intuisjon for når indekser er viktige

***

**Viktigste læring:** 
Indekser er forskjellen mellom et system som skalerer og et som kollapser når data vokser. 
Men de har en pris så velg dem med omhu basert på faktisk bruk!