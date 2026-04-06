class ShowHealthApi {
    constructor(apiClient) {
        this._apiClient = apiClient;
    }

    async fetchStatus() {
        var url = this._apiClient.getUrl('/ShowHealth/Status');
        var response = await this._apiClient.getJSON(url);
        return response;
    }
}

class ShowHealthSorter {
    sort(series, mode) {
        var sorted = series.slice();

        switch (mode) {
            case 'status':
                return this._sortByStatus(sorted);
            case 'urgency':
                return this._sortByUrgency(sorted);
            case 'name':
                return this._sortByName(sorted);
            default:
                return sorted;
        }
    }

    _isIncomplete(s) {
        return (s.missingEpisodes && s.missingEpisodes.length > 0) ||
               (s.missingSeasons && s.missingSeasons.length > 0);
    }

    _sortByStatus(series) {
        var self = this;
        return series.sort(function (a, b) {
            var aInc = self._isIncomplete(a);
            var bInc = self._isIncomplete(b);
            if (aInc !== bInc) {
                return aInc ? -1 : 1;
            }
            return a.name.localeCompare(b.name);
        });
    }

    _sortByUrgency(series) {
        var self = this;
        var withNext = [];
        var incomplete = [];
        var complete = [];

        for (var i = 0; i < series.length; i++) {
            var s = series[i];
            if (s.nextEpisode && s.nextEpisode.releaseDate) {
                withNext.push(s);
            } else if (self._isIncomplete(s)) {
                incomplete.push(s);
            } else {
                complete.push(s);
            }
        }

        withNext.sort(function (a, b) {
            return new Date(a.nextEpisode.releaseDate) - new Date(b.nextEpisode.releaseDate);
        });
        incomplete.sort(function (a, b) { return a.name.localeCompare(b.name); });
        complete.sort(function (a, b) { return a.name.localeCompare(b.name); });

        return withNext.concat(incomplete, complete);
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

    render(series, container) {
        var html = '<table style="width:100%;border-collapse:collapse;font-size:0.9em;">';
        html += this._renderHeader();

        for (var i = 0; i < series.length; i++) {
            html += this._renderSeriesRow(series[i], i);
            html += this._renderDetailRow(series[i], i);
        }

        html += '</table>';
        container.innerHTML = html;
        this._bindEvents(container, series);
    }

    _renderHeader() {
        return '<thead><tr style="border-bottom:2px solid #333;text-align:left;">' +
            '<th style="width:30px;padding:8px 4px;"></th>' +
            '<th style="width:54px;padding:8px 4px;"></th>' +
            '<th style="padding:8px;">Series</th>' +
            '<th style="padding:8px;">Status</th>' +
            '<th style="padding:8px;">Seasons</th>' +
            '<th style="padding:8px;">Missing</th>' +
            '<th style="padding:8px;">Next Episode</th>' +
            '</tr></thead><tbody>';
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

        var statusBadge = s.status === 'ended'
            ? '<span style="background:#1a3a1a;color:#4caf50;padding:2px 8px;border-radius:3px;font-size:0.85em;">Ended</span>'
            : '<span style="background:#1a2a3a;color:#42a5f5;padding:2px 8px;border-radius:3px;font-size:0.85em;">Running</span>';

        var missingSeasons = s.missingSeasons ? s.missingSeasons.length : 0;
        var seasonsColor = missingSeasons > 0 ? 'color:#e5383b;' : '';
        var seasonsCell = '<span style="' + seasonsColor + '">' + s.seasonsLocal + '/' + s.seasonsTotal + '</span>';

        var missingCell;
        if (!incomplete) {
            missingCell = '<span style="color:#4caf50;">Complete</span>';
        } else {
            var count = (s.missingEpisodes ? s.missingEpisodes.length : 0);
            missingCell = '<span style="color:#e5383b;">' + count + ' episode' + (count !== 1 ? 's' : '') + '</span>';
        }

        var nextCell = '';
        if (s.nextEpisode && s.nextEpisode.releaseDate) {
            nextCell = '<span style="background:#2a2a1a;color:#ffa726;padding:2px 8px;border-radius:3px;font-size:0.85em;">' +
                       this._escapeHtml(s.nextEpisode.releaseDate) + '</span>';
        }

        var cursor = incomplete ? 'cursor:pointer;' : '';

        return '<tr data-index="' + index + '" style="border-bottom:1px solid #222;opacity:' + opacity + ';' + cursor + '">' +
            '<td style="padding:8px 4px;text-align:center;">' + arrow + '</td>' +
            '<td style="padding:8px 4px;">' + poster + '</td>' +
            '<td style="padding:8px;">' + nameCell + '</td>' +
            '<td style="padding:8px;">' + statusBadge + '</td>' +
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
                var title = e.title ? ' \u2014 ' + this._escapeHtml(e.title) : '';
                detailHtml += '<span style="border-left:3px solid #e5383b;padding:4px 10px;background:#2a2a2a;border-radius:0 3px 3px 0;font-size:0.85em;">' +
                              epNum + title + '</span>';
            }

            detailHtml += '</div>';
        }

