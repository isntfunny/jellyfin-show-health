# Show Health

Jellyfin-Plugin das alle Serien in der Bibliothek auf Vollstaendigkeit prueft. Es gleicht den lokalen Bestand (Seasons und Episoden) mit den IMDb-Daten ab und meldet:

- **Fehlende Episoden** — welche Folgen innerhalb vorhandener Seasons fehlen
- **Fehlende Seasons** — welche kompletten Staffeln in der Bibliothek nicht vorhanden sind
- **Serien-Status** — ob eine Serie noch laeuft oder abgeschlossen ist (`endYear` aus der IMDb API)
- **Kommende Episoden** — wann die naechste Folge erscheint, basierend auf `releaseDate` der IMDb-Episodendaten

## Funktionsweise

1. **Bibliothek scannen** — Alle Serien aus Jellyfin lesen (`ILibraryManager`), inklusive vorhandener Seasons und Episoden.
2. **IMDb abgleichen** — Pro Serie ueber die IMDb API die Seasons (`/titles/{id}/seasons`) und Episoden (`/titles/{id}/episodes`) abrufen.
3. **Differenz berechnen** — Lokalen Bestand mit IMDb-Soll vergleichen und fehlende Eintraege ermitteln.
4. **Status bestimmen** — Anhand von `endYear` (Title-Objekt) erkennen, ob die Serie abgeschlossen ist. Anhand von `releaseDate` kuenftiger Episoden anzeigen, wann neue Folgen kommen.
5. **Ergebnis anzeigen** — Uebersicht in der Plugin-Konfigurationsseite und optional ueber einen REST-Endpunkt.

## IMDb API

Das Plugin nutzt die [IMDb API](https://api.imdbapi.dev) (v2.7.12). Relevante Endpunkte:

| Endpunkt | Zweck |
|----------|-------|
| `GET /titles/{titleId}` | Titel-Details inkl. `startYear`, `endYear`, `type` |
| `GET /titles/{titleId}/seasons` | Seasons mit `episodeCount` pro Staffel |
| `GET /titles/{titleId}/episodes` | Episoden mit `episodeNumber`, `releaseDate`, `season` |
| `GET /titles:batchGet` | Bis zu 5 Titel auf einmal abfragen |

### Rate-Limiting

Der integrierte `ImdbApiRateLimiter` sorgt dafuer, dass API-Limits eingehalten werden. Antworten werden ueber den `ImdbApiCache` zwischengespeichert, um unnoetige Requests zu vermeiden.

## Architektur

```
Jellyfin.Plugin.ShowHealth/
├── Plugin.cs                          # Plugin-Einstiegspunkt
├── Configuration/
│   ├── PluginConfiguration.cs         # Einstellungen (API-Key, Schwellwerte)
│   └── configPage.html                # Web-Oberflaeche
├── Services/
│   ├── ImdbApi/                       # IMDb API Client, Cache, Rate-Limiter, Models
│   └── Jellyfin/
│       └── JellyfinLibraryService.cs  # Zugriff auf die Jellyfin-Bibliothek
```

## Serien-Status-Erkennung

| Bedingung | Status |
|-----------|--------|
| `endYear` ist gesetzt und liegt in der Vergangenheit | Abgeschlossen |
| `endYear` ist nicht gesetzt | Laufend |
| Episoden mit `releaseDate` in der Zukunft vorhanden | Neue Folgen geplant |

## Build

```bash
dotnet build Jellyfin.Plugin.ShowHealth.sln
```

## Installation

1. Plugin bauen (`dotnet publish`)
2. `Jellyfin.Plugin.ShowHealth.dll` in den Jellyfin-Plugin-Ordner kopieren
3. Jellyfin neu starten
4. Plugin in den Einstellungen konfigurieren (API-Key eintragen)

## Debug / Entwicklung

VS Code Tasks sind vorbereitet — sie bauen das Plugin und kopieren die Artefakte in den lokalen Jellyfin-Plugin-Ordner. Details siehe [HANDBOOK.md](HANDBOOK.md).

## Technische Basis

- .NET 9.0 / net9.0
- Jellyfin NuGet-Pakete: `Jellyfin.Controller`, `Jellyfin.Model`
- Ziel-ABI: 10.11.0.0
- Lizenz: GPLv3

## Lizenz

Dieses Plugin steht unter der [GNU General Public License v3](LICENSE).
