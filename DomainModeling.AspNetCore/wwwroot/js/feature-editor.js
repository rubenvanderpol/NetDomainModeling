/**
 * Feature Editor — visual canvas for designing feature diagrams.
 *
 * Reuses the diagram node/edge rendering style. Users can:
 *  - Create multiple named features (saved/loaded via API)
 *  - Add types from the existing domain model or create new ones
 *  - Add every type from a bounded context in one action (#21)
 *  - Draw relationships by dragging a line from one node to another
 *  - Drag/pan/zoom the canvas; drag a bounded-context frame to move its types (#24)
 *  - Optionally run in read-only mode (existing graph items only)
 */
import { esc, escAttr, shortName, ALL_SECTIONS, SECTION_META } from './helpers.js';
import { renderTabBar } from './tabs.js';

// ── Constants ────────────────────────────────────────
const NODE_W = 200;
const PROP_H = 17;
const HEADER_H = 26;
const NAME_H = 24;
const DIVIDER_H = 8;
const PAD = 12;

const KIND_CFG = {
  aggregate:        { stereotype: '«Aggregate»',        color: '#d4a0ff', bg: '#1f1828', border: '#7c5aa8' },
  entity:           { stereotype: '«Entity»',           color: '#7ab8ff', bg: '#161e2c', border: '#4a7bbf' },
  valueObject:      { stereotype: '«Value Object»',     color: '#4ee8ad', bg: '#142820', border: '#36a87a' },
  subType:          { stereotype: '«Sub Type»',         color: '#a0b4c8', bg: '#1a1e24', border: '#6880a0' },
  event:            { stereotype: '«Domain Event»',     color: '#fdd04e', bg: '#2a2418', border: '#b89530' },
  integrationEvent: { stereotype: '«Integration Event»', color: '#48e8d8', bg: '#14282a', border: '#30a89e' },
  commandHandlerTarget: { stereotype: '«Handles target»', color: '#f0a050', bg: '#2a2218', border: '#c07830' },
  eventHandler:     { stereotype: '«Event Handler»',    color: '#ff8ac8', bg: '#2a1824', border: '#b85888' },
  commandHandler:   { stereotype: '«Command Handler»',  color: '#ff8ac8', bg: '#2a1824', border: '#b85888' },
  queryHandler:     { stereotype: '«Query Handler»',    color: '#ff8ac8', bg: '#2a1824', border: '#b85888' },
  repository:       { stereotype: '«Repository»',       color: '#ffab5c', bg: '#2a2018', border: '#b87838' },
  service:          { stereotype: '«Service»',          color: '#bda0ff', bg: '#1e1828', border: '#7860b0' },
};

const EDGE_COLORS = {
  Contains: '#60a5fa', References: '#34d399', ReferencesById: '#34d399',
  Has: '#60a5fa', HasMany: '#60a5fa',
  Emits: '#fbbf24', Handles: '#f472b6', Manages: '#fb923c', Publishes: '#2dd4bf',
};

const RELATION_KINDS = ['Contains', 'References', 'ReferencesById', 'Has', 'HasMany', 'Emits', 'Handles', 'Manages', 'Publishes'];

const KIND_TO_SECTION = {
  aggregate: 'aggregates', entity: 'entities', valueObject: 'valueObjects',
  subType: 'subTypes',
  event: 'domainEvents', integrationEvent: 'integrationEvents',
  commandHandlerTarget: 'commandHandlerTargets', eventHandler: 'eventHandlers',
  commandHandler: 'commandHandlers', queryHandler: 'queryHandlers',
  repository: 'repositories', service: 'domainServices',
};

const SECTION_TO_KIND = {};
for (const [k, v] of Object.entries(KIND_TO_SECTION)) SECTION_TO_KIND[v] = k;

const KIND_LABELS = {
  aggregate: 'Aggregate', entity: 'Entity', valueObject: 'Value Object',
  event: 'Domain Event', integrationEvent: 'Integration Event',
  commandHandlerTarget: 'Cmd handler target', eventHandler: 'Event Handler',
  commandHandler: 'Command Handler', queryHandler: 'Query Handler',
  repository: 'Repository', service: 'Domain Service',
};

const LAYERS = ['Domain', 'Application', 'Infrastructure'];
const LAYER_COLORS = { Domain: '#a78bfa', Application: '#60a5fa', Infrastructure: '#fb923c' };

// ── Module state ─────────────────────────────────────
let baseUrl = '';
let domainData = null;   // full domain graph
let featureList = [];     // list of feature names
let currentFeatureName = null;
let currentFeatureReadOnly = false;
let st = null;            // current feature editor state
let dirty = false;
let connecting = null;    // { sourceId, mouseX, mouseY } when drawing a relation line
let featureExports = [];  // available export registrations from server

// ── Public API ───────────────────────────────────────

export async function initFeatureEditor(apiBaseUrl, data) {
  baseUrl = apiBaseUrl;
  domainData = data;
  await loadFeatureList();
  await loadFeatureExports();
}

function isReadOnlyFeature() {
  return currentFeatureReadOnly === true;
}

export function renderFeatureEditorView() {
  let html = renderTabBar('features');

  html += '<div class="fe-layout">';

  // ── Left: Feature list + type palette ──
  html += '<div class="fe-sidebar" id="feSidebar">';
  html += renderFeatureListPanel();
  html += renderPalettePanel();
  html += '</div>';

  // ── Center: Canvas ──
  html += '<div class="fe-canvas-wrap" id="feCanvasWrap">';
  if (!currentFeatureName) {
    html += '<div class="fe-empty-state">Select or create a feature to start editing.</div>';
  } else {
    html += '<div class="fe-canvas-toolbar">';
    html += `<span class="fe-feature-name">${esc(currentFeatureName)}</span>`;
    if (isReadOnlyFeature()) {
      html += '<span class="fe-mode-badge" title="Only existing domain items can be included">read-only feature mode</span>';
    }
    html += `<span class="fe-dirty-indicator" id="feDirtyIndicator" style="display:${dirty ? 'inline' : 'none'}">● unsaved</span>`;
    html += '<span class="fe-toolbar-spacer"></span>';
    html += '<button class="fe-btn" onclick="window.__featureEditor.fit()" title="Fit to view">⊡ Fit</button>';
    if (featureExports.length > 0) {
      html += '<label class="fe-export-opt" title="Append command-handler DI scaffold (ICommandHandler&lt;T&gt;) to text exports">';
      html += '<input type="checkbox" id="feRegisterCommands" /> Register commands';
      html += '</label>';
    }
    for (const exp of featureExports) {
      html += `<button class="fe-btn" onclick="window.__featureEditor.downloadExport('${escAttr(exp.name)}')" title="Download ${esc(exp.name)}">⬇ ${esc(exp.name)}</button>`;
    }
    html += `<button class="fe-btn primary" onclick="window.__featureEditor.save()" title="Save feature" id="feSaveBtn">Save</button>`;
    html += `<button class="fe-btn danger" onclick="window.__featureEditor.deleteFeature()" title="Delete feature">Delete</button>`;
    html += '</div>';
    html += '<div class="fe-canvas" id="feCanvas">';
    html += '<div class="diagram-controls">';
    html += '<button onclick="window.__featureEditor.zoom(1.25)" title="Zoom in">+</button>';
    html += '<button onclick="window.__featureEditor.zoom(0.8)" title="Zoom out">−</button>';
    html += '</div>';
    html += '<svg id="feSvg"></svg>';
    html += '</div>';
  }
  html += '</div>';

  // ── Right: Properties panel ──
  html += '<div class="fe-panel" id="fePanel">';
  html += renderPropertiesPanel();
  html += '</div>';

  html += '</div>';
  return html;
}

export function mountFeatureEditor() {
  if (!st || !currentFeatureName) return;
  renderSvg();
  fitToView();
  setupInteraction();
}

// ── Feature list management ──────────────────────────

async function loadFeatureList() {
  try {
    const res = await fetch(`${baseUrl}/features`);
    if (res.ok) featureList = await res.json();
  } catch { featureList = []; }
}

async function loadFeatureExports() {
  try {
    const res = await fetch(`${baseUrl}/features/exports`);
    if (res.ok) featureExports = await res.json();
  } catch { featureExports = []; }
}

