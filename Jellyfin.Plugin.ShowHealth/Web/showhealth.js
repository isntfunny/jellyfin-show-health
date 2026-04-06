class ShowHealthApi {
    constructor(apiClient) {
        this._apiClient = apiClient;
    }

    async fetchStatus() {
        var url = this._apiClient.getUrl('/ShowHealth/Status');
        var response = await this._apiClient.getJSON(url);
        return response;
    }

    async fetchSeries() {
        var url = this._apiClient.getUrl('/ShowHealth/Series');
        return await this._apiClient.getJSON(url);
    }

    async analyzeSeries(imdbId) {
        var url = this._apiClient.getUrl('/ShowHealth/Analyze/' + imdbId);
        return await this._apiClient.getJSON(url);
    }
}

class ShowHealthSorter {
    sort(series, mode, ascending) {
        var sorted = series.slice();

        switch (mode) {
            case 'status':
                sorted = this._sortByStatus(sorted);
                break;
            case 'missing':
                sorted = this._sortByMissing(sorted);
                break;
            case 'release':
                sorted = this._sortByRelease(sorted);
                break;
            case 'name':
                sorted = this._sortByName(sorted);
                break;
            default:
                break;
        }

        if (!ascending) {
            sorted.reverse();
        }

        return sorted;
    }

    _isAnalyzed(s) {
        return s._analyzed === true;
    }

    _totalMissing(s) {
        var eps = s.missingEpisodes ? s.missingEpisodes.length : 0;
        var seasonEps = 0;
        if (s.missingSeasons) {
            for (var i = 0; i < s.missingSeasons.length; i++) {
                seasonEps += s.missingSeasons[i].episodeCount || 0;
            }
        }
        return eps + seasonEps;
    }

    _sortByStatus(series) {
        var self = this;
        return series.sort(function (a, b) {
            var aAnalyzed = self._isAnalyzed(a);
            var bAnalyzed = self._isAnalyzed(b);
            if (aAnalyzed !== bAnalyzed) return aAnalyzed ? -1 : 1;
            if (!aAnalyzed) return a.name.localeCompare(b.name);
            var aMissing = self._totalMissing(a);
            var bMissing = self._totalMissing(b);
            if ((aMissing > 0) !== (bMissing > 0)) return aMissing > 0 ? -1 : 1;
            return a.name.localeCompare(b.name);
        });
    }

    _sortByMissing(series) {
        var self = this;
        return series.sort(function (a, b) {
            var aAnalyzed = self._isAnalyzed(a);
            var bAnalyzed = self._isAnalyzed(b);
            if (aAnalyzed !== bAnalyzed) return aAnalyzed ? -1 : 1;
            if (!aAnalyzed) return a.name.localeCompare(b.name);
            var diff = self._totalMissing(b) - self._totalMissing(a);
            if (diff !== 0) return diff;
            return a.name.localeCompare(b.name);
        });
    }

    _sortByRelease(series) {
        var self = this;
        return series.sort(function (a, b) {
            var aAnalyzed = self._isAnalyzed(a);
            var bAnalyzed = self._isAnalyzed(b);
            if (aAnalyzed !== bAnalyzed) return aAnalyzed ? -1 : 1;
            if (!aAnalyzed) return a.name.localeCompare(b.name);
            var aRank = self._releaseDateRank(a);
            var bRank = self._releaseDateRank(b);
            if (aRank.tier !== bRank.tier) return aRank.tier - bRank.tier;
            if (aRank.date && bRank.date) return aRank.date.localeCompare(bRank.date);
            return a.name.localeCompare(b.name);
        });
    }

    // Tier 0: concrete date (YYYY-MM-DD or YYYY-MM), Tier 1: year-only, Tier 2: TBA (running, no date), Tier 3: ended
    _releaseDateRank(s) {
        if (s.nextEpisode && s.nextEpisode.releaseDate) {
            var rd = s.nextEpisode.releaseDate;
            if (rd.length > 4) {
                return { tier: 0, date: rd };
            }
            return { tier: 1, date: rd };
        }
        if (s.status === 'ended') {
            return { tier: 3, date: null };
        }
        return { tier: 2, date: null };
    }

    _sortByName(series) {
        return series.sort(function (a, b) {
            return a.name.localeCompare(b.name);
        });
    }
}

class ShowHealthTable {
    constructor(apiClient) {
        this._apiClient = apiClient;
        this._expandedRows = {};
    }

