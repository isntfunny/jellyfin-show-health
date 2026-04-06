# Show Health Dashboard — Design Spec

## Ziel

Ein Jellyfin Plugin Dashboard das alle TV-Serien der Bibliothek auf Vollstaendigkeit prueft.
Es vergleicht den lokalen Bestand (Seasons/Episoden) mit den IMDb-Daten und zeigt:

- Fehlende Episoden pro Season
- Komplett fehlende Seasons
- Serien-Status (laufend / abgeschlossen)
- Kommende Episoden mit Datum

## Frontend

### Registrierung

- Custom Page via `IHasWebPages` mit `EnableInMainMenu = true`
- Menuepunkt "Show Health" mit Icon `health_and_safety`
- Dateien: `Web/showhealth.html` (Seite), `Web/showhealth.js` (Controller)

### Layout: Hybrid-Tabelle mit aufklappbaren Details

Spalten:
| # | Spalte | Inhalt |
|---|--------|--------|
| 1 | Expand-Arrow | Pfeil zum Aufklappen (nur bei unvollstaendigen Serien) |
| 2 | Poster | Thumbnail via Jellyfin Items API (`/Items/{id}/Images/Primary?height=54`) |
| 3 | Serie | Name + Jahreszeitraum |
| 4 | Status | Badge: "Abgeschlossen" (gruen) / "Laufend" (blau) |
| 5 | Seasons | Vorhanden/Gesamt (z.B. "2/3") |
| 6 | Fehlend | Anzahl fehlender Episoden oder "Vollstaendig" |
| 7 | Naechste Folge | Datum-Badge oder "Season X angekuendigt" |

Aufklappbare Detail-Zeile:
- Pro Season mit fehlenden Episoden eine Gruppe
- Jede fehlende Episode als Chip: "E03 — Episodentitel"
- Roter Rand-Indikator links am Chip

### Sortierung

Drei Modi, schaltbar ueber Buttons in der Sortierleiste:
- **Nach Status**: Unvollstaendige zuerst, dann vollstaendige
- **Nach Dringlichkeit**: Serien mit baldigen neuen Folgen zuerst, dann fehlende, dann vollstaendige
- **A-Z**: Alphabetisch nach Serienname

### Darstellung

- Vollstaendige Serien werden abgeblendet (opacity)
- Zusammenfassung oben rechts: "X Serien · Y unvollstaendig"
- Loading-Spinner waehrend Daten geladen werden (`Dashboard.showLoadingMsg()`)

### JavaScript-Architektur

Plain JS mit ES-Klassen fuer Uebersichtlichkeit:

```
showhealth.js
  ├── ShowHealthPage        — Hauptklasse, Entry-Point via export default
  │     Verantwortung: Initialisierung, Event-Binding, Lifecycle
  │
  ├── ShowHealthApi         — API-Kommunikation
  │     Verantwortung: fetch gegen /ShowHealth/Status, Error-Handling
  │
  ├── ShowHealthTable       — Tabellen-Rendering
  │     Verantwortung: Tabelle bauen, Zeilen rendern, Expand/Collapse
  │
  └── ShowHealthSorter      — Sortierlogik
        Verantwortung: Daten sortieren nach Status/Dringlichkeit/Name
```

Alles in einer JS-Datei (Embedded Resource Constraint), aber sauber in Klassen getrennt.

## Backend

### ShowHealthController (`ControllerBase`)

Ein Endpunkt:

```
GET /ShowHealth/Status
```

Response: JSON mit allen Serien und ihrem Vollstaendigkeits-Status.

Ablauf:
1. `JellyfinLibraryService.GetSeriesWithImdbId()` — alle Serien mit IMDb-ID aus Jellyfin holen
2. Pro Serie: `ImdbApiClient.GetTitleSeasonsAsync()` — Seasons von IMDb
3. Pro Season mit Differenz: `ImdbApiClient.GetTitleEpisodesAsync()` — Episoden von IMDb
4. Vergleich: lokale Episoden (via `JellyfinLibraryService.GetEpisodesForSeason()`) vs. IMDb-Episoden
5. Status bestimmen: `endYear` gesetzt → abgeschlossen, sonst laufend
6. Naechste Episode: Episoden mit `releaseDate` in der Zukunft
7. JSON Response zusammenbauen

### Vergleichslogik (eigene Klasse)

```
ShowHealthAnalyzer
  Verantwortung: Jellyfin-Daten mit IMDb-Daten vergleichen
  Input: JellyfinSeriesInfo + Seasons/Episoden, IMDb Seasons/Episoden
  Output: SeriesHealthResult (fehlende Seasons, fehlende Episoden, Status, naechste Episode)
```

### JSON Response Format