function renderFeatureListPanel() {
  let html = '<div class="fe-section">';
  html += '<div class="fe-section-header">Features</div>';
  html += '<div class="fe-feature-list" id="feFeatureList">';
  for (const name of featureList) {
    const active = name === currentFeatureName ? ' active' : '';
    html += `<div class="fe-feature-item${active}" onclick="window.__featureEditor.loadFeature('${escAttr(name)}')">${esc(name)}</div>`;
  }
  html += '</div>';
  html += '<div class="fe-new-feature">';
  html += '<input type="text" class="fe-input" id="feNewFeatureName" placeholder="New feature name…" />';
  html += '<label class="fe-checkbox-row"><input type="checkbox" id="feNewFeatureReadOnly" /> Read-only</label>';
  html += '<button class="fe-btn primary" onclick="window.__featureEditor.createFeature()">+ Create</button>';
  html += '</div>';
  html += '</div>';
  return html;
}

// ── Type palette ─────────────────────────────────────

function renderPalettePanel() {
  let html = '<div class="fe-section">';
  html += `<div class="fe-section-header">${isReadOnlyFeature() ? 'Add Existing Types' : 'Add Types'}</div>`;
  if (currentFeatureName) {
    html += '<div class="fe-bulk-bc">';
    html += '<label class="fe-bulk-bc-label" for="feBulkBcSelect">Add entire context</label>';
    html += '<div class="fe-bulk-bc-row">';
    html += '<select class="fe-input fe-bulk-bc-select" id="feBulkBcSelect" title="Add every discovered type from this bounded context to the diagram">';
    html += '<option value="">Select context…</option>';
    for (const name of getBoundedContextNames()) {
      html += `<option value="${escAttr(name)}">${esc(name)}</option>`;
    }
    html += '</select>';
    html += '<button type="button" class="fe-btn primary fe-bulk-bc-btn" onclick="window.__featureEditor.addAllFromBoundedContext()" title="Place all types from the selected bounded context on the canvas">Add all</button>';
    html += '</div></div>';
  }
  html += '<input type="text" class="fe-input fe-search" id="fePaletteSearch" placeholder="Search types…" oninput="window.__featureEditor.filterPalette()" />';
  html += '<div class="fe-palette" id="fePalette">';
  html += renderPaletteItems('');
  html += '</div>';

  if (!isReadOnlyFeature()) {
    // Custom type creation
    html += '<div class="fe-section-header" style="margin-top:12px">Create New Type</div>';
    html += '<input type="text" class="fe-input" id="feNewTypeName" placeholder="Type name…" />';
    html += '<select class="fe-input" id="feNewTypeKind">';
    for (const [kind, label] of Object.entries(KIND_LABELS)) {
      html += `<option value="${kind}">${label}</option>`;
    }
    html += '</select>';
    html += '<button class="fe-btn" onclick="window.__featureEditor.addNewType()" style="margin-top:4px">+ Add Custom Type</button>';
  } else {
    html += '<div class="fe-readonly-hint">Custom type creation is disabled in read-only feature mode.</div>';
  }

  html += '</div>';
  return html;
}

function renderPaletteItems(filter) {
  if (!domainData) return '<div class="fe-palette-empty">No domain data loaded</div>';

  const lowerFilter = filter.toLowerCase();
  const addedIds = st ? new Set(st.nodes.map(n => n.id)) : new Set();
  let html = '';

  for (const ctx of (domainData.boundedContexts || [])) {
    for (const sec of ALL_SECTIONS) {
      const kind = SECTION_TO_KIND[sec];
      if (!kind) continue;
      const items = ctx[sec] || [];
      for (const item of items) {
        if (addedIds.has(item.fullName)) continue;
        if (lowerFilter && !item.name.toLowerCase().includes(lowerFilter) && !item.fullName.toLowerCase().includes(lowerFilter)) continue;
        const cfg = KIND_CFG[kind];
        html += `<div class="fe-palette-item" onclick="window.__featureEditor.addExistingType('${escAttr(item.fullName)}', '${kind}')" title="${esc(item.fullName)}">`;
        html += `<span class="fe-palette-dot" style="background:${cfg.color}"></span>`;
        html += `<span class="fe-palette-name">${esc(item.name)}</span>`;
        html += `<span class="fe-palette-kind">${KIND_LABELS[kind]}</span>`;
        html += '</div>';
      }
    }
  }

  if (!html) {
    html = '<div class="fe-palette-empty">No matching types found</div>';
  }
  return html;
}

// ── Properties panel ─────────────────────────────────

function renderPropertiesPanel() {
  if (!st) return '<div class="fe-panel-empty">No feature loaded.</div>';
  const readOnly = isReadOnlyFeature();

  if (st.selectedNode) {
    const n = st.nMap[st.selectedNode];
    if (!n) return renderPanelInstructions();
    const cfg = KIND_CFG[n.kind];

    let h = `<div class="fe-panel-title" style="color:${cfg.color}">${cfg.stereotype}</div>`;
    h += `<div class="fe-panel-field"><label>Name</label><div class="fe-panel-value">${esc(n.name)}</div></div>`;
    h += `<div class="fe-panel-field"><label>Full Name</label><div class="fe-panel-value">${esc(n.id)}</div></div>`;

    // Alias
    h += `<div class="fe-panel-field"><label>Alias</label>`;
    if (readOnly) {
      h += `<div class="fe-panel-value">${esc(n.alias || '—')}</div>`;
    } else {
      h += `<input type="text" class="fe-input" value="${escAttr(n.alias || '')}" placeholder="Display name override…" `;
      h += `onchange="window.__featureEditor.changeAlias('${escAttr(n.id)}', this.value)" /></div>`;
    }
    if (readOnly) h += '</div>';

    // Description
    h += `<div class="fe-panel-field"><label>Description</label>`;
    if (readOnly) {
      h += `<div class="fe-panel-value">${esc(n.description || '—')}</div></div>`;
    } else {
      h += `<textarea class="fe-input" rows="3" placeholder="Custom description…" `;
      h += `onchange="window.__featureEditor.changeDescription('${escAttr(n.id)}', this.value)">${esc(n.description || '')}</textarea></div>`;
    }

    // Bounded Context
    h += `<div class="fe-panel-field"><label>Bounded Context</label>`;
    h += readOnly
      ? `<div class="fe-panel-value">${esc(n.boundedContext || '—')}</div>`
      : renderBoundedContextDropdown(n);
    h += '</div>';

    // Layer
    h += `<div class="fe-panel-field"><label>Layer</label>`;
    h += readOnly
      ? `<div class="fe-panel-value">${esc(n.layer || '—')}</div>`
      : renderLayerDropdown(n);
    h += '</div>';

    // Properties — editable list
    h += '<div class="fe-panel-section">Properties</div>';
    if (n.structuredProps && n.structuredProps.length > 0) {
      for (let i = 0; i < n.structuredProps.length; i++) {
        const p = n.structuredProps[i];
        h += `<div class="fe-panel-prop-row">`;
        h += `<span class="fe-panel-prop-text">${esc(p.name)}: ${esc(p.type)}</span>`;
        if (!readOnly) {
          h += `<button class="fe-btn-icon" onclick="window.__featureEditor.removeProperty('${escAttr(n.id)}', ${i})" title="Remove property">✕</button>`;
        }
        h += `</div>`;
      }
    } else {
      h += '<div class="fe-panel-prop-empty">No properties</div>';
    }
    if (!readOnly) {
      h += '<div class="fe-add-prop-row">';
      h += '<input type="text" class="fe-input fe-input-sm" id="feNewPropName" placeholder="name" />';
      h += '<input type="text" class="fe-input fe-input-sm" id="feNewPropType" placeholder="type" />';
      h += `<button class="fe-btn-icon fe-btn-add" onclick="window.__featureEditor.addProperty('${escAttr(n.id)}')" title="Add property">+</button>`;
      h += '</div>';
    }

    // Emitted events (derived from Emits edges)
    const emittedEvents = getEmittedEventsForNode(n.id);
    if (emittedEvents.length > 0) {
      h += '<div class="fe-panel-section">Emitted Events</div>';
      for (const evName of emittedEvents) {
        h += `<div class="fe-panel-prop">⚡ ${esc(evName)}</div>`;
      }
    }

    // Relationships
    const rels = st.edges.filter(e => e.source === n.id || e.target === n.id);
    if (rels.length > 0) {
      h += '<div class="fe-panel-section">Relationships</div>';
      for (const e of rels) {
        const other = e.source === n.id ? shortName(e.target) : shortName(e.source);
        const dir = e.source === n.id ? '→' : '←';
        h += `<div class="fe-panel-rel">${dir} <span style="color:${EDGE_COLORS[e.kind] || '#888'}">${e.kind}</span> ${esc(other)}</div>`;
      }
    }

    // Actions
    h += '<div class="fe-panel-section">Actions</div>';
    if (!readOnly) {
      h += `<button class="fe-btn fe-connect-btn" onclick="window.__featureEditor.startConnect('${escAttr(n.id)}')" title="Drag to another node to create a relation">⟶ Draw Relation</button>`;
    }
    h += `<button class="fe-btn danger" onclick="window.__featureEditor.removeNode('${escAttr(n.id)}')" style="margin-top:4px">✕ Remove from Feature</button>`;

    return h;
  }

  if (st.selectedEdge !== null) {
    const e = st.edges[st.selectedEdge];
    if (!e) return renderPanelInstructions();

    let h = `<div class="fe-panel-title" style="color:${EDGE_COLORS[e.kind] || '#888'}">Relationship</div>`;
    h += `<div class="fe-panel-field"><label>Source</label><div class="fe-panel-value">${esc(shortName(e.source))}</div></div>`;
    h += `<div class="fe-panel-field"><label>Target</label><div class="fe-panel-value">${esc(shortName(e.target))}</div></div>`;
    h += `<div class="fe-panel-field"><label>Kind</label>`;
    h += readOnly
      ? `<div class="fe-panel-value">${esc(e.kind)}</div>`
      : renderRelKindDropdown(e.kind, st.selectedEdge);
    h += '</div>';
    if (e.label) h += `<div class="fe-panel-field"><label>Label</label><div class="fe-panel-value">${esc(e.label)}</div></div>`;
    if (!readOnly) {
      h += '<div class="fe-panel-section">Actions</div>';
      h += `<button class="fe-btn danger" onclick="window.__featureEditor.removeEdge(${st.selectedEdge})">✕ Remove Relation</button>`;
    }
    return h;
  }

  return renderPanelInstructions();
}

