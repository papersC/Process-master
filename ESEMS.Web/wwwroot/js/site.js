// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// =============================================================
// SP_PATHBASE — IIS sub-application support
// -------------------------------------------------------------
// Reads the IIS PathBase (e.g. "/App") from the <meta name="path-base">
// tag rendered by _Layout.cshtml. UsePathBase strips PathBase from
// Request.Path on the server, but the browser still needs the full URL
// when navigating root-relative links or calling fetch('/...'). This
// block:
//   1. Exposes window.SP_PATHBASE for any code that needs to compose URLs.
//   2. Wraps window.fetch so root-relative calls auto-prefix.
//   3. On DOMContentLoaded, sweeps href|src|action|formaction attributes
//      that start with "/" but don't already start with PathBase, and
//      rewrites them in place. Covers static markup; dynamically-injected
//      links via innerHTML need to use SP_PATHBASE explicitly OR be
//      caught by a future MutationObserver if needed.
// Note: window.location.href = '/X' assignments CANNOT be intercepted —
// those need per-call prefixing (handled in bucket C).
// =============================================================
(function () {
    'use strict';

    var meta = document.querySelector('meta[name="path-base"]');
    var base = (meta && meta.getAttribute('content')) || '';
    if (base === '/') base = '';
    window.SP_PATHBASE = base;

    if (!base) return; // Root deployment — nothing to rewrite.

    function shouldPrefix(url) {
        if (typeof url !== 'string') return false;
        if (!url.length || url[0] !== '/') return false;     // not root-relative
        if (url.length > 1 && url[1] === '/') return false;  // protocol-relative //example.com/...
        if (url.indexOf(base + '/') === 0 || url === base) return false; // already prefixed
        return true;
    }

    // 1. fetch wrapper.
    if (typeof window.fetch === 'function') {
        var originalFetch = window.fetch.bind(window);
        window.fetch = function (input, init) {
            if (typeof input === 'string' && shouldPrefix(input)) {
                input = base + input;
            }
            return originalFetch(input, init);
        };
    }

    // 2. XMLHttpRequest wrapper (covers jQuery $.ajax, XHR-based libs, BPMN.js).
    if (window.XMLHttpRequest && XMLHttpRequest.prototype.open) {
        var originalOpen = XMLHttpRequest.prototype.open;
        XMLHttpRequest.prototype.open = function (method, url) {
            if (shouldPrefix(url)) {
                arguments[1] = base + url;
            }
            return originalOpen.apply(this, arguments);
        };
    }

    // 3. DOMContentLoaded sweep over server-rendered attributes.
    function rewriteAttrs(root) {
        var attrs = ['href', 'src', 'action', 'formaction'];
        for (var i = 0; i < attrs.length; i++) {
            var attr = attrs[i];
            var els = root.querySelectorAll('[' + attr + '^="/"]');
            for (var j = 0; j < els.length; j++) {
                var el = els[j];
                var v = el.getAttribute(attr);
                if (shouldPrefix(v)) {
                    el.setAttribute(attr, base + v);
                }
            }
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { rewriteAttrs(document); });
    } else {
        rewriteAttrs(document);
    }

    // 4. MutationObserver — catches dynamically-inserted markup (innerHTML
    //    template-literal insertions, live notification rendering, etc.).
    //    Scoped to childList+subtree on document.body. Only inspects the
    //    newly-added subtree, so cost scales with mutations, not DOM size.
    if (typeof MutationObserver === 'function') {
        var startObserving = function () {
            var observer = new MutationObserver(function (mutations) {
                for (var i = 0; i < mutations.length; i++) {
                    var added = mutations[i].addedNodes;
                    for (var j = 0; j < added.length; j++) {
                        var node = added[j];
                        if (node.nodeType !== 1) continue; // ELEMENT_NODE
                        rewriteAttrs(node);
                        // Also check the node itself (querySelectorAll only descendants).
                        for (var k = 0; k < 4; k++) {
                            var attr = ['href', 'src', 'action', 'formaction'][k];
                            if (node.hasAttribute && node.hasAttribute(attr)) {
                                var v = node.getAttribute(attr);
                                if (shouldPrefix(v)) node.setAttribute(attr, base + v);
                            }
                        }
                    }
                }
            });
            observer.observe(document.body, { childList: true, subtree: true });
        };
        if (document.body) {
            startObserving();
        } else {
            document.addEventListener('DOMContentLoaded', startObserving);
        }
    }
})();