    renderInitial(seriesList, container) {
        var html = '<table style="width:100%;border-collapse:collapse;font-size:0.9em;">';
        html += this._renderHeader();

        for (var i = 0; i < seriesList.length; i++) {
            html += this._renderInitialRow(seriesList[i], i);
        }

        html += '</tbody></table>';
        container.innerHTML = html;
    }

    updateRow(index, healthResult, container) {
        var row = container.querySelector('tr[data-index="' + index + '"]');
        if (!row) {
            return;
        }

        var incomplete = this._isIncomplete(healthResult);
        var opacity = incomplete ? '1' : '0.5';

        row.style.opacity = opacity;
        row.style.cursor = incomplete ? 'pointer' : '';

        // Arrow cell
        var arrowCell = row.cells[0];
        if (incomplete) {
            arrowCell.innerHTML = '<span class="showhealth-arrow" style="cursor:pointer;font-size:1.1em;transition:transform 0.2s;display:inline-block;">\u25B6</span>';
        } else {
            arrowCell.innerHTML = '';
        }

        // Seasons
        var seasonsCell = row.cells[3];
        var missingSeasons = healthResult.missingSeasons ? healthResult.missingSeasons.length : 0;
        var seasonsColor = missingSeasons > 0 ? 'color:#e5383b;' : '';
        seasonsCell.innerHTML = '<span style="' + seasonsColor + '">' + healthResult.seasonsLocal + '/' + healthResult.seasonsTotal + '</span>';

        // Missing
        var missingCell = row.cells[4];
        if (!incomplete) {
            missingCell.innerHTML = '<span style="color:#4caf50;">Complete</span>';
        } else {
            missingCell.innerHTML = this._renderMissingText(healthResult);
        }

        // Next episode (merged with status)
        var nextCell = row.cells[5];
        if (healthResult.nextEpisode && healthResult.nextEpisode.releaseDate) {
            nextCell.innerHTML = '<span style="background:#2a2a1a;color:#ffa726;padding:2px 8px;border-radius:3px;font-size:0.85em;">' +
                       this._escapeHtml(healthResult.nextEpisode.releaseDate) + '</span>';
        } else if (healthResult.status === 'ended') {
            nextCell.innerHTML = '<span style="background:#1a3a1a;color:#4caf50;padding:2px 8px;border-radius:3px;font-size:0.85em;">Ended</span>';
        } else {
            nextCell.innerHTML = '<span style="background:#2a2a2a;color:#888;padding:2px 8px;border-radius:3px;font-size:0.85em;">TBA</span>';
        }

        // Insert detail row if incomplete
        if (incomplete) {
            var detailHtml = this._renderDetailRow(healthResult, index);
            if (detailHtml) {
                row.insertAdjacentHTML('afterend', detailHtml);
            }

            // Bind click for expand/collapse
            var self = this;
            row.addEventListener('click', function () {
                self._expandedRows[index] = !self._expandedRows[index];
                var detailRow = container.querySelector('tr[data-detail-index="' + index + '"]');
                var arrow = row.querySelector('.showhealth-arrow');

                if (detailRow) {
                    detailRow.style.display = self._expandedRows[index] ? '' : 'none';
                }
                if (arrow) {
                    arrow.style.transform = self._expandedRows[index] ? 'rotate(90deg)' : '';
                }
            });

            // Bind chip clicks in detail row
            var detailRow = container.querySelector('tr[data-detail-index="' + index + '"]');
            if (detailRow) {
                var chips = detailRow.querySelectorAll('.showhealth-chip');
                for (var ci = 0; ci < chips.length; ci++) {
                    chips[ci].addEventListener('click', function (e) {
                        e.stopPropagation();
                        var text = this.getAttribute('data-copy');
                        if (text) {
                            navigator.clipboard.writeText(text).then(function () {
                                Dashboard.alert('Copied: ' + text);
                            });
                        }
                    });
                }
            }
        }
    }

    render(series, container) {
        var html = '<table style="width:100%;border-collapse:collapse;font-size:0.9em;">';
        html += this._renderHeader();

        for (var i = 0; i < series.length; i++) {
            var s = series[i];
            if (s._analyzed) {
                html += this._renderSeriesRow(s, i);
                html += this._renderDetailRow(s, i);
            } else {
                html += this._renderInitialRow(s, i);
            }
        }

        html += '</tbody></table>';
        container.innerHTML = html;
        this._bindEvents(container, series);
    }

