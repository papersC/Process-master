// My Space — per-user document library
// Expects a label map at window.__myspaceLabels (set by Index.cshtml)
(function () {
    'use strict';

    const L = window.__myspaceLabels || { files: 'files', confirmDelete: 'Delete this file?', deleteFailed: 'Delete failed', uploadFailed: 'Upload failed', categories: {}, download: 'Download', delete: 'Delete' };

    function categoryLabel(raw) {
        if (!raw) return '';
        return (L.categories && L.categories[raw]) || raw;
    }

    const FILE_ICONS = {
        pdf: 'file-text',
        xlsx: 'file-spreadsheet', xls: 'file-spreadsheet', csv: 'file-spreadsheet',
        docx: 'file-text', doc: 'file-text', txt: 'file-text',
        pptx: 'presentation', ppt: 'presentation',
        png: 'image', jpg: 'image', jpeg: 'image', gif: 'image', bmp: 'image'
    };

    function iconFor(name) {
        const ext = (name.split('.').pop() || '').toLowerCase();
        return FILE_ICONS[ext] || 'file';
    }

    function formatBytes(bytes) {
        if (bytes == null) return '';
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
        return (bytes / (1024 * 1024 * 1024)).toFixed(1) + ' GB';
    }

    function formatDate(iso) {
        if (!iso) return '';
        try {
            const d = new Date(iso);
            return d.toLocaleDateString() + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        } catch { return iso; }
    }

    function escapeHtml(s) {
        if (s == null) return '';
        return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    let allDocs = [];

    async function loadDocs() {
        document.getElementById('msLoading').classList.remove('hidden');
        document.getElementById('msTableWrap').classList.add('hidden');
        document.getElementById('msEmptyState').classList.add('hidden');

        try {
            const res = await fetch('/api/MySpace');
            if (!res.ok) throw new Error('Failed to load');
            allDocs = await res.json();
            renderDocs();
        } catch (err) {
            console.error('MySpace load failed', err);
            allDocs = [];
            renderDocs();
        } finally {
            document.getElementById('msLoading').classList.add('hidden');
        }
    }

    function renderDocs() {
        const search = (document.getElementById('msSearchInput').value || '').toLowerCase().trim();
        const category = document.getElementById('msCategoryFilter').value || '';

        let filtered = allDocs;
        if (search) {
            filtered = filtered.filter(d =>
                (d.originalName || '').toLowerCase().includes(search) ||
                (d.description || '').toLowerCase().includes(search) ||
                (d.tags || '').toLowerCase().includes(search)
            );
        }
        if (category) {
            filtered = filtered.filter(d => d.category === category);
        }

        document.getElementById('msDocCount').textContent = filtered.length + ' ' + L.files;

        const body = document.getElementById('msTableBody');
        const wrap = document.getElementById('msTableWrap');
        const empty = document.getElementById('msEmptyState');

        if (filtered.length === 0) {
            body.innerHTML = '';
            wrap.classList.add('hidden');
            empty.classList.remove('hidden');
            if (window.lucide) lucide.createIcons();
            return;
        }

        empty.classList.add('hidden');
        wrap.classList.remove('hidden');

        body.innerHTML = filtered.map(d => `
            <tr class="hover:bg-gray-50 transition">
                <td class="px-3 py-2">
                    <div class="w-8 h-8 rounded bg-blue-50 text-blue-600 inline-flex items-center justify-center">
                        <i data-lucide="${iconFor(d.originalName || '')}" class="w-4 h-4"></i>
                    </div>
                </td>
                <td class="px-3 py-2">
                    <div class="text-sm font-medium text-gray-800 truncate" title="${escapeHtml(d.originalName)}">${escapeHtml(d.originalName)}</div>
                    ${d.description ? `<div class="text-xs text-gray-500 truncate">${escapeHtml(d.description)}</div>` : ''}
                </td>
                <td class="px-3 py-2 text-sm text-gray-600">${escapeHtml(categoryLabel(d.category))}</td>
                <td class="px-3 py-2 text-sm text-gray-600">${formatBytes(d.fileSize)}</td>
                <td class="px-3 py-2 text-sm text-gray-600">${formatDate(d.uploadedAt)}</td>
                <td class="px-3 py-2 text-end">
                    <div class="inline-flex items-center gap-1">
                        <a href="/api/MySpace/${d.id}/download" class="inline-flex items-center justify-center w-8 h-8 rounded hover:bg-blue-50 text-blue-600" title="${escapeHtml(L.download)}">
                            <i data-lucide="download" class="w-4 h-4"></i>
                        </a>
                        <button type="button" class="ms-delete inline-flex items-center justify-center w-8 h-8 rounded hover:bg-red-50 text-red-600" data-id="${d.id}" title="${escapeHtml(L.delete)}">
                            <i data-lucide="trash-2" class="w-4 h-4"></i>
                        </button>
                    </div>
                </td>
            </tr>
        `).join('');

        body.querySelectorAll('.ms-delete').forEach(btn => {
            btn.addEventListener('click', () => deleteDoc(btn.dataset.id));
        });

        if (window.lucide) lucide.createIcons();
    }

    async function deleteDoc(id) {
        // SweetAlert2 confirmation — native confirm() looks unstyled inside our brand chrome.
        const ok = await Swal.fire({
            title: L.confirmDeleteTitle || 'Delete file',
            text: L.confirmDelete,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: L.confirmDeleteBtn || L.delete || 'Delete',
            cancelButtonText: L.cancel || 'Cancel',
            confirmButtonColor: '#B91C1C',
            cancelButtonColor: '#64748b',
            reverseButtons: true,
            focusCancel: true
        }).then(r => r.isConfirmed);
        if (!ok) return;
        try {
            const res = await fetch('/api/MySpace/' + id, { method: 'DELETE' });
            if (!res.ok) throw new Error('delete');
            allDocs = allDocs.filter(d => d.id !== id);
            renderDocs();
        } catch (err) {
            Swal.fire({ title: L.deleteFailed, icon: 'error', confirmButtonColor: '#005B99' });
        }
    }

    function showUploadToast(pct) {
        const toast = document.getElementById('msUploadToast');
        const bar = document.getElementById('msUploadBar');
        const lbl = document.getElementById('msUploadPct');
        toast.classList.remove('hidden');
        bar.style.width = pct + '%';
        lbl.textContent = pct + '%';
    }

    function hideUploadToast() {
        setTimeout(() => document.getElementById('msUploadToast').classList.add('hidden'), 400);
    }

    async function uploadFiles(fileList) {
        if (!fileList || fileList.length === 0) return;

        const fd = new FormData();
        for (const f of fileList) fd.append('files', f);
        fd.append('category', document.getElementById('msCategoryFilter').value || 'General');

        showUploadToast(10);

        try {
            // CSRF: grab the anti-forgery token from the layout-level hidden form
            // so the server's [ValidateAntiForgeryToken] attribute accepts the POST.
            const csrfToken = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]')?.value
                || document.querySelector('input[name="__RequestVerificationToken"]')?.value
                || '';

            const xhr = new XMLHttpRequest();
            xhr.open('POST', '/api/MySpace/upload-multiple');
            if (csrfToken) xhr.setRequestHeader('RequestVerificationToken', csrfToken);
            xhr.upload.onprogress = (e) => {
                if (e.lengthComputable) {
                    showUploadToast(Math.round((e.loaded / e.total) * 100));
                }
            };
            xhr.onload = async () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    showUploadToast(100);
                    hideUploadToast();
                    await loadDocs();
                } else {
                    hideUploadToast();
                    Swal.fire({ title: L.uploadFailed, text: 'HTTP ' + xhr.status, icon: 'error', confirmButtonColor: '#005B99' });
                }
            };
            xhr.onerror = () => { hideUploadToast(); Swal.fire({ title: L.uploadFailed, icon: 'error', confirmButtonColor: '#005B99' }); };
            xhr.send(fd);
        } catch (err) {
            hideUploadToast();
            Swal.fire({ title: L.uploadFailed, icon: 'error', confirmButtonColor: '#005B99' });
        }
    }

    // Wire up
    document.addEventListener('DOMContentLoaded', () => {
        const fileInput = document.getElementById('msFileInput');
        const btn1 = document.getElementById('btnUploadFiles');
        const btn2 = document.getElementById('btnUploadFirst');
        const dropZone = document.getElementById('msDropZone');

        btn1?.addEventListener('click', () => fileInput.click());
        btn2?.addEventListener('click', () => fileInput.click());
        fileInput.addEventListener('change', () => uploadFiles(fileInput.files));

        document.getElementById('msSearchInput').addEventListener('input', renderDocs);
        document.getElementById('msCategoryFilter').addEventListener('change', renderDocs);

        // Drag & drop on whole window
        ['dragenter', 'dragover'].forEach(evt => {
            window.addEventListener(evt, (e) => {
                if (e.dataTransfer?.types?.includes('Files')) {
                    e.preventDefault();
                    dropZone.classList.remove('hidden');
                }
            });
        });
        ['dragleave', 'drop'].forEach(evt => {
            window.addEventListener(evt, (e) => {
                if (e.type === 'dragleave' && e.relatedTarget) return;
                dropZone.classList.add('hidden');
            });
        });
        window.addEventListener('drop', (e) => {
            if (e.dataTransfer?.files?.length) {
                e.preventDefault();
                uploadFiles(e.dataTransfer.files);
            }
        });

        loadDocs();
    });
})();
