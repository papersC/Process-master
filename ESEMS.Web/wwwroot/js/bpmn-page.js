// ══════════════════════════════════════════════════════════════
// BPMN Page Logic — extracted from Views/AI/Diagrams.cshtml
// Depends on: window.bpmnPageConfig (set inline by Razor)
//             window.bpmnModeler (from bpmn-modeler-app.js)
//             bootstrap (Bootstrap 5)
// ══════════════════════════════════════════════════════════════

(function () {
    'use strict';

    const cfg = window.bpmnPageConfig || {};
    const S = cfg.strings || {};

    // ── State ──────────────────────────────────────────────────────
    let currentLoadedProcessId = null;
    let currentLoadedProcessLabel = null;
    let allProcesses = [];
    let bpmnProcesses = [];
    let allProcessesLoaded = false;
    let bpmnProcessesLoaded = false;
    let proceduresLoaded = false;

    // ── API helper ────────────────────────────────────────────────
    function getAntiForgeryToken() {
        var tokenEl = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
        return tokenEl ? tokenEl.value : '';
    }

    async function apiFetch(url, options) {
        options = options || {};
        // Auto-inject antiforgery token for POST requests
        if (options.method && options.method.toUpperCase() === 'POST') {
            options.headers = options.headers || {};
            if (!options.headers['RequestVerificationToken']) {
                options.headers['RequestVerificationToken'] = getAntiForgeryToken();
            }
        }
        const response = await fetch(url, options);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const data = await response.json();
        if (data.success === false) throw new Error(data.error || S.unknownError || 'Unknown error');
        return data;
    }

    // ── Searchable dropdown helper ───────────────────────────────
    function initSearchDropdown(inputId, resultsId, hiddenId, dataFn, onSelect) {
        const input = document.getElementById(inputId);
        const results = document.getElementById(resultsId);
        const hidden = document.getElementById(hiddenId);
        if (!input || !results) return;

        let activeIdx = -1;

        input.addEventListener('input', () => {
            const q = input.value.toLowerCase().trim();
            const data = dataFn();
            hidden.value = '';
            activeIdx = -1;
            if (!q) { results.classList.remove('show'); return; }
            const filtered = data.filter(p =>
                p.code.toLowerCase().includes(q) || p.name.toLowerCase().includes(q)
            );
            renderResults(results, filtered, onSelect, hidden, input);
        });

        input.addEventListener('focus', () => {
            if (input.value.trim()) input.dispatchEvent(new Event('input'));
        });

        input.addEventListener('keydown', (e) => {
            const items = results.querySelectorAll('.search-item');
            if (!items.length) return;
            if (e.key === 'ArrowDown') { e.preventDefault(); activeIdx = Math.min(activeIdx + 1, items.length - 1); highlightItem(items, activeIdx); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); activeIdx = Math.max(activeIdx - 1, 0); highlightItem(items, activeIdx); }
            else if (e.key === 'Enter' && activeIdx >= 0) { e.preventDefault(); items[activeIdx].click(); }
            else if (e.key === 'Escape') { results.classList.remove('show'); }
        });

        document.addEventListener('click', (e) => {
            if (!input.contains(e.target) && !results.contains(e.target)) results.classList.remove('show');
        });
    }

    function renderResults(container, items, onSelect, hidden, input) {
        if (!items.length) {
            container.innerHTML = '<div class="search-empty">' + (S.noResults || 'No results found') + '</div>';
            container.classList.add('show');
            return;
        }
        container.innerHTML = items.map(p =>
            '<div class="search-item" data-id="' + p.id + '" data-label="' + p.code + ' - ' + p.name + '">' +
            '<span class="code">' + p.code + '</span> <span class="name">- ' + p.name + '</span></div>'
        ).join('');
        container.classList.add('show');
        container.querySelectorAll('.search-item').forEach(el => {
            el.addEventListener('click', () => {
                hidden.value = el.dataset.id;
                input.value = el.dataset.label;
                container.classList.remove('show');
                if (onSelect) onSelect(el.dataset.id, el.dataset.label);
            });
        });
    }

    function highlightItem(items, idx) {
        items.forEach((el, i) => el.classList.toggle('active', i === idx));
    }

    // ── Linked process UI helpers ────────────────────────────────
    function setLinkedProcess(id, label) {
        currentLoadedProcessId = id;
        currentLoadedProcessLabel = label;
        const badge = document.getElementById('toolbarLinkedBadge');
        const badgeName = document.getElementById('toolbarLinkedName');
        if (badge && badgeName) { badgeName.textContent = label; badge.classList.remove('d-none'); }
        updateSaveModalLinkedState();
        if (typeof lucide !== 'undefined') lucide.createIcons();
    }

    function unlinkProcess() {
        currentLoadedProcessId = null;
        currentLoadedProcessLabel = null;
        const badge = document.getElementById('toolbarLinkedBadge');
        if (badge) badge.classList.add('d-none');
        updateSaveModalLinkedState();
    }

    function updateSaveModalLinkedState() {
        const banner = document.getElementById('linkedProcessBanner');
        const label = document.getElementById('linkedProcessLabel');
        const searchSection = document.getElementById('saveSearchSection');
        if (currentLoadedProcessId) {
            label.textContent = currentLoadedProcessLabel;
            banner.classList.remove('d-none');
            searchSection.classList.add('d-none');
        } else {
            banner.classList.add('d-none');
            searchSection.classList.remove('d-none');
        }
        if (typeof lucide !== 'undefined') lucide.createIcons();
    }

    // ── Data fetching (lazy) ─────────────────────────────────────
    async function ensureAllProcesses() {
        if (allProcessesLoaded) return;
        try {
            const r = await apiFetch('/AI/GetProcesses');
            if (r.processes) allProcesses = r.processes;
        } catch (e) { console.error('Error loading processes:', e); }
        allProcessesLoaded = true;
    }

    async function ensureBpmnProcesses() {
        if (bpmnProcessesLoaded) return;
        try {
            const r = await apiFetch('/AI/GetProcessesWithBPMN');
            if (r.processes) bpmnProcesses = r.processes;
        } catch (e) { console.error('Error loading BPMN processes:', e); }
        bpmnProcessesLoaded = true;
    }

    async function ensureProcedures() {
        if (proceduresLoaded) return;
        try {
            const result = await apiFetch('/AI/GetProcessTasksWithBPMN');
            if (result.tasks) {
                const select = document.getElementById('selectLoadProcedure');
                const countBadge = document.getElementById('procedureCount');
                countBadge.textContent = result.tasks.length;
                result.tasks.forEach(t => {
                    const option = document.createElement('option');
                    option.value = t.id;
                    option.textContent = t.code + ' - ' + t.name;
                    if (t.processName) option.textContent += ' (' + t.processName + ')';
                    select.appendChild(option);
                });
            }
        } catch (e) { console.error('Error loading procedures:', e); }
        proceduresLoaded = true;
    }

    // ── Confirm Save ─────────────────────────────────────────────
    async function confirmSave() {
        const saveError = document.getElementById('saveError');
        const saveSuccess = document.getElementById('saveSuccess');
        saveError.classList.add('d-none');
        saveSuccess.classList.add('d-none');

        let processId = currentLoadedProcessId || document.getElementById('selectProcess').value;
        const newProcessName = document.getElementById('newProcessName').value.trim();
        const changeDesc = document.getElementById('changeDescription').value.trim();

        if (!processId && !newProcessName) {
            saveError.textContent = S.selectOrCreateProcess || 'Please select a process or enter a new process name';
            saveError.classList.remove('d-none');
            return;
        }

        try {
            const { xml } = await window.bpmnModeler.saveXML({ format: true });
            const result = await apiFetch('/AI/SaveBPMNToProcess', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    processId: processId || null,
                    processName: newProcessName || null,
                    bpmnXml: xml,
                    changeDescription: changeDesc || null
                })
            });

            saveSuccess.textContent = result.message || S.savedSuccessfully || 'Saved successfully!';
            saveSuccess.classList.remove('d-none');

            if (result.processId) {
                const label = currentLoadedProcessLabel
                    || document.getElementById('searchSaveProcess').value
                    || newProcessName;
                setLinkedProcess(result.processId, label);
                bpmnProcessesLoaded = false;
                allProcessesLoaded = false;
            }

            setTimeout(() => {
                const modalEl = document.getElementById('saveToProcessModal');
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }, 1500);
        } catch (error) {
            saveError.textContent = error.message;
            saveError.classList.remove('d-none');
        }
    }

    // ── Load from Process ────────────────────────────────────────
    async function loadFromProcess() {
        const processId = document.getElementById('selectLoadProcess').value;
        const loadedInfo = document.getElementById('loadedProcessInfo');
        const loadedName = document.getElementById('loadedProcessName');

        if (!processId) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'warning', title: S.warning || 'Warning', text: S.selectProcess || 'Please search and select a process' });
            }
            return;
        }

        try {
            document.getElementById('loadingOverlay').classList.remove('d-none');
            document.getElementById('bpmnStatus').textContent = S.loadingDiagram || 'Loading diagram...';

            const result = await apiFetch('/AI/LoadProcessBPMN?id=' + encodeURIComponent(processId));

            if (result.bpmnXml) {
                await window.bpmnModeler.importXML(result.bpmnXml);
                if (typeof window.safeBpmnFit === 'function') {
                    await window.safeBpmnFit(window.bpmnModeler);
                } else {
                    try { window.bpmnModeler.get('canvas').zoom('fit-viewport'); } catch (e) { /* ignore non-finite */ }
                }

                const label = result.processCode + ' - ' + result.processName;
                setLinkedProcess(result.processId, label);
                loadedName.textContent = label;
                loadedInfo.classList.remove('d-none');
                document.getElementById('bpmnStatus').textContent = S.diagramLoaded || 'Diagram loaded - Ready for refinement';

                if (typeof Swal !== 'undefined') {
                    Swal.fire({ icon: 'success', title: S.loaded || 'Loaded!', text: S.bpmnLoadedSuccess || 'BPMN diagram loaded successfully.', timer: 3000 });
                }
                if (typeof lucide !== 'undefined') lucide.createIcons();
            } else {
                throw new Error(S.loadFailed || 'Failed to load diagram');
            }
        } catch (error) {
            console.error('Error loading BPMN:', error);
            document.getElementById('bpmnStatus').textContent = S.loadFailed || 'Load failed';
            if (typeof Swal !== 'undefined') { Swal.fire({ icon: 'error', title: S.error || 'Error', text: error.message }); }
        } finally {
            document.getElementById('loadingOverlay').classList.add('d-none');
        }
    }

    // ── Load from Procedure ──────────────────────────────────────
    async function loadFromProcedure() {
        const procedureId = document.getElementById('selectLoadProcedure').value;
        const loadedInfo = document.getElementById('loadedProcedureInfo');
        const loadedName = document.getElementById('loadedProcedureName');

        if (!procedureId) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'warning', title: S.warning || 'Warning', text: S.selectProcedure || 'Please select a procedure' });
            }
            return;
        }

        try {
            document.getElementById('loadingOverlay').classList.remove('d-none');
            document.getElementById('bpmnStatus').textContent = S.loadingDiagram || 'Loading diagram...';

            const result = await apiFetch('/AI/LoadProcessTaskBPMN?id=' + encodeURIComponent(procedureId));

            if (result.bpmnXml) {
                await window.bpmnModeler.importXML(result.bpmnXml);
                if (typeof window.safeBpmnFit === 'function') {
                    await window.safeBpmnFit(window.bpmnModeler);
                } else {
                    try { window.bpmnModeler.get('canvas').zoom('fit-viewport'); } catch (e) { /* ignore non-finite */ }
                }

                loadedName.textContent = result.processTaskCode + ' - ' + result.processTaskName;
                loadedInfo.classList.remove('d-none');
                document.getElementById('bpmnStatus').textContent = S.procedureDiagramLoaded || 'Procedure diagram loaded';

                if (typeof Swal !== 'undefined') {
                    Swal.fire({ icon: 'success', title: S.loaded || 'Loaded!', text: S.procedureBpmnLoaded || 'Procedure BPMN diagram loaded successfully.', timer: 3000 });
                }
                if (typeof lucide !== 'undefined') lucide.createIcons();
            } else {
                throw new Error(S.loadFailed || 'Failed to load diagram');
            }
        } catch (error) {
            console.error('Error loading procedure BPMN:', error);
            document.getElementById('bpmnStatus').textContent = S.loadFailed || 'Load failed';
            if (typeof Swal !== 'undefined') { Swal.fire({ icon: 'error', title: S.error || 'Error', text: error.message }); }
        } finally {
            document.getElementById('loadingOverlay').classList.add('d-none');
        }
    }

    // ── Optimize Description ─────────────────────────────────────
    async function optimizeDescription() {
        const descriptionField = document.getElementById('processDescription');
        const currentDescription = descriptionField.value.trim();

        if (!currentDescription) {
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'warning', title: S.warning || 'Warning', text: S.enterDescriptionFirst || 'Please enter a process description first' });
            }
            return;
        }

        try {
            document.getElementById('loadingOverlay').classList.remove('d-none');
            document.getElementById('bpmnStatus').textContent = S.optimizingDescription || 'Optimizing description...';

            const result = await apiFetch('/AI/OptimizePrompt', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ prompt: currentDescription, isArabic: !!(window.bpmnPageConfig && window.bpmnPageConfig.isRtl) })
            });

            if (result.optimizedPrompt) {
                descriptionField.value = result.optimizedPrompt;
                document.getElementById('bpmnStatus').textContent = S.descriptionOptimized || 'Description optimized successfully';
                if (typeof Swal !== 'undefined') {
                    Swal.fire({ icon: 'success', title: S.optimized || 'Optimized!', text: S.descriptionOptimizedText || 'Process description optimized. You can now generate the diagram.', timer: 3000 });
                }
            } else {
                throw new Error('Optimization failed');
            }
        } catch (error) {
            console.error('Error optimizing description:', error);
            if (typeof Swal !== 'undefined') {
                Swal.fire({ icon: 'error', title: S.error || 'Error', text: error.message || S.optimizeFailed || 'Failed to optimize description' });
            }
            document.getElementById('bpmnStatus').textContent = S.optimizeFailed || 'Description optimization failed';
        } finally {
            document.getElementById('loadingOverlay').classList.add('d-none');
        }
    }

    // ── Event delegation for color/font pickers ──────────────────
    function initColorPicker() {
        document.querySelectorAll('.color-swatch[data-fill]').forEach(btn => {
            btn.addEventListener('click', () => window.bpmnApplyColor(btn.dataset.fill, btn.dataset.stroke));
        });
        document.querySelector('.color-swatch[data-reset]')?.addEventListener('click', () => window.bpmnResetColor());

        const applyCustomBtn = document.getElementById('btnApplyCustomColor');
        if (applyCustomBtn) {
            applyCustomBtn.addEventListener('click', () => {
                window.bpmnApplyColor(
                    document.getElementById('customFillColor').value,
                    document.getElementById('customStrokeColor').value
                );
            });
        }
    }

    function initFontControls() {
        document.querySelectorAll('[data-font-size]').forEach(btn => {
            btn.addEventListener('click', () => window.bpmnApplyFontSize(parseInt(btn.dataset.fontSize)));
        });
        document.querySelectorAll('[data-font-family]').forEach(btn => {
            btn.addEventListener('click', () => window.bpmnApplyFontFamily(btn.dataset.fontFamily, btn.dataset.fontLabel));
        });
    }

    // ── Bootstrap from linked process (deep-link from /Processes/Details) ─
    async function bootstrapLinkedProcess() {
        const lp = cfg.linkedProcess;
        if (!lp || !lp.id) return;
        const label = (lp.code ? lp.code + ' - ' : '') + (lp.name || '');
        // Wait for the modeler to be ready
        const waitModeler = () => new Promise(resolve => {
            const check = () => {
                if (window.bpmnModeler && typeof window.bpmnModeler.importXML === 'function') resolve();
                else setTimeout(check, 50);
            };
            check();
        });
        await waitModeler();
        if (lp.xml && lp.xml.trim().length > 0) {
            try {
                await window.bpmnModeler.importXML(lp.xml);
                if (typeof window.safeBpmnFit === 'function') {
                    await window.safeBpmnFit(window.bpmnModeler);
                } else {
                    try { window.bpmnModeler.get('canvas').zoom('fit-viewport'); } catch (e) { /* ignore non-finite */ }
                }
                const status = document.getElementById('bpmnStatus');
                if (status) status.textContent = (S.diagramLoaded || 'Diagram loaded - Ready for refinement');
            } catch (e) { console.error('Failed to bootstrap linked BPMN:', e); }
        }
        setLinkedProcess(lp.id, label);
        // Pre-fill the AI generator name field with the process name
        const titleField = document.getElementById('processTitle');
        if (titleField && !titleField.value) titleField.value = lp.name || '';
    }

    // ── DOMContentLoaded ─────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', function () {
        if (typeof lucide !== 'undefined') lucide.createIcons();

        // Init searchable dropdowns
        initSearchDropdown('searchLoadProcess', 'loadProcessResults', 'selectLoadProcess',
            () => bpmnProcesses, null);
        initSearchDropdown('searchSaveProcess', 'saveProcessResults', 'selectProcess',
            () => allProcesses, null);

        // Init color/font event delegation
        initColorPicker();
        initFontControls();

        // If user arrived from a process page (?processId=), bootstrap link + xml
        bootstrapLinkedProcess();

        // Lazy-load: fetch bpmn processes on first focus of load search
        const loadSearchInput = document.getElementById('searchLoadProcess');
        if (loadSearchInput) {
            loadSearchInput.addEventListener('focus', () => ensureBpmnProcesses(), { once: true });
        }

        // Lazy-load: fetch all processes on first save modal open
        document.getElementById('btnSaveToProcess')?.addEventListener('click', async function () {
            await ensureAllProcesses();
            document.getElementById('saveError').classList.add('d-none');
            document.getElementById('saveSuccess').classList.add('d-none');
            document.getElementById('searchSaveProcess').value = '';
            document.getElementById('selectProcess').value = '';
            document.getElementById('newProcessName').value = '';
            document.getElementById('changeDescription').value = '';
            updateSaveModalLinkedState();
            const modal = new bootstrap.Modal(document.getElementById('saveToProcessModal'));
            modal.show();
        });

        // Lazy-load: fetch procedures when accordion panel expands
        const procedurePanel = document.getElementById('panelProcedure');
        if (procedurePanel) {
            procedurePanel.addEventListener('show.bs.collapse', () => ensureProcedures(), { once: true });
        }

        // Load from Process button
        document.getElementById('btnLoadFromProcess')?.addEventListener('click', loadFromProcess);

        // Confirm save button
        document.getElementById('btnConfirmSave')?.addEventListener('click', confirmSave);

        // Load from Procedure button
        document.getElementById('btnLoadFromProcedure')?.addEventListener('click', loadFromProcedure);

        // Optimize Description button
        document.getElementById('btnOptimizePrompt')?.addEventListener('click', optimizeDescription);
    });

    // Expose unlinkProcess for modal button
    window.unlinkProcess = unlinkProcess;
})();