// =============================================================
// Field fill-state coloring (UX/QA Batch 1)
// -------------------------------------------------------------
// Toggles the `.is-filled` class on every form control whenever
// it has a value. The matching CSS in wwwroot/css/site.css paints
// a subtle brand tint when filled and reverts when cleared.
//
// Covers text/textarea/select/date/number/file/checkbox/radio,
// runs on input/change/blur, and applies a one-time pass at
// DOMContentLoaded so server-rendered values get the class on
// first paint (e.g. populated Edit forms).
// =============================================================
(function () {
    'use strict';

    // Tag names we treat as form controls.
    var TAGS = { INPUT: 1, TEXTAREA: 1, SELECT: 1 };

    // Input types we never decorate (buttons / submit / reset / hidden / image).
    var SKIP_TYPES = {
        button: 1, submit: 1, reset: 1, hidden: 1, image: 1
    };

    function shouldHandle(el) {
        if (!el || !el.tagName) return false;
        if (!TAGS[el.tagName]) return false;
        var type = (el.type || '').toLowerCase();
        if (SKIP_TYPES[type]) return false;
        return true;
    }

    function hasValue(el) {
        var type = (el.type || '').toLowerCase();
        if (type === 'checkbox' || type === 'radio') {
            return !!el.checked;
        }
        if (type === 'file') {
            return el.files && el.files.length > 0;
        }
        // Text-like inputs, textarea, select.
        var v = el.value;
        return v !== null && v !== undefined && String(v).length > 0;
    }

    function update(el) {
        if (!shouldHandle(el)) return;
        if (hasValue(el)) {
            el.classList.add('is-filled');
        } else {
            el.classList.remove('is-filled');
        }
    }

    // Single document-level delegation — covers controls added later
    // (modals, dynamically-rendered partials, etc.) without rebinding.
    function onEvent(e) { update(e.target); }
    document.addEventListener('input', onEvent, true);
    document.addEventListener('change', onEvent, true);
    document.addEventListener('blur', onEvent, true);

    // Initial pass: server-rendered Edit forms already carry values, so
    // walk the DOM once on DOMContentLoaded and tag everything that's filled.
    function initialPass() {
        var nodes = document.querySelectorAll('input, textarea, select');
        for (var i = 0; i < nodes.length; i++) update(nodes[i]);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialPass);
    } else {
        initialPass();
    }
})();

