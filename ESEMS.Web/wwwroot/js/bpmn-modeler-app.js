/**
 * BPMN Modeler Application
 * Professional BPMN 2.0 editor using bpmn-js (free, open source - Apache 2.0)
 * With Azure OpenAI AI integration for MBRHE
 */

(function() {
    'use strict';

    // Localization helper – pulls from window.bpmnPageConfig.strings with fallback
    function S(key, fallback) {
        var cfg = window.bpmnPageConfig && window.bpmnPageConfig.strings;
        return (cfg && cfg[key]) || fallback || key;
    }

    // Antiforgery token helper – reads from the hidden form in _Layout.cshtml
    function getAntiForgeryToken() {
        var tokenEl = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
        return tokenEl ? tokenEl.value : '';
    }

    // bpmn-js's canvas.zoom('fit-viewport') divides by the SVG container's
    // width/height. If the container is still laid out as 0x0 (initial paint
    // not done, parent display:none, accordion still collapsing, etc.) the
    // resulting matrix is non-finite and bpmn-js throws:
    //   "Failed to execute 'scale' on 'SVGMatrix': The provided float value is non-finite."
    // This helper waits for the next two animation frames so layout has
    // settled, then calls fit-viewport inside a try/catch so a still-zero
    // container fails silent rather than blanking the page.
    async function safeFitViewport(modeler) {
        if (!modeler) return;
        try {
            await new Promise(function (r) {
                requestAnimationFrame(function () { requestAnimationFrame(r); });
            });
            modeler.get('canvas').zoom('fit-viewport');
        } catch (e) {
            console.warn('safeFitViewport: fit-viewport skipped (canvas not yet sized)', e);
        }
    }
    // Expose for sibling page scripts (bpmn-page.js).
    window.safeBpmnFit = safeFitViewport;

    // Default empty BPMN diagram
    const EMPTY_DIAGRAM = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
                  xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
                  xmlns:di="http://www.omg.org/spec/DD/20100524/DI"
                  id="Definitions_1"
                  targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="false">
    <bpmn:startEvent id="StartEvent_1" name="Start"/>
  </bpmn:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="StartEvent_1_di" bpmnElement="StartEvent_1">
        <dc:Bounds x="180" y="160" width="36" height="36"/>
        <bpmndi:BPMNLabel>
          <dc:Bounds x="186" y="203" width="24" height="14"/>
        </bpmndi:BPMNLabel>
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;

    // DOM Elements helper
    const $ = id => document.getElementById(id);

    // bpmn-js modeler instance
    let modeler = null;

    // Font configuration (global – applies to all rendered text)
    let currentFontSize = 12;            // number in px
    let currentFontFamily = 'Arial, sans-serif';

    // Status & Error helpers
    function setStatus(msg) {
        const statusEl = $('bpmnStatus');
        if (statusEl) statusEl.textContent = msg;
    }

    function showError(msg) {
        const errorEl = $('bpmnError');
        if (errorEl) {
            errorEl.textContent = msg;
            errorEl.classList.remove('d-none');
        }
    }

    function hideError() {
        const errorEl = $('bpmnError');
        if (errorEl) errorEl.classList.add('d-none');
    }

    function showLoading(show = true) {
        const loadingOverlay = $('loadingOverlay');
        if (loadingOverlay) loadingOverlay.classList.toggle('d-none', !show);
    }

    // Initialize bpmn-js modeler with enhanced drawing features
    async function initModeler() {
        try {
            const canvas = $('bpmnCanvas');
            if (!canvas) {
                console.error('Canvas element not found');
                return;
            }

            // Initialize modeler with enhanced configuration for better drawing experience
            modeler = new BpmnJS({
                container: canvas,
                keyboard: {
                    bindTo: document
                },
                // Enhanced modeler configuration
                bpmnRenderer: {
                    defaultFillColor: '#ffffff',
                    defaultStrokeColor: '#000000'
                },
                // Enable grid snapping for precise alignment
                gridSnapping: {
                    active: true,
                    snapOnResize: true,
                    snapOnCreate: true
                },
                // Font configuration
                textRenderer: {
                    defaultStyle: {
                        fontSize: currentFontSize,
                        fontFamily: currentFontFamily
                    },
                    externalStyle: {
                        fontSize: currentFontSize,
                        fontFamily: currentFontFamily
                    }
                }
            });

            await modeler.importXML(EMPTY_DIAGRAM);

            // Expose modeler globally so Diagrams.cshtml can access it
            window.bpmnModeler = modeler;

            // Configure canvas for better drawing experience.
            // safeFitViewport waits one frame and try/catches the non-finite
            // SVGMatrix error that bpmn-js throws when the canvas is 0x0.
            await safeFitViewport(modeler);
            const canvasAPI = modeler.get('canvas');

            // Enable grid snapping
            try {
                const gridSnapping = modeler.get('gridSnapping');
                if (gridSnapping) {
                    gridSnapping.setActive(true);
                }
            } catch (e) {
                console.log('Grid snapping not available in this version');
            }

            // Add event listeners for better UX feedback
            const eventBus = modeler.get('eventBus');

            // Show feedback when dragging elements
            eventBus.on('shape.move.start', () => {
                setStatus(S('movingElement', 'Moving element... (Hold Shift to disable snapping)'));
            });

            eventBus.on('shape.move.end', () => {
                setStatus(S('elementMoved', 'Element moved'));
                setTimeout(() => setStatus(S('readyEdit', 'Ready - Drag elements from the palette or click existing elements to edit')), 2000);
            });

            // Show feedback when creating elements
            eventBus.on('create.end', (event) => {
                setStatus(S('elementCreated', '{0} created').replace('{0}', event.shape.type));
                setTimeout(() => setStatus(S('readyEdit', 'Ready - Drag elements from the palette or click existing elements to edit')), 2000);
            });

            // Show feedback when connecting elements
            eventBus.on('connection.added', () => {
                setStatus(S('connectionCreated', 'Connection created'));
                setTimeout(() => setStatus(S('readyEdit', 'Ready - Drag elements from the palette or click existing elements to edit')), 2000);
            });

            // Re-apply RTL transforms after shape/label re-renders
            if (isRtlMode()) {
                eventBus.on(['shape.added', 'shape.changed', 'element.changed'], function() {
                    setTimeout(function() {
                        applyRtlToPoolsAndLanes();
                        applyRtlToTextAnnotations();
                    }, 50);
                });
            }

            setStatus(S('ready', 'Ready - Drag elements from the left palette to create your diagram'));
            console.log('BPMN Modeler initialized with enhanced drawing features');

            // Make the palette draggable after a short delay so it renders first
            setTimeout(makePaletteDraggable, 300);
        } catch (err) {
            console.error('Failed to initialize BPMN modeler:', err);
            showError(S('initError', 'Failed to initialize BPMN modeler') + ': ' + err.message);
        }
    }

    // ── Make the bpmn-js palette draggable ────────────────────────────
    function makePaletteDraggable() {
        const palette = document.querySelector('#bpmnCanvas .djs-palette');
        if (!palette) return;

        // Inject a drag handle at the top of the palette
        const handle = document.createElement('div');
        handle.className = 'palette-drag-handle';
        handle.innerHTML = '<div class="grip"></div>';
        handle.title = S('dragToReposition', 'Drag to reposition');
        palette.insertBefore(handle, palette.firstChild);

        let isDragging = false;
        let offsetX = 0, offsetY = 0;

        handle.addEventListener('mousedown', (e) => {
            isDragging = true;
            const rect = palette.getBoundingClientRect();
            const containerRect = palette.parentElement.getBoundingClientRect();
            offsetX = e.clientX - rect.left + containerRect.left;
            offsetY = e.clientY - rect.top + containerRect.top;
            palette.classList.add('dragging');
            palette.style.transition = 'none';
            e.preventDefault();
            e.stopPropagation();
        });

        document.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            const container = palette.parentElement;
            const cr = container.getBoundingClientRect();
            let newLeft = e.clientX - offsetX;
            let newTop = e.clientY - offsetY;
            // Clamp within the canvas container
            newLeft = Math.max(0, Math.min(newLeft, cr.width - palette.offsetWidth));
            newTop = Math.max(0, Math.min(newTop, cr.height - palette.offsetHeight));
            palette.style.left = newLeft + 'px';
            palette.style.top = newTop + 'px';
            e.preventDefault();
        });

        document.addEventListener('mouseup', () => {
            if (!isDragging) return;
            isDragging = false;
            palette.classList.remove('dragging');
            palette.style.transition = '';
            // Persist position in localStorage
            try {
                localStorage.setItem('bpmn-palette-pos', JSON.stringify({
                    left: palette.style.left,
                    top: palette.style.top
                }));
            } catch (_) { /* ignore */ }
        });

        // Restore saved position
        try {
            const saved = JSON.parse(localStorage.getItem('bpmn-palette-pos'));
            if (saved && saved.left && saved.top) {
                palette.style.left = saved.left;
                palette.style.top = saved.top;
            }
        } catch (_) { /* ignore */ }
    }

    // Get current BPMN XML
    async function getCurrentXML() {
        try {
            const { xml } = await modeler.saveXML({ format: true });
            return xml;
        } catch (err) {
            console.error('Failed to get XML:', err);
            return null;
        }
    }

    function normalizeMalformedEntitySequences(xml) {
        if (!xml) return xml;

        // Fix common double-encoding from AI output.
        xml = xml.replace(/&amp;#/g, '&#');
        xml = xml.replace(/&amp;x/gi, '&#x');
        xml = xml.replace(/&amp;/g, '&');

        // Fix separators like ;&#x062A; or ; &x062A; between entity fragments.
        xml = xml.replace(/;\s*(&#x?[0-9a-fA-F]+;?)/gi, '$1');
        xml = xml.replace(/;\s*(&x[0-9a-fA-F]+;)/gi, '$1');
        xml = xml.replace(/;\s*(#x[0-9a-fA-F]+;)/gi, '$1');

        // Normalize malformed hex entity variants such as &x062A;, #x062A;, x062A;.
        // Also catch bare xHHHH; at any position (including after ; or another entity).
        xml = xml.replace(/&x([0-9a-fA-F]{2,6});?/g, '&#x$1;');
        xml = xml.replace(/(^|[^&])#x([0-9a-fA-F]{2,6});?/g, '$1&#x$2;');
        xml = xml.replace(/(?:^|(?<=;)|(?<=["\s=,]))x([0-9a-fA-F]{2,6});?/g, '&#x$1;');
        // Fallback: any bare x followed by 3-4 hex digits and semicolon not inside a word
        xml = xml.replace(/([^A-Za-z0-9&#])x([0-9a-fA-F]{2,6});/g, '$1&#x$2;');

        // Normalize malformed decimal entity variants.
        xml = xml.replace(/&([0-9]{2,7});?/g, '&#$1;');
        xml = xml.replace(/(^|[^&])#([0-9]{2,7});?/g, '$1&#$2;');

        return xml;
    }

    // Decode XML numeric character references to Unicode (except XML-reserved chars)
    function decodeXmlEntities(xml) {
        if (!xml) return xml;

        // Run normalization + decode in a loop to handle chained/nested malformed entities
        var prev;
        var maxPasses = 3;
        for (var pass = 0; pass < maxPasses; pass++) {
            prev = xml;
            xml = normalizeMalformedEntitySequences(xml);

            var reserved = {0x22:1, 0x26:1, 0x27:1, 0x3C:1, 0x3E:1};
            xml = xml.replace(/&#x([0-9a-fA-F]+);?/gi, function(m, hex) {
                var code = parseInt(hex, 16);
                return reserved[code] ? m : String.fromCodePoint(code);
            });
            xml = xml.replace(/&#(\d+);?/g, function(m, dec) {
                var code = parseInt(dec, 10);
                return reserved[code] ? m : String.fromCodePoint(code);
            });

            if (xml === prev) break;
        }

        // Strip residual semicolons left between decoded Arabic letters.
        xml = xml.replace(/;+(?=[\u0600-\u06FF])/g, '');
        xml = xml.replace(/([\u0600-\u06FF]);+/g, '$1');
        xml = xml.replace(/=";+/g, '="');
        xml = xml.replace(/;+"/g, '"');

        return xml;
    }

    // ── RTL support: move pool/lane headers to right side ──────────
    function isRtlMode() {
        return !!(window.bpmnPageConfig && window.bpmnPageConfig.isRtl);
    }

    function applyRtlToPoolsAndLanes() {
        if (!isRtlMode() || !modeler) return;

        var container = document.getElementById('bpmnCanvas');
        if (!container) return;

        container.querySelectorAll('[data-element-id]').forEach(function(el) {
            var vis = el.querySelector('.djs-visual');
            if (!vis) return;
            var rect = vis.querySelector('rect');
            var text = vis.querySelector('text');
            if (!rect || !text) return;

            var w = parseFloat(rect.getAttribute('width'));
            var h = parseFloat(rect.getAttribute('height'));
            if (isNaN(w) || isNaN(h) || w < 500 || h < 80) return;

            // Check if this is a pool (has separator path) or lane (large rect with rotated text)
            var path = vis.querySelector('path');
            var transform = text.getAttribute('transform') || '';
            var isRotated = transform.indexOf('matrix') !== -1 && (transform.indexOf('-1') !== -1 || transform.indexOf(' -1 ') !== -1);
            if (!isRotated) return;

            // Already moved? Check marker
            if (el.getAttribute('data-rtl-applied') === '1') return;
            el.setAttribute('data-rtl-applied', '1');

            var elementId = el.getAttribute('data-element-id') || '';
            var isPool = elementId.toLowerCase().indexOf('pool') !== -1 ||
                         elementId.toLowerCase().indexOf('participant') !== -1 ||
                         (path && path.getAttribute('d') && path.getAttribute('d').indexOf('M30,') !== -1);

            // For lanes: check by element type in bpmn-js registry
            if (!isPool && modeler) {
                try {
                    var regEl = modeler.get('elementRegistry').get(elementId);
                    if (regEl && regEl.type === 'bpmn:Participant') isPool = true;
                } catch(e) {}
            }

            // Move separator path from left (M30,0 L30,h) to right (M(w-30),0 L(w-30),h)
            if (path) {
                var d = path.getAttribute('d');
                if (d && d.indexOf('M30,') !== -1) {
                    var newX = (w - 30).toFixed(0);
                    path.setAttribute('d', 'M' + newX + ',0L' + newX + ',' + h.toFixed(0));
                }
            }

            // For lanes without a separator path, add one on the right side
            if (!isPool && !path) {
                var ns = 'http://www.w3.org/2000/svg';
                var newPath = document.createElementNS(ns, 'path');
                var laneLineX = (w - 60).toFixed(0); // inside pool header area
                newPath.setAttribute('d', 'M' + laneLineX + ',0L' + laneLineX + ',' + h.toFixed(0));
                newPath.setAttribute('style', 'fill: none; stroke-linecap: round; stroke-linejoin: round; stroke: rgb(0, 0, 0); stroke-width: 1.5px;');
                vis.insertBefore(newPath, text);
            }

            // Centre the label in the right-hand header strip.
            // bpmn-js originally centres the label with text-anchor:middle and
            // a per-tspan x offset equal to the box centre (e.g. 98px for a
            // lane, 238px for a pool). Under our 90° rotation that leftover x
            // maps to screen-Y and shoves the label DOWN the lane by that many
            // px — different per label, which is the scattered "text position"
            // bug. Reset every tspan to the local origin, then rotate and
            // centre: f = h/2 centres along the lane height, text-anchor:middle
            // centres the run, and dominant-baseline:central puts the glyph
            // centre on the baseline so it sits in the middle of the strip.
            var textX = isPool ? (w - 15) : (w - 45);
            var laneTspans = text.querySelectorAll('tspan');
            for (var ti = 0; ti < laneTspans.length; ti++) {
                laneTspans[ti].setAttribute('x', '0');
                laneTspans[ti].setAttribute('y', '0');
                laneTspans[ti].removeAttribute('dy');
            }
            text.setAttribute('text-anchor', 'middle');
            text.setAttribute('dominant-baseline', 'central');
            text.setAttribute('transform', 'matrix(0 1 -1 0 ' + textX.toFixed(1) + ' ' + (h / 2).toFixed(1) + ')');
        });
    }

    function applyRtlToArrows() {
        if (!isRtlMode() || !modeler) return;

        var container = document.getElementById('bpmnCanvas');
        if (!container) return;
        var registry = modeler.get('elementRegistry');

        // Fix sequence flow arrows: ensure they connect left-edge-to-right-edge for RTL
        registry.filter(function(e) {
            return e.type === 'bpmn:SequenceFlow' || e.type === 'bpmn:MessageFlow';
        }).forEach(function(flow) {
            var source = flow.source;
            var target = flow.target;
            if (!source || !target) return;

            var waypoints = flow.waypoints;
            if (!waypoints || waypoints.length < 2) return;

            var first = waypoints[0];
            var last = waypoints[waypoints.length - 1];

            // For same-lane RTL flows: source should connect from LEFT edge, target from RIGHT edge
            // Source left edge = source.x, Target right edge = target.x + target.width
            var srcCenterY = source.y + (source.height || 0) / 2;
            var tgtCenterY = target.y + (target.height || 0) / 2;
            var sameLane = Math.abs(srcCenterY - tgtCenterY) < 20;

            if (sameLane && source.x > target.x) {
                // RTL same-lane: source is to the right of target
                var srcLeftX = source.x;
                var tgtRightX = target.x + (target.width || 0);

                // Only fix if arrows seem to go wrong direction (right edge to left edge = LTR)
                if (first.x > srcLeftX + 10) {
                    first.x = srcLeftX;
                    first.y = srcCenterY;
                }
                if (last.x < tgtRightX - 10) {
                    last.x = tgtRightX;
                    last.y = tgtCenterY;
                }
            }
        });
    }

    // ── RTL support: mirror text annotations to the right ──────────────
    // bpmn-js draws the annotation bracket on the LEFT edge and starts the text
    // there (text-anchor:start, tspan x≈7). Under the Arabic page's RTL
    // direction the text's start sits at the left bracket and the Arabic flows
    // leftward OUT of the box, clipping — the "garbled"/misaligned annotation
    // seen in Arabic diagrams. Mirror it: move the bracket to the right edge and
    // right-align the text so it reads correctly inside the box. Idempotent
    // (always recomputed from the element's own width/height), so it is safe to
    // re-run on every re-render. Shared by the live canvas and the SVG export.
    function rtlFixAnnotationVisual(vis, w, h) {
        if (!vis || isNaN(w) || isNaN(h)) return;
        var path = vis.querySelector('path');
        // Right-side bracket: spine just outside the right edge, ticks pointing in.
        if (path) path.setAttribute('d', 'm ' + w + ',0 l 10,0 l 0,' + h + ' l -10,0');
        var text = vis.querySelector('text');
        if (!text) return;
        // RTL reading starts at the right (the bracket). Under direction:rtl,
        // text-anchor:start anchors the text's START — its RIGHT edge — so we
        // pin it just inside the right edge and let it flow leftward from the
        // bracket. (text-anchor:end anchors the LEFT edge instead, which makes
        // the text overshoot rightward past the bracket — the mis-aligned
        // starting point.)
        text.setAttribute('text-anchor', 'start');
        text.setAttribute('direction', 'rtl');
        var tspans = text.querySelectorAll('tspan');
        for (var i = 0; i < tspans.length; i++) {
            tspans[i].setAttribute('x', (w - 5).toFixed(1));
        }
    }

    function applyRtlToTextAnnotations() {
        if (!isRtlMode() || !modeler) return;
        var registry = modeler.get('elementRegistry');
        registry.filter(function(e) { return e.type === 'bpmn:TextAnnotation'; })
            .forEach(function(ann) {
                var gfx;
                try { gfx = registry.getGraphics(ann.id); } catch (e) { return; }
                if (!gfx) return;
                rtlFixAnnotationVisual(gfx.querySelector('.djs-visual'), ann.width, ann.height);
            });
    }

    // Import BPMN XML into modeler
    async function importBPMN(xml) {
        try {
            hideError();
            xml = decodeXmlEntities(xml);
            await modeler.importXML(xml);
            await safeFitViewport(modeler);

            // Apply RTL transformations for Arabic
            if (isRtlMode()) {
                setTimeout(function() {
                    applyRtlToPoolsAndLanes();
                    applyRtlToArrows();
                    applyRtlToTextAnnotations();
                }, 100);
            }

            setStatus(S('diagramLoadedSuccess', 'Diagram loaded successfully'));
            return true;
        } catch (err) {
            console.error('Failed to import BPMN:', err);
            showError(S('importFailed', 'Failed to import BPMN') + ': ' + err.message);
            return false;
        }
    }

    // Generate BPMN via AI
    async function generateBPMN() {
        const title = $('processTitle').value.trim();
        const description = $('processDescription').value.trim();

        if (!title) {
            showError(S('enterProcessName', 'Please enter a process name'));
            return;
        }

        showLoading(true);
        setStatus(S('generatingAI', 'Generating BPMN diagram with AI...'));
        hideError();

        try {
            console.log('Sending request to /AI/GenerateBPMN with:', { title, description });

            const response = await fetch('/AI/GenerateBPMN', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({
                    title: title,
                    description: description || title,
                    steps: [],
                    // "Vertical layout" toggle from the AI generator panel — the
                    // server transposes the generated diagram top-to-bottom.
                    vertical: !!(document.getElementById('bpmnVertical') && document.getElementById('bpmnVertical').checked)
                })
            });

            console.log('Response status:', response.status);
            const data = await response.json();
            console.log('Response data:', data);

            if (data.success && data.bpmnXml) {
                console.log('Importing BPMN XML...');
                const success = await importBPMN(data.bpmnXml);
                if (success) {
                    setStatus(S('aiGenerated', 'AI generated diagram loaded successfully'));
                }
            } else {
                console.error('Generation failed:', data.error);
                showError(data.error || S('generateFailed', 'Failed to generate BPMN'));
                setStatus(S('generationFailed', 'Generation failed'));
            }
        } catch (err) {
            console.error('AI generation error:', err);
            showError(S('networkError', 'Network error') + ': ' + err.message);
            setStatus(S('generationFailed', 'Generation failed'));
        } finally {
            console.log('Hiding loading overlay');
            showLoading(false);
        }
    }

    // Refine existing diagram via AI
    async function refineBPMN() {
        const instructions = $('refineInstructions').value.trim();
        if (!instructions) {
            showError(S('enterRefinement', 'Please enter refinement instructions'));
            return;
        }

        const currentXml = await getCurrentXML();
        if (!currentXml) {
            showError(S('noDiagramToRefine', 'No diagram to refine'));
            return;
        }

        showLoading(true);
        setStatus(S('refiningAI', 'Refining diagram with AI...'));
        hideError();

        try {
            const response = await fetch('/AI/RefineBPMN', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify({
                    currentBpmnXml: currentXml,
                    instructions: instructions
                })
            });

            const data = await response.json();

            if (data.success && data.bpmnXml) {
                const success = await importBPMN(data.bpmnXml);
                if (success) {
                    setStatus(S('diagramRefined', 'Diagram refined successfully'));
                    $('refineInstructions').value = '';
                }
            } else {
                showError(data.error || S('refineFailed', 'Failed to refine BPMN'));
                setStatus(S('refinementFailed', 'Refinement failed'));
            }
        } catch (err) {
            console.error('AI refinement error:', err);
            showError(S('networkError', 'Network error') + ': ' + err.message);
            setStatus(S('refinementFailed', 'Refinement failed'));
        } finally {
            showLoading(false);
        }
    }

    // Export functions
    async function exportXML() {
        const xml = await getCurrentXML();
        if (xml) {
            downloadFile(xml, 'diagram.bpmn', 'application/xml');
            setStatus(S('xmlExported', 'BPMN XML exported'));
        }
    }

    // Apply RTL transforms to exported SVG string
    function applyRtlToSvgString(svgStr) {
        if (!isRtlMode()) return svgStr;

        var parser = new DOMParser();
        var doc = parser.parseFromString(svgStr, 'image/svg+xml');

        doc.querySelectorAll('[data-element-id]').forEach(function(el) {
            var vis = el.querySelector('.djs-visual');
            if (!vis) return;

            // Text annotations have no <rect>, so the pool/lane logic below
            // skips them — mirror them here (bracket→right, text right-aligned),
            // reading their bounds from the live registry.
            var annId = el.getAttribute('data-element-id') || '';
            var regAnn = null;
            try { regAnn = modeler.get('elementRegistry').get(annId); } catch (e) {}
            if (regAnn && regAnn.type === 'bpmn:TextAnnotation') {
                rtlFixAnnotationVisual(vis, regAnn.width, regAnn.height);
                return;
            }

            var rect = vis.querySelector('rect');
            var text = vis.querySelector('text');
            if (!rect || !text) return;

            var w = parseFloat(rect.getAttribute('width'));
            var h = parseFloat(rect.getAttribute('height'));
            if (isNaN(w) || isNaN(h) || w < 500 || h < 80) return;

            var transform = text.getAttribute('transform') || '';
            var isRotated = transform.indexOf('matrix') !== -1;
            if (!isRotated) return;
            if (el.getAttribute('data-rtl-applied') === '1') return;

            var path = vis.querySelector('path');
            var elementId = el.getAttribute('data-element-id') || '';
            var isPool = elementId.toLowerCase().indexOf('pool') !== -1 ||
                         elementId.toLowerCase().indexOf('participant') !== -1 ||
                         (path && path.getAttribute('d') && path.getAttribute('d').indexOf('M30,') !== -1);

            // Move pool separator
            if (path) {
                var d = path.getAttribute('d');
                if (d && d.indexOf('M30,') !== -1) {
                    var newX = (w - 30).toFixed(0);
                    path.setAttribute('d', 'M' + newX + ',0L' + newX + ',' + h.toFixed(0));
                }
            }

            // Add lane separator
            if (!isPool && !path) {
                var ns = 'http://www.w3.org/2000/svg';
                var newPath = doc.createElementNS(ns, 'path');
                var laneLineX = (w - 60).toFixed(0);
                newPath.setAttribute('d', 'M' + laneLineX + ',0L' + laneLineX + ',' + h.toFixed(0));
                newPath.setAttribute('style', 'fill: none; stroke-linecap: round; stroke-linejoin: round; stroke: rgb(0, 0, 0); stroke-width: 1.5px;');
                vis.insertBefore(newPath, text);
            }

            // Centre the label in the header strip (see applyRtlToPoolsAndLanes
            // for the why): reset the tspans' leftover centring offset, then
            // rotate and centre along the lane height + strip.
            var textX = isPool ? (w - 15) : (w - 45);
            var exTspans = text.querySelectorAll('tspan');
            for (var ti = 0; ti < exTspans.length; ti++) {
                exTspans[ti].setAttribute('x', '0');
                exTspans[ti].setAttribute('y', '0');
                exTspans[ti].removeAttribute('dy');
            }
            text.setAttribute('text-anchor', 'middle');
            text.setAttribute('dominant-baseline', 'central');
            text.setAttribute('transform', 'matrix(0 1 -1 0 ' + textX.toFixed(1) + ' ' + (h / 2).toFixed(1) + ')');
            el.setAttribute('data-rtl-applied', '1');
        });

        return new XMLSerializer().serializeToString(doc);
    }

    async function exportSVG() {
        try {
            const { svg } = await modeler.saveSVG();
            downloadFile(applyRtlToSvgString(svg), 'diagram.svg', 'image/svg+xml');
            setStatus(S('svgExported', 'SVG exported'));
        } catch (err) {
            showError(S('svgExportFailed', 'Failed to export SVG') + ': ' + err.message);
        }
    }

    async function exportPNG() {
        try {
            const { svg: rawSvg } = await modeler.saveSVG();
            const svg = applyRtlToSvgString(rawSvg);

            // Create a canvas to convert SVG to PNG
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            const img = new Image();

            img.onload = function() {
                canvas.width = img.width;
                canvas.height = img.height;
                ctx.fillStyle = 'white';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                ctx.drawImage(img, 0, 0);

                canvas.toBlob(function(blob) {
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = 'diagram.png';
                    a.click();
                    URL.revokeObjectURL(url);
                    setStatus(S('pngExported', 'PNG exported'));
                });
            };

            img.src = 'data:image/svg+xml;base64,' + btoa(unescape(encodeURIComponent(svg)));
        } catch (err) {
            showError(S('pngExportFailed', 'Failed to export PNG') + ': ' + err.message);
        }
    }

    function downloadFile(content, filename, mimeType) {
        const blob = new Blob([content], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        URL.revokeObjectURL(url);
    }

    // File import
    function handleFileImport(file) {
        const reader = new FileReader();
        reader.onload = async (e) => {
            await importBPMN(e.target.result);
        };
        reader.readAsText(file);
    }

    // Zoom controls
    function zoomIn() {
        modeler.get('zoomScroll').stepZoom(1);
    }

    function zoomOut() {
        modeler.get('zoomScroll').stepZoom(-1);
    }

    function fitView() {
        // Defensive — user can click Fit while the canvas is still
        // collapsing/animating. Skip silently if the SVG has no size.
        try { modeler.get('canvas').zoom('fit-viewport'); } catch (e) { /* ignore non-finite */ }
    }

    // Undo/Redo
    function undo() {
        modeler.get('commandStack').undo();
    }

    function redo() {
        modeler.get('commandStack').redo();
    }

    // New diagram. Replaced native confirm() with SweetAlert so the
    // dialog matches the brand chrome and renders correctly in RTL.
    async function newDiagram() {
        if (typeof Swal !== 'undefined') {
            const r = await Swal.fire({
                title: S('newDiagramConfirmTitle', 'Start a new diagram?'),
                text:  S('newDiagramConfirm', 'Current changes will be lost.'),
                icon: 'warning', showCancelButton: true,
                confirmButtonText: S('newDiagramConfirmBtn', 'Start new'),
                cancelButtonText:  S('cancelBtn', 'Cancel'),
                confirmButtonColor: '#005B99', cancelButtonColor: '#64748b'
            });
            if (!r.isConfirmed) return;
        } else if (!confirm(S('newDiagramConfirm', 'Create a new diagram? Current changes will be lost.'))) {
            return;
        }
        await importBPMN(EMPTY_DIAGRAM);
        setStatus(S('newDiagramCreated', 'New diagram created'));
    }

    // Focus mode toggle – maximize / restore the diagram canvas
    function toggleFocusMode() {
        const page = $('bpmnPage');
        if (!page) return;
        page.classList.toggle('focus-mode');

        // After the CSS transition, tell bpmn-js to recalculate its viewport
        setTimeout(() => {
            if (modeler) {
                try { modeler.get('canvas').resized(); } catch (_) { /* ignore */ }
                modeler.get('canvas').zoom('fit-viewport');
            }
        }, 120);

        // Update button icon
        const btn = $('btnFocus');
        if (btn) {
            const isFocused = page.classList.contains('focus-mode');
            btn.title = isFocused ? S('exitFocusMode', 'Exit Focus Mode') : S('focusMode', 'Focus Mode');
        }
    }

    // Toggle properties panel
    function togglePropertiesPanel() {
        const panel = $('propertiesPanel');
        const canvas = $('bpmnCanvas');
        const btn = $('btnToggleProperties');

        if (panel && canvas && btn) {
            const isHidden = panel.hasAttribute('hidden');
            if (isHidden) {
                panel.removeAttribute('hidden');
                canvas.classList.add('with-properties');
                btn.classList.add('active');
            } else {
                panel.setAttribute('hidden', '');
                canvas.classList.remove('with-properties');
                btn.classList.remove('active');
            }
        }
    }

    // Toggle minimap
    function toggleMinimap() {
        const minimap = $('minimapContainer');
        const btn = $('btnToggleMinimap');

        if (minimap && btn) {
            const isHidden = minimap.hasAttribute('hidden');
            if (isHidden) {
                minimap.removeAttribute('hidden');
                btn.classList.add('active');
                initMinimap();
            } else {
                minimap.setAttribute('hidden', '');
                btn.classList.remove('active');
            }
        }
    }

    // Initialize minimap (simplified version)
    function initMinimap() {
        const minimap = $('minimapContainer');
        if (!minimap || !modeler) return;

        // Create a simple minimap by rendering a scaled-down version
        const canvas = modeler.get('canvas');
        const viewbox = canvas.viewbox();

        // This is a placeholder - full minimap would require diagram-js-minimap module
        minimap.innerHTML = '<div class="flex items-center justify-center h-full text-xs text-gray-500">Minimap Preview</div>';
    }

    // Safe event listener helper (avoids TypeError on missing elements)
    function on(id, event, handler) {
        const el = $(id);
        if (el) el.addEventListener(event, handler);
    }

    // ── Coloring helpers ──────────────────────────────────────────────
    let selectedColor = null; // { fill, stroke } or null

    function getSelectedElements() {
        if (!modeler) return [];
        const selection = modeler.get('selection');
        return selection.get() || [];
    }

    function applyColorToSelection(fill, stroke) {
        const elements = getSelectedElements();
        if (elements.length === 0) {
            showError(S('selectElements', 'Select one or more elements first'));
            setTimeout(hideError, 2500);
            return;
        }
        const modeling = modeler.get('modeling');
        modeling.setColor(elements, { fill: fill, stroke: stroke });
        setStatus(S('colorApplied', 'Color applied to {0} element(s)').replace('{0}', elements.length));
        setTimeout(() => setStatus(S('readyEdit', 'Ready')), 2000);
    }

    function resetColorOnSelection() {
        const elements = getSelectedElements();
        if (elements.length === 0) {
            showError(S('selectElements', 'Select one or more elements first'));
            setTimeout(hideError, 2500);
            return;
        }
        const modeling = modeler.get('modeling');
        modeling.setColor(elements, { fill: undefined, stroke: undefined });
        setStatus(S('colorReset', 'Color reset on {0} element(s)').replace('{0}', elements.length));
        setTimeout(() => setStatus(S('readyEdit', 'Ready')), 2000);
    }

    // Expose coloring API globally for inline color-picker buttons
    window.bpmnApplyColor = applyColorToSelection;
    window.bpmnResetColor = resetColorOnSelection;

    // ── Font helpers ──────────────────────────────────────────────────
    async function reinitWithFont() {
        if (!modeler) return;
        // Save current diagram. `let` (not `const`) — it gets reassigned to
        // the entity-decoded XML below before re-import. With `const` the
        // reassignment threw "Assignment to constant variable", which (since
        // the old modeler was already destroyed) aborted every font change
        // and left the canvas blank.
        let xml = await getCurrentXML();
        if (!xml) return;

        // Destroy old instance
        modeler.destroy();

        // Recreate with updated textRenderer config
        const canvas = $('bpmnCanvas');
        modeler = new BpmnJS({
            container: canvas,
            keyboard: { bindTo: document },
            bpmnRenderer: { defaultFillColor: '#ffffff', defaultStrokeColor: '#000000' },
            gridSnapping: { active: true, snapOnResize: true, snapOnCreate: true },
            textRenderer: {
                defaultStyle:  { fontSize: currentFontSize, fontFamily: currentFontFamily },
                externalStyle: { fontSize: currentFontSize, fontFamily: currentFontFamily }
            }
        });

        // Re-import diagram
        xml = decodeXmlEntities(xml);
        await modeler.importXML(xml);
        window.bpmnModeler = modeler;
        await safeFitViewport(modeler);
        // Re-attach drag handle after modeler recreation
        setTimeout(makePaletteDraggable, 300);
        setStatus(S('fontUpdated', 'Font updated'));
        setTimeout(() => setStatus(S('readyEdit', 'Ready')), 2000);
    }

    async function applyFontSize(size) {
        currentFontSize = size;
        // Update toolbar label
        const lbl = $('currentFontSizeLabel');
        if (lbl) lbl.textContent = size + 'px';
        await reinitWithFont();
    }

    async function applyFontFamily(family, displayName) {
        currentFontFamily = family;
        // Update toolbar label
        const lbl = $('currentFontFamilyLabel');
        if (lbl) lbl.textContent = displayName || family.split(',')[0].replace(/'/g, '');
        await reinitWithFont();
    }

    // Expose font API globally
    window.bpmnApplyFontSize = applyFontSize;
    window.bpmnApplyFontFamily = applyFontFamily;

    // Wire up event listeners
    function setupEventListeners() {
        // AI buttons
        on('btnAiGenerate', 'click', generateBPMN);
        on('btnAiRefine', 'click', refineBPMN);

        // Toolbar buttons
        on('btnNew', 'click', newDiagram);
        on('btnUndo', 'click', undo);
        on('btnRedo', 'click', redo);
        on('btnZoomIn', 'click', zoomIn);
        on('btnZoomOut', 'click', zoomOut);
        on('btnFit', 'click', fitView);
        on('btnToggleProperties', 'click', togglePropertiesPanel);
        on('btnToggleMinimap', 'click', toggleMinimap);
        on('btnExportXml', 'click', exportXML);
        on('btnExportSvg', 'click', exportSVG);
        on('btnExportPng', 'click', exportPNG);
        on('btnFocus', 'click', toggleFocusMode);

        // Import buttons
        on('btnImportXml', 'click', () => $('importBpmnFile').click());
        $('importBpmnFile')?.addEventListener('change', (e) => {
            if (e.target.files[0]) {
                handleFileImport(e.target.files[0]);
                e.target.value = '';
            }
        });

        // Visio file
        $('visioFile')?.addEventListener('change', async (e) => {
            if (e.target.files[0]) {
                showLoading(true);
                setStatus(S('convertingVisio', 'Converting Visio file...'));

                const formData = new FormData();
                formData.append('file', e.target.files[0]);

                try {
                    const formHeaders = { 'RequestVerificationToken': getAntiForgeryToken() };
                    const response = await fetch('/AI/ConvertVisio', {
                        method: 'POST',
                        headers: formHeaders,
                        body: formData
                    });
                    const data = await response.json();

                    if (data.success && data.bpmnXml) {
                        await importBPMN(data.bpmnXml);
                        setStatus(S('visioConverted', 'Visio file converted successfully'));
                    } else {
                        showError(data.error || S('visioConvertFailed', 'Failed to convert Visio file'));
                    }
                } catch (err) {
                    showError(S('visioError', 'Visio conversion error') + ': ' + err.message);
                } finally {
                    showLoading(false);
                    e.target.value = '';
                }
            }
        });
    }

    // Keyboard shortcuts
    function setupKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // Ctrl/Cmd + S - Save as XML
            if ((e.ctrlKey || e.metaKey) && e.key === 's') {
                e.preventDefault();
                exportXML();
            }
            // Ctrl/Cmd + E - Export SVG
            if ((e.ctrlKey || e.metaKey) && e.key === 'e') {
                e.preventDefault();
                exportSVG();
            }
            // Ctrl/Cmd + P - Toggle Properties
            if ((e.ctrlKey || e.metaKey) && e.key === 'p') {
                e.preventDefault();
                togglePropertiesPanel();
            }
            // Ctrl/Cmd + M - Toggle Minimap
            if ((e.ctrlKey || e.metaKey) && e.key === 'm') {
                e.preventDefault();
                toggleMinimap();
            }
            // F11 - Toggle Focus Mode
            if (e.key === 'F11') {
                e.preventDefault();
                toggleFocusMode();
            }
        });
    }

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', async () => {
        await initModeler();
        setupEventListeners();
        setupKeyboardShortcuts();
        console.log('BPMN Modeler initialized successfully');
        console.log('Keyboard shortcuts: Ctrl+S (Save), Ctrl+E (Export SVG), Ctrl+P (Properties), Ctrl+M (Minimap), F11 (Focus)');
    });

})();

