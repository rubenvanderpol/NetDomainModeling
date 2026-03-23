/**
 * Main entry point — wires up data loading, navigation, sidebar, and views.
 */
import {
  esc, escAttr, shortName, ALL_SECTIONS, SECTION_META,
  mergeBoundedContextNodes, renderBoundedContextMultiDropdownInner, toggleDropdownMenu,
} from './helpers.js';
import { renderDetailView } from './views.js';
import {
  renderDiagramView, initDiagram, initDiagramBoundedContexts, mergeDiagramBoundedContexts,
  diagramZoom, diagramFit, diagramResetLayout, diagramToggleKind, diagramShowAll, diagramDownloadSvg,
  diagramToggleAliases, diagramToggleLayers, diagramToggleEdgeKind, diagramToggleEdgeFilter,
  diagramToggleKindFilter, diagramShowAllKinds, diagramHideAllKinds,
  diagramToggleBcFilter, toggleDiagramBoundedContext, diagramBoundedContextsShowAll,
} from './diagram.js';

const API_URL = window.__config?.apiUrl || '/domain-model/json';
const BASE_URL = API_URL.replace(/\/json$/, '');
const TESTING_MODE = window.__config?.testingMode === true;
const FEATURE_EDITOR_MODE = window.__config?.featureEditorMode === true;

let testingModule = null; // lazy-loaded when testing mode is on
let featureEditorModule = null; // lazy-loaded when feature editor mode is on

// Custom metadata (alias / description) per fullName
let metadata = {};

const EXPLORER_CTX_KEY = 'domainModelExplorerContexts';
/** @deprecated Migration source only; replaced by per-view keys. */
const LEGACY_CTX_KEY = 'domainModelSelectedContexts';
const METADATA_STORAGE_KEY = 'domainModelMetadata';
const DIAGRAM_CTX_KEY = 'domainModelDiagramContexts';
const FE_CTX_KEY = 'domainModelFeatureEditorContexts';

let data = null;
let explorerContextNames = new Set();
let currentCtx = null; // merged view of selected contexts
let currentView = 'diagram';
let currentDetail = null;
let availableExports = [];

function migrateContextStorageKeys(validNames) {
  const validSet = new Set(validNames);
  let explorerPayload = localStorage.getItem(EXPLORER_CTX_KEY);
  try {
    if (!explorerPayload && localStorage.getItem(LEGACY_CTX_KEY)) {
      const arr = JSON.parse(localStorage.getItem(LEGACY_CTX_KEY)).filter((n) => validSet.has(n));
      explorerPayload = JSON.stringify(arr.length ? arr : [...validSet]);
      localStorage.setItem(EXPLORER_CTX_KEY, explorerPayload);
    }
    if (!explorerPayload && validNames.length > 0) {
      explorerPayload = JSON.stringify([...validSet]);
      localStorage.setItem(EXPLORER_CTX_KEY, explorerPayload);
    }
    if (explorerPayload) {
      if (!localStorage.getItem(DIAGRAM_CTX_KEY)) localStorage.setItem(DIAGRAM_CTX_KEY, explorerPayload);
      if (!localStorage.getItem(FE_CTX_KEY)) localStorage.setItem(FE_CTX_KEY, explorerPayload);
    }
  } catch { /* ignore corrupt data */ }
}

function loadExplorerContextSet(validNames) {
  const validSet = new Set(validNames);
  try {
    const raw = localStorage.getItem(EXPLORER_CTX_KEY);
    if (raw) {
      const arr = JSON.parse(raw);
      const s = new Set(arr.filter((n) => validSet.has(n)));
      if (s.size > 0) return s;
    }
  } catch { /* ignore corrupt data */ }
  return new Set(validSet);
}