    _renderHeader() {
        return '<thead><tr style="border-bottom:2px solid #333;text-align:left;">' +
            '<th style="width:30px;padding:8px 4px;"></th>' +
            '<th style="width:54px;padding:8px 4px;"></th>' +
            '<th style="padding:8px;">Series</th>' +
            '<th style="padding:8px;">Seasons</th>' +
            '<th style="padding:8px;">Missing</th>' +
            '<th style="padding:8px;">Next Episode</th>' +
            '</tr></thead><tbody>';
    }

    _renderInitialRow(s, index) {
        var posterUrl = this._apiClient.getUrl('/Items/' + s.jellyfinId + '/Images/Primary', { height: 54 });
        var poster = '<img src="' + posterUrl + '" style="height:54px;border-radius:3px;" onerror="this.style.display=\'none\'" />';

        var yearRange = s.startYear ? (s.startYear + '\u2013') : '';
        var nameCell = '<div>' + this._escapeHtml(s.name) + '</div>' +
                       '<div style="color:#888;font-size:0.85em;">' + yearRange + '</div>';

        var pendingText = '<span style="color:#888;font-style:italic;">Indizieren...</span>';

        return '<tr data-index="' + index + '" style="border-bottom:1px solid #222;opacity:0.5;">' +
            '<td style="padding:8px 4px;text-align:center;"></td>' +
            '<td style="padding:8px 4px;">' + poster + '</td>' +
            '<td style="padding:8px;">' + nameCell + '</td>' +
            '<td style="padding:8px;">' + pendingText + '</td>' +
            '<td style="padding:8px;">' + pendingText + '</td>' +
            '<td style="padding:8px;">' + pendingText + '</td>' +
            '</tr>';
    }

    _isIncomplete(s) {
        return (s.missingEpisodes && s.missingEpisodes.length > 0) ||
               (s.missingSeasons && s.missingSeasons.length > 0);
    }

    _renderSeriesRow(s, index) {
        var incomplete = this._isIncomplete(s);
        var opacity = incomplete ? '1' : '0.5';
        var expanded = this._expandedRows[index];
        var arrow = incomplete
            ? '<span class="showhealth-arrow" style="cursor:pointer;font-size:1.1em;transition:transform 0.2s;display:inline-block;' +
              (expanded ? 'transform:rotate(90deg);' : '') + '">\u25B6</span>'
            : '';

        var posterUrl = this._apiClient.getUrl('/Items/' + s.jellyfinId + '/Images/Primary', { height: 54 });
        var poster = '<img src="' + posterUrl + '" style="height:54px;border-radius:3px;" onerror="this.style.display=\'none\'" />';

        var yearRange = s.startYear ? (s.startYear + (s.endYear ? '\u2013' + s.endYear : '\u2013')) : '';
        var nameCell = '<div>' + this._escapeHtml(s.name) + '</div>' +
                       '<div style="color:#888;font-size:0.85em;">' + yearRange + '</div>';

        var missingSeasons = s.missingSeasons ? s.missingSeasons.length : 0;
        var seasonsColor = missingSeasons > 0 ? 'color:#e5383b;' : '';
        var seasonsCell = '<span style="' + seasonsColor + '">' + s.seasonsLocal + '/' + s.seasonsTotal + '</span>';

        var missingCell;
        if (!incomplete) {
            missingCell = '<span style="color:#4caf50;">Complete</span>';
        } else {
            missingCell = this._renderMissingText(s);
        }

        var nextCell = '';
        if (s.nextEpisode && s.nextEpisode.releaseDate) {
            nextCell = '<span style="background:#2a2a1a;color:#ffa726;padding:2px 8px;border-radius:3px;font-size:0.85em;">' +
                       this._escapeHtml(s.nextEpisode.releaseDate) + '</span>';
        } else if (s.status === 'ended') {
            nextCell = '<span style="background:#1a3a1a;color:#4caf50;padding:2px 8px;border-radius:3px;font-size:0.85em;">Ended</span>';
        } else {
            nextCell = '<span style="background:#2a2a2a;color:#888;padding:2px 8px;border-radius:3px;font-size:0.85em;">TBA</span>';
        }

        return '<tr data-index="' + index + '" style="border-bottom:1px solid #222;opacity:' + opacity + ';' + (incomplete ? 'cursor:pointer;' : '') + '">' +
            '<td style="padding:8px 4px;text-align:center;">' + arrow + '</td>' +
            '<td style="padding:8px 4px;">' + poster + '</td>' +
            '<td style="padding:8px;">' + nameCell + '</td>' +
            '<td style="padding:8px;">' + seasonsCell + '</td>' +
            '<td style="padding:8px;">' + missingCell + '</td>' +
            '<td style="padding:8px;">' + nextCell + '</td>' +
            '</tr>';
    }

