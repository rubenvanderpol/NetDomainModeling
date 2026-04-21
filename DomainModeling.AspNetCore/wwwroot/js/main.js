/**
 * Main entry point — wires up data loading, navigation, sidebar, and views.
 */
import { esc, escAttr, shortName, ALL_SECTIONS, SECTION_META, SECTION_TO_DIAGRAM_KIND } from './helpers.js';
import { renderDetailView } from './views.js';
import { renderUbiquitousLanguageView, mountUbiquitousLanguage } from './ubiquitous-language.js';
import {
  renderDiagramView, initDiagram, diagramZoom, diagramFit, diagramResetLayout, diagramToggleKind, diagramShowAll,
  diagramDownloadSvg, diagramToggleAliases, diagramToggleLayers, diagramToggleEdgeKind, diagramToggleEdgeFilter,
  diagramToggleKindFilter, diagramShowAllKinds, diagramHideAllKinds, setDiagramLayoutBaseUrl, setServerDiagramLayoutCache,
  isDiagramNodeHidden, getDiagramState, metadataImpliesDiagramHiddenByDefault, reapplyDiagramVisibilityAfterMetadataChange,
  removeLegacyHiddenNodeId,
} from './diagram.js';

const API_URL = window.__config?.apiUrl || '/domain-model/json';
const BASE_URL = API_URL.replace(/\/json$/, '');
const TESTING_MODE = window.__config?.testingMode === true;
const FEATURE_EDITOR_MODE = window.__config?.featureEditorMode === true;
const TRACE_VIEW_MODE = window.__config?.traceViewMode === true;

let testingModule = null; // lazy-loaded when testing mode is on
let featureEditorModule = null; // lazy-loaded when feature editor mode is on
let traceModule = null; // lazy-loaded when trace view is on

// Custom metadata (alias / description) per fullName
let metadata = {};

const STORAGE_KEY = 'domainModelSelectedContexts';
// Legacy browser persistence key (migration-only).
const METADATA_STORAGE_KEY = 'domainModelMetadata';

let data = null;
let selectedContextNames = new Set();
let currentCtx = null; // merged view of selected contexts
let currentView = 'diagram';
let currentDetail = null;
let availableExports = [];

function saveSelection() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify([...selectedContextNames]));
}

function loadSelection() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) return new Set(JSON.parse(stored));
  } catch { /* ignore corrupt data */ }
  return null;
}

// ── Bootstrap ────────────────────────────────────────