function saveExplorerContexts() {
  try {
    localStorage.setItem(EXPLORER_CTX_KEY, JSON.stringify([...explorerContextNames]));
  } catch { /* ignore */ }
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

    // Load available exports
    try {
      const exportsRes = await fetch(`${BASE_URL}/exports`);
      if (exportsRes.ok) {
        availableExports = await exportsRes.json();
      }
    } catch { /* exports endpoint optional */ }

    window.__domainData = data;

    if (data.boundedContexts && data.boundedContexts.length > 0) {
      const validNames = data.boundedContexts.map((c) => c.name);
      migrateContextStorageKeys(validNames);
      explorerContextNames = loadExplorerContextSet(validNames);
      initDiagramBoundedContexts(data.boundedContexts);
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

    render();
    initSidebarToggle();
  } catch (e) {
    document.getElementById('loadingState').textContent = 'Failed to load domain model. Check the console.';
    console.error('Failed to load domain model:', e);
  }
}

// ── Merge selected bounded contexts ──────────────────

function mergeContexts() {
  const selected = (data.boundedContexts || []).filter((c) => explorerContextNames.has(c.name));
  return mergeBoundedContextNodes(selected);
}

function toggleExplorerBoundedContext(event, name) {
  if (event) {
    event.stopPropagation();
    event.preventDefault();
  }
  if (explorerContextNames.has(name)) {
    if (explorerContextNames.size > 1) explorerContextNames.delete(name);
  } else {
    explorerContextNames.add(name);
  }
  saveExplorerContexts();
  currentCtx = mergeContexts();
  currentDetail = null;
  render();
}

function explorerBoundedContextsShowAll() {
  for (const c of (data?.boundedContexts || [])) explorerContextNames.add(c.name);
  saveExplorerContexts();
  currentCtx = mergeContexts();
  currentDetail = null;
  render();
}

function toggleExplorerBcFilter() {
  toggleDropdownMenu('explorerBcFilterMenu', 'explorerBcFilterTrigger');
}

function renderExplorerBcDropdownInner() {
  return renderBoundedContextMultiDropdownInner({
    allContexts: data?.boundedContexts || [],
    selectedSet: explorerContextNames,
    triggerId: 'explorerBcFilterTrigger',
    menuId: 'explorerBcFilterMenu',
    toggleMenuCall: 'window.__nav.toggleExplorerBcFilter()',
    toggleContextCall: 'window.__nav.toggleExplorerBoundedContext',
    showAllCall: 'window.__nav.explorerBoundedContextsShowAll()',
    triggerTitle: 'Bounded contexts in the explorer list',
  });
}

function refreshExplorerBcDropdown() {
  const el = document.getElementById('explorerBcWrap');
  if (!el) return;
  el.innerHTML = renderExplorerBcDropdownInner();
}

function refreshDiagramView() {
  if (currentView !== 'diagram') return;
  renderMain();
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
}

function renderSidebar() {
  const nav = document.getElementById('sidebarNav');
  if (!currentCtx) { nav.innerHTML = ''; return; }

  const allContexts = data.boundedContexts || [];
  const contextLabel = explorerContextNames.size === allContexts.length
    ? 'All Bounded Contexts'
    : [...explorerContextNames].join(', ');
  document.getElementById('contextName').textContent = contextLabel;

  let html = '';

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
        ${items.map(item => `
          <div class="nav-item${isActive(sec.key, item) ? ' active' : ''}"
               onclick="window.__nav.showDetail('${sec.key}', '${escAttr(item.fullName)}')"
               data-fullname="${escAttr(item.fullName)}">
            <span class="nav-dot" style="background:${sec.color}"></span>
            ${esc(item.name)}
          </div>
        `).join('')}
      </div>
    </div>`;
  }

  html += `<div class="nav-section" style="margin-top:12px;border-top:1px solid var(--border);padding-top:8px;">
    <div class="nav-item${currentView === 'diagram' ? ' active' : ''}" onclick="window.__nav.switchTab('diagram')">
      <span class="nav-dot" style="background:var(--clr-relationship)"></span>
      Diagram
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

  html += `</div>`;

  nav.innerHTML = html;
  refreshExplorerBcDropdown();
}

