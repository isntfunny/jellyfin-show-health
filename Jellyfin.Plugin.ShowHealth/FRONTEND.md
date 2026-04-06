# Jellyfin Plugin Frontend - Admin Dashboard & Custom Pages

## Uebersicht

Es gibt **drei Wege** um HTML-Frontends in einem Jellyfin Plugin zu bauen:

| Typ | Wo erscheint es | Interface | Aufwand |
|-----|-----------------|-----------|---------|
| **Config Page** | Dashboard -> Plugins -> Konfiguration | `IHasWebPages` | Niedrig |
| **Custom Page** | Eigener Menuepunkt im Dashboard/Haupt-UI | `IHasWebPages` | Mittel |
| **Plugin Pages** | User-facing Pages im Haupt-UI | Drittanbieter-Plugin | Hoch |

Beide Seitentypen verwenden dasselbe Interface `IHasWebPages` mit `GetPages()`. Der Unterschied liegt in der Konfiguration der `PluginPageInfo`-Eintraege (z.B. `EnableInMainMenu`).

---

## 1. Config Page (Admin Dashboard)

Die einfachste Variante. Zeigt eine Einstellungsseite im Dashboard unter Plugins.

### C# Plugin

```csharp
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "Show Health";

    public override Guid Id => Guid.Parse("...");

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        };
    }
}
```

### csproj

```xml
<ItemGroup>
  <None Remove="Configuration\configPage.html" />
  <EmbeddedResource Include="Configuration\configPage.html" />
</ItemGroup>
```

### HTML Template

Config Pages verwenden ein Inline-`<script>` innerhalb des Page-Divs. Es wird **kein** separater JS-Controller und **kein** `data-controller`-Attribut verwendet.

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <title>Show Health</title>
</head>
<body>
    <div id="ShowHealthConfigPage" data-role="page"
         class="page type-interior pluginConfigurationPage"
         data-require="emby-input,emby-button,emby-select,emby-checkbox">
        <div data-role="content">
            <div class="content-primary">
                <form id="ShowHealthConfigForm">
                    <div class="inputContainer">
                        <label class="inputLabel inputLabelUnfocused" for="txtApiKey">API Key</label>
                        <input id="txtApiKey" name="txtApiKey" type="text" is="emby-input" />
                        <div class="fieldDescription">IMDB API Key</div>
                    </div>
                    <div class="checkboxContainer checkboxContainer-withDescription">
                        <label class="emby-checkbox-label">
                            <input id="chkEnabled" name="chkEnabled" type="checkbox" is="emby-checkbox" />
                            <span>Enabled</span>
                        </label>
                    </div>
                    <div class="selectContainer">
                        <label class="selectLabel" for="selectInterval">Interval</label>
                        <select is="emby-select" id="selectInterval" name="selectInterval">
                            <option value="3600">1 hour</option>
                            <option value="86400">24 hours</option>
                        </select>
                    </div>
                    <div>
                        <button is="emby-button" type="submit"
                                class="raised button-submit block emby-button">
                            <span>Save</span>
                        </button>
                    </div>
                </form>
            </div>
        </div>
        <script type="text/javascript">
            var ShowHealthConfig = {
                pluginUniqueId: 'PLUGIN-GUID-HERE'
            };

            document.querySelector('#ShowHealthConfigPage')
                .addEventListener('pageshow', function () {
                    Dashboard.showLoadingMsg();
                    ApiClient.getPluginConfiguration(ShowHealthConfig.pluginUniqueId).then(function (config) {
                        document.querySelector('#txtApiKey').value = config.ApiKey;
                        document.querySelector('#chkEnabled').checked = config.Enabled;
                        document.querySelector('#selectInterval').value = config.Interval;
                        Dashboard.hideLoadingMsg();
                    });
                });

            document.querySelector('#ShowHealthConfigForm')
                .addEventListener('submit', function (e) {
                    Dashboard.showLoadingMsg();
                    ApiClient.getPluginConfiguration(ShowHealthConfig.pluginUniqueId).then(function (config) {
                        config.ApiKey = document.querySelector('#txtApiKey').value;
                        config.Enabled = document.querySelector('#chkEnabled').checked;
                        config.Interval = document.querySelector('#selectInterval').value;
                        ApiClient.updatePluginConfiguration(ShowHealthConfig.pluginUniqueId, config).then(function (result) {
                            Dashboard.processPluginConfigurationUpdateResult(result);
                        });
                    });
                    e.preventDefault();
                    return false;
                });
        </script>
    </div>