async function init() {
  try {
    const res = await fetch(API_URL);
    data = await res.json();

    // One-time migration: browser localStorage -> server disk folder.
    // We only migrate entries that don't yet exist on the server to avoid overwriting.
    let legacyMetadata = {};
    try {
      const stored = localStorage.getItem(METADATA_STORAGE_KEY);
      if (stored) legacyMetadata = JSON.parse(stored);
    } catch { /* ignore corrupt data */ }

    try {
      const metaRes = await fetch(`${BASE_URL}/metadata`);
      let serverMeta = {};
      if (metaRes.ok) {
        serverMeta = await metaRes.json();
      }

      metadata = { ...legacyMetadata, ...serverMeta };

      const keysToMigrate = Object.keys(legacyMetadata || {})
        .filter((k) => !serverMeta || !serverMeta[k]);

      if (keysToMigrate.length > 0) {
        await Promise.allSettled(keysToMigrate.map(async (fullName) => {
          const entry = legacyMetadata[fullName];
          return fetch(`${BASE_URL}/metadata/${encodeURIComponent(fullName)}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(entry),
          });
        }));

        try { localStorage.removeItem(METADATA_STORAGE_KEY); } catch { /* ignore */ }
      }
    } catch { /* metadata endpoint optional */ 
      // If the server metadata endpoint is unreachable, fall back to legacy localStorage for display.
      metadata = legacyMetadata;
    }
    window.__metadata = metadata;

    setDiagramLayoutBaseUrl(BASE_URL);
    try {
      const layoutRes = await fetch(`${BASE_URL}/diagram-layout`);
      if (layoutRes.ok) {
        setServerDiagramLayoutCache(await layoutRes.json());
      }
    } catch { /* diagram-layout endpoint optional */ }

    // Load available exports
    try {
      const exportsRes = await fetch(`${BASE_URL}/exports`);
      if (exportsRes.ok) {
        availableExports = await exportsRes.json();
      }
    } catch { /* exports endpoint optional */ }

    if (data.boundedContexts && data.boundedContexts.length > 0) {
      const saved = loadSelection();
      const validNames = new Set(data.boundedContexts.map(c => c.name));
      if (saved && [...saved].some(n => validNames.has(n))) {
        for (const name of saved) {
          if (validNames.has(name)) selectedContextNames.add(name);
        }
      } else {
        for (const ctx of data.boundedContexts) {
          selectedContextNames.add(ctx.name);
        }
      }
      currentCtx = mergeContexts();
    }

    // Lazy-load testing module when testing mode is enabled
    if (TESTING_MODE) {
      testingModule = await import('./testing.js');
      await testingModule.initTesting(API_URL.replace('/json', ''));
      wireTestingGlobals();
    }

    // Lazy-load feature editor module when feature editor mode is enabled
    if (FEATURE_EDITOR_MODE) {
      featureEditorModule = await import('./feature-editor.js');
      await featureEditorModule.initFeatureEditor(BASE_URL, data);
      wireFeatureEditorGlobals();
    }

    if (TRACE_VIEW_MODE) {
      traceModule = await import('./trace.js');
      wireTraceGlobals();
    }

    render();
    initSidebarToggle();
    window.__onDiagramHiddenNodesChanged = syncExplorerDiagramHideCheckboxes;
  } catch (e) {
    document.getElementById('loadingState').textContent = 'Failed to load domain model. Check the console.';
    console.error('Failed to load domain model:', e);
  }
}

// ── Merge selected bounded contexts ──────────────────

function mergeContexts() {
  const selected = (data.boundedContexts || []).filter(c => selectedContextNames.has(c.name));
  if (selected.length === 0) return null;
  if (selected.length === 1) return selected[0];
  const merged = { name: selected.map(c => c.name).join(' + ') };
  for (const key of ALL_SECTIONS) {
    merged[key] = selected.flatMap(c => c[key] || []);
  }
  merged.relationships = selected.flatMap(c => c.relationships || []);
  return merged;
}

function toggleContext(name) {
  if (selectedContextNames.has(name)) {
    if (selectedContextNames.size > 1) selectedContextNames.delete(name);
  } else {
    selectedContextNames.add(name);
  }
  saveSelection();
  currentCtx = mergeContexts();
  currentDetail = null;
  render();
}

// ── Sidebar collapse ─────────────────────────────────

const SIDEBAR_COLLAPSED_KEY = 'domainModelSidebarCollapsed';

function initSidebarToggle() {
  const sidebar = document.getElementById('sidebar');
  const btn = document.getElementById('sidebarToggle');
  if (!sidebar || !btn) return;

  const collapsed = localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true';
  if (collapsed) {
    sidebar.classList.add('collapsed');
    btn.textContent = '\u276F';
    btn.title = 'Expand sidebar';
  }

  btn.addEventListener('click', () => {
    const isCollapsed = sidebar.classList.toggle('collapsed');
    btn.textContent = isCollapsed ? '\u276F' : '\u276E';
    btn.title = isCollapsed ? 'Expand sidebar' : 'Collapse sidebar';
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, isCollapsed);
  });
}

// ── Render ───────────────────────────────────────────

function render() {
  renderSidebar();
  renderMain();
  syncFeatureEditorViewBodyClass();
  requestAnimationFrame(() => syncExplorerDiagramHideCheckboxes());
}

/** Hide explorer sidebar in feature editor view mode so canvas matches Diagram tab width. */
function syncFeatureEditorViewBodyClass() {
  try {
    const active = FEATURE_EDITOR_MODE && featureEditorModule && currentView === 'features'
      && featureEditorModule.isFeatureEditorViewModeLayoutActive();
    document.body.classList.toggle('feature-editor-view-mode', !!active);
  } catch {
    document.body.classList.remove('feature-editor-view-mode');
  }
}
window.__syncFeatureEditorViewBodyClass = syncFeatureEditorViewBodyClass;

function renderSidebar() {
  const nav = document.getElementById('sidebarNav');
  if (!currentCtx) { nav.innerHTML = ''; return; }

  const allContexts = data.boundedContexts || [];
  const contextLabel = selectedContextNames.size === allContexts.length
    ? 'All Bounded Contexts'
    : [...selectedContextNames].join(', ');
  document.getElementById('contextName').textContent = contextLabel;

  let html = '';

  // Bounded context selector (only shown if there are multiple contexts)
  if (allContexts.length > 1) {
    html += `<div class="nav-section ctx-selector">
      <div class="nav-section-header" onclick="window.__nav.toggleSection(this)">
        <span class="dot" style="width:6px;height:6px;border-radius:50%;background:var(--accent)"></span>
        Bounded Contexts
        <span class="badge">${selectedContextNames.size}/${allContexts.length}</span>
        <span class="chevron">▼</span>
      </div>
      <div class="nav-items">
        ${allContexts.map(c => {
          const checked = selectedContextNames.has(c.name);
          return `<label class="nav-item ctx-option${checked ? ' active' : ''}" onclick="event.stopPropagation()">
            <input type="checkbox" ${checked ? 'checked' : ''}
                   onchange="window.__nav.toggleContext('${escAttr(c.name)}')" />
            <span class="ctx-name">${esc(c.name)}</span>
          </label>`;
        }).join('')}
      </div>
    </div>`;
  }

  for (const sec of SECTION_META) {
    const items = currentCtx[sec.key] || [];
    if (items.length === 0) continue;
    html += `<div class="nav-section" data-section="${sec.key}">
      <div class="nav-section-header" onclick="window.__nav.toggleSection(this)">
        <span class="dot" style="width:6px;height:6px;border-radius:50%;background:${sec.color}"></span>
        ${sec.label}
        <span class="badge">${items.length}</span>
        <span class="chevron">▼</span>
      </div>
      <div class="nav-items">
        ${items.map(item => {
          const dk = SECTION_TO_DIAGRAM_KIND[sec.key];
          const hidden = dk && isNavDiagramHidden(dk, item.fullName);
          const visTitle = hidden
            ? 'Show on main diagram'
            : 'Hide from main diagram';
          return `
          <div class="nav-item nav-item-with-diagram-toggle${isActive(sec.key, item) ? ' active' : ''}"
               data-fullname="${escAttr(item.fullName)}">
            ${dk ? `<label class="nav-diagram-visibility" title="${escAttr(visTitle)}" onclick="event.stopPropagation()">
              <input type="checkbox" ${hidden ? '' : 'checked'}
                     data-nav-kind="${escAttr(sec.key)}"
                     onchange="window.__nav.toggleDiagramVisibility('${escAttr(item.fullName)}', this.checked)" />
            </label>` : '<span class="nav-diagram-visibility-spacer"></span>'}
            <span class="nav-item-label" onclick="window.__nav.showDetail('${sec.key}', '${escAttr(item.fullName)}')">
              <span class="nav-dot" style="background:${sec.color}"></span>
              ${esc(item.name)}
            </span>
          </div>`;
        }).join('')}
      </div>
    </div>`;
  }

  html += `<div class="nav-section" style="margin-top:12px;border-top:1px solid var(--border);padding-top:8px;">
    <div class="nav-item${currentView === 'diagram' ? ' active' : ''}" onclick="window.__nav.switchTab('diagram')">
      <span class="nav-dot" style="background:var(--clr-relationship)"></span>
      Diagram
    </div>
    <div class="nav-item${currentView === 'ubiquitous-language' ? ' active' : ''}" onclick="window.__nav.switchTab('ubiquitous-language')">
      <span class="nav-dot" style="background:#c4a574"></span>
      Language
    </div>`;

  if (FEATURE_EDITOR_MODE) {
    html += `<div class="nav-item${currentView === 'features' ? ' active' : ''}" onclick="window.__nav.switchTab('features')">
      <span class="nav-dot" style="background:#f59e0b"></span>
      ⚙ Features
    </div>`;
  }

  if (TESTING_MODE) {
    html += `<div class="nav-item${currentView === 'testing' ? ' active' : ''}" onclick="window.__nav.switchTab('testing')">
      <span class="nav-dot" style="background:#c084fc"></span>
      🧪 Testing
    </div>`;
  }

  if (TRACE_VIEW_MODE) {
    html += `<div class="nav-item${currentView === 'trace' ? ' active' : ''}" onclick="window.__nav.switchTab('trace')">
      <span class="nav-dot" style="background:#22d3ee"></span>
      Trace
    </div>`;
  }

  html += `</div>`;

  nav.innerHTML = html;
}

function isActive(key, item) {
  if (currentView === 'detail' && currentDetail) {
    return currentDetail.kind === key && currentDetail.item.fullName === item.fullName;
  }
  return false;
}

function isNavDiagramHidden(diagramKind, fullName) {
  const st = getDiagramState();
  if (st && st.hiddenKinds.has(diagramKind)) return true;
  return isDiagramNodeHidden(fullName);
}

function syncExplorerDiagramHideCheckboxes() {
  for (const row of document.querySelectorAll('.nav-item-with-diagram-toggle')) {
    const fullName = row.getAttribute('data-fullname');
    const input = row.querySelector('input[type="checkbox"][data-nav-kind]');
    if (!fullName || !input) continue;
    const kindKey = input.getAttribute('data-nav-kind');
    const dk = kindKey ? SECTION_TO_DIAGRAM_KIND[kindKey] : null;
    if (!dk) continue;
    const hidden = isNavDiagramHidden(dk, fullName);
    input.checked = !hidden;
    const lab = row.querySelector('.nav-diagram-visibility');
    if (lab) {
      lab.title = hidden
        ? 'Show on main diagram'
        : 'Hide from main diagram';
    }
  }
}

function metadataEntryIsEmpty(entry) {
  if (!entry || typeof entry !== 'object') return true;
  const a = entry.alias && String(entry.alias).trim();
  const d = entry.description && String(entry.description).trim();
  if (a || d) return false;
  if (entry.hiddenOnDiagram === true || entry.hiddenOnDiagram === false) return false;
  return true;
}

async function persistMetadata(fullName, entry) {
  if (metadataEntryIsEmpty(entry)) {
    delete metadata[fullName];
  } else {
    metadata[fullName] = entry;
  }
  window.__metadata = metadata;

  try {
    if (metadataEntryIsEmpty(entry)) {
      await fetch(`${BASE_URL}/metadata/${encodeURIComponent(fullName)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ alias: null, description: null, hiddenOnDiagram: null }),
      });
    } else {
      await fetch(`${BASE_URL}/metadata/${encodeURIComponent(fullName)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(entry),
      });
    }
  } catch { /* server unreachable */ }
}

async function toggleDiagramVisibility(fullName, visible) {
  const prev = metadata[fullName] || {};
  const next = { ...prev, hiddenOnDiagram: visible ? false : true };
  removeLegacyHiddenNodeId(fullName);
  await persistMetadata(fullName, next);
  reapplyDiagramVisibilityAfterMetadataChange();
}

function renderMain() {
  const main = document.getElementById('mainContent');

  if (!currentCtx) {
    main.innerHTML = '<div class="empty-state"><h2>No Bounded Contexts</h2><p>The domain graph is empty.</p></div>';
    return;
  }

  if (currentView === 'features' && FEATURE_EDITOR_MODE && featureEditorModule) {
    main.innerHTML = featureEditorModule.renderFeatureEditorView();
    requestAnimationFrame(() => featureEditorModule.mountFeatureEditor());
    return;
  }

  if (currentView === 'testing' && TESTING_MODE && testingModule) {
    main.innerHTML = testingModule.renderTestingView();
    requestAnimationFrame(() => testingModule.mountTesting());
    return;
  }

  if (currentView === 'trace' && TRACE_VIEW_MODE && traceModule) {
    main.innerHTML = traceModule.renderTraceView();
    const selectedCtxs = (data.boundedContexts || []).filter(c => selectedContextNames.has(c.name));
    traceModule.mountTrace(currentCtx, selectedCtxs);
    return;
  }

  if (currentView === 'ubiquitous-language') {
    main.innerHTML = renderUbiquitousLanguageView();
    void mountUbiquitousLanguage(BASE_URL, selectedContextNames);
    return;
  }

  if (currentView === 'detail' && currentDetail) {
    main.innerHTML = renderDetailView(currentDetail.kind, currentDetail.item, currentCtx, metadata, saveMetadata);
    return;
  }

  // Default to diagram
  main.innerHTML = renderDiagramView();
  const selectedCtxs = (data.boundedContexts || []).filter(c => selectedContextNames.has(c.name));
  requestAnimationFrame(() => initDiagram(currentCtx, selectedCtxs));
}

// ── Metadata persistence ─────────────────────────────

async function saveMetadata(fullName, alias, description) {
  const prev = metadata[fullName] || {};
  const next = {
    ...prev,
    alias: alias && String(alias).trim() ? alias : null,
    description: description && String(description).trim() ? description : null,
  };
  if (!metadataImpliesDiagramHiddenByDefault(next)) {
    delete next.hiddenOnDiagram;
  } else if (next.hiddenOnDiagram === undefined) {
    next.hiddenOnDiagram = true;
  }
  await persistMetadata(fullName, next);
  reapplyDiagramVisibilityAfterMetadataChange();
}

// ── Navigation (exposed as window.__nav) ─────────────

function switchTab(tab) {
  currentView = tab;
  currentDetail = null;
  render();
  document.getElementById('mainContent').scrollTop = 0;
}

function showDetail(kind, fullName) {
  const items = currentCtx[kind] || [];
  const item = items.find(i => i.fullName === fullName);
  if (!item) return;
  currentView = 'detail';
  currentDetail = { kind, item };
  render();
  document.getElementById('mainContent').scrollTop = 0;
}

function navigateTo(fullName) {
  for (const sec of ALL_SECTIONS) {
    const items = currentCtx[sec] || [];
    const item = items.find(i => i.fullName === fullName);
    if (item) {
      showDetail(sec, fullName);
      return;
    }
  }
}

function toggleSection(el) {
  el.parentElement.classList.toggle('collapsed');
}

// ── Expose to global scope for onclick handlers ──────
window.__nav = {
  switchTab, showDetail, navigateTo, toggleSection, toggleContext, toggleDiagramVisibility,
};
window.__saveMetadata = saveMetadata;
window.__downloadExport = async function(name) {
  try {
    const url = `${BASE_URL}/exports/${encodeURIComponent(name)}`;
    const res = await fetch(url);
    if (!res.ok) throw new Error('Download failed');
    const blob = await res.blob();
    const disposition = res.headers.get('Content-Disposition') || '';
    const match = disposition.match(/filename="?([^"]+)"?/);
    const filename = match ? match[1] : `${name}.txt`;
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(a.href);
  } catch (err) {
    console.error('Export download failed', err);
  }
};
window.__diagram = {
  zoom: diagramZoom,
  fit: diagramFit,
  resetLayout: () => diagramResetLayout(currentCtx),
  toggleKind: diagramToggleKind,
  showAll: diagramShowAll,
  downloadSvg: diagramDownloadSvg,
  toggleAliases: diagramToggleAliases,
  toggleLayers: diagramToggleLayers,
  toggleEdgeKind: diagramToggleEdgeKind,
  toggleEdgeFilter: diagramToggleEdgeFilter,
  toggleKindFilter: diagramToggleKindFilter,
  showAllKinds: diagramShowAllKinds,
  hideAllKinds: diagramHideAllKinds,
};
window.__metadata = metadata;

// ── Testing globals (wired after lazy-load) ──────────────
function wireTestingGlobals() {
  if (!testingModule) return;
  window.__testing = {
    selectType: testingModule.selectType,
    selectMethod: testingModule.selectMethod,
    create: testingModule.create,
    deleteInstance: testingModule.deleteInstance,
    toggleInstance: testingModule.toggleInstance,
    editInstance: testingModule.editInstance,
    cancelEdit: testingModule.cancelEdit,
    saveInstance: testingModule.saveInstance,
    startInvoke: testingModule.startInvoke,
    cancelInvoke: testingModule.cancelInvoke,
    invokeMethod: testingModule.invokeInstanceMethod,
  };
}
// ── Feature editor globals (wired after lazy-load) ───────
function wireTraceGlobals() {
  if (!traceModule) return;
  window.__trace = {
    clear: () => { traceModule.clearTracePanel(); },
    reconnect: () => { void traceModule.reconnectTraceHub(); },
  };
}

function wireFeatureEditorGlobals() {
  if (!featureEditorModule) return;
  window.__featureEditor = {
    zoom: featureEditorModule.featureEditorZoom,
    fit: featureEditorModule.featureEditorFit,
    loadFeature: featureEditorModule.loadFeature,
    createFeature: featureEditorModule.createFeature,
    save: featureEditorModule.saveFeature,
    deleteFeature: featureEditorModule.deleteFeature,
    addExistingType: featureEditorModule.addExistingType,
    addAllFromBoundedContext: featureEditorModule.addAllFromBoundedContext,
    addNewType: featureEditorModule.addNewType,
    removeNode: featureEditorModule.removeNode,
    removeEdge: featureEditorModule.removeEdge,
    changeEdgeKind: featureEditorModule.changeEdgeKind,
    startConnect: featureEditorModule.startConnect,
    filterPalette: featureEditorModule.filterPalette,
    addProperty: featureEditorModule.addProperty,
    removeProperty: featureEditorModule.removeProperty,
    addMethod: featureEditorModule.addMethod,
    removeMethod: featureEditorModule.removeMethod,
    addRule: featureEditorModule.addRule,
    removeRule: featureEditorModule.removeRule,
    downloadExport: featureEditorModule.downloadExport,
    toggleRelDropdown: featureEditorModule.toggleRelDropdown,
    changeAlias: featureEditorModule.changeAlias,
    renameCustomType: featureEditorModule.renameCustomType,
    changeDescription: featureEditorModule.changeDescription,
    changeBoundedContext: featureEditorModule.changeBoundedContext,
    toggleBcDropdown: featureEditorModule.toggleBcDropdown,
    changeLayer: featureEditorModule.changeLayer,
    toggleLayerDropdown: featureEditorModule.toggleLayerDropdown,
    toggleViewMode: featureEditorModule.toggleFeatureEditorViewMode,
    toggleAliases: featureEditorModule.toggleFeatureEditorAliases,
    toggleLayers: featureEditorModule.toggleFeatureEditorLayers,
    toggleFeKindFilter: featureEditorModule.toggleFeKindFilter,
    toggleFeEdgeFilter: featureEditorModule.toggleFeEdgeFilter,
    toggleFeKind: featureEditorModule.toggleFeKind,
    showAllFeKinds: featureEditorModule.showAllFeKinds,
    hideAllFeKinds: featureEditorModule.hideAllFeKinds,
    toggleFeEdgeKind: featureEditorModule.toggleFeEdgeKind,
    onDiagramViewFlagsChanged: featureEditorModule.onDiagramViewFlagsChanged,
  };
}
// ── Go! ──────────────────────────────────────────────
init();