function isActive(key, item) {
  if (currentView === 'detail' && currentDetail) {
    return currentDetail.kind === key && currentDetail.item.fullName === item.fullName;
  }
  return false;
}

function renderMain() {
  const main = document.getElementById('mainContent');

  if (!currentCtx) {
    main.innerHTML = '<div class="empty-state"><h2>No Bounded Contexts</h2><p>The domain graph is empty.</p></div>';
    return;
  }

  if (currentView === 'features' && FEATURE_EDITOR_MODE && featureEditorModule) {
    main.innerHTML = featureEditorModule.renderFeatureEditorView();
    requestAnimationFrame(() => {
      featureEditorModule.refreshFeatureEditorBoundedContextDropdown();
      featureEditorModule.mountFeatureEditor();
    });
    return;
  }

  if (currentView === 'testing' && TESTING_MODE && testingModule) {
    main.innerHTML = testingModule.renderTestingView();
    requestAnimationFrame(() => testingModule.mountTesting());
    return;
  }

  if (currentView === 'detail' && currentDetail) {
    main.innerHTML = renderDetailView(currentDetail.kind, currentDetail.item, currentCtx, metadata, saveMetadata);
    return;
  }

  // Default to diagram
  main.innerHTML = renderDiagramView();
  const { merged, selectedCtxs } = mergeDiagramBoundedContexts(data);
  requestAnimationFrame(() => initDiagram(merged, selectedCtxs));
}

// ── Metadata persistence ─────────────────────────────

async function saveMetadata(fullName, alias, description) {
  const entry = { alias: alias || null, description: description || null };

  // Always update local state (server persistence is handled via PUT)
  if ((!alias || !alias.trim()) && (!description || !description.trim())) {
    delete metadata[fullName];
  } else {
    metadata[fullName] = entry;
  }
  window.__metadata = metadata;

  // Best-effort sync to server
  try {
    await fetch(`${BASE_URL}/metadata/${encodeURIComponent(fullName)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(entry),
    });
  } catch { /* server unreachable */ }
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
  switchTab,
  showDetail,
  navigateTo,
  toggleSection,
  toggleExplorerBoundedContext,
  explorerBoundedContextsShowAll,
  toggleExplorerBcFilter,
  refreshDiagramView,
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
  resetLayout: () => {
    const { merged } = mergeDiagramBoundedContexts(window.__domainData);
    if (merged) diagramResetLayout(merged);
  },
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
  toggleBcFilter: diagramToggleBcFilter,
  toggleBoundedContext: toggleDiagramBoundedContext,
  boundedContextsShowAll: diagramBoundedContextsShowAll,
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
    addNewType: featureEditorModule.addNewType,
    removeNode: featureEditorModule.removeNode,
    removeEdge: featureEditorModule.removeEdge,
    changeEdgeKind: featureEditorModule.changeEdgeKind,
    startConnect: featureEditorModule.startConnect,
    filterPalette: featureEditorModule.filterPalette,
    addProperty: featureEditorModule.addProperty,
    removeProperty: featureEditorModule.removeProperty,
    downloadExport: featureEditorModule.downloadExport,
    toggleRelDropdown: featureEditorModule.toggleRelDropdown,
    changeAlias: featureEditorModule.changeAlias,
    changeDescription: featureEditorModule.changeDescription,
    changeBoundedContext: featureEditorModule.changeBoundedContext,
    toggleBcDropdown: featureEditorModule.toggleBcDropdown,
    changeLayer: featureEditorModule.changeLayer,
    toggleLayerDropdown: featureEditorModule.toggleLayerDropdown,
    toggleFePaletteBcFilter: featureEditorModule.toggleFePaletteBcFilter,
    toggleFePaletteBoundedContext: featureEditorModule.toggleFePaletteBoundedContext,
    fePaletteBoundedContextsShowAll: featureEditorModule.fePaletteBoundedContextsShowAll,
  };
}
// ── Go! ──────────────────────────────────────────────
init();