function renderPanelInstructions() {
  let h = '<div class="fe-panel-empty">';
  if (isReadOnlyFeature()) {
    h += '<p>Read-only mode: add existing types from the palette.</p>';
    h += '<p>Select a node to inspect details.</p>';
    h += '</div>';
    return h;
  }
  h += '<p>Click a node to inspect it.</p>';
  h += '<p>Click a node then <strong>"Draw Relation"</strong>, then click another node to connect them.</p>';
  h += '<p>Or drag from a node\'s <strong>connector port</strong> (⬤) to another node.</p>';
  h += '</div>';
  return h;
}

function refreshPanel() {
  const el = document.getElementById('fePanel');
  if (el) el.innerHTML = renderPropertiesPanel();
}

// ── Emitted Events derivation (from Emits edges) ────

function getEmittedEventsForNode(nodeId) {
  if (!st) return [];
  return st.edges
    .filter(e => e.source === nodeId && e.kind === 'Emits')
    .map(e => {
      const tgt = st.nMap[e.target];
      return tgt ? tgt.name : shortName(e.target);
    });
}

// ── Property management ──────────────────────────────

export function addProperty(nodeId) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  const nameInput = document.getElementById('feNewPropName');
  const typeInput = document.getElementById('feNewPropType');
  if (!nameInput || !typeInput) return;
  const name = nameInput.value.trim();
  const type = typeInput.value.trim() || 'string';
  if (!name) return;

  if (!n.structuredProps) n.structuredProps = [];
  n.structuredProps.push({ name, type });
  rebuildDisplayProps(n);
  nameInput.value = '';
  typeInput.value = '';
  markDirty();
  renderSvg();
  refreshPanel();
}

export function removeProperty(nodeId, idx) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !n.structuredProps) return;
  n.structuredProps.splice(idx, 1);
  rebuildDisplayProps(n);
  markDirty();
  renderSvg();
  refreshPanel();
}

function rebuildDisplayProps(n) {
  n.props = (n.structuredProps || []).map(p => p.name + ': ' + p.type);
  n.h = nodeHeight(n);
}

// ── Feature CRUD ─────────────────────────────────────

export async function createFeature() {
  const input = document.getElementById('feNewFeatureName');
  const readOnlyInput = document.getElementById('feNewFeatureReadOnly');
  if (!input) return;
  const name = input.value.trim();
  if (!name) return;
  const readOnly = readOnlyInput?.checked === true;

  // Initialize empty feature
  const feature = { readOnly, nodes: [], edges: [], positions: {} };
  try {
    const res = await fetch(`${baseUrl}/features/${encodeURIComponent(name)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(feature),
    });
    if (!res.ok) { console.error('Failed to create feature'); return; }
  } catch (e) { console.error('Failed to create feature', e); return; }

  await loadFeatureList();
  currentFeatureName = name;
  currentFeatureReadOnly = readOnly;
  loadFeatureState(feature);
  dirty = false;
  rerender();
}

export async function loadFeature(name) {
  if (dirty && !confirm('You have unsaved changes. Discard them?')) return;
  try {
    const res = await fetch(`${baseUrl}/features/${encodeURIComponent(name)}`);
    if (!res.ok) { console.error('Feature not found'); return; }
    const feature = await res.json();
    currentFeatureName = name;
    currentFeatureReadOnly = feature?.readOnly === true;
    loadFeatureState(feature);
    dirty = false;
    rerender();
  } catch (e) { console.error('Failed to load feature', e); }
}

export async function saveFeature() {
  if (!st || !currentFeatureName) return;
  const feature = serializeFeature();
  try {
    const res = await fetch(`${baseUrl}/features/${encodeURIComponent(currentFeatureName)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(feature),
    });
    if (res.ok) {
      dirty = false;
      const indicator = document.getElementById('feDirtyIndicator');
      if (indicator) indicator.style.display = 'none';
    }
  } catch (e) { console.error('Failed to save feature', e); }
}

export async function deleteFeature() {
  if (!currentFeatureName) return;
  if (!confirm(`Delete feature "${currentFeatureName}"?`)) return;
  try {
    await fetch(`${baseUrl}/features/${encodeURIComponent(currentFeatureName)}`, { method: 'DELETE' });
  } catch { /* ignore */ }
  currentFeatureName = null;
  currentFeatureReadOnly = false;
  st = null;
  dirty = false;
  await loadFeatureList();
  rerender();
}

export async function downloadExport(exportName) {
  if (!currentFeatureName) return;
  try {
    let url = `${baseUrl}/features/${encodeURIComponent(currentFeatureName)}/exports/${encodeURIComponent(exportName)}`;
    const regCmd = document.getElementById('feRegisterCommands');
    if (regCmd && regCmd.checked) {
      url += (url.includes('?') ? '&' : '?') + 'registerCommands=true';
    }
    const res = await fetch(url);
    if (!res.ok) { alert('Export failed: ' + res.statusText); return; }
    const blob = await res.blob();
    const disposition = res.headers.get('content-disposition') || '';
    const match = disposition.match(/filename="?([^"]+)"?/);
    const fileName = match ? match[1] : `${currentFeatureName}-${exportName}`;
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(a.href);
  } catch (e) { console.error('Export download failed', e); }
}

function rerender() {
  // Re-render the whole feature editor view
  const main = document.getElementById('mainContent');
  if (!main) return;
  main.innerHTML = renderFeatureEditorView();
  requestAnimationFrame(() => mountFeatureEditor());
}

// ── Feature state serialization ──────────────────────

function serializeFeature() {
  if (!st) return { readOnly: currentFeatureReadOnly, nodes: [], edges: [], positions: {} };
  const positions = {};
  for (const n of st.nodes) {
    positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
  }
  return {
    readOnly: currentFeatureReadOnly,
    nodes: st.nodes.map(n => ({
      id: n.id, name: n.name, kind: n.kind, isCustom: n.isCustom || false,
      alias: n.alias || null, description: n.description || null,
      boundedContext: n.boundedContext || '', layer: n.layer || '',
      props: n.props, structuredProps: n.structuredProps || [],
      methods: n.methods, events: n.events,
    })),
    edges: st.edges.map(e => ({
      source: e.source, target: e.target, kind: e.kind, label: e.label || '',
    })),
    positions,
  };
}

