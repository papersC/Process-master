// Process Document Linking — multi-file repeater with "From Computer" and
// "From My Space" sources. Rows are rendered into #docLinkTableBody and a
// JSON snapshot is written into #DocumentLinksJson on every change, ready
// for the form post.
(function () {
    'use strict';

    const selects = JSON.parse(document.getElementById('docLinkSelects').textContent || '{}');
    const labels = JSON.parse(document.getElementById('docLinkLabels').textContent || '{}');
    let rows = [];
    try {
        const initial = JSON.parse(document.getElementById('docLinkExisting').textContent || '[]') || [];
        rows = initial.map(normalizeRow);
    } catch { rows = []; }

    function normalizeRow(r) {
        // Accept both camelCase and PascalCase from server
        return {
            id: r.id || r.Id || null,
            userDocumentId: r.userDocumentId || r.UserDocumentId,
            originalName: r.originalName || r.OriginalName || '(file)',
            fileSize: r.fileSize || r.FileSize || 0,
            url: r.url || r.Url || null,
            documentCategoryId: r.documentCategoryId || r.DocumentCategoryId || '',
            documentTypeId: r.documentTypeId || r.DocumentTypeId || '',
            documentLanguage: r.documentLanguage || r.DocumentLanguage || ''
        };
    }

    function formatBytes(bytes) {
        if (!bytes) return '';
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    // Styled upload-error dialog. Falls back to alert() only if SweetAlert
    // isn't loaded on the page (Swal is registered in _Layout via CDN).
    function showUploadError(title, detail) {
        if (typeof Swal !== 'undefined' && Swal.fire) {
            Swal.fire({
                icon: 'error',
                title: title,
                text: detail || '',
                confirmButtonText: 'OK',
                confirmButtonColor: '#005B99'
            });
        } else {
            alert(title + (detail ? '\n\n' + detail : ''));
        }
    }

    function escapeHtml(s) {
        if (s == null) return '';
        return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function iconFor(name) {
        const ext = (name.split('.').pop() || '').toLowerCase();
        const map = {
            pdf: 'file-text', docx: 'file-text', doc: 'file-text', txt: 'file-text',
            xlsx: 'file-spreadsheet', xls: 'file-spreadsheet', csv: 'file-spreadsheet',
            pptx: 'presentation', ppt: 'presentation',
            png: 'image', jpg: 'image', jpeg: 'image', gif: 'image', bmp: 'image'
        };
        return map[ext] || 'file';
    }

    function buildSelect(name, options, selectedValue) {
        const opts = ['<option value="">' + escapeHtml(labels.optional || '--') + '</option>']
            .concat(options.map(o => {
                const sel = (selectedValue != null && String(selectedValue) === String(o.value)) ? ' selected' : '';
                return '<option value="' + escapeHtml(o.value) + '"' + sel + '>' + escapeHtml(o.text) + '</option>';
            }));
        return '<select class="' + name + ' w-full px-2 py-1.5 border border-gray-300 rounded text-xs bg-white focus:ring-2 focus:ring-blue-500 focus:border-blue-500">' + opts.join('') + '</select>';
    }

    function render() {
        const body = document.getElementById('docLinkTableBody');
        const wrap = document.getElementById('docLinkTableWrap');
        const empty = document.getElementById('docLinkEmpty');
        const hidden = document.getElementById('DocumentLinksJson');

        if (rows.length === 0) {
            body.innerHTML = '';
            wrap.classList.add('hidden');
            empty.classList.remove('hidden');
        } else {
            empty.classList.add('hidden');
            wrap.classList.remove('hidden');

            body.innerHTML = rows.map((r, idx) => `
                <tr data-idx="${idx}">
                    <td class="px-3 py-2 align-top">
                        <div class="flex items-start gap-2 min-w-0">
                            <div class="w-8 h-8 rounded bg-blue-50 text-blue-600 inline-flex items-center justify-center flex-shrink-0">
                                <i data-lucide="${iconFor(r.originalName || '')}" class="w-4 h-4"></i>
                            </div>
                            <div class="min-w-0">
                                <div class="text-xs font-medium text-gray-800 truncate" title="${escapeHtml(r.originalName)}">
                                    ${r.url
                                        ? `<a href="${escapeHtml(r.url)}" target="_blank" class="hover:underline">${escapeHtml(r.originalName)}</a>`
                                        : escapeHtml(r.originalName)}
                                </div>
                                <div class="text-xs text-gray-400">${formatBytes(r.fileSize)}</div>
                            </div>
                        </div>
                    </td>
                    <td class="px-3 py-2 align-top">${buildSelect('dl-cat', selects.categories || [], r.documentCategoryId)}</td>
                    <td class="px-3 py-2 align-top">${buildSelect('dl-type', selects.types || [], r.documentTypeId)}</td>
                    <td class="px-3 py-2 align-top">${buildSelect('dl-lang', selects.languages || [], r.documentLanguage)}</td>
                    <td class="px-3 py-2 align-top text-end">
                        <button type="button" class="dl-remove inline-flex items-center justify-center w-7 h-7 rounded text-red-600 hover:bg-red-50" title="${escapeHtml(labels.remove || 'Remove')}">
                            <i data-lucide="trash-2" class="w-4 h-4"></i>
                        </button>
                    </td>
                </tr>
            `).join('');

            // Wire up per-row interactions
            body.querySelectorAll('tr').forEach(tr => {
                const idx = parseInt(tr.dataset.idx, 10);
                tr.querySelector('.dl-cat').addEventListener('change', e => { rows[idx].documentCategoryId = e.target.value; writeBack(); });
                tr.querySelector('.dl-type').addEventListener('change', e => { rows[idx].documentTypeId = e.target.value; writeBack(); });
                tr.querySelector('.dl-lang').addEventListener('change', e => { rows[idx].documentLanguage = e.target.value; writeBack(); });
                tr.querySelector('.dl-remove').addEventListener('click', () => { rows.splice(idx, 1); render(); });
            });
        }

        hidden.value = JSON.stringify(rows.map(r => ({
            userDocumentId: r.userDocumentId,
            documentCategoryId: r.documentCategoryId || null,
            documentTypeId: r.documentTypeId || null,
            documentLanguage: r.documentLanguage || null
        })));

        if (window.lucide) lucide.createIcons();
    }

    function writeBack() {
        document.getElementById('DocumentLinksJson').value = JSON.stringify(rows.map(r => ({
            userDocumentId: r.userDocumentId,
            documentCategoryId: r.documentCategoryId || null,
            documentTypeId: r.documentTypeId || null,
            documentLanguage: r.documentLanguage || null
        })));
    }

    // ── Upload-from-computer flow ──────────────────────────────────────
    function showToast(pct) {
        const t = document.getElementById('docLinkUploadToast');
        const b = document.getElementById('docLinkUploadBar');
        const l = document.getElementById('docLinkUploadPct');
        t.classList.remove('hidden');
        b.style.width = pct + '%';
        l.textContent = pct + '%';
    }
    function hideToast() {
        setTimeout(() => document.getElementById('docLinkUploadToast').classList.add('hidden'), 400);
    }

    async function uploadFromComputer(fileList) {
        if (!fileList || fileList.length === 0) return;
        const fd = new FormData();
        for (const f of fileList) fd.append('files', f);

        // CSRF: the upload-multiple endpoint is decorated with
        // [ValidateAntiForgeryToken] in MySpaceController. Without this header
        // the server returns HTTP 400 "Bad Request" — the trace shown on the
        // client looks like a generic IETF rfc9110 error, which is impossible
        // to debug without the source. Grab the token from the layout-level
        // hidden form (same pattern as wwwroot/js/myspace.js:172).
        const csrfToken = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]')?.value
            || document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || '';

        showToast(5);

        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/api/MySpace/upload-multiple');
        if (csrfToken) xhr.setRequestHeader('RequestVerificationToken', csrfToken);
        xhr.upload.onprogress = (e) => {
            if (e.lengthComputable) showToast(Math.round((e.loaded / e.total) * 100));
        };
        xhr.onload = () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                try {
                    const uploaded = JSON.parse(xhr.responseText);
                    for (const d of uploaded) {
                        rows.push({
                            id: null,
                            userDocumentId: d.id,
                            originalName: d.originalName,
                            fileSize: d.fileSize,
                            url: d.url,
                            documentCategoryId: '',
                            documentTypeId: '',
                            documentLanguage: ''
                        });
                    }
                    showToast(100);
                    hideToast();
                    render();
                } catch (err) {
                    hideToast();
                    showUploadError(labels.uploadFailed || 'Upload failed', err && err.message);
                }
            } else {
                hideToast();
                var detail = 'HTTP ' + xhr.status + (xhr.responseText ? ': ' + xhr.responseText.substring(0, 200) : '');
                showUploadError(labels.uploadFailed || 'Upload failed', detail);
            }
        };
        xhr.onerror = () => { hideToast(); showUploadError(labels.uploadFailed || 'Upload failed', 'Network error'); };
        xhr.send(fd);
    }

    // ── From My Space picker flow ──────────────────────────────────────
    let mySpaceCache = [];

    async function openMySpacePicker() {
        const backdrop = document.getElementById('mySpacePickerBackdrop');
        const modal = document.getElementById('mySpacePickerModal');
        backdrop.classList.remove('hidden');
        modal.classList.remove('hidden');
        modal.style.display = 'flex';

        const list = document.getElementById('mySpacePickerList');
        list.innerHTML = '<div class="text-center py-6 text-gray-400 text-sm"><div class="inline-block animate-spin rounded-full h-6 w-6 border-2 border-blue-500 border-t-transparent"></div></div>';

        try {
            const res = await fetch('/api/MySpace');
            mySpaceCache = await res.json();
        } catch {
            mySpaceCache = [];
        }
        renderMySpaceList();
    }

    function closeMySpacePicker() {
        document.getElementById('mySpacePickerBackdrop').classList.add('hidden');
        const m = document.getElementById('mySpacePickerModal');
        m.classList.add('hidden');
        m.style.display = 'none';
    }

    function renderMySpaceList() {
        const list = document.getElementById('mySpacePickerList');
        const search = (document.getElementById('mySpacePickerSearch').value || '').toLowerCase().trim();
        const already = new Set(rows.map(r => r.userDocumentId));

        let items = mySpaceCache;
        if (search) {
            items = items.filter(d =>
                (d.originalName || '').toLowerCase().includes(search) ||
                (d.description || '').toLowerCase().includes(search) ||
                (d.tags || '').toLowerCase().includes(search)
            );
        }

        if (items.length === 0) {
            list.innerHTML = '<div class="text-center py-6 text-xs text-gray-400">' +
                escapeHtml(mySpaceCache.length === 0 ? (labels.emptyMySpace || '') : (labels.noResults || '')) +
                '</div>';
            return;
        }

        list.innerHTML = items.map(d => `
            <button type="button" class="ms-pick w-full flex items-center gap-3 px-3 py-2 rounded hover:bg-blue-50 text-start transition"
                    data-id="${escapeHtml(d.id)}" ${already.has(d.id) ? 'disabled style="opacity:.4;cursor:not-allowed;"' : ''}>
                <div class="w-8 h-8 rounded bg-blue-50 text-blue-600 inline-flex items-center justify-center flex-shrink-0">
                    <i data-lucide="${iconFor(d.originalName || '')}" class="w-4 h-4"></i>
                </div>
                <div class="min-w-0 flex-1">
                    <div class="text-xs font-medium text-gray-800 truncate">${escapeHtml(d.originalName || '')}</div>
                    <div class="text-xs text-gray-400">${formatBytes(d.fileSize)} • ${escapeHtml(d.category || '')}</div>
                </div>
                ${already.has(d.id) ? '<i data-lucide="check" class="w-4 h-4 text-green-600"></i>' : '<i data-lucide="plus" class="w-4 h-4 text-blue-600"></i>'}
            </button>
        `).join('');

        list.querySelectorAll('.ms-pick').forEach(btn => {
            if (btn.disabled) return;
            btn.addEventListener('click', () => {
                const id = btn.dataset.id;
                const d = mySpaceCache.find(x => x.id === id);
                if (!d) return;
                rows.push({
                    id: null,
                    userDocumentId: d.id,
                    originalName: d.originalName,
                    fileSize: d.fileSize,
                    url: d.url,
                    documentCategoryId: '',
                    documentTypeId: '',
                    documentLanguage: ''
                });
                render();
                closeMySpacePicker();
            });
        });

        if (window.lucide) lucide.createIcons();
    }

    // ── Toggle that collapses/expands the whole Document Linking panel ──
    function wireToggle() {
        const btn = document.getElementById('docLinkingToggle');
        const knob = document.getElementById('docLinkingToggleKnob');
        const panel = document.getElementById('docLinkingFields');
        if (!btn || !panel) return;

        // Track whether the user has explicitly turned the feature off.
        // When off, we serialize an empty array so no links are persisted,
        // but we keep the in-memory rows around so the user can re-enable
        // without losing their work.
        let enabled = false;

        function setState(on) {
            enabled = on;
            btn.setAttribute('aria-checked', on ? 'true' : 'false');
            btn.style.backgroundColor = on ? '#4169E1' : '#d1d5db';
            if (knob) knob.style.transform = on ? 'translateX(20px)' : 'translateX(0)';
            panel.classList.toggle('hidden', !on);
            // Serialize according to the current state
            writeBack();
        }

        btn.addEventListener('click', () => setState(!enabled));

        // Auto-open if we already have existing links (edit view) or on
        // first load with a fresh row added from upload/pick
        const wrapper = btn.closest('[data-has-existing]');
        const hasExisting = wrapper?.dataset.hasExisting === '1' || rows.length > 0;
        setState(hasExisting);

        // When the JS flow adds rows (via upload or pick), auto-expand.
        window.__docLinkExpand = () => { if (!enabled) setState(true); };
    }

    // Replace the base writeBack so it respects the toggle state.
    const originalWriteBack = writeBack;
    // eslint-disable-next-line no-func-assign
    writeBack = function () {
        const btn = document.getElementById('docLinkingToggle');
        const enabled = btn ? btn.getAttribute('aria-checked') === 'true' : true;
        const hidden = document.getElementById('DocumentLinksJson');
        if (!enabled) {
            hidden.value = '[]';
            return;
        }
        originalWriteBack();
    };

    // Patch render() similarly: keep the full body hidden when disabled.
    const originalRender = render;
    // eslint-disable-next-line no-func-assign
    render = function () {
        originalRender();
        // Re-serialize so the toggle state is respected
        writeBack();
    };

    // Wire-up on DOM ready
    document.addEventListener('DOMContentLoaded', () => {
        const btnComputer = document.getElementById('btnDocFromComputer');
        const btnMySpace = document.getElementById('btnDocFromMySpace');
        const fileInput = document.getElementById('docLinkComputerInput');
        const closeBtn = document.getElementById('mySpacePickerClose');
        const cancelBtn = document.getElementById('mySpacePickerCancel');
        const backdrop = document.getElementById('mySpacePickerBackdrop');
        const searchInput = document.getElementById('mySpacePickerSearch');

        btnComputer?.addEventListener('click', () => fileInput.click());
        fileInput?.addEventListener('change', () => {
            uploadFromComputer(fileInput.files);
            fileInput.value = '';
        });

        btnMySpace?.addEventListener('click', openMySpacePicker);
        closeBtn?.addEventListener('click', closeMySpacePicker);
        cancelBtn?.addEventListener('click', closeMySpacePicker);
        backdrop?.addEventListener('click', closeMySpacePicker);
        searchInput?.addEventListener('input', renderMySpaceList);

        wireToggle();
        render();
    });
})();
