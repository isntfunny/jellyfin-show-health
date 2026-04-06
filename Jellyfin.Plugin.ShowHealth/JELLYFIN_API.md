# Jellyfin Plugin Development - Ressourcen

## Offizielle Dokumentation

- **Plugin Development Guide:** https://mintlify.com/jellyfin/jellyfin/development/plugin-development
- **Plugin Template:** https://github.com/jellyfin/jellyfin-plugin-template
- **Plugin Katalog:** https://jellyfin.org/docs/general/server/plugins/

## Source Code (GitHub)

### Haupt-Repository
- **Server:** https://github.com/jellyfin/jellyfin

### Wichtige Dateien für Plugin-Entwicklung

| Was | Pfad im Repo |
|-----|------|
| ILibraryManager Interface | `MediaBrowser.Controller/Library/ILibraryManager.cs` |
| InternalItemsQuery | `MediaBrowser.Controller/Entities/InternalItemsQuery.cs` |
| BaseItem (alle Items) | `MediaBrowser.Controller/Entities/BaseItem.cs` |
| Series (TV Serien) | `MediaBrowser.Controller/Entities/TV/Series.cs` |
| Season | `MediaBrowser.Controller/Entities/TV/Season.cs` |
| Episode | `MediaBrowser.Controller/Entities/TV/Episode.cs` |
| QueryResult | `MediaBrowser.Model/Querying/QueryResult.cs` |
| MetadataProvider Enums | `MediaBrowser.Model/Entities/MetadataProvider.cs` |
| BaseItemKind Enum | `Jellyfin.Data/Enums/BaseItemKind.cs` |
| SeriesStatus Enum | `Jellyfin.Data/Enums/SeriesStatus.cs` |

## API Referenz

### REST API (HTTP)
- **OpenAPI/Swagger:** `http://<jellyfin-server>:8096/api-docs/swagger.html`
- **TypeScript SDK:** https://typescript-sdk.jellyfin.org/
- **API Doku:** https://mintlify.com/jellyfin/jellyfin/api/

### Server-interne API (für Plugins)
Plugins laufen **im selben Prozess** wie der Server. Kein HTTP nötig — Interfaces werden per DI injiziert.

```csharp
// Beispiel: ILibraryManager per Dependency Injection
public class MyPlugin
{
    private readonly ILibraryManager _libraryManager;

    public MyPlugin(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }
}
```

## Wichtige Interfaces für Plugins

| Interface | Zweck |
|-----------|-------|
| `ILibraryManager` | Medienbibliothek abfragen, Items suchen |
| `IUserManager` | Benutzer verwalten |
| `IServerApplicationHost` | Server-Info (Version, Pfade) |
| `IHttpClientFactory` | HTTP Requests (für externe APIs) |
| `ILogger<T>` | Logging |

## Return Types im Detail

### `GetAllSeriesAsync()` → `Task<IReadOnlyList<JellyfinSeriesInfo>>`

Liefert **alle** Serien der Bibliothek, paginiert in Batches von 500.

```csharp
var series = await _jellyfinLibrary.GetAllSeriesAsync();
// series[0].Name          → "Breaking Bad"
// series[0].ImdbId        → "tt0903747"
// series[0].TvdbId        → "81189"
// series[0].ProductionYear → 2008
// series[0].Status         → SeriesStatus.Ended
// series[0].Genres         → ["Crime", "Drama", "Thriller"]
// series[0].Studios        → ["AMC", "Sony Pictures Television"]
// series[0].CommunityRating → 9.5
// series[0].Overview       → "A high school chemistry teacher..."
// series[0].Id             → GUID (Jellyfin intern)
```

### `GetSeriesWithImdbIdAsync()` → `Task<IReadOnlyList<JellyfinSeriesInfo>>`

Wie oben, aber filtert Serien **ohne** IMDB ID heraus. Ideal für IMDB API Abgleiche.

### `GetSeasonsForSeries(Guid seriesId)` → `IReadOnlyList<JellyfinSeasonInfo>`

Alle Seasons einer konkreten Serie, sortiert nach Season-Nummer.

```csharp
var seasons = _jellyfinLibrary.GetSeasonsForSeries(series.Id);
// seasons[0].Name         → "Season 1"
// seasons[0].IndexNumber  → 1
// seasons[0].EpisodeCount → 7
// seasons[0].SeriesId     → GUID der Parent-Serie
// seasons[0].Id           → GUID der Season
```

### `GetEpisodesForSeason(Guid seasonId)` → `IReadOnlyList<JellyfinEpisodeInfo>`

Alle Episoden einer Season.

```csharp
var episodes = _jellyfinLibrary.GetEpisodesForSeason(season.Id);
// episodes[0].Name            → "Pilot"
// episodes[0].IndexNumber     → 1
// episodes[0].ParentIndexNumber → 1
// episodes[0].ImdbId          → "tt0959621"
// episodes[0].PremiereDate    → 2008-01-20
// episodes[0].RunTimeTicks    → 34800000000  (÷ TimeSpan.TicksPerMinute = Minuten)
// episodes[0].Overview        → "Walter White..."
// episodes[0].CommunityRating → 8.9
// episodes[0].SeasonId        → GUID der Parent-Season
// episodes[0].SeriesId        → GUID der Parent-Serie
// episodes[0].Id              → GUID der Episode
```

### `GetSeriesCount()` → `int`

Schnelle Abfrage der Gesamtanzahl ohne Items zu laden.

### `GetImdbIdForItem(Guid itemId)` → `string?`

IMDB ID für beliebige Items (Serie, Episode, Film).

## Serien-Hierarchie auflösen

```
JellyfinLibraryService
├── GetAllSeriesAsync()           → Alle Serien
│   └── GetSeasonsForSeries(id)   → Alle Seasons einer Serie
│       └── GetEpisodesForSeason(id) → Alle Episoden einer Season
│           └── ImdbId → IMDB API → Details, Ratings, etc.
```

## Serien aus der Bibliothek holen (roh)

```csharp
var query = new InternalItemsQuery
{
    IncludeItemTypes = new[] { BaseItemKind.Series },
    Recursive = true,
    StartIndex = 0,
    Limit = 500,
};

var result = _libraryManager.QueryItems(query);
// result.Items             → IReadOnlyList<BaseItem>
// result.TotalRecordCount  → int (Gesamtanzahl für Pagination)
```

## IMDB ID aus einer Serie extrahieren

```csharp
// Aus ProviderIds Dictionary
var imdbId = series.ProviderIds.GetValueOrDefault("Imdb");

// Fallback: GetUserDataKeys (liefert priorisierte IDs)
var keys = series.GetUserDataKeys();
// Erste die mit "tt" beginnt ist die IMDB ID
```

## Nützliche NuGet Packages

| Package | Version | Zweck |
|---------|---------|-------|
| `Jellyfin.Controller` | 10.11.8 | ILibraryManager, BaseItem, Series, Season, Episode |
| `Jellyfin.Model` | 10.11.8 | QueryResult, MetadataProvider, InternalItemsQuery |
| `Jellyfin.Data` | 10.11.8 | BaseItemKind, SeriesStatus, User |
| `MediaBrowser.Common` | 10.11.8 | IPlugin, IServerApplicationPaths |

## Debugging Tips

1. **Lokaler Server:** Jellyfin Server im Debug-Mode starten
2. **Plugin DLL:** Nach `bin/Debug/net9.0/` kopieren
3. **Logs:** `/var/log/jellyfin/` oder Jellyfin Dashboard → Logs
4. **Swagger UI:** `http://localhost:8096/api-docs/swagger.html` für REST API Testing