function loadFeatureState(feature) {
  currentFeatureReadOnly = feature?.readOnly === true;
  st = {
    nodes: [], edges: [], nMap: {},
    zoom: 1, panX: 0, panY: 0,
    selectedNode: null, selectedEdge: null,
  };

  // Rebuild nodes
  for (const saved of (feature.nodes || [])) {
    const cfg = KIND_CFG[saved.kind];
    if (!cfg) continue;
    const n = {
      id: saved.id,
      name: saved.name,
      kind: saved.kind,
      isCustom: saved.isCustom || false,
      alias: saved.alias || null,
      description: saved.description || null,
      boundedContext: saved.boundedContext || '',
      layer: saved.layer || '',
      cfg,
      structuredProps: saved.structuredProps || [],
      props: saved.props || [],
      methods: saved.methods || [],
      events: saved.events || [],
      x: 0, y: 0, vx: 0, vy: 0,
      w: NODE_W, h: 0,
    };
    // If we have structured props, rebuild display props from them
    if (n.structuredProps.length > 0 && n.props.length === 0) {
      n.props = n.structuredProps.map(p => p.name + ': ' + p.type);
    }
    n.h = nodeHeight(n);
    // Restore position
    const pos = feature.positions?.[n.id];
    if (pos) { n.x = pos.x; n.y = pos.y; }
    st.nodes.push(n);
    st.nMap[n.id] = n;
  }

  // Rebuild edges
  for (const saved of (feature.edges || [])) {
    if (st.nMap[saved.source] && st.nMap[saved.target]) {
      st.edges.push({ source: saved.source, target: saved.target, kind: saved.kind, label: saved.label || '' });
    }
  }

  // Auto-layout nodes without positions
  const needsLayout = st.nodes.filter(n => n.x === 0 && n.y === 0);
  if (needsLayout.length > 0 && needsLayout.length === st.nodes.length) {
    applyAutoLayout(st.nodes, st.edges, st.nMap);
  }
}

// ── Adding types ─────────────────────────────────────

/** Alias / description saved in the main explorer (GET /domain-model/metadata), not per-feature. */
function getGlobalTypeMetadata(fullName) {
  const meta = (typeof window !== 'undefined' && window.__metadata) ? window.__metadata[fullName] : null;
  if (!meta) return { alias: null, description: null };
  const alias = meta.alias && String(meta.alias).trim() ? String(meta.alias).trim() : null;
  const description = meta.description && String(meta.description).trim() ? String(meta.description).trim() : null;
  return { alias, description };
}

/**
 * Builds a feature-editor node from domain graph data. Returns null if the type is already on the canvas or unknown.
 */
function buildFeatureNodeFromDomain(fullName, kind) {
  if (!st || st.nMap[fullName]) return null;
  const cfg = KIND_CFG[kind];
  if (!cfg) return null;

  const item = findDomainItem(fullName, kind);
  const globalMeta = getGlobalTypeMetadata(fullName);

  const n = {
    id: fullName,
    name: item ? item.name : shortName(fullName),
    kind,
    isCustom: false,
    alias: globalMeta.alias,
    description: globalMeta.description || (item && item.description) || null,
    boundedContext: findDomainContext(fullName) || '',
    layer: (item && item.layer) || '',
    cfg,
    structuredProps: item ? (item.properties || []).map(p => ({ name: p.name, type: p.typeName })) : [],
    props: item ? (item.properties || []).map(p => p.name + ': ' + p.typeName) : [],
    methods: item ? (item.methods || []).map(m => m.name + '(' + (m.parameters || []).map(p => p.typeName).join(', ') + ')') : [],
    events: [],
    x: 0, y: 0, vx: 0, vy: 0, w: NODE_W, h: 0,
  };
  n.h = nodeHeight(n);
  return n;
}

export function addExistingType(fullName, kind) {
  if (!st) return;
  const n = buildFeatureNodeFromDomain(fullName, kind);
  if (!n) return;

  placeNewNode(n);
  st.nodes.push(n);
  st.nMap[n.id] = n;

  importAllRelationshipsFromDomain();

  markDirty();
  renderSvg();
  refreshPalette();
  refreshPanel();
}

/** Places nodes in a grid when adding many types at once (issue #21 — bounded context bulk add). */
function placeNewNodeAtBulkIndex(n, index) {
  if (!st) return;
  const canvas = document.getElementById('feCanvas');
  const cols = 4;
  const row = Math.floor(index / cols);
  const col = index % cols;
  if (canvas) {
    const cx = (canvas.clientWidth / 2 - st.panX) / st.zoom;
    const cy = (canvas.clientHeight / 2 - st.panY) / st.zoom;
    const sx = 270;
    const sy = 220;
    n.x = cx - n.w / 2 + (col - (cols - 1) / 2) * sx;
    n.y = cy - n.h / 2 + row * sy;
  }
}

/**
 * Adds every discovered DDD type from the selected bounded context to the current feature diagram.
 * Relationships are synced once from the domain graph for any pair of types now on the canvas.
 */
export function addAllFromBoundedContext() {
  if (!st || !domainData) return;
  const sel = document.getElementById('feBulkBcSelect');
  const ctxName = (sel && sel.value) ? String(sel.value).trim() : '';
  if (!ctxName) return;

  const ctx = (domainData.boundedContexts || []).find(c => c.name === ctxName);
  if (!ctx) return;

  let bulkIndex = 0;
  let added = 0;
  for (const sec of ALL_SECTIONS) {
    const kind = SECTION_TO_KIND[sec];
    if (!kind) continue;
    for (const item of (ctx[sec] || [])) {
      const n = buildFeatureNodeFromDomain(item.fullName, kind);
      if (!n) continue;
      placeNewNodeAtBulkIndex(n, bulkIndex++);
      st.nodes.push(n);
      st.nMap[n.id] = n;
      added++;
    }
  }

  if (added === 0) return;

  importAllRelationshipsFromDomain();
  recalcNodeHeights();
  markDirty();
  renderSvg();
  refreshPalette();
  refreshPanel();
  fitToView();
}

export function addNewType() {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const nameInput = document.getElementById('feNewTypeName');
  const kindSelect = document.getElementById('feNewTypeKind');
  if (!nameInput || !kindSelect) return;

  const name = nameInput.value.trim();
  const kind = kindSelect.value;
  if (!name) return;

  const fullName = 'Custom.' + name;
  if (st.nMap[fullName]) { alert('A type with that name already exists in this feature.'); return; }

  const cfg = KIND_CFG[kind];
  const n = {
    id: fullName,
    name,
    kind,
    isCustom: true,
    alias: null,
    description: null,
    boundedContext: '',
    layer: '',
    cfg,
    structuredProps: [],
    props: [], methods: [], events: [],
    x: 0, y: 0, vx: 0, vy: 0, w: NODE_W, h: 0,
  };
  n.h = nodeHeight(n);
  placeNewNode(n);

  st.nodes.push(n);
  st.nMap[n.id] = n;

  nameInput.value = '';
  markDirty();
  renderSvg();
  refreshPanel();
}

function placeNewNode(n) {
  if (!st) return;
  // Place in the center of the current viewport
  const canvas = document.getElementById('feCanvas');
  if (canvas) {
    const cx = (canvas.clientWidth / 2 - st.panX) / st.zoom;
    const cy = (canvas.clientHeight / 2 - st.panY) / st.zoom;
    n.x = cx - n.w / 2 + (Math.random() - 0.5) * 100;
    n.y = cy - n.h / 2 + (Math.random() - 0.5) * 100;
  }
}

export function removeNode(id) {
  if (!st) return;
  st.nodes = st.nodes.filter(n => n.id !== id);
  delete st.nMap[id];
  st.edges = st.edges.filter(e => e.source !== id && e.target !== id);
  if (st.selectedNode === id) st.selectedNode = null;
  markDirty();
  renderSvg();
  refreshPalette();
  refreshPanel();
}

// ── Relation management ──────────────────────────────

export function startConnect(sourceId) {
  if (isReadOnlyFeature()) return;
  connecting = { sourceId, mouseX: 0, mouseY: 0 };
  const canvas = document.getElementById('feCanvas');
  if (canvas) canvas.style.cursor = 'crosshair';
}

