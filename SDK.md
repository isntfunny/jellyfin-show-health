# Jellyfin SDK und API Recherche

## Kurzfazit

Es gibt kein separates, grosses "Jellyfin Plugin SDK" als eigenes Produkt. Der offizielle Weg fuer Plugins ist:

- das offizielle Repository `jellyfin/jellyfin-plugin-template`
- Jellyfin-NuGet-Pakete wie `Jellyfin.Controller` und `Jellyfin.Model`
- die offizielle REST-API-Dokumentation unter `https://api.jellyfin.org/`

Fuer `show-quality` ist das offizielle Template die beste Boilerplate-Basis.

## Wichtige Quellen

### 1. Offizielle Plugin-Doku

Quelle: `https://jellyfin.org/docs/general/server/plugins/`

Wichtige Punkte:

- Jellyfin verweist fuer neue Plugins direkt auf `jellyfin/jellyfin-plugin-template`.
- Plugins koennen manuell in den Jellyfin-Plugin-Ordner gelegt werden.
- Offizielle Plugin-Kategorien zeigen, wie Plugins typischerweise eingeordnet werden: `General`, `Metadata`, `Authentication`, `Channels`, `Live TV`, `Notifications`.
- Offizielle und Drittanbieter-Plugin-Repositories werden ueber Manifest-Dateien verteilt.

Offizielle Repositories:

- Stable: `https://repo.jellyfin.org/files/plugin/manifest.json`
- Unstable: `https://repo.jellyfin.org/files/plugin-unstable/manifest.json`

### 2. Offizielles Plugin-Template

Quelle: `https://github.com/jellyfin/jellyfin-plugin-template`

Warum relevant:

- offizielles Template fuer neue Plugins
- enthaelt fertige Struktur fuer Plugin-Klasse, Konfiguration, Build-Metadaten und GitHub Workflows
- dokumentiert den empfohlenen Start fuer neue Plugins

Wichtige Erkenntnisse aus dem Template:

- Projekt basiert auf `.NET SDK 9.0`
- Projektdatei zielt auf `net9.0`
- Standard-NuGet-Referenzen:
  - `Jellyfin.Controller`
  - `Jellyfin.Model`
- Beide Package-Referenzen verwenden `<ExcludeAssets>runtime</ExcludeAssets>`
  - das ist wichtig, damit das Plugin sauber von Jellyfin geladen wird
- Die Hauptklasse erbt typischerweise von `BasePlugin<TConfiguration>`
- Ein Konfigurations-HTML kann ueber `IHasWebPages` eingebunden werden

Kompatibilitaetshinweis aus der Doku:

- Die Version der Jellyfin-NuGet-Pakete sollte zur Jellyfin-Server-Version passen.
- Sonst kann das Plugin als `NotSupported` auftauchen.

### 3. Offizielle Jellyfin REST API

Quelle: `https://api.jellyfin.org/`

Beobachtung aus der API-Doku:

- dokumentierte Version in der ReDoc-Oberflaeche: `Jellyfin API (10.11.8)`

Fuer Plugins relevante API-Bereiche, die in der Doku sichtbar sind:

- `Items`
- `Library`
- `ItemLookup`
- `ItemRefresh`
- `Plugins`
- `ScheduledTasks`
- `UserLibrary`
- `Playstate`
- `Session`
- `Configuration`
- `Package`

Das ist besonders nuetzlich, wenn `show-quality` spaeter:

- Daten aus der Bibliothek auslesen soll
- Qualitaetsinformationen pro Item berechnen oder anzeigen soll
- eigene Verwaltungs- oder Diagnose-Endpunkte bereitstellen soll

### 4. Referenz-Plugin aus der Praxis

Quelle: `https://github.com/jellyfin/jellyfin-plugin-anilist`

Warum relevant:

- zeigt eine reale Jellyfin-Plugin-Struktur in Produktion
- bestaetigt den typischen Aufbau mit eigener Plugin-Klasse, Build-Metadaten und Release-Struktur
- nuetzlich als Referenz, wenn spaeter echte Jellyfin-Integrationen oder Metadata-Provider umgesetzt werden

## Relevante technische Bausteine fuer Plugin-Entwicklung

Aus Template und offizieller Doku ergeben sich diese Kernbausteine:

### Pflichtbasis

- `Plugin`-Klasse mit `Name`, `Id` und Konstruktor
- Konfigurationsklasse auf Basis von `BasePluginConfiguration`
- optional eingebettete Konfigurationsseite

### Haeufige Erweiterungspunkte

Offiziell dokumentierte Interfaces/Klassen aus der Template-Doku:

- `IAuthenticationProvider`
- `IBaseItemComparer`
- `IIntroProvider`
- `IItemResolver`
- `ILibraryPostScanTask`
- `IMetadataSaver`
- `IResolverIgnoreRule`
- `IScheduledTask`
- `IPluginConfigurationPage`
- `IPluginServiceRegistrator`
- `IHostedService`
- `ControllerBase`

### Haeufige Core-Services fuer DI

Die Template-Doku nennt unter anderem:

- `ILibraryManager`
- `IServerConfigurationManager`
- `ITaskManager`
- `IUserManager`
- `IXmlSerializer`
- `IServerApplicationPaths`
- `INetworkManager`
- `ILocalizationManager`

## Empfehlung fuer `show-quality`

Die sinnvollste Basis ist:

1. offizielles Plugin-Template als Boilerplate
2. `Jellyfin.Controller` und `Jellyfin.Model` als Kern-Referenzen
3. spaeter je nach Ziel eine dieser Richtungen:

- `General`-Plugin mit eigener Konfigurationsseite und interner Logik
- Metadata-/Library-nahes Plugin mit Zugriff auf Bibliotheksobjekte
- Plugin mit eigenen REST-Endpunkten via `ControllerBase`
- Plugin mit Hintergrundlogik ueber `IHostedService` oder `IScheduledTask`

## Quellenliste

- `https://jellyfin.org/docs/general/server/plugins/`
- `https://api.jellyfin.org/`
- `https://github.com/jellyfin/jellyfin-plugin-template`
- `https://github.com/jellyfin/jellyfin-plugin-anilist`