// =============================================================
// Unsaved-changes guard
// -------------------------------------------------------------
// Any <form data-guard-unsaved> gets a beforeunload prompt if the user
// closes / navigates away after touching it. Suppressed on legitimate
// form submit. Add data-guard-skip to any input that should NOT mark
// the form dirty (e.g. search fields, view-only toggles).
// =============================================================
window.SP_GUARD_UNSAVED = (function () {
    function attach(form) {
        if (!form || form.__guardWired) return;
        form.__guardWired = true;
        var dirty = false;
        var submitting = false;
        function markDirty(e) {
            if (e && e.target && e.target.closest && e.target.closest('[data-guard-skip]')) return;
            dirty = true;
        }
        form.addEventListener('input',  markDirty);
        form.addEventListener('change', markDirty);
        form.addEventListener('submit', function () { submitting = true; });
        window.addEventListener('beforeunload', function (e) {
            if (submitting || !dirty) return;
            e.preventDefault();
            e.returnValue = '';
        });
        // Public hook so a custom Cancel button can suppress the prompt.
        form.markClean = function () { dirty = false; };
        form.markSubmitting = function () { submitting = true; };
    }
    function init() {
        document.querySelectorAll('form[data-guard-unsaved]').forEach(attach);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    return { attach: attach };
})();

// =============================================================
// Once-submit guard (anti double-submit)
// -------------------------------------------------------------
// Any <form data-once-submit> has its submit button(s) disabled the
// moment the form fires `submit`. Catches user double-clicks (the
// dominant cause of duplicate POSTs in this app). Network retries and
// scripted re-submits aren't covered — that's a separate server-side
// idempotency concern. Server-side dedup is still TODO; this is the
// cheap front-line guard. The button stays disabled until the page
// reloads or navigates (PRG handles the happy path).
//
// Adds a spinner-style "..." suffix to the button label so the user
// sees feedback. Restored if the form validation fails client-side
// (the browser cancels the submit event in that case — listen for
// the `invalid` event bubbling up).
// =============================================================
window.SP_ONCE_SUBMIT = (function () {
    function attach(form) {
        if (!form || form.__onceWired) return;
        form.__onceWired = true;
        var locked = false;
        form.addEventListener('submit', function (e) {
            // If browser-side validation fails, the submit event still fires
            // but is then cancelled. Skip disabling in that case.
            if (typeof form.checkValidity === 'function' && !form.checkValidity()) return;
            if (locked) { e.preventDefault(); return; }
            locked = true;
            var btns = form.querySelectorAll('button[type=submit], input[type=submit]');
            btns.forEach(function (b) {
                b.dataset.origLabel = b.dataset.origLabel || (b.tagName === 'INPUT' ? b.value : b.innerHTML);
                b.disabled = true;
                if (b.tagName === 'INPUT') {
                    b.value = b.dataset.origLabel + ' …';
                } else {
                    b.innerHTML = b.dataset.origLabel + ' …';
                }
            });
        });
        // Restore on bubble-up invalid (e.g. asp-validation kicked in
        // after the submit fired and ModelState returns the user to the
        // same view).
        form.addEventListener('invalid', function () {
            locked = false;
            var btns = form.querySelectorAll('button[type=submit], input[type=submit]');
            btns.forEach(function (b) {
                if (b.dataset.origLabel) {
                    b.disabled = false;
                    if (b.tagName === 'INPUT') b.value = b.dataset.origLabel;
                    else b.innerHTML = b.dataset.origLabel;
                }
            });
        }, true);
    }
    function init() {
        document.querySelectorAll('form[data-once-submit]').forEach(attach);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    return { attach: attach };
})();

// =============================================================
// Live "Still need: X" checklist
// -------------------------------------------------------------
// Drop a <div data-required-checklist data-required-fields="id:Label|id2:Label2"
//          data-text-prefix="Still need: " data-text-ok="Ready to submit.">
// onto any form. The util walks the listed input IDs on every input/change
// and updates the banner text. State flips between "missing" (yellow,
// dashed circle) and "ok" (green, check-circle).
// Empty data-required-fields = always shows "ok" (no-op).
// Field labels can be bilingual via Razor: data-required-fields="@(isRtl ? 'NameAr:الاسم' : 'NameEn:Name')|..."
// =============================================================
window.SP_LIVE_CHECKLIST = (function () {
    function update(el) {
        var spec = (el.getAttribute('data-required-fields') || '').trim();
        var prefix = el.getAttribute('data-text-prefix') || 'Still need: ';
        var okText = el.getAttribute('data-text-ok') || 'Ready to submit.';
        var sep = el.getAttribute('data-text-sep') || ' · ';
        var missing = [];
        if (spec.length) {
            spec.split('|').forEach(function (part) {
                var bits = part.split(':');
                var id = (bits[0] || '').trim();
                var label = (bits.slice(1).join(':') || id).trim();
                if (!id) return;
                var input = document.getElementById(id);
                if (!input) return;
                var v = '';
                if (input.type === 'checkbox' || input.type === 'radio') {
                    v = input.checked ? 'on' : '';
                } else {
                    v = (input.value || '').trim();
                }
                if (!v) missing.push(label);
            });
        }
        var icon = el.querySelector('[data-lucide]');
        var text = el.querySelector('.wiz-checklist-text');
        if (missing.length === 0) {
            el.setAttribute('data-state', 'ok');
            if (icon) icon.setAttribute('data-lucide', 'check-circle-2');
            if (text) text.textContent = okText;
        } else {
            el.setAttribute('data-state', 'missing');
            if (icon) icon.setAttribute('data-lucide', 'circle-dashed');
            if (text) text.textContent = prefix + missing.join(sep);
        }
        if (window.lucide) lucide.createIcons();
    }
    function init() {
        var els = document.querySelectorAll('[data-required-checklist]');
        if (els.length === 0) return;
        function refreshAll() { els.forEach(update); }
        document.addEventListener('input', refreshAll);
        document.addEventListener('change', refreshAll);
        refreshAll();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
    return { refresh: function () { document.querySelectorAll('[data-required-checklist]').forEach(update); } };
})();