function finishConnect(targetId) {
  if (isReadOnlyFeature()) return;
  if (!connecting || !st) return;
  const { sourceId } = connecting;
  connecting = null;
  const canvas = document.getElementById('feCanvas');
  if (canvas) canvas.style.cursor = '';

  if (sourceId === targetId) return;
  // Check if relation already exists
  if (st.edges.some(e => e.source === sourceId && e.target === targetId)) return;

  // Show kind picker overlay
  showRelationKindPicker((kind) => {
    st.edges.push({ source: sourceId, target: targetId, kind, label: '' });
    recalcNodeHeights();
    markDirty();
    renderSvg();
    refreshPanel();
  });
}

/** Renders a styled single-select dropdown for relation kind in the panel. */
function renderRelKindDropdown(currentKind, edgeIdx) {
  const DASHED = new Set(['Emits', 'Handles', 'Publishes', 'References', 'ReferencesById']);
  let h = `<div class="rel-dropdown" id="relKindDropdown">`;
  h += `<button class="rel-dropdown-trigger" onclick="window.__featureEditor.toggleRelDropdown()" type="button">`;
  h += `<span class="rel-line-sample${DASHED.has(currentKind) ? ' dashed' : ''}" style="color:${EDGE_COLORS[currentKind] || '#888'};width:16px;height:0;border-top:2px solid currentColor;${DASHED.has(currentKind) ? 'border-top-style:dashed;' : ''}"></span>`;
  h += `<span>${esc(currentKind)}</span>`;
  h += '<span class="rel-chevron">▾</span>';
  h += '</button>';
  h += '<div class="rel-dropdown-menu single-select" id="relKindMenu">';
  for (const k of RELATION_KINDS) {
    const color = EDGE_COLORS[k] || '#888';
    const dashed = DASHED.has(k);
    const sel = k === currentKind ? ' selected' : '';
    h += `<div class="rel-dropdown-item${sel}" onclick="window.__featureEditor.changeEdgeKind(${edgeIdx}, '${k}')">`;
    h += `<span class="rel-line-sample${dashed ? ' dashed' : ''}" style="color:${color}"></span>`;
    h += `<span class="rel-kind-label">${esc(k)}</span>`;
    if (k === currentKind) h += '<span style="color:var(--accent);font-size:11px">✓</span>';
    h += '</div>';
  }
  h += '</div></div>';
  return h;
}

/** Shows the relation-kind picker overlay (replaces prompt()). */
function showRelationKindPicker(callback) {
  const DASHED = new Set(['Emits', 'Handles', 'Publishes', 'References', 'ReferencesById']);
  const overlay = document.createElement('div');
  overlay.className = 'rel-picker-overlay';
  let h = '<div class="rel-picker-card">';
  h += '<div class="rel-picker-title">Select Relationship Kind</div>';
  for (const k of RELATION_KINDS) {
    const color = EDGE_COLORS[k] || '#888';
    const dashed = DASHED.has(k);
    h += `<div class="rel-picker-item" data-kind="${escAttr(k)}">`;
    h += `<span class="rel-line-sample${dashed ? ' dashed' : ''}" style="color:${color}"></span>`;
    h += `<span>${esc(k)}</span>`;
    h += '</div>';
  }
  h += '<button class="rel-picker-cancel">Cancel</button>';
  h += '</div>';
  overlay.innerHTML = h;
  document.body.appendChild(overlay);

  function cleanup() { overlay.remove(); }
  overlay.querySelector('.rel-picker-cancel').addEventListener('click', cleanup);
  overlay.addEventListener('click', (ev) => { if (ev.target === overlay) cleanup(); });
  overlay.querySelectorAll('.rel-picker-item').forEach(item => {
    item.addEventListener('click', () => {
      const kind = item.dataset.kind;
      cleanup();
      callback(kind);
    });
  });
}

/** Toggle the single-select relation kind dropdown in the panel. */
export function toggleRelDropdown() {
  const menu = document.getElementById('relKindMenu');
  const trigger = menu?.previousElementSibling;
  if (!menu) return;
  const open = menu.classList.toggle('visible');
  if (trigger) trigger.classList.toggle('open', open);
  if (open) {
    const close = (ev) => {
      if (!menu.contains(ev.target) && ev.target !== trigger && !trigger.contains(ev.target)) {
        menu.classList.remove('visible');
        trigger.classList.remove('open');
        document.removeEventListener('click', close);
      }
    };
    setTimeout(() => document.addEventListener('click', close), 0);
  }
}

// ── Bounded Context dropdown ─────────────────────────

function renderBoundedContextDropdown(node) {
  const ctxNames = getBoundedContextNames();
  const current = node.boundedContext || '';
  let h = `<div class="rel-dropdown" id="bcDropdown">`;
  h += `<button class="rel-dropdown-trigger" onclick="window.__featureEditor.toggleBcDropdown()" type="button">`;
  h += `<span class="fe-bc-dot" style="background:var(--accent);width:7px;height:7px;border-radius:50%"></span>`;
  h += `<span>${current ? esc(current) : '<span style="color:var(--text-dim)">None</span>'}</span>`;
  h += '<span class="rel-chevron">▾</span>';
  h += '</button>';
  h += '<div class="rel-dropdown-menu single-select" id="bcDropdownMenu">';
  // "None" option
  h += `<div class="rel-dropdown-item${!current ? ' selected' : ''}" onclick="window.__featureEditor.changeBoundedContext('${escAttr(node.id)}', '')">`;
  h += `<span class="rel-kind-label" style="color:var(--text-dim);font-style:italic">None</span>`;
  if (!current) h += '<span style="color:var(--accent);font-size:11px">✓</span>';
  h += '</div>';
  for (const name of ctxNames) {
    const sel = name === current;
    h += `<div class="rel-dropdown-item${sel ? ' selected' : ''}" onclick="window.__featureEditor.changeBoundedContext('${escAttr(node.id)}', '${escAttr(name)}')">`;
    h += `<span class="fe-bc-dot" style="background:var(--accent);width:7px;height:7px;border-radius:50%;flex-shrink:0"></span>`;
    h += `<span class="rel-kind-label">${esc(name)}</span>`;
    if (sel) h += '<span style="color:var(--accent);font-size:11px">✓</span>';
    h += '</div>';
  }
  h += '</div></div>';
  return h;
}

export function changeAlias(nodeId, value) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.alias = (value && value.trim()) ? value.trim() : null;
  markDirty();
  renderSvg();
  refreshPanel();
}

export function changeDescription(nodeId, value) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.description = (value && value.trim()) ? value.trim() : null;
  markDirty();
  refreshPanel();
}

export function changeBoundedContext(nodeId, ctxName) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.boundedContext = ctxName;
  markDirty();
  refreshPanel();
}

export function toggleBcDropdown() {
  toggleDropdownById('bcDropdownMenu', 'bcDropdown');
}

// ── Layer dropdown ───────────────────────────────────

function renderLayerDropdown(node) {
  const current = node.layer || '';
  const currentColor = LAYER_COLORS[current] || 'var(--text-dim)';
  let h = `<div class="rel-dropdown" id="layerDropdown">`;
  h += `<button class="rel-dropdown-trigger" onclick="window.__featureEditor.toggleLayerDropdown()" type="button">`;
  h += `<span style="width:14px;height:3px;border-radius:1px;background:${currentColor};flex-shrink:0"></span>`;
  h += `<span>${current ? esc(current) : '<span style="color:var(--text-dim)">None</span>'}</span>`;
  h += '<span class="rel-chevron">▾</span>';
  h += '</button>';
  h += '<div class="rel-dropdown-menu single-select" id="layerDropdownMenu">';
  // "None" option
  h += `<div class="rel-dropdown-item${!current ? ' selected' : ''}" onclick="window.__featureEditor.changeLayer('${escAttr(node.id)}', '')">`;
  h += `<span class="rel-kind-label" style="color:var(--text-dim);font-style:italic">None</span>`;
  if (!current) h += '<span style="color:var(--accent);font-size:11px">✓</span>';
  h += '</div>';
  for (const layer of LAYERS) {
    const sel = layer === current;
    const color = LAYER_COLORS[layer];
    h += `<div class="rel-dropdown-item${sel ? ' selected' : ''}" onclick="window.__featureEditor.changeLayer('${escAttr(node.id)}', '${escAttr(layer)}')">`;
    h += `<span style="width:14px;height:3px;border-radius:1px;background:${color};flex-shrink:0"></span>`;
    h += `<span class="rel-kind-label">${esc(layer)}</span>`;
    if (sel) h += '<span style="color:var(--accent);font-size:11px">✓</span>';
    h += '</div>';
  }
  h += '</div></div>';
  return h;
}