</body>
</html>
```

### Wichtige HTML-Klassen

| Klasse | Zweck |
|--------|-------|
| `pluginConfigurationPage` | Muss auf dem page-div stehen |
| `type-interior` | Dashboard-Layout mit Sidebar |
| `content-primary` | Hauptinhalt-Bereich |

### Jellyfin Web Components

| Component | Verwendung |
|-----------|-----------|
| `is="emby-input"` | Text-Input mit Jellyfin-Styling |
| `is="emby-select"` | Dropdown mit Jellyfin-Styling |
| `is="emby-checkbox"` | Checkbox mit Jellyfin-Styling |
| `is="emby-button"` | Button mit Jellyfin-Styling |
| `is="paper-icon-button-light"` | Icon-Button (Material Icons) |

---

## 2. Custom Page (data-controller + ES Module)

Ermoeglicht eigene Seiten im Dashboard oder Haupt-UI. Das **Reports Plugin** ist das beste Beispiel.

Custom Pages verwenden `data-controller` im HTML und eine separate JS-Datei als ES-Modul mit `export default function(view)`.

### C# Plugin

```csharp
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public override Guid Id => new Guid("...");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            // Hauptseite - erscheint im Menue
            new PluginPageInfo
            {
                Name = "showhealth",
                EmbeddedResourcePath = GetType().Namespace + ".Web.showhealth.html",
                EnableInMainMenu = true,
                MenuIcon = "health_and_safety"
            },
            // JavaScript Controller (separate PluginPageInfo)
            new PluginPageInfo
            {
                Name = "showhealthjs",
                EmbeddedResourcePath = GetType().Namespace + ".Web.showhealth.js"
            }
        };
    }
}
```

### HTML Template

Custom Pages verwenden `data-controller` und **kein** Inline-Script. Das HTML ist ein Fragment ohne `<!DOCTYPE>`, `<html>`, `<head>` oder `<body>` Tags.

```html
<div id="showHealthPage" data-role="page"
     class="page type-interior pluginConfigurationPage"
     data-title="Show Health"
     data-controller="__plugin/showhealthjs">
    <div class="content-primary">
        <h1>Show Health Dashboard</h1>
        <div id="healthStatus"></div>
    </div>
</div>
```

### JavaScript Controller (ES Module)

Die JS-Datei verwendet `export default function(view)` und lauscht auf das `viewshow`-Event:

```javascript
// showhealth.js - wird als separate Embedded Resource geladen
export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        // Daten laden und UI aktualisieren
        ApiClient.getJSON(ApiClient.getUrl('/Shows/Health')).then(function (result) {
            var html = '';
            result.Items.forEach(function (item) {
                html += '<div>' + item.Name + '</div>';
            });
            view.querySelector('#healthStatus').innerHTML = html;
            Dashboard.hideLoadingMsg();
        });
    });
}
```

### csproj

```xml
<ItemGroup>
  <None Remove="Web\showhealth.html" />
  <EmbeddedResource Include="Web\showhealth.html" />
  <None Remove="Web\showhealth.js" />
  <EmbeddedResource Include="Web\showhealth.js" />
</ItemGroup>
```

### PluginPageInfo Properties

| Property | Typ | Zweck |
|----------|-----|-------|
| `Name` | `string` | URL-Pfad (`/web/index.html#!/showhealth`) |
| `DisplayName` | `string?` | Angezeigter Name im Menue |
| `EmbeddedResourcePath` | `string` | Pfad zur eingebetteten Resource |
| `EnableInMainMenu` | `bool` | Zeigt im Hauptmenue (true) oder nur als versteckte Seite (false) |
| `MenuSection` | `string?` | Gruppierung im Menue |
| `MenuIcon` | `string?` | Material Icon Name |

---

## 3. API Calls vom Frontend

Jellyfin stellt globale JavaScript-Objekte bereit:

```javascript
// Server-URL (ohne trailing slash)
var serverUrl = ApiClient.serverAddress();

// Authentifizierter JSON-GET-Request
ApiClient.getJSON(ApiClient.getUrl('/Shows/NextUp')).then(function (result) {
    console.log(result.Items);
});

// Plugin Config laden
ApiClient.getPluginConfiguration('PLUGIN-GUID').then(function (config) {
    // config lesen
});

// Plugin Config speichern (zwei Argumente: pluginId und config-Objekt)
ApiClient.updatePluginConfiguration('PLUGIN-GUID', config).then(function (result) {
    Dashboard.processPluginConfigurationUpdateResult(result);
});

// Allgemeiner AJAX-Request
ApiClient.ajax({
    type: 'GET',
    url: ApiClient.getUrl('/Items', { IncludeItemTypes: 'Series' })
}).then(function (response) {
    var data = JSON.parse(response);
});
```

