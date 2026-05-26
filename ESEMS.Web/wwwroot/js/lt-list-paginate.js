// lt-list-paginate.js
// Unified client-side pager for the shared .lt-* list tables that AREN'T
// DataTables and don't already ship their own pager. Renders the same
// .lt-pager control used by Processes/Index (#ltPager) and the DataTables
// footer: a "showing a–b of n" label, numbered «‹ 1 … ›» buttons, and a
// rows-per-page select — so every table in the app paginates identically.
//
// Composition with each page's own search/filter: pagination hides
// "paged-out" rows with the .ltp-out CLASS (display:none !important), kept
// separate from the inline style.display the filters toggle. A row is visible
// iff it passes the filter (inline display !== 'none') AND it is on the
// current page (no .ltp-out class) — so the two compose via CSS instead of
// fighting.
//
// SKIPS: DataTables tables (.esems-datatable / .datatable — the global
// DataTables init paginates those in the same style), cards that already have
// a pager (#ltPager / .lt-pager / a DataTables pager), grouped/tree tables
// (rowspan or nested <table>), single-cell empty-state rows, tables with no
// data rows, and any table opting out with data-no-paginate="true".
(function () {
    'use strict';
    var DEFAULT_SIZE = 25;
    var SIZES = [10, 25, 50, 100, 0]; // 0 = "All"
    var isRtl = document.documentElement.getAttribute('dir') === 'rtl';

    var L = {
        rows: isRtl ? 'صف لكل صفحة' : 'Rows per page',
        all: isRtl ? 'الكل' : 'All',
        none: isRtl ? 'لا توجد نتائج' : 'No matches',
        info: isRtl ? 'عرض {a}–{b} من {n}' : 'Showing {a}–{b} of {n}'
    };

    function init() {
        document.querySelectorAll('.lt-table-wrap table').forEach(setupTable);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function setupTable(table) {
        if (table.getAttribute('data-no-paginate') === 'true') return;
        // DataTables owns these — the global init paginates them in the same
        // .lt-pager-styled footer. Check the CLASS (present from page load),
        // not the .dataTables_wrapper (created later, after this script runs).
        if (table.classList.contains('esems-datatable') || table.classList.contains('datatable')) return;

        var card = table.closest('.lt-card') || table.parentElement;
        if (!card) return;
        // Don't double-paginate: skip if this card already has its own pager.
        if (card.querySelector('.lt-pager, [id^="ltPager"], .dataTables_paginate, .dataTables_wrapper')) return;

        var tbody = table.tBodies && table.tBodies[0];
        if (!tbody) return;
        // Flat lists only — skip grouped / tree / nested tables.
        if (tbody.querySelector('td[rowspan], th[rowspan], table')) return;

        var dataRows = Array.prototype.slice.call(tbody.rows).filter(function (r) {
            return r.cells.length > 1 && r.getAttribute('data-empty') !== 'true';
        });
        if (dataRows.length === 0) return;

        var state = { page: 1, size: DEFAULT_SIZE, matches: dataRows.slice() };

        // Build: [info] [controls] [rows-per-page] — space-between in the CSS
        // lays them out info→start / size→end, which mirrors correctly in RTL.
        var pager = document.createElement('div');
        pager.className = 'lt-pager';

        var info = document.createElement('div');
        info.className = 'lt-pager-info';

        var controls = document.createElement('div');
        controls.className = 'lt-pager-controls';

        var sizeWrap = document.createElement('div');
        sizeWrap.className = 'lt-pager-size';
        var sizeLabel = document.createElement('span');
        sizeLabel.textContent = L.rows;
        var sizeSel = document.createElement('select');
        sizeSel.setAttribute('aria-label', L.rows);
        SIZES.forEach(function (s) {
            var o = document.createElement('option');
            o.value = String(s);
            o.textContent = s === 0 ? L.all : String(s);
            if (s === DEFAULT_SIZE) o.selected = true;
            sizeSel.appendChild(o);
        });
        sizeWrap.appendChild(sizeLabel);
        sizeWrap.appendChild(sizeSel);

        pager.appendChild(info);
        pager.appendChild(controls);
        pager.appendChild(sizeWrap);
        card.appendChild(pager);

        function recompute() {
            // Matches = rows NOT hidden by the page's own filter. Independent of
            // pagination, which uses the .ltp-out class (not inline display).
            state.matches = dataRows.filter(function (r) { return r.style.display !== 'none'; });
        }
        function totalPages() {
            return state.size > 0 ? Math.max(1, Math.ceil(state.matches.length / state.size)) : 1;
        }
        function render() {
            var pages = totalPages();
            if (state.page > pages) state.page = pages;
            if (state.page < 1) state.page = 1;
            var start = state.size > 0 ? (state.page - 1) * state.size : 0;
            var end = state.size > 0 ? start + state.size : state.matches.length;
            state.matches.forEach(function (r, i) { r.classList.toggle('ltp-out', i < start || i >= end); });
            renderInfo(start, end);
            renderButtons(pages);
        }
        function renderInfo(start, end) {
            var total = state.matches.length;
            if (total === 0) { info.textContent = L.none; return; }
            info.innerHTML = L.info
                .replace('{a}', '<b>' + (start + 1) + '</b>')
                .replace('{b}', '<b>' + Math.min(end, total) + '</b>')
                .replace('{n}', '<b>' + total + '</b>');
        }
        function renderButtons(pages) {
            controls.innerHTML = '';
            if (pages <= 1) return;

            var make = function (label, page, opts) {
                var b = document.createElement('button');
                b.type = 'button';
                b.className = 'lt-pager-btn' + (opts && opts.active ? ' active' : '');
                b.innerHTML = label;
                if (opts && opts.disabled) { b.disabled = true; }
                else if (page) { b.addEventListener('click', function () { state.page = page; render(); scrollTop(); }); }
                controls.appendChild(b);
            };
            var ellipsis = function () {
                var s = document.createElement('span');
                s.className = 'lt-pager-ellipsis';
                s.textContent = '…';
                controls.appendChild(s);
            };

            make('«', 1, { disabled: state.page === 1 });
            make('‹', state.page - 1, { disabled: state.page === 1 });

            // 1, current-1, current, current+1, last — with ellipses for gaps.
            var wanted = [1, pages, state.page, state.page - 1, state.page + 1];
            var seen = {};
            var sorted = wanted.filter(function (p) {
                if (p < 1 || p > pages || seen[p]) return false;
                seen[p] = true; return true;
            }).sort(function (a, b) { return a - b; });
            var prev = 0;
            sorted.forEach(function (p) {
                if (p - prev > 1) ellipsis();
                make(String(p), p, { active: p === state.page });
                prev = p;
            });

            make('›', state.page + 1, { disabled: state.page === pages });
            make('»', pages, { disabled: state.page === pages });
        }
        function scrollTop() {
            var wrap = table.closest('.lt-table-wrap');
            if (wrap) wrap.scrollTop = 0;
        }

        sizeSel.addEventListener('change', function () {
            state.size = parseInt(sizeSel.value, 10) || 0;
            state.page = 1;
            render();
        });

        // Re-paginate after any toolbar filter (search box, status pills, selects).
        // Deferred so the page's OWN handler sets row display first.
        var onFilter = function () { setTimeout(function () { state.page = 1; recompute(); render(); }, 0); };
        var shell = table.closest('.lt-shell');
        var toolbar = shell ? shell.querySelector('.lt-toolbar') : null;
        if (toolbar) {
            ['input', 'change', 'click'].forEach(function (ev) { toolbar.addEventListener(ev, onFilter); });
        } else {
            var search = (shell || document).querySelector('.lt-search input, #searchInput');
            if (search) search.addEventListener('input', onFilter);
        }

        recompute();
        render();
    }
})();