export function changeLayer(nodeId, layer) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.layer = layer;
  markDirty();
  refreshPanel();
}

export function toggleLayerDropdown() {
  toggleDropdownById('layerDropdownMenu', 'layerDropdown');
}

/** Generic toggle helper for dropdown menus. */
function toggleDropdownById(menuId, wrapperId) {
  const menu = document.getElementById(menuId);
  const wrapper = document.getElementById(wrapperId);
  const trigger = wrapper?.querySelector('.rel-dropdown-trigger');
  if (!menu) return;
  const open = menu.classList.toggle('visible');
  if (trigger) trigger.classList.toggle('open', open);
  if (open) {
    const close = (ev) => {
      if (!menu.contains(ev.target) && ev.target !== trigger && !trigger.contains(ev.target)) {
        menu.classList.remove('visible');
        if (trigger) trigger.classList.remove('open');
        document.removeEventListener('click', close);
      }
    };
    setTimeout(() => document.addEventListener('click', close), 0);
  }
}

export function removeEdge(idx) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  st.edges.splice(idx, 1);
  st.selectedEdge = null;
  recalcNodeHeights();
  markDirty();
  renderSvg();
  refreshPanel();
}

export function changeEdgeKind(idx, kind) {
  if (isReadOnlyFeature()) return;
  if (!st || !st.edges[idx]) return;
  st.edges[idx].kind = kind;
  recalcNodeHeights();
  markDirty();
  renderSvg();
}

/** Recalculate all node heights (needed when edges change since Emits affects height). */
function recalcNodeHeights() {
  if (!st) return;
  for (const n of st.nodes) {
    n.h = nodeHeight(n);
  }
}

// ── Import relationships from domain data ────────────

/** Add edges for every domain relationship whose endpoints are both on the feature canvas. */
function importAllRelationshipsFromDomain() {
  if (!st || !domainData) return;
  for (const c of (domainData.boundedContexts || [])) {
    for (const rel of (c.relationships || [])) {
      const hasSource = st.nMap[rel.sourceType];
      const hasTarget = st.nMap[rel.targetType];
      if (!hasSource || !hasTarget) continue;
      if (st.edges.some(e => e.source === rel.sourceType && e.target === rel.targetType && e.kind === rel.kind)) continue;
      st.edges.push({ source: rel.sourceType, target: rel.targetType, kind: rel.kind, label: rel.label || '' });
    }
  }
}

function findDomainItem(fullName, kind) {
  if (!domainData) return null;
  const secKey = KIND_TO_SECTION[kind];
  for (const ctx of (domainData.boundedContexts || [])) {
    const items = ctx[secKey] || [];
    const found = items.find(i => i.fullName === fullName);
    if (found) return found;
  }
  return null;
}

/** Find the bounded context name that contains a given type. */
function findDomainContext(fullName) {
  if (!domainData) return null;
  for (const ctx of (domainData.boundedContexts || [])) {
    for (const sec of ALL_SECTIONS) {
      if ((ctx[sec] || []).some(i => i.fullName === fullName)) return ctx.name;
    }
  }
  return null;
}

/** Get all bounded context names from domain data. */
function getBoundedContextNames() {
  if (!domainData) return [];
  return (domainData.boundedContexts || []).map(c => c.name);
}

// ── Dirty tracking ───────────────────────────────────

function markDirty() {
  dirty = true;
  const indicator = document.getElementById('feDirtyIndicator');
  if (indicator) indicator.style.display = 'inline';
}

// ── Palette refresh ──────────────────────────────────

export function filterPalette() {
  const input = document.getElementById('fePaletteSearch');
  const container = document.getElementById('fePalette');
  if (input && container) container.innerHTML = renderPaletteItems(input.value);
}

function refreshPalette() {
  const input = document.getElementById('fePaletteSearch');
  const container = document.getElementById('fePalette');
  if (container) container.innerHTML = renderPaletteItems(input?.value || '');
}

// ── Auto-layout (reused from diagram logic) ──────────

function applyAutoLayout(nodes, edges, nMap) {
  const kindRow = {
    aggregate: 0, entity: 1, valueObject: 1, event: 2, integrationEvent: 2,
    eventHandler: 3, commandHandlerTarget: 2, commandHandler: 3, queryHandler: 3, repository: 4, service: 4,
  };
  const rowBuckets = {};
  for (const n of nodes) {
    const r = kindRow[n.kind] || 0;
    (rowBuckets[r] = rowBuckets[r] || []).push(n);
  }
  for (const [row, rNodes] of Object.entries(rowBuckets)) {
    const y = parseInt(row) * 240;
    rNodes.forEach((n, i) => { n.x = (i - (rNodes.length - 1) / 2) * 270; n.y = y; });
  }
  for (let i = 0; i < 150; i++) {
    const alpha = 1 - i / 150;
    for (let a = 0; a < nodes.length; a++) {
      for (let b = a + 1; b < nodes.length; b++) {
        const na = nodes[a], nb = nodes[b];
        let dx = nb.x - na.x, dy = nb.y - na.y;
        const dist = Math.sqrt(dx * dx + dy * dy) || 1;
        const force = (8000 * alpha) / (dist * dist);
        const fx = (dx / dist) * force, fy = (dy / dist) * force;
        na.vx -= fx; na.vy -= fy; nb.vx += fx; nb.vy += fy;
      }
    }
    for (const e of edges) {
      const s = nMap[e.source], t = nMap[e.target];
      if (!s || !t) continue;
      const dx = t.x - s.x, dy = t.y - s.y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;
      const force = dist * 0.004 * alpha;
      const fx = (dx / dist) * force, fy = (dy / dist) * force;
      s.vx += fx; s.vy += fy; t.vx -= fx; t.vy -= fy;
    }
    for (const n of nodes) { n.vx *= 0.82; n.vy *= 0.82; n.x += n.vx; n.y += n.vy; }
  }
  for (const n of nodes) { n.vx = 0; n.vy = 0; }
}

function nodeHeight(n) {
  const derivedEvents = getEmittedEventsForNode(n.id);
  let h = PAD + HEADER_H + NAME_H;
  if (n.props.length > 0) h += DIVIDER_H + n.props.length * PROP_H;
  if (n.methods.length > 0) h += DIVIDER_H + n.methods.length * PROP_H;
  if (derivedEvents.length > 0) h += DIVIDER_H + derivedEvents.length * PROP_H;
  return h + PAD;
}

// ── Boundary computation ─────────────────────────────

const BC_COLORS = ['#6366f1', '#06b6d4', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6', '#ec4899', '#14b8a6'];

function computeFeatureContextBounds(nodes) {
  const groups = {};
  for (const n of nodes) {
    if (!n.boundedContext) continue;
    if (!groups[n.boundedContext]) groups[n.boundedContext] = [];
    groups[n.boundedContext].push(n);
  }
  const bounds = [];
  const names = Object.keys(groups).sort();
  for (let i = 0; i < names.length; i++) {
    const name = names[i];
    const ctxNodes = groups[name];
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of ctxNodes) {
      minX = Math.min(minX, n.x);
      minY = Math.min(minY, n.y);
      maxX = Math.max(maxX, n.x + n.w);
      maxY = Math.max(maxY, n.y + n.h);
    }
    const pad = 30;
    const color = BC_COLORS[i % BC_COLORS.length];
    bounds.push({ name, x: minX - pad, y: minY - pad - 32, w: maxX - minX + pad * 2, h: maxY - minY + pad * 2 + 32, color });
  }
  return bounds;
}

function computeFeatureLayerBounds(nodes) {
  const groups = {};
  for (const n of nodes) {
    if (!n.layer) continue;
    const key = (n.boundedContext || '__default') + '\0' + n.layer;
    if (!groups[key]) groups[key] = { boundedContext: n.boundedContext, layer: n.layer, nodes: [] };
    groups[key].nodes.push(n);
  }
  const bounds = [];
  for (const g of Object.values(groups)) {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of g.nodes) {
      minX = Math.min(minX, n.x);
      minY = Math.min(minY, n.y);
      maxX = Math.max(maxX, n.x + n.w);
      maxY = Math.max(maxY, n.y + n.h);
    }
    const pad = 20;
    const color = LAYER_COLORS[g.layer] || '#888';
    bounds.push({ name: g.layer, x: minX - pad, y: minY - pad - 24, w: maxX - minX + pad * 2, h: maxY - minY + pad * 2 + 24, color });
  }
  return bounds;
}