---

## 4. Plugin Pages (Drittanbieter)

[IAmParadox27/jellyfin-plugin-pages](https://github.com/IAmParadox27/jellyfin-plugin-pages) erlaubt es Plugins, **User-facing Pages** im Haupt-UI hinzuzufuegen (nicht nur Admin-Dashboard).

### Installation
1. Plugin Repo: `https://www.iamparadox.dev/jellyfin/plugins/manifest.json`
2. `File Transformation` Plugin installieren
3. `Plugin Pages` installieren

### Eigene Pages registrieren

Die Konfiguration erfolgt ueber das Plugin-Pages-Framework. Siehe die Dokumentation im Repository fuer die aktuelle Konfigurations-Methode.

---

## 5. Best Practices

### Config Page vs. Custom Page

| Aspekt | Config Page | Custom Page |
|--------|-------------|-------------|
| HTML-Struktur | Vollstaendiges HTML-Dokument | HTML-Fragment (nur div) |
| JavaScript | Inline `<script>` im HTML | Separate JS-Datei als ES Module |
| Event | `pageshow` | `viewshow` |
| Controller | Keiner (Inline-Script) | `data-controller="__plugin/name"` |
| Registrierung | Eine `PluginPageInfo` | Zwei `PluginPageInfo` (HTML + JS) |

**Wichtig:** Diese Muster nicht vermischen. Verwende entweder Inline-Script mit `pageshow` (Config Page) oder `data-controller` mit `export default function` und `viewshow` (Custom Page).

### Loading States

```javascript
Dashboard.showLoadingMsg(); // Spinner anzeigen
Dashboard.hideLoadingMsg(); // Spinner verstecken
```

### Toast Notifications

```javascript
Dashboard.alert('Something went wrong');
Dashboard.confirm('Are you sure?', 'Confirm', function (result) {
    if (result) { /* user confirmed */ }
});
```

### Data Binding Pattern (Config Page)

```javascript
var MyPluginConfig = {
    pluginUniqueId: 'PLUGIN-GUID'
};

document.querySelector('#MyConfigPage')
    .addEventListener('pageshow', function () {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(MyPluginConfig.pluginUniqueId).then(function (config) {
            document.querySelector('#txtSetting').value = config.Setting;
            Dashboard.hideLoadingMsg();
        });
    });

document.querySelector('#MyConfigForm')
    .addEventListener('submit', function (e) {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(MyPluginConfig.pluginUniqueId).then(function (config) {
            config.Setting = document.querySelector('#txtSetting').value;
            ApiClient.updatePluginConfiguration(MyPluginConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });
        e.preventDefault();
        return false;
    });
```

### Data Binding Pattern (Custom Page)

```javascript
export default function (view) {
    view.addEventListener('viewshow', function () {
        Dashboard.showLoadingMsg();
        // Daten laden und anzeigen
        Dashboard.hideLoadingMsg();
    });
}
```

---

## 6. Example Plugins zum Anschauen

| Plugin | Repo | Zweck |
|--------|------|-------|
| **Template** | `jellyfin/jellyfin-plugin-template` | Minimale Config Page (Referenz-Implementierung) |
| **Reports** | `jellyfin/jellyfin-plugin-reports` | Custom Page mit data-controller, ES Module, Tabellen, Export |
| **Plugin Pages** | `IAmParadox27/jellyfin-plugin-pages` | User-facing Pages Framework |

---

## 7. Debugging

1. **Browser DevTools:** `F12` im Jellyfin Web-UI
2. **Console:** `console.log()` erscheint in DevTools
3. **HTML neu laden:** Dashboard -> Plugins -> Config -> Speichern
4. **Cache leeren:** Browser Hard-Reload (`Ctrl+Shift+R`)
5. **Server Logs:** Jellyfin Log-Verzeichnis pruefen (abhaengig von Installation)
6. **Embedded Resources pruefen:** Im Build-Output sicherstellen, dass die Dateien als Embedded Resource enthalten sind (`dotnet build` Output oder `.dll` mit ILSpy inspizieren)