    _renderDetailRow(s, index) {
        var incomplete = this._isIncomplete(s);
        if (!incomplete) {
            return '';
        }

        var display = this._expandedRows[index] ? '' : 'display:none;';

        var grouped = {};
        if (s.missingEpisodes) {
            for (var i = 0; i < s.missingEpisodes.length; i++) {
                var ep = s.missingEpisodes[i];
                var season = ep.season;
                if (!grouped[season]) {
                    grouped[season] = [];
                }
                grouped[season].push(ep);
            }
        }

        var detailHtml = '<td colspan="6" style="padding:8px 8px 16px 60px;">';
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
                var copyText = this._escapeHtml(s.name) + ' ' + snPad + epNum;
                var title = e.title ? ' \u2014 ' + this._escapeHtml(e.title) : '';
                detailHtml += '<span class="showhealth-chip" data-copy="' + copyText + '" style="border-left:3px solid #e5383b;padding:4px 10px;background:#2a2a2a;border-radius:0 3px 3px 0;font-size:0.85em;cursor:pointer;" title="Click to copy">' +
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
                var copyText2 = this._escapeHtml(s.name) + ' ' + snPad2 + ' complete';
                var epInfo = ms.episodeCount ? ' (' + ms.episodeCount + ' ep)' : '';
                detailHtml += '<span class="showhealth-chip" data-copy="' + copyText2 + '" style="border-left:3px solid #e5383b;padding:4px 10px;background:#2a2a2a;border-radius:0 3px 3px 0;font-size:0.85em;cursor:pointer;" title="Click to copy">Season ' +
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
                if (!s._analyzed || !self._isIncomplete(s)) {
                    return;
                }

                row.addEventListener('click', function () {
                    self._expandedRows[idx] = !self._expandedRows[idx];
                    var detailRow = container.querySelector('tr[data-detail-index="' + idx + '"]');
                    var arrow = row.querySelector('.showhealth-arrow');

                    if (detailRow) {
                        detailRow.style.display = self._expandedRows[idx] ? '' : 'none';
                    }
                    if (arrow) {
                        arrow.style.transform = self._expandedRows[idx] ? 'rotate(90deg)' : '';
                    }
                });
            })(rows[i]);
        }

        // Chip click → copy to clipboard
        var chips = container.querySelectorAll('.showhealth-chip');
        for (var ci = 0; ci < chips.length; ci++) {
            chips[ci].addEventListener('click', function (e) {
                e.stopPropagation();
                var text = this.getAttribute('data-copy');
                if (text) {
                    navigator.clipboard.writeText(text).then(function () {
                        Dashboard.alert('Copied: ' + text);
                    });
                }
            });
        }
    }

    _totalMissing(s) {
        var eps = s.missingEpisodes ? s.missingEpisodes.length : 0;
        var seasonEps = 0;
        if (s.missingSeasons) {
            for (var i = 0; i < s.missingSeasons.length; i++) {
                seasonEps += s.missingSeasons[i].episodeCount || 0;
            }
        }
        return eps + seasonEps;
    }

    _renderMissingText(s) {
        var total = this._totalMissing(s);
        if (total === 0 && !this._isIncomplete(s)) {
            return '<span style="color:#4caf50;">Complete</span>';
        }
        return '<span style="color:#e5383b;">' + total + ' episode' + (total !== 1 ? 's' : '') + '</span>';
    }

    _escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
}

class ShowHealthPage {
    constructor(view) {
        this._view = view;
        this._api = new ShowHealthApi(ApiClient);
        this._sorter = new ShowHealthSorter();
        this._table = new ShowHealthTable(ApiClient);
        this._currentSort = 'status';
        this._sortAsc = true;
        this._data = null;
        this._seriesList = [];
        this._analysisResults = {};
        this._indexingComplete = false;
    }

    async init() {
        this._bindSortButtons();
        this._updateSortButtonState();
        this._setSortButtonsEnabled(false);
        await this._loadData();
    }