// ── SVG rendering (reuses diagram style) ─────────────

function renderSvg() {
  const svg = document.getElementById('feSvg');
  if (!svg || !st) return;

  let s = '<defs>';
  for (const [kind, color] of Object.entries(EDGE_COLORS)) {
    s += `<marker id="fe-arrow-${kind}" viewBox="0 0 10 6" refX="10" refY="3" markerWidth="8" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,3 L0,6 Z" fill="${color}" /></marker>`;
  }
  s += '<marker id="fe-diamond" viewBox="0 0 12 8" refX="0" refY="4" markerWidth="10" markerHeight="8" orient="auto-start-reverse"><path d="M0,4 L6,0 L12,4 L6,8 Z" fill="#60a5fa" /></marker>';
  s += '</defs>';

  s += `<g id="feViewport" transform="translate(${st.panX},${st.panY}) scale(${st.zoom})">`;

  // Bounded context boundaries (drawn first, behind everything)
  const ctxBounds = computeFeatureContextBounds(st.nodes);
  for (const b of ctxBounds) {
    s += `<g class="dg-ctx-boundary" data-ctx="${escAttr(b.name)}" style="cursor:move">`;
    s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="12" fill="rgba(255,255,255,.10)" stroke="${b.color}" stroke-width="1.5" stroke-dasharray="8,5" opacity="0.8" />`;
    s += `<text x="${b.x + 14}" y="${b.y + 24}" fill="${b.color}" font-size="20" font-weight="700" font-family="-apple-system,sans-serif" opacity="0.85">${esc(b.name)}</text>`;
    s += '</g>';
  }

  // Layer boundaries (behind nodes, in front of context boundaries)
  const layerBounds = computeFeatureLayerBounds(st.nodes);
  for (const b of layerBounds) {
    s += `<g class="dg-layer-boundary">`;
    s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="8" fill="none" stroke="${b.color}" stroke-width="1" stroke-dasharray="4,3" opacity="0.6" />`;
    s += `<text x="${b.x + 10}" y="${b.y + 18}" fill="${b.color}" font-size="13" font-weight="600" font-family="-apple-system,sans-serif" font-style="italic" opacity="0.7">${esc(b.name)}</text>`;
    s += '</g>';
  }

  // Edges
  for (let ei = 0; ei < st.edges.length; ei++) {
    const e = st.edges[ei];
    const src = st.nMap[e.source], tgt = st.nMap[e.target];
    if (!src || !tgt) continue;
    const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
    const tgtCx = tgt.x + tgt.w / 2, tgtCy = tgt.y + tgt.h / 2;
    const p1 = rectEdge(srcCx, srcCy, src.w + 8, src.h + 8, tgtCx, tgtCy);
    const p2 = rectEdge(tgtCx, tgtCy, tgt.w + 8, tgt.h + 8, srcCx, srcCy);
    const color = EDGE_COLORS[e.kind] || '#5c6070';
    const dashed = (e.kind === 'Emits' || e.kind === 'Handles' || e.kind === 'Publishes' || e.kind === 'References' || e.kind === 'ReferencesById') ? ' stroke-dasharray="6,4"' : '';
    const markerStart = (e.kind === 'Contains' || e.kind === 'Has' || e.kind === 'HasMany') ? ' marker-start="url(#fe-diamond)"' : '';
    const markerEnd = (e.kind === 'References' || e.kind === 'ReferencesById') ? '' : ` marker-end="url(#fe-arrow-${e.kind})"`;
    const selected = st.selectedEdge === ei;
    const sw = selected ? 3 : 1.5;
    const op = selected ? 1 : 0.65;
    // Invisible hit area
    s += `<line class="fe-edge" data-idx="${ei}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="transparent" stroke-width="12" style="cursor:pointer" />`;
    s += `<line class="fe-edge-vis" data-idx="${ei}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="${color}" stroke-width="${sw}"${dashed}${markerStart}${markerEnd} opacity="${op}" style="pointer-events:none" />`;
    const mx = (p1.x + p2.x) / 2, my = (p1.y + p2.y) / 2;
    s += `<text x="${mx}" y="${my - 6}" text-anchor="middle" fill="${color}" font-size="9" font-family="-apple-system,sans-serif" opacity="0.7" style="pointer-events:none">${esc(e.label || e.kind)}</text>`;
  }

  // Connection line being drawn
  if (connecting && st.nMap[connecting.sourceId]) {
    const src = st.nMap[connecting.sourceId];
    const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
    s += `<line id="feConnectLine" x1="${srcCx}" y1="${srcCy}" x2="${connecting.mouseX}" y2="${connecting.mouseY}" stroke="#6366f1" stroke-width="2" stroke-dasharray="6,4" opacity="0.8" style="pointer-events:none" />`;
    s += `<circle cx="${connecting.mouseX}" cy="${connecting.mouseY}" r="4" fill="#6366f1" opacity="0.8" style="pointer-events:none" />`;
  }

  // Nodes
  for (const n of st.nodes) {
    const c = n.cfg;
    const selected = st.selectedNode === n.id;
    const strokeW = selected ? 2.5 : 1.5;
    const stroke = selected ? '#6366f1' : c.border;
    s += `<g class="fe-node" data-id="${escAttr(n.id)}" transform="translate(${n.x},${n.y})" style="cursor:pointer">`;
    s += `<rect x="3" y="3" width="${n.w}" height="${n.h}" rx="8" fill="rgba(0,0,0,.3)" />`;
    s += `<rect width="${n.w}" height="${n.h}" rx="8" fill="${c.bg}" stroke="${stroke}" stroke-width="${strokeW}" />`;

    // Connector port (top-right circle for drag-to-connect)
    if (!isReadOnlyFeature()) {
      s += `<circle class="fe-port" cx="${n.w - 8}" cy="12" r="5" fill="${c.color}" opacity="0.6" style="cursor:crosshair" />`;
    }

    // Custom type indicator
    if (n.isCustom) {
      s += `<text x="${n.w - 20}" y="14" text-anchor="end" fill="#6366f1" font-size="8" font-family="-apple-system,sans-serif" opacity="0.7">NEW</text>`;
    }

    let ty = 20;
    s += `<text x="${n.w / 2}" y="${ty}" text-anchor="middle" fill="${c.color}" font-size="10" font-family="-apple-system,sans-serif" opacity="0.85">${c.stereotype}</text>`;
    ty += 22;
    const displayName = (n.alias && n.alias.trim()) ? n.alias.trim() : n.name;
    s += `<text class="fe-name" x="${n.w / 2}" y="${ty}" text-anchor="middle" fill="#f0f2f7" font-size="14" font-weight="600" font-family="-apple-system,sans-serif">${esc(displayName)}</text>`;
    if (n.props.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const p of n.props) { ty += 17; s += `<text x="16" y="${ty}" fill="#a0a4b8" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(p)}</text>`; }
    }
    if (n.methods.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const m of n.methods) { ty += 17; s += `<text x="16" y="${ty}" fill="#a78bfa" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(m)}</text>`; }
    }
    // Derived emitted events from Emits edges
    const derivedEvents = getEmittedEventsForNode(n.id);
    if (derivedEvents.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const ev of derivedEvents) { ty += 17; s += `<text x="16" y="${ty}" fill="#fbbf24" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc('⚡ ' + ev)}</text>`; }
    }
    s += '</g>';
  }

  s += '</g>';
  svg.innerHTML = s;
}

// ── Geometry ─────────────────────────────────────────

function rectEdge(cx, cy, w, h, px, py) {
  const dx = px - cx, dy = py - cy;
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const absDx = Math.abs(dx), absDy = Math.abs(dy);
  const hw = w / 2, hh = h / 2;
  const t = (absDx * hh > absDy * hw) ? hw / (absDx || 1) : hh / (absDy || 1);
  return { x: cx + dx * t, y: cy + dy * t };
}

// ── Fit / Zoom ───────────────────────────────────────