```json
{
  "series": [
    {
      "name": "Breaking Bad",
      "jellyfinId": "guid-here",
      "imdbId": "tt0903747",
      "startYear": 2008,
      "endYear": 2013,
      "status": "ended",
      "seasonsLocal": 5,
      "seasonsTotal": 5,
      "missingSeasons": [],
      "missingEpisodes": [
        {
          "season": 2,
          "episode": 3,
          "title": "Bit by a Dead Bee",
          "imdbId": "tt1054725"
        }
      ],
      "nextEpisode": null
    },
    {
      "name": "The Last of Us",
      "jellyfinId": "guid-here",
      "imdbId": "tt3581920",
      "startYear": 2023,
      "endYear": null,
      "status": "running",
      "seasonsLocal": 2,
      "seasonsTotal": 3,
      "missingSeasons": [3],
      "missingEpisodes": [
        {
          "season": 3,
          "episode": 1,
          "title": "TBA",
          "imdbId": null
        }
      ],
      "nextEpisode": {
        "season": 3,
        "episode": 1,
        "title": "TBA",
        "releaseDate": "2026-04-13"
      }
    }
  ],
  "summary": {
    "total": 12,
    "incomplete": 3,
    "running": 5,
    "ended": 7
  }
}
```

### Poster-URL

Das Frontend baut die Poster-URL selbst aus der Jellyfin-ID:
```
/Items/{jellyfinId}/Images/Primary?height=54
```

Kein Poster-Feld im JSON noetig — Jellyfin liefert das ueber seine Standard-API.

## Datenfluss

```
Browser
  → showhealth.js (ShowHealthPage)
    → ShowHealthApi.fetchStatus()
      → GET /ShowHealth/Status
        → ShowHealthController
          → JellyfinLibraryService (lokale Serien/Seasons/Episoden)
          → ImdbApiClient (IMDb Seasons/Episoden)
          → ShowHealthAnalyzer (Vergleich)
        ← JSON
    → ShowHealthTable.render(data)
    → ShowHealthSorter.sort(data, mode)
```

## Bestehende Komponenten (bereits implementiert)

- `JellyfinLibraryService` — Serien, Seasons, Episoden aus Jellyfin lesen
- `ImdbApiClient` — IMDb API mit Cache, Rate-Limiting, Retry
- `Plugin.cs` — Web-Page Registrierung (Custom Page + JS Controller)
- `Web/showhealth.html` + `Web/showhealth.js` — Hello World Platzhalter

## Neue Komponenten (zu implementieren)

| Komponente | Typ | Datei |
|------------|-----|-------|
| `ShowHealthController` | C# ControllerBase | `Api/ShowHealthController.cs` |
| `ShowHealthAnalyzer` | C# Klasse | `Services/ShowHealthAnalyzer.cs` |
| `ShowHealthScanTask` | C# IScheduledTask | `Tasks/ShowHealthScanTask.cs` |
| `SeriesHealthResult` | C# Model | `Models/SeriesHealthResult.cs` |
| `ShowHealthPage` | JS Klasse | `Web/showhealth.js` |
| `ShowHealthApi` | JS Klasse | `Web/showhealth.js` |
| `ShowHealthTable` | JS Klasse | `Web/showhealth.js` |
| `ShowHealthSorter` | JS Klasse | `Web/showhealth.js` |
| Dashboard HTML | HTML | `Web/showhealth.html` |

## Scheduled Task

Ein `IScheduledTask` der den Vergleich periodisch ausfuehrt:

- Name: "Show Health Scan"
- Kategorie: "Library"
- Default-Intervall: alle 24 Stunden
- Ablauf: identisch zum Controller-Endpunkt (gleicher `ShowHealthAnalyzer`)
- Ergebnis wird im Speicher gecacht — der Controller liefert das gecachte Ergebnis
- Kann manuell ueber das Jellyfin Dashboard unter "Scheduled Tasks" getriggert werden

Wichtig: **Nur die IMDb API Responses werden gecacht** (via `ImdbApiCache`). Die Jellyfin-Bibliotheksdaten werden bei jedem Request/Scan frisch abgerufen — so werden lokale Aenderungen (neue Downloads, geloeschte Dateien) sofort erkannt.

### Datenfluss

```
GET /ShowHealth/Status (oder Scheduled Task)
  → ShowHealthAnalyzer
    → JellyfinLibraryService (IMMER frisch aus der Bibliothek)
    → ImdbApiClient (gecacht via ImdbApiCache)
    → Vergleich
  ← JSON Response
```

## Notifications

Jellyfin Notification System (`INotificationService` / `IActivityManager`):

- Nach jedem Scheduled Task Scan:
  - Wenn neue fehlende Episoden erkannt werden (Differenz zum letzten Scan)
  - Activity Log Eintrag: "Show Health: 3 neue fehlende Episoden erkannt"
- Keine Notification bei unveraendertem Status (kein Spam)
- Nutzt Jellyfins eingebautes Notification-System — funktioniert mit allen konfigurierten Notification-Plugins (Webhook, Email, etc.)

## Nicht im Scope (bewusst ausgeklammert)

- Config Page (kein API-Key UI, keine Einstellungen vorerst)
- Filterung nach Genre, Jahr, etc.
- Export-Funktion