    _bindSortButtons() {
        var self = this;
        var buttons = this._view.querySelectorAll('#showHealthSortBar button[data-sort]');

        for (var i = 0; i < buttons.length; i++) {
            (function (btn) {
                btn.addEventListener('click', function () {
                    if (!self._indexingComplete) {
                        return;
                    }
                    var mode = btn.getAttribute('data-sort');
                    if (self._currentSort === mode) {
                        self._sortAsc = !self._sortAsc;
                    } else {
                        self._currentSort = mode;
                        self._sortAsc = true;
                    }
                    self._updateSortButtonState();
                    self._renderTable();
                });
            })(buttons[i]);
        }
    }

    _setSortButtonsEnabled(enabled) {
        var buttons = this._view.querySelectorAll('#showHealthSortBar button[data-sort]');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].style.opacity = enabled ? '1' : '0.5';
            buttons[i].style.pointerEvents = enabled ? '' : 'none';
        }
    }

    _updateSortButtonState() {
        var labels = { status: 'By Status', missing: 'By Missing', release: 'By Release', name: 'A-Z' };
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
        errorEl.style.display = 'none';

        var summaryEl = this._view.querySelector('#showHealthSummary');
        var container = this._view.querySelector('#showHealthTableContainer');

        Dashboard.showLoadingMsg();

        try {
            // Step 1: Fetch series list instantly
            var response = await this._api.fetchSeries();
            this._seriesList = response.series;
            var total = this._seriesList.length;

            Dashboard.hideLoadingMsg();

            // Step 2: Render table immediately with basic data
            this._table.renderInitial(this._seriesList, container);

            // Step 3: Show initial progress
            summaryEl.textContent = 'Indizieren... 0/' + total;

            // Step 4: Analyze each series one by one
            for (var i = 0; i < this._seriesList.length; i++) {
                var series = this._seriesList[i];
                try {
                    var result = await this._api.analyzeSeries(series.imdbId);
                    this._analysisResults[series.imdbId] = result;

                    // Merge analysis result into series list item
                    Object.assign(this._seriesList[i], result);
                    this._seriesList[i]._analyzed = true;

                    // Update row in-place
                    this._table.updateRow(i, result, container);
                } catch (err) {
                    // Mark as analyzed but failed - show as-is
                    this._seriesList[i]._analyzed = true;
                    this._seriesList[i].status = 'unknown';
                    this._seriesList[i].seasonsLocal = 0;
                    this._seriesList[i].seasonsTotal = 0;
                    this._seriesList[i].missingEpisodes = [];
                    this._seriesList[i].missingSeasons = [];
                    this._table.updateRow(i, this._seriesList[i], container);
                }

                // Update progress
                summaryEl.textContent = 'Indizieren... ' + (i + 1) + '/' + total;
            }

            // Step 5: Show final summary
            this._indexingComplete = true;
            this._setSortButtonsEnabled(true);
            this._buildDataFromResults();
            this._updateSummary();
            this._renderTable();
        } catch (err) {
            Dashboard.hideLoadingMsg();
            errorEl.textContent = 'Failed to load show health data: ' + (err.message || err);
            errorEl.style.display = 'block';
        }
    }

    _buildDataFromResults() {
        var series = this._seriesList;
        var incomplete = 0;
        var running = 0;
        var ended = 0;

        for (var i = 0; i < series.length; i++) {
            var s = series[i];
            if (s._analyzed) {
                if ((s.missingEpisodes && s.missingEpisodes.length > 0) ||
                    (s.missingSeasons && s.missingSeasons.length > 0)) {
                    incomplete++;
                }
                if (s.status === 'running') running++;
                if (s.status === 'ended') ended++;
            }
        }

        this._data = {
            series: series,
            summary: {
                total: series.length,
                incomplete: incomplete,
                running: running,
                ended: ended,
            },
        };
    }

    _updateSummary() {
        var summaryEl = this._view.querySelector('#showHealthSummary');
        if (this._data && this._data.summary) {
            var s = this._data.summary;
            summaryEl.textContent = s.total + ' series \u00B7 ' + s.incomplete + ' incomplete';
        }
    }

    _renderTable() {
        if (!this._data) {
            return;
        }
        var container = this._view.querySelector('#showHealthTableContainer');
        var sorted = this._sorter.sort(this._data.series, this._currentSort, this._sortAsc);
        this._table.render(sorted, container);
    }
}

export default function (view) {
    view.addEventListener('viewshow', function () {
        var page = new ShowHealthPage(view);
        page.init();
    });
}