function fitToView() {
  if (!st || st.nodes.length === 0) return;
  const wrap = document.getElementById('feCanvas');
  if (!wrap) return;
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const n of st.nodes) {
    minX = Math.min(minX, n.x); minY = Math.min(minY, n.y);
    maxX = Math.max(maxX, n.x + n.w); maxY = Math.max(maxY, n.y + n.h);
  }
  const pad = 80;
  const gw = maxX - minX + pad * 2, gh = maxY - minY + pad * 2;
  const ww = wrap.clientWidth, wh = wrap.clientHeight;
  const zoom = Math.min(ww / gw, wh / gh, 1.5);
  st.zoom = zoom;
  st.panX = (ww - gw * zoom) / 2 - minX * zoom + pad * zoom;
  st.panY = (wh - gh * zoom) / 2 - minY * zoom + pad * zoom;
  renderSvg();
}

export function featureEditorZoom(factor) {
  if (!st) return;
  const wrap = document.getElementById('feCanvas');
  if (!wrap) return;
  const cx = wrap.clientWidth / 2, cy = wrap.clientHeight / 2;
  st.panX = cx - (cx - st.panX) * factor;
  st.panY = cy - (cy - st.panY) * factor;
  st.zoom *= factor;
  renderSvg();
}

export function featureEditorFit() { fitToView(); }

// ── Interaction: drag nodes, pan, zoom, select, connect ──

function setupInteraction() {
  const svg = document.getElementById('feSvg');
  if (!svg || !st) return;

  let dragNode = null, dragOffX = 0, dragOffY = 0;
  let dragCtx = null, dragCtxStartX = 0, dragCtxStartY = 0, dragCtxNodeStarts = null;
  let panning = false, panStartX = 0, panStartY = 0, panOrigX = 0, panOrigY = 0;
  let portDrag = false; // dragging from a connector port

  svg.addEventListener('mousedown', function (ev) {
    const portEl = ev.target.closest('.fe-port');
    const nodeEl = ev.target.closest('.fe-node');
    const edgeEl = ev.target.closest('.fe-edge');
    const ctxEl = ev.target.closest('.dg-ctx-boundary');

    if (!isReadOnlyFeature() && portEl && nodeEl) {
      // Start connection from port
      ev.preventDefault();
      const id = nodeEl.dataset.id;
      connecting = { sourceId: id, mouseX: 0, mouseY: 0 };
      portDrag = true;
      svg.style.cursor = 'crosshair';
      const pt = svgPoint(svg, ev);
      connecting.mouseX = pt.x;
      connecting.mouseY = pt.y;
      renderSvg();
      return;
    }

    if (!isReadOnlyFeature() && connecting && nodeEl) {
      // Finish connection
      ev.preventDefault();
      finishConnect(nodeEl.dataset.id);
      renderSvg();
      return;
    }

    if (!isReadOnlyFeature() && connecting && !nodeEl) {
      // Cancel connection on background click
      connecting = null;
      svg.style.cursor = '';
      renderSvg();
      return;
    }

    if (nodeEl) {
      ev.preventDefault();
      const n = st.nMap[nodeEl.dataset.id];
      if (!n) return;
      st.selectedNode = n.id;
      st.selectedEdge = null;
      dragNode = n;
      const pt = svgPoint(svg, ev);
      dragOffX = pt.x - n.x;
      dragOffY = pt.y - n.y;
      svg.classList.add('dragging-node');
      renderSvg();
      refreshPanel();
    } else if (edgeEl) {
      ev.preventDefault();
      const idx = parseInt(edgeEl.dataset.idx);
      st.selectedEdge = idx;
      st.selectedNode = null;
      renderSvg();
      refreshPanel();
    } else if (ctxEl) {
      ev.preventDefault();
      const ctxName = ctxEl.dataset.ctx;
      dragCtx = ctxName;
      const pt = svgPoint(svg, ev);
      dragCtxStartX = pt.x;
      dragCtxStartY = pt.y;
      dragCtxNodeStarts = new Map();
      for (const n of st.nodes) {
        if (n.boundedContext === ctxName) {
          dragCtxNodeStarts.set(n.id, { x: n.x, y: n.y });
        }
      }
      svg.classList.add('dragging-node');
    } else {
      // Deselect + start pan
      if (st.selectedNode || st.selectedEdge !== null) {
        st.selectedNode = null;
        st.selectedEdge = null;
        renderSvg();
        refreshPanel();
      }
      panning = true;
      panStartX = ev.clientX;
      panStartY = ev.clientY;
      panOrigX = st.panX;
      panOrigY = st.panY;
      svg.classList.add('dragging');
    }
  });

  svg.addEventListener('mousemove', function (ev) {
    if (connecting && portDrag) {
      const pt = svgPoint(svg, ev);
      connecting.mouseX = pt.x;
      connecting.mouseY = pt.y;
      renderSvg();
    } else if (dragNode) {
      const pt = svgPoint(svg, ev);
      dragNode.x = pt.x - dragOffX;
      dragNode.y = pt.y - dragOffY;
      markDirty();
      renderSvg();
    } else if (dragCtx) {
      const pt = svgPoint(svg, ev);
      const dx = pt.x - dragCtxStartX, dy = pt.y - dragCtxStartY;
      for (const [id, start] of dragCtxNodeStarts) {
        const n = st.nMap[id];
        if (n) { n.x = start.x + dx; n.y = start.y + dy; }
      }
      markDirty();
      renderSvg();
    } else if (panning) {
      st.panX = panOrigX + (ev.clientX - panStartX);
      st.panY = panOrigY + (ev.clientY - panStartY);
      renderSvg();
    }
  });

  function endDrag(ev) {
    if (portDrag && connecting) {
      // Check if released on a node
      const nodeEl = ev.target?.closest?.('.fe-node');
      if (nodeEl && nodeEl.dataset.id !== connecting.sourceId) {
        finishConnect(nodeEl.dataset.id);
      }
      connecting = null;
      portDrag = false;
      svg.style.cursor = '';
      renderSvg();
    }
    if (dragCtx) markDirty();
    dragNode = null;
    dragCtx = null;
    dragCtxNodeStarts = null;
    panning = false;
    svg.classList.remove('dragging', 'dragging-node');
  }
  svg.addEventListener('mouseup', endDrag);
  svg.addEventListener('mouseleave', function() {
    if (portDrag) {
      connecting = null;
      portDrag = false;
      svg.style.cursor = '';
      renderSvg();
    }
    if (dragCtx) markDirty();
    dragNode = null;
    dragCtx = null;
    dragCtxNodeStarts = null;
    panning = false;
    svg.classList.remove('dragging', 'dragging-node');
  });

  svg.addEventListener('wheel', function (ev) {
    ev.preventDefault();
    const factor = ev.deltaY < 0 ? 1.1 : 0.9;
    const rect = svg.getBoundingClientRect();
    const mx = ev.clientX - rect.left, my = ev.clientY - rect.top;
    st.panX = mx - (mx - st.panX) * factor;
    st.panY = my - (my - st.panY) * factor;
    st.zoom *= factor;
    renderSvg();
  }, { passive: false });

  // Keyboard: Escape to cancel connection / deselect, Delete to remove
  document.addEventListener('keydown', function handler(ev) {
    if (!document.getElementById('feSvg')) {
      document.removeEventListener('keydown', handler);
      return;
    }
    if (ev.target.tagName === 'INPUT' || ev.target.tagName === 'TEXTAREA' || ev.target.tagName === 'SELECT') return;

    if (ev.key === 'Escape') {
      if (connecting) {
        connecting = null;
        const canvas = document.getElementById('feCanvas');
        if (canvas) canvas.style.cursor = '';
        renderSvg();
      } else {
        st.selectedNode = null;
        st.selectedEdge = null;
        renderSvg();
        refreshPanel();
      }
    }
    if (ev.key === 'Delete' || ev.key === 'Backspace') {
      if (st.selectedNode) {
        removeNode(st.selectedNode);
      } else if (st.selectedEdge !== null) {
        removeEdge(st.selectedEdge);
      }
    }
  });
}

function svgPoint(svg, ev) {
  const rect = svg.getBoundingClientRect();
  return {
    x: (ev.clientX - rect.left - st.panX) / st.zoom,
    y: (ev.clientY - rect.top - st.panY) / st.zoom,
  };
}
