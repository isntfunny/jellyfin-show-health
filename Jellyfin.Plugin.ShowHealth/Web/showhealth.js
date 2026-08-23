function countGaps(s) {
    var gaps = 0;
    if (s.missingEpisodes) {
        for (var i = 0; i < s.missingEpisodes.length; i++) {
            if (s.missingEpisodes[i].isGap) gaps++;
        }
    }
    if (s.missingSeasons) {
        for (var i = 0; i < s.missingSeasons.length; i++) {
            if (s.missingSeasons[i].isGap) gaps += s.missingSeasons[i].episodeCount || 0;
        }
    }
    return gaps;
}

function countTrailing(s) {
    var trail = 0;
    if (s.missingEpisodes) {
        for (var i = 0; i < s.missingEpisodes.length; i++) {
            if (!s.missingEpisodes[i].isGap) trail++;
        }
    }
    if (s.missingSeasons) {
        for (var i = 0; i < s.missingSeasons.length; i++) {
            if (!s.missingSeasons[i].isGap) trail += s.missingSeasons[i].episodeCount || 0;
        }
    }
    return trail;
}

function totalMissing(s) {
    return countGaps(s) + countTrailing(s);
}

function isIncomplete(s) {
    return (s.missingEpisodes && s.missingEpisodes.length > 0) ||
           (s.missingSeasons && s.missingSeasons.length > 0);
}