        if (s.missingSeasons && s.missingSeasons.length > 0) {
            detailHtml += '<div style="margin-bottom:8px;"><strong style="color:#aaa;">Missing Seasons</strong></div>';
            detailHtml += '<div style="display:flex;flex-wrap:wrap;gap:6px;margin-bottom:12px;">';
            for (var mi = 0; mi < s.missingSeasons.length; mi++) {
                detailHtml += '<span style="border-left:3px solid #e5383b;padding:4px 10px;background:#2a2a2a;border-radius:0 3px 3px 0;font-size:0.85em;">Season ' +
                              s.missingSeasons[mi] + '</span>';
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
                if (!self._isIncomplete(s)) {
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
        this._data = null;
    }

    async init() {
        this._bindSortButtons();
        this._updateSortButtonState();
        await this._loadData();
    }

    _bindSortButtons() {
        var self = this;
        var buttons = this._view.querySelectorAll('#showHealthSortBar button[data-sort]');

        for (var i = 0; i < buttons.length; i++) {
            (function (btn) {
                btn.addEventListener('click', function () {
                    self._currentSort = btn.getAttribute('data-sort');
                    self._updateSortButtonState();
                    if (self._data) {
                        self._renderTable();
                    }
                });
            })(buttons[i]);
        }
    }

    _updateSortButtonState() {
        var buttons = this._view.querySelectorAll('#showHealthSortBar button[data-sort]');
        for (var i = 0; i < buttons.length; i++) {
            var btn = buttons[i];
            if (btn.getAttribute('data-sort') === this._currentSort) {
                btn.style.background = '#00a4dc';
                btn.style.color = '#fff';
            } else {
                btn.style.background = '';
                btn.style.color = '';
            }
        }
    }

    async _loadData() {
        var errorEl = this._view.querySelector('#showHealthError');
        errorEl.style.display = 'none';

        Dashboard.showLoadingMsg();

        try {
            this._data = await this._api.fetchStatus();
            this._updateSummary();
            this._renderTable();
        } catch (err) {
            errorEl.textContent = 'Failed to load show health data: ' + (err.message || err);
            errorEl.style.display = 'block';
        } finally {
            Dashboard.hideLoadingMsg();
        }
    }

    _updateSummary() {
        var summaryEl = this._view.querySelector('#showHealthSummary');
        if (this._data && this._data.summary) {
            var s = this._data.summary;
            summaryEl.textContent = s.total + ' series \u00B7 ' + s.incomplete + ' incomplete';
        }
    }

    _renderTable() {
        var container = this._view.querySelector('#showHealthTableContainer');
        var sorted = this._sorter.sort(this._data.series, this._currentSort);
        this._table.render(sorted, container);
    }
}

export default function (view) {
    view.addEventListener('viewshow', function () {
        var page = new ShowHealthPage(view);
        page.init();
    });
}