function escapeHtml(text) {
    if (!text) return '';
    var div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

class ShowHealthApi {
    constructor(apiClient) {
        this._apiClient = apiClient;
    }

    async fetchSnapshot() {
        var url = this._apiClient.getUrl('/ShowHealth/Snapshot');
        return await this._apiClient.getJSON(url);
    }

    async runScan() {
        var url = this._apiClient.getUrl('/ShowHealth/RunScan');
        await this._apiClient.ajax({ type: 'POST', url: url });
    }

    async fetchIgnored() {
        var url = this._apiClient.getUrl('/ShowHealth/Ignored');
        return await this._apiClient.getJSON(url);
    }

    async addIgnored(imdbId, name) {
        var url = this._apiClient.getUrl('/ShowHealth/Ignored');
        await this._apiClient.ajax({
            type: 'POST', url: url,
            contentType: 'application/json',
            data: JSON.stringify({ imdbId: imdbId, name: name })
        });
    }

    async removeIgnored(imdbId) {
        var url = this._apiClient.getUrl('/ShowHealth/Ignored/' + imdbId);
        await this._apiClient.ajax({ type: 'DELETE', url: url });
    }

    async clearCache() {
        var url = this._apiClient.getUrl('/ShowHealth/ClearCache');
        await this._apiClient.ajax({ type: 'POST', url: url });
    }
}

class ShowHealthSorter {
    sort(series, mode, ascending) {
        var sorted = series.slice();
        switch (mode) {
            case 'status': sorted = this._sortByStatus(sorted); break;
            case 'gaps': sorted = this._sortByGaps(sorted); break;
            case 'trailing': sorted = this._sortByTrailing(sorted); break;
            case 'release': sorted = this._sortByRelease(sorted); break;
            case 'name': sorted = this._sortByName(sorted); break;
        }
        if (!ascending) sorted.reverse();
        return sorted;
    }

    _sortByStatus(series) {
        return series.sort(function (a, b) {
            var aM = totalMissing(a), bM = totalMissing(b);
            if ((aM > 0) !== (bM > 0)) return aM > 0 ? -1 : 1;
            return a.name.localeCompare(b.name);
        });
    }

    _sortByGaps(series) {
        return series.sort(function (a, b) {
            var diff = countGaps(b) - countGaps(a);
            return diff !== 0 ? diff : a.name.localeCompare(b.name);
        });
    }

    _sortByTrailing(series) {
        return series.sort(function (a, b) {
            var diff = countTrailing(b) - countTrailing(a);
            return diff !== 0 ? diff : a.name.localeCompare(b.name);
        });
    }

    _sortByRelease(series) {
        var self = this;
        return series.sort(function (a, b) {
            var aR = self._releaseDateRank(a), bR = self._releaseDateRank(b);
            if (aR.tier !== bR.tier) return aR.tier - bR.tier;
            if (aR.date && bR.date) return aR.date.localeCompare(bR.date);
            return a.name.localeCompare(b.name);
        });
    }

    _releaseDateRank(s) {
        if (s.nextEpisode && s.nextEpisode.releaseDate) {
            var rd = s.nextEpisode.releaseDate;
            return rd.length > 4 ? { tier: 0, date: rd } : { tier: 1, date: rd };
        }
        return s.status === 'ended' ? { tier: 3, date: null } : { tier: 2, date: null };
    }

    _sortByName(series) {
        return series.sort(function (a, b) { return a.name.localeCompare(b.name); });
    }
}

class ShowHealthTable {
    constructor(apiClient, onIgnore) {
        this._apiClient = apiClient;
        this._expandedRows = {};
        this._onIgnore = onIgnore;
    }

    render(series, container) {
        var colStyle = 'style="width:14%;padding:8px;"';
        var html = '<table style="width:100%;border-collapse:collapse;font-size:0.9em;table-layout:fixed;">';
        html += '<thead><tr style="border-bottom:2px solid #333;text-align:left;">' +
            '<th style="width:30px;padding:8px 4px;"></th>' +
            '<th style="width:54px;padding:8px 4px;"></th>' +
            '<th style="padding:8px;">Show</th>' +
            '<th ' + colStyle + '>Seasons</th>' +
            '<th ' + colStyle + '>Gaps</th>' +
            '<th ' + colStyle + '>Trailing</th>' +
            '<th ' + colStyle + '>Next Episode</th>' +
            '</tr></thead><tbody>';

        for (var i = 0; i < series.length; i++) {
            html += this._renderRow(series[i], i);
            html += this._renderDetailRow(series[i], i);
        }

        html += '</tbody></table>';
        container.innerHTML = html;
        this._bindEvents(container, series);
    }

    _renderRow(s, index) {
        var incomplete = isIncomplete(s);
        var opacity = incomplete ? '1' : '0.5';
        var expanded = this._expandedRows[index];
        var arrow = incomplete
            ? '<span class="showhealth-arrow" style="cursor:pointer;font-size:1.1em;transition:transform 0.2s;display:inline-block;' +
              (expanded ? 'transform:rotate(90deg);' : '') + '">\u25B6</span>'
            : '';

        var posterUrl = this._apiClient.getUrl('/Items/' + s.jellyfinId + '/Images/Primary', { height: 54 });
        var poster = '<img src="' + posterUrl + '" style="height:54px;border-radius:3px;" onerror="this.style.display=\'none\'" />';

        var yearRange = s.startYear ? (s.startYear + (s.endYear ? '\u2013' + s.endYear : '\u2013')) : '';
        var ignoreBtn = '<span class="showhealth-ignore" data-imdb="' + this._escapeAttr(s.imdbId) + '" data-name="' + this._escapeAttr(s.name) + '" style="cursor:pointer;color:#666;font-size:0.8em;margin-left:6px;" title="Ignore">\u2715</span>';
        var nameCell = '<div>' + escapeHtml(s.name) + ignoreBtn + '</div>' +
                       '<div style="color:#888;font-size:0.85em;">' + yearRange + '</div>';

        var missingSeasons = s.missingSeasons ? s.missingSeasons.length : 0;
        var seasonsColor = missingSeasons > 0 ? 'color:#e5383b;' : '';
        var seasonsCell = '<span style="' + seasonsColor + '">' + s.seasonsLocal + '/' + s.seasonsTotal + '</span>';

        var gaps = countGaps(s);
        var gapsCell = gaps > 0
            ? '<span style="color:#e5383b;">' + gaps + '</span>'
            : '<span style="color:#4caf50;">\u2014</span>';

        var trailing = countTrailing(s);
        var trailingCell = trailing > 0
            ? '<span style="color:#ffa726;">' + trailing + '</span>'
            : '<span style="color:#4caf50;">\u2014</span>';

        var nextCell = '';
        if (s.nextEpisode && s.nextEpisode.releaseDate) {
            nextCell = '<span style="background:#2a2a1a;color:#ffa726;padding:2px 8px;border-radius:3px;font-size:0.85em;">' +
                       escapeHtml(s.nextEpisode.releaseDate) + '</span>';
        } else if (s.status === 'ended') {
            nextCell = '<span style="background:#1a3a1a;color:#4caf50;padding:2px 8px;border-radius:3px;font-size:0.85em;">Ended</span>';
        } else {
            nextCell = '<span style="background:#2a2a2a;color:#888;padding:2px 8px;border-radius:3px;font-size:0.85em;">TBA</span>';
        }

        return '<tr data-index="' + index + '" style="border-bottom:1px solid #222;opacity:' + opacity + ';' + (incomplete ? 'cursor:pointer;' : '') + '">' +
            '<td style="padding:8px 4px;text-align:center;">' + arrow + '</td>' +
            '<td style="padding:8px 4px;">' + poster + '</td>' +
            '<td style="padding:8px;">' + nameCell + '</td>' +
            '<td style="width:14%;padding:8px;">' + seasonsCell + '</td>' +
            '<td style="width:14%;padding:8px;">' + gapsCell + '</td>' +
            '<td style="width:14%;padding:8px;">' + trailingCell + '</td>' +
            '<td style="width:14%;padding:8px;">' + nextCell + '</td>' +
            '</tr>';
    }

    _renderDetailRow(s, index) {
        if (!isIncomplete(s)) return '';

        var display = this._expandedRows[index] ? '' : 'display:none;';
        var grouped = {};
        if (s.missingEpisodes) {
            for (var i = 0; i < s.missingEpisodes.length; i++) {
                var ep = s.missingEpisodes[i];
                if (!grouped[ep.season]) grouped[ep.season] = [];
                grouped[ep.season].push(ep);
            }
        }

        var detailHtml = '<td colspan="7" style="padding:8px 8px 16px 60px;">';
        var seasons = Object.keys(grouped).sort(function (a, b) { return Number(a) - Number(b); });

        for (var si = 0; si < seasons.length; si++) {
            var sn = seasons[si];
            detailHtml += '<div style="margin-bottom:8px;"><strong style="color:#aaa;">Season ' + sn + '</strong></div>';
            detailHtml += '<div style="display:flex;flex-wrap:wrap;gap:6px;margin-bottom:12px;">';
            var eps = grouped[sn];
            for (var ei = 0; ei < eps.length; ei++) {
                var e = eps[ei];
                var epNum = 'E' + String(e.episode).padStart(2, '0');
                var snPad = 'S' + String(sn).padStart(2, '0');
                var copyText = this._escapeAttr(s.name) + ' ' + snPad + epNum;
                var title = e.title ? ' \u2014 ' + escapeHtml(e.title) : '';
                var chipColor = e.isGap ? '#e5383b' : '#ffa726';
                detailHtml += '<span class="showhealth-chip" data-copy="' + copyText + '" style="border-left:3px solid ' + chipColor + ';padding:4px 10px;background:#2a2a2a;border-radius:0 3px 3px 0;font-size:0.85em;cursor:pointer;" title="Click to copy">' +
                              epNum + title + '</span>';
            }
            detailHtml += '</div>';
        }

        if (s.missingSeasons && s.missingSeasons.length > 0) {
            detailHtml += '<div style="margin-bottom:8px;"><strong style="color:#aaa;">Missing Seasons</strong></div>';
            detailHtml += '<div style="display:flex;flex-wrap:wrap;gap:6px;margin-bottom:12px;">';
            for (var mi = 0; mi < s.missingSeasons.length; mi++) {
                var ms = s.missingSeasons[mi];
                var snPad2 = 'S' + String(ms.season).padStart(2, '0');
                var copyText2 = this._escapeAttr(s.name) + ' ' + snPad2 + ' complete';
                var epInfo = ms.episodeCount ? ' (' + ms.episodeCount + ' ep)' : '';
                var seasonChipColor = ms.isGap ? '#e5383b' : '#ffa726';
                detailHtml += '<span class="showhealth-chip" data-copy="' + copyText2 + '" style="border-left:3px solid ' + seasonChipColor + ';padding:4px 10px;background:#2a2a2a;border-radius:0 3px 3px 0;font-size:0.85em;cursor:pointer;" title="Click to copy">Season ' +
                              ms.season + epInfo + '</span>';
            }
            detailHtml += '</div>';
        }

        detailHtml += '</td>';
        return '<tr class="showhealth-detail" data-detail-index="' + index + '" style="' + display + '">' + detailHtml + '</tr>';
    }

    _bindEvents(container, series) {
        var self = this;
        var rows = container.querySelectorAll('tr[data-index]');
        for (var i = 0; i < rows.length; i++) {
            (function (row) {
                var idx = parseInt(row.getAttribute('data-index'), 10);
                var s = series[idx];
                if (!isIncomplete(s)) return;
                row.addEventListener('click', function () {
                    self._expandedRows[idx] = !self._expandedRows[idx];
                    var detailRow = container.querySelector('tr[data-detail-index="' + idx + '"]');
                    var arrow = row.querySelector('.showhealth-arrow');
                    if (detailRow) detailRow.style.display = self._expandedRows[idx] ? '' : 'none';
                    if (arrow) arrow.style.transform = self._expandedRows[idx] ? 'rotate(90deg)' : '';
                });
            })(rows[i]);
        }

        var chips = container.querySelectorAll('.showhealth-chip');
        for (var ci = 0; ci < chips.length; ci++) {
            chips[ci].addEventListener('click', function (e) {
                e.stopPropagation();
                var text = this.getAttribute('data-copy');
                if (text) {
                    navigator.clipboard.writeText(text).then(function () {
                        Dashboard.alert('Copied:' + text);
                    });
                }
            });
        }

        // Ignore buttons
        var ignoreBtns = container.querySelectorAll('.showhealth-ignore');
        for (var ii = 0; ii < ignoreBtns.length; ii++) {
            ignoreBtns[ii].addEventListener('click', function (e) {
                e.stopPropagation();
                var imdbId = this.getAttribute('data-imdb');
                var name = this.getAttribute('data-name');
                if (self._onIgnore && imdbId) {
                    self._onIgnore(imdbId, name);
                }
            });
        }
    }

    _escapeAttr(text) {
        if (!text) return '';
        return text.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/'/g, '&#39;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }
}

class ShowHealthExporter {
    constructor() {
        this._dialog = null;
    }

    show(seriesList) {
        this._seriesList = seriesList;
        this._showDialog();
    }

    _showDialog() {
        this._removeDialog();
        var overlay = document.createElement('div');
        overlay.id = 'showHealthExportOverlay';
        overlay.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.7);z-index:9999;display:flex;align-items:center;justify-content:center;';

        var dialog = document.createElement('div');
        dialog.style.cssText = 'background:#1a1a1a;border-radius:8px;padding:24px;min-width:320px;max-width:420px;box-shadow:0 8px 32px rgba(0,0,0,0.5);';
        dialog.innerHTML =
            '<h2 style="margin:0 0 16px 0;font-size:1.2em;">CSV Export</h2>' +
            '<p style="color:#aaa;font-size:0.9em;margin:0 0 16px 0;">Only shows with missing content will be exported.</p>' +
            '<div style="display:flex;flex-direction:column;gap:10px;margin-bottom:20px;">' +
                '<button id="showHealthExportEpisodes" style="padding:12px 16px;background:#2a2a2a;border:1px solid #444;border-radius:6px;color:#fff;cursor:pointer;text-align:left;font-size:0.9em;">' +
                    '<strong>All Episodes</strong><br><span style="color:#aaa;font-size:0.85em;">Every missing episode individually \u2014 missing seasons expanded to single episodes</span>' +
                '</button>' +
                '<button id="showHealthExportGaps" style="padding:12px 16px;background:#2a2a2a;border:1px solid #444;border-radius:6px;color:#fff;cursor:pointer;text-align:left;font-size:0.9em;">' +
                    '<strong>Only Gaps</strong><br><span style="color:#aaa;font-size:0.85em;">Only real gaps \u2014 no trailing content</span>' +
                '</button>' +
                '<button id="showHealthExportSeasons" style="padding:12px 16px;background:#2a2a2a;border:1px solid #444;border-radius:6px;color:#fff;cursor:pointer;text-align:left;font-size:0.9em;">' +
                    '<strong>By Show</strong><br><span style="color:#aaa;font-size:0.85em;">One row per show \u2014 seasons and episodes summarized</span>' +
                '</button>' +
            '</div>' +
            '<div style="text-align:right;">' +
                '<button id="showHealthExportCancel" style="padding:8px 20px;background:none;border:1px solid #555;border-radius:4px;color:#aaa;cursor:pointer;">Cancel</button>' +
            '</div>';

        overlay.appendChild(dialog);
        document.body.appendChild(overlay);
        this._dialog = overlay;

        var self = this;
        overlay.querySelector('#showHealthExportCancel').addEventListener('click', function () { self._removeDialog(); });
        overlay.addEventListener('click', function (e) { if (e.target === overlay) self._removeDialog(); });
        overlay.querySelector('#showHealthExportEpisodes').addEventListener('click', function () { self._exportEpisodes(); self._removeDialog(); });
        overlay.querySelector('#showHealthExportGaps').addEventListener('click', function () { self._exportGaps(); self._removeDialog(); });
        overlay.querySelector('#showHealthExportSeasons').addEventListener('click', function () { self._exportSeasons(); self._removeDialog(); });
    }

    _removeDialog() {
        if (this._dialog) { this._dialog.remove(); this._dialog = null; }
    }

    _getIncompleteSeries() {
        return this._seriesList.filter(function (s) { return isIncomplete(s); });
    }

    _collectEpisodeRows(gapOnly) {
        var rows = [];
        var series = this._getIncompleteSeries();
        for (var i = 0; i < series.length; i++) {
            var s = series[i];
            if (s.missingEpisodes) {
                for (var j = 0; j < s.missingEpisodes.length; j++) {
                    var ep = s.missingEpisodes[j];
                    if (gapOnly && !ep.isGap) continue;
                    rows.push([s.name, 'S' + String(ep.season).padStart(2, '0'), 'E' + String(ep.episode).padStart(2, '0'), ep.title || '', ep.tvMazeId || '']);
                }
            }
            if (s.missingSeasons) {
                for (var k = 0; k < s.missingSeasons.length; k++) {
                    var ms = s.missingSeasons[k];
                    if (gapOnly && !ms.isGap) continue;
                    var snPad = 'S' + String(ms.season).padStart(2, '0');
                    if (ms.episodes && ms.episodes.length > 0) {
                        for (var ei = 0; ei < ms.episodes.length; ei++) {
                            var mse = ms.episodes[ei];
                            rows.push([s.name, snPad, 'E' + String(mse.episode).padStart(2, '0'), mse.title || '', mse.tvMazeId || '']);
                        }
                    } else {
                        for (var e = 1; e <= (ms.episodeCount || 0); e++) {
                            rows.push([s.name, snPad, 'E' + String(e).padStart(2, '0'), '', '']);
                        }
                    }
                }
            }
        }
        return rows;
    }

    _exportEpisodes() {
        var rows = [['Show', 'Season', 'Episode', 'Title', 'TVmaze ID']].concat(this._collectEpisodeRows(false));
        this._downloadCsv('show-health-episodes.csv', rows);
    }

    _exportGaps() {
        var data = this._collectEpisodeRows(true);
        if (data.length === 0) { Dashboard.alert('No gaps found.'); return; }
        var rows = [['Show', 'Season', 'Episode', 'Title', 'TVmaze ID']].concat(data);
        this._downloadCsv('show-health-gaps.csv', rows);
    }

    _exportSeasons() {
        var rows = [['Show', 'Status', 'Seasons (local/total)', 'Missing Seasons', 'Missing Episodes']];
        var series = this._getIncompleteSeries();
        for (var i = 0; i < series.length; i++) {
            var s = series[i];
            var msList = (s.missingSeasons || []).map(function (ms) {
                var info = 'S' + String(ms.season).padStart(2, '0');
                if (ms.episodeCount) info += ' (' + ms.episodeCount + ' Ep)';
                return info;
            }).join(', ');
            var epsList = (s.missingEpisodes || []).map(function (ep) {
                return 'S' + String(ep.season).padStart(2, '0') + 'E' + String(ep.episode).padStart(2, '0');
            }).join(', ');
            rows.push([s.name, s.status || '', (s.seasonsLocal || 0) + '/' + (s.seasonsTotal || 0), msList, epsList]);
        }
        this._downloadCsv('show-health-by-series.csv', rows);
    }

    _downloadCsv(filename, rows) {
        var csv = rows.map(function (row) {
            return row.map(function (cell) {
                var str = String(cell);
                if (str.indexOf(',') !== -1 || str.indexOf('"') !== -1 || str.indexOf('\n') !== -1) {
                    return '"' + str.replace(/"/g, '""') + '"';
                }
                return str;
            }).join(',');
        }).join('\n');
        var blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        URL.revokeObjectURL(url);
    }
}

class ShowHealthPage {
    constructor(view) {
        this._view = view;
        this._api = new ShowHealthApi(ApiClient);
        this._sorter = new ShowHealthSorter();
        var self = this;
        this._table = new ShowHealthTable(ApiClient, function (imdbId, name) {
            self._ignoreSeries(imdbId, name);
        });
        this._exporter = new ShowHealthExporter();
        var prefs = this._loadPrefs();
        this._currentSort = prefs.sort || 'status';
        this._sortAsc = prefs.asc !== false;
        this._hideComplete = prefs.hideComplete || false;
        this._hideTrailing = prefs.hideTrailing || false;
        this._data = null;
        this._ignoredIds = {};
        this._handlers = [];
    }

    async init() {
        this._bindSortButtons();
        this._bindSettings();
        this._bindRunScan();
        this._updateSortButtonState();
        await this._loadIgnored();
        await this._loadData();
    }

    _bindSortButtons() {
        var self = this;
        var buttons = this._view.querySelectorAll('#showHealthSortBar button[data-sort]');
        for (var i = 0; i < buttons.length; i++) {
            (function (btn) {
                var handler = function () {
                    if (!self._data) return;
                    var mode = btn.getAttribute('data-sort');
                    if (self._currentSort === mode) { self._sortAsc = !self._sortAsc; }
                    else { self._currentSort = mode; self._sortAsc = true; }
                    self._savePrefs();
                    self._updateSortButtonState();
                    self._renderTable();
                };
                btn.addEventListener('click', handler);
                self._handlers.push({ el: btn, ev: 'click', fn: handler });
            })(buttons[i]);
        }
    }

    _bindSettings() {
        var self = this;
        var btn = this._view.querySelector('#showHealthSettings');
        if (!btn) return;
        var handler = function () { self._showSettingsDialog(); };
        btn.addEventListener('click', handler);
        this._handlers.push({ el: btn, ev: 'click', fn: handler });
    }

    _bindRunScan() {
        var self = this;
        var btn = this._view.querySelector('#showHealthRunScan');
        if (!btn) return;
        var handler = async function () {
            btn.disabled = true;
            btn.textContent = 'Analyzing...';
            try {
                await self._api.runScan();
                Dashboard.alert('Analysis started. Reload the page in a few minutes.');
            } catch (err) {
                Dashboard.alert('Error: ' + (err.message || err));
                btn.disabled = false;
                btn.textContent = 'Analyze now';
            }
        };
        btn.addEventListener('click', handler);
        this._handlers.push({ el: btn, ev: 'click', fn: handler });
    }

    _showSettingsDialog() {
        var existing = document.getElementById('showHealthSettingsOverlay');
        if (existing) existing.remove();

        var overlay = document.createElement('div');
        overlay.id = 'showHealthSettingsOverlay';
        overlay.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.7);z-index:9999;display:flex;align-items:center;justify-content:center;';

        var dialog = document.createElement('div');
        dialog.style.cssText = 'background:#1a1a1a;border-radius:8px;padding:24px;min-width:340px;max-width:440px;box-shadow:0 8px 32px rgba(0,0,0,0.5);';

        var self = this;
        dialog.innerHTML =
            '<h2 style="margin:0 0 20px 0;font-size:1.2em;">Settings</h2>' +

            '<div style="margin-bottom:16px;">' +
                '<label style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:8px 0;">' +
                    '<input type="checkbox" id="shDlgHideComplete" ' + (this._hideComplete ? 'checked' : '') + ' style="width:18px;height:18px;" />' +
                    '<span>Hide complete</span>' +
                '</label>' +
                '<label style="display:flex;align-items:center;gap:8px;cursor:pointer;padding:8px 0;">' +
                    '<input type="checkbox" id="shDlgHideTrailing" ' + (this._hideTrailing ? 'checked' : '') + ' style="width:18px;height:18px;" />' +
                    '<span>Hide trailing</span>' +
                '</label>' +
            '</div>' +

            '<div style="display:flex;flex-direction:column;gap:8px;margin-bottom:20px;">' +
                '<button id="shDlgExport" style="padding:10px 16px;background:#2a2a2a;border:1px solid #444;border-radius:6px;color:#fff;cursor:pointer;text-align:left;font-size:0.9em;">CSV Export</button>' +
                '<button id="shDlgIgnored" style="padding:10px 16px;background:#2a2a2a;border:1px solid #444;border-radius:6px;color:#fff;cursor:pointer;text-align:left;font-size:0.9em;">Manage ignored shows</button>' +
                '<button id="shDlgClearCache" style="padding:10px 16px;background:#2a2a2a;border:1px solid #e5383b;border-radius:6px;color:#e5383b;cursor:pointer;text-align:left;font-size:0.9em;">Reset TVmaze cache<br><span style="color:#888;font-size:0.85em;">Deletes all cached TVmaze responses and forces a full re-fetch. Your Jellyfin library data is never cached. Only use if TVmaze data seems wrong.</span></button>' +
            '</div>' +

            '<div style="text-align:right;">' +
                '<button id="shDlgClose" style="padding:8px 20px;background:none;border:1px solid #555;border-radius:4px;color:#aaa;cursor:pointer;">Close</button>' +
            '</div>';

        overlay.appendChild(dialog);
        document.body.appendChild(overlay);

        // Close
        var close = function () { overlay.remove(); };
        dialog.querySelector('#shDlgClose').addEventListener('click', close);
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });

        // Checkboxes
        dialog.querySelector('#shDlgHideComplete').addEventListener('change', function () {
            self._hideComplete = this.checked;
            self._savePrefs();
            self._renderTable();
        });
        dialog.querySelector('#shDlgHideTrailing').addEventListener('change', function () {
            self._hideTrailing = this.checked;
            self._savePrefs();
            self._renderTable();
        });

        // Export
        dialog.querySelector('#shDlgExport').addEventListener('click', function () {
            close();
            if (self._data) self._exporter.show(self._data.series);
        });

        // Ignored
        dialog.querySelector('#shDlgIgnored').addEventListener('click', function () {
            close();
            self._showIgnoredDialog();
        });

        // Clear cache
        dialog.querySelector('#shDlgClearCache').addEventListener('click', async function () {
            if (!confirm('Reset all cached TVmaze data and re-fetch everything?\n\nThis will take several minutes. Only use if episode/season data seems incorrect.')) return;
            close();
            try {
                await self._api.clearCache();
                Dashboard.alert('Cache reset, fresh analysis started. Reload in a few minutes.');
            } catch (err) {
                Dashboard.alert('Error: ' + (err.message || err));
            }
        });
    }

    async _loadIgnored() {
        try {
            var list = await this._api.fetchIgnored();
            this._ignoredIds = {};
            for (var i = 0; i < list.length; i++) {
                this._ignoredIds[list[i].imdbId] = list[i].name;
            }
        } catch (e) {
            this._ignoredIds = {};
        }
    }

    async _ignoreSeries(imdbId, name) {
        if (!confirm('Ignore ' + name + '?')) return;
        try {
            await this._api.addIgnored(imdbId, name);
            this._ignoredIds[imdbId] = name;
            this._renderTable();
        } catch (e) {
            Dashboard.alert('Error: ' + (e.message || e));
        }
    }

    async _unignoreSeries(imdbId) {
        try {
            await this._api.removeIgnored(imdbId);
            delete this._ignoredIds[imdbId];
            this._renderTable();
        } catch (e) {
            Dashboard.alert('Error: ' + (e.message || e));
        }
    }

    _showIgnoredDialog() {
        var existing = document.getElementById('showHealthIgnoredOverlay');
        if (existing) existing.remove();

        var keys = Object.keys(this._ignoredIds);
        if (keys.length === 0) {
            Dashboard.alert('No ignored shows.');
            return;
        }

        var overlay = document.createElement('div');
        overlay.id = 'showHealthIgnoredOverlay';
        overlay.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.7);z-index:9999;display:flex;align-items:center;justify-content:center;';

        var dialog = document.createElement('div');
        dialog.style.cssText = 'background:#1a1a1a;border-radius:8px;padding:24px;min-width:320px;max-width:480px;max-height:70vh;overflow-y:auto;box-shadow:0 8px 32px rgba(0,0,0,0.5);';

        var html = '<h2 style="margin:0 0 16px 0;font-size:1.2em;">Ignored Shows</h2>';
        html += '<div style="display:flex;flex-direction:column;gap:8px;margin-bottom:20px;">';
        for (var i = 0; i < keys.length; i++) {
            var id = keys[i];
            var name = this._ignoredIds[id];
            html += '<div style="display:flex;justify-content:space-between;align-items:center;padding:8px 12px;background:#2a2a2a;border-radius:4px;">' +
                '<span>' + escapeHtml(name) + '</span>' +
                '<button class="showhealth-unignore" data-imdb="' + id + '" style="background:#1a3a1a;color:#4caf50;border:none;border-radius:4px;padding:4px 12px;cursor:pointer;font-size:0.85em;">Restore</button>' +
                '</div>';
        }
        html += '</div>';
        html += '<div style="text-align:right;"><button id="showHealthIgnoredClose" style="padding:8px 20px;background:none;border:1px solid #555;border-radius:4px;color:#aaa;cursor:pointer;">Close</button></div>';

        dialog.innerHTML = html;
        overlay.appendChild(dialog);
        document.body.appendChild(overlay);

        var self = this;
        overlay.querySelector('#showHealthIgnoredClose').addEventListener('click', function () { overlay.remove(); });
        overlay.addEventListener('click', function (e) { if (e.target === overlay) overlay.remove(); });

        var unignoreBtns = dialog.querySelectorAll('.showhealth-unignore');
        for (var ui = 0; ui < unignoreBtns.length; ui++) {
            unignoreBtns[ui].addEventListener('click', function () {
                var id = this.getAttribute('data-imdb');
                self._unignoreSeries(id);
                this.closest('div[style]').remove();
                // Close dialog if empty
                if (dialog.querySelectorAll('.showhealth-unignore').length === 0) {
                    overlay.remove();
                }
            });
        }
    }

    _loadPrefs() {
        try {
            var json = localStorage.getItem('showHealthPrefs');
            return json ? JSON.parse(json) : {};
        } catch (e) {
            return {};
        }
    }

    _savePrefs() {
        try {
            localStorage.setItem('showHealthPrefs', JSON.stringify({
                sort: this._currentSort,
                asc: this._sortAsc,
                hideComplete: this._hideComplete,
                hideTrailing: this._hideTrailing
            }));
        } catch (e) { /* ignore */ }
    }

    destroy() {
        for (var i = 0; i < this._handlers.length; i++) {
            var h = this._handlers[i];
            h.el.removeEventListener(h.ev, h.fn);
        }
        this._handlers = [];
    }

    _updateSortButtonState() {
        var labels = { status: 'Status', gaps: 'Gaps', trailing: 'Trailing', release: 'Release', name: 'A-Z' };
        var buttons = this._view.querySelectorAll('#showHealthSortBar button[data-sort]');
        for (var i = 0; i < buttons.length; i++) {
            var btn = buttons[i];
            var mode = btn.getAttribute('data-sort');
            if (mode === this._currentSort) {
                btn.style.background = '#00a4dc';
                btn.style.color = '#fff';
                btn.textContent = labels[mode] + ' ' + (this._sortAsc ? '\u25B2' : '\u25BC');
            } else {
                btn.style.background = '';
                btn.style.color = '';
                btn.textContent = labels[mode];
            }
        }
    }

    async _loadData() {
        var errorEl = this._view.querySelector('#showHealthError');
        var firstRunEl = this._view.querySelector('#showHealthFirstRun');
        var sortBar = this._view.querySelector('#showHealthSortBar');
        errorEl.style.display = 'none';
        firstRunEl.style.display = 'none';

        try {
            var data = await this._api.fetchSnapshot();
            this._data = data;
            sortBar.style.display = '';
            this._updateSummary();
            this._renderTable();
        } catch (err) {
            if (err && (err.status === 404 || (err.message && err.message.indexOf('404') !== -1))) {
                firstRunEl.style.display = '';
                sortBar.style.display = 'none';
            } else {
                errorEl.textContent = 'Failed to load: ' + (err.message || err);
                errorEl.style.display = 'block';
            }
        }
    }

    _updateSummary() {
        var summaryEl = this._view.querySelector('#showHealthSummary');
        if (!this._data || !this._data.summary) return;
        var s = this._data.summary;
        summaryEl.textContent = s.total + ' shows \u00B7 ' + s.incomplete + ' incomplete';
    }

    _renderTable() {
        if (!this._data) return;
        var self = this;
        var series = this._data.series.filter(function (s) {
            return !self._ignoredIds[s.imdbId];
        });
        if (this._hideComplete) {
            series = series.filter(function (s) { return isIncomplete(s); });
        }
        if (this._hideTrailing) {
            series = series.filter(function (s) { return countGaps(s) > 0 || !isIncomplete(s); });
        }
        var container = this._view.querySelector('#showHealthTableContainer');
        var sorted = this._sorter.sort(series, this._currentSort, this._sortAsc);
        this._table.render(sorted, container);
    }
}

export default function (view) {
    var currentPage = null;
    view.addEventListener('viewshow', function () {
        if (currentPage) currentPage.destroy();
        currentPage = new ShowHealthPage(view);
        currentPage.init();
    });
    view.addEventListener('viewhide', function () {
        if (currentPage) { currentPage.destroy(); currentPage = null; }
    });
}
