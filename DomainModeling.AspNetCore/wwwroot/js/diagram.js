/**
 * Interactive SVG diagram with force layout.
 * Layout (positions, viewport, filters) syncs to server disk when available, with localStorage fallback.
 */
import { esc, escAttr, shortName } from './helpers.js';
import { renderTabBar } from './tabs.js';

// Global layout (not scoped by bounded-context selection)
const STORAGE_KEY = 'domain-model-diagram-positions-global';
const HIDDEN_KINDS_KEY = 'domain-model-diagram-hidden-kinds-global';
const HIDDEN_EDGE_KINDS_KEY = 'domain-model-diagram-hidden-edge-kinds-global';
const VIEWPORT_KEY = 'domain-model-diagram-viewport-global';
const SHOW_ALIASES_KEY = 'domain-model-diagram-show-aliases';
const SHOW_LAYERS_KEY = 'domain-model-diagram-show-layers';
// Legacy per-context keys (one-time migration)
const LEGACY_POSITIONS_KEY = 'domain-model-diagram-positions';
const LEGACY_HIDDEN_KINDS_KEY = 'domain-model-diagram-hidden-kinds';
const LEGACY_HIDDEN_EDGE_KINDS_KEY = 'domain-model-diagram-hidden-edge-kinds';
const LEGACY_VIEWPORT_KEY = 'domain-model-diagram-viewport';

const FLUSH_MS = 450;
let diagramLayoutBaseUrl = null;
/** @type {object | null} */
let serverLayoutDoc = null;
let diagramLayoutFlushTimer = null;
let suppressDiagramLayoutFlush = false;
let diagramLocalStorageMigrated = false;

const EDGE_CFG = {
  Contains:      { label: 'Contains',          color: '#60a5fa', dashed: false },
  References:    { label: 'References',         color: '#34d399', dashed: true  },
  ReferencesById:{ label: 'References (by Id)', color: '#34d399', dashed: true  },
  Has:           { label: 'Has',                color: '#60a5fa', dashed: false },
  HasMany:       { label: 'Has Many',           color: '#60a5fa', dashed: false },
  Emits:         { label: 'Emits',              color: '#fbbf24', dashed: true  },
  Handles:       { label: 'Handles',            color: '#f472b6', dashed: true  },
  Manages:       { label: 'Manages',            color: '#fb923c', dashed: false },
  Publishes:     { label: 'Publishes',          color: '#2dd4bf', dashed: true  },
};

const KIND_CFG = {
  aggregate:        { label: 'Aggregates',          color: '#d4a0ff', bg: '#1f1828', border: '#7c5aa8', stereotype: '\xABAggregate\xBB' },
  entity:           { label: 'Entities',             color: '#7ab8ff', bg: '#161e2c', border: '#4a7bbf', stereotype: '\xABEntity\xBB' },
  valueObject:      { label: 'Value Objects',        color: '#4ee8ad', bg: '#142820', border: '#36a87a', stereotype: '\xABValue Object\xBB' },
  subType:          { label: 'Sub Types',            color: '#a0b4c8', bg: '#1a1e24', border: '#6880a0', stereotype: '\xABSub Type\xBB' },
  event:            { label: 'Domain Events',        color: '#fdd04e', bg: '#2a2418', border: '#b89530', stereotype: '\xABDomain Event\xBB' },
  integrationEvent: { label: 'Integration Events',   color: '#48e8d8', bg: '#14282a', border: '#30a89e', stereotype: '\xABIntegration Event\xBB' },
  commandHandlerTarget: { label: 'Cmd handler targets', color: '#f0a050', bg: '#2a2218', border: '#c07830', stereotype: '\xABHandles target\xBB' },
  eventHandler:     { label: 'Event Handlers',       color: '#ff8ac8', bg: '#2a1824', border: '#b85888', stereotype: '\xABEvent Handler\xBB' },
  commandHandler:   { label: 'Command Handlers',     color: '#ff8ac8', bg: '#2a1824', border: '#b85888', stereotype: '\xABCommand Handler\xBB' },
  queryHandler:     { label: 'Query Handlers',       color: '#ff8ac8', bg: '#2a1824', border: '#b85888', stereotype: '\xABQuery Handler\xBB' },
  repository:       { label: 'Repositories',         color: '#ffab5c', bg: '#2a2018', border: '#b87838', stereotype: '\xABRepository\xBB' },
  service:          { label: 'Services',             color: '#bda0ff', bg: '#1e1828', border: '#7860b0', stereotype: '\xABService\xBB' },
};

// ── Persistence (localStorage + optional server file storage) ──

export function setDiagramLayoutBaseUrl(baseUrl) {
  diagramLayoutBaseUrl = baseUrl && baseUrl.length ? baseUrl.replace(/\/$/, '') : null;
}

export function setServerDiagramLayoutCache(doc) {
  serverLayoutDoc = doc && typeof doc === 'object' && !Array.isArray(doc) ? doc : null;
}

function migrateLegacyDiagramLocalStorage() {
  if (diagramLocalStorageMigrated) return;
  diagramLocalStorageMigrated = true;
  try {
    if (!localStorage.getItem(STORAGE_KEY) && localStorage.getItem(LEGACY_POSITIONS_KEY)) {
      const raw = localStorage.getItem(LEGACY_POSITIONS_KEY);
      const all = raw ? JSON.parse(raw) : {};
      const merged = {};
      for (const bucket of Object.values(all || {})) {
        if (bucket && typeof bucket === 'object' && !Array.isArray(bucket)) {
          Object.assign(merged, bucket);
        }
      }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
      localStorage.removeItem(LEGACY_POSITIONS_KEY);
    }
  } catch { /* ignore */ }

  try {
    if (!localStorage.getItem(HIDDEN_KINDS_KEY) && localStorage.getItem(LEGACY_HIDDEN_KINDS_KEY)) {
      const raw = localStorage.getItem(LEGACY_HIDDEN_KINDS_KEY);
      const all = raw ? JSON.parse(raw) : {};
      const set = new Set();
      for (const arr of Object.values(all || {})) {
        if (Array.isArray(arr)) for (const k of arr) set.add(k);
      }
      localStorage.setItem(HIDDEN_KINDS_KEY, JSON.stringify([...set]));
      localStorage.removeItem(LEGACY_HIDDEN_KINDS_KEY);
    }
  } catch { /* ignore */ }

  try {
    if (!localStorage.getItem(HIDDEN_EDGE_KINDS_KEY) && localStorage.getItem(LEGACY_HIDDEN_EDGE_KINDS_KEY)) {
      const raw = localStorage.getItem(LEGACY_HIDDEN_EDGE_KINDS_KEY);
      const all = raw ? JSON.parse(raw) : {};
      const set = new Set();
      for (const arr of Object.values(all || {})) {
        if (Array.isArray(arr)) for (const k of arr) set.add(k);
      }
      localStorage.setItem(HIDDEN_EDGE_KINDS_KEY, JSON.stringify([...set]));
      localStorage.removeItem(LEGACY_HIDDEN_EDGE_KINDS_KEY);
    }
  } catch { /* ignore */ }

  try {
    if (!localStorage.getItem(VIEWPORT_KEY) && localStorage.getItem(LEGACY_VIEWPORT_KEY)) {
      const raw = localStorage.getItem(LEGACY_VIEWPORT_KEY);
      const all = raw ? JSON.parse(raw) : {};
      let picked = null;
      for (const v of Object.values(all || {})) {
        if (v && typeof v.zoom === 'number') picked = v;
      }
      if (picked) {
        localStorage.setItem(VIEWPORT_KEY, JSON.stringify(picked));
      }
      localStorage.removeItem(LEGACY_VIEWPORT_KEY);
    }
  } catch { /* ignore */ }
}

function buildLayoutPayloadFromState() {
  if (!dgState) return null;
  const positions = {};
  for (const n of dgState.allNodes) {
    positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
  }
  return {
    positions,
    viewport: {
      zoom: Math.round(dgState.zoom * 1000) / 1000,
      panX: Math.round(dgState.panX * 10) / 10,
      panY: Math.round(dgState.panY * 10) / 10,
    },
    hiddenKinds: [...dgState.hiddenKinds],
    hiddenEdgeKinds: [...dgState.hiddenEdgeKinds],
    showAliases,
    showLayers,
  };
}

function scheduleFlushDiagramLayout() {
  if (suppressDiagramLayoutFlush) return;
  if (!diagramLayoutBaseUrl) return;
  if (diagramLayoutFlushTimer) clearTimeout(diagramLayoutFlushTimer);
  diagramLayoutFlushTimer = setTimeout(() => {
    diagramLayoutFlushTimer = null;
    void flushDiagramLayoutToServer();
  }, FLUSH_MS);
}

async function flushDiagramLayoutToServer() {
  if (!diagramLayoutBaseUrl || !dgState) return;
  const payload = buildLayoutPayloadFromState();
  if (!payload) return;
  try {
    const res = await fetch(`${diagramLayoutBaseUrl}/diagram-layout`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    if (res.ok) serverLayoutDoc = payload;
  } catch { /* ignore network errors */ }
}

export function syncDiagramToolbarToggles() {
  const aliasBg = showAliases ? 'var(--bg-hover)' : '';
  const layerBg = showLayers ? 'var(--bg-hover)' : '';
  const aliasBtn = document.getElementById('diagramAliasToggle');
  if (aliasBtn) aliasBtn.style.background = aliasBg;
  const layerBtn = document.getElementById('diagramLayerToggle');
  if (layerBtn) layerBtn.style.background = layerBg;
  const feAlias = document.getElementById('feAliasToggle');
  if (feAlias) feAlias.style.background = aliasBg;
  const feLayer = document.getElementById('feLayerToggle');
  if (feLayer) feLayer.style.background = layerBg;
}

/** Read alias/layer toggles from localStorage (e.g. after feature editor view toolbar changed them). */
export function reloadDiagramViewFlagsFromStorage() {
  try {
    showAliases = localStorage.getItem(SHOW_ALIASES_KEY) === 'true';
  } catch { showAliases = false; }
  try {
    showLayers = localStorage.getItem(SHOW_LAYERS_KEY) === 'true';
  } catch { showLayers = false; }
}

export function getDiagramShowAliases() {
  return showAliases;
}

export function getDiagramShowLayers() {
  return showLayers;
}

export function loadDiagramHiddenKindsSet() {
  return new Set(loadHiddenKinds());
}

export function loadDiagramHiddenEdgeKindsSet() {
  return new Set(loadHiddenEdgeKinds());
}

export function saveDiagramHiddenKindsSet(hiddenKinds) {
  saveHiddenKinds(hiddenKinds instanceof Set ? hiddenKinds : new Set(hiddenKinds || []));
}

export function saveDiagramHiddenEdgeKindsSet(hiddenEdgeKinds) {
  saveHiddenEdgeKinds(hiddenEdgeKinds instanceof Set ? hiddenEdgeKinds : new Set(hiddenEdgeKinds || []));
}

function loadPositions() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const o = JSON.parse(raw);
    return o && typeof o === 'object' && !Array.isArray(o) ? o : null;
  } catch { return null; }
}

function savePositions(nodes) {
  try {
    const positions = {};
    for (const n of nodes) {
      positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(positions));
  } catch { /* quota exceeded or private mode */ }
  scheduleFlushDiagramLayout();
}

function clearPositions() {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch { /* ignore */ }
  scheduleFlushDiagramLayout();
}

function loadHiddenKinds() {
  try {
    const raw = localStorage.getItem(HIDDEN_KINDS_KEY);
    if (!raw) return new Set();
    const arr = JSON.parse(raw);
    return new Set(Array.isArray(arr) ? arr : []);
  } catch { return new Set(); }
}

function saveHiddenKinds(hiddenKinds) {
  try {
    localStorage.setItem(HIDDEN_KINDS_KEY, JSON.stringify([...hiddenKinds]));
  } catch { /* quota exceeded or private mode */ }
  scheduleFlushDiagramLayout();
}

function loadHiddenEdgeKinds() {
  try {
    const raw = localStorage.getItem(HIDDEN_EDGE_KINDS_KEY);
    if (!raw) return new Set();
    const arr = JSON.parse(raw);
    return new Set(Array.isArray(arr) ? arr : []);
  } catch { return new Set(); }
}

function saveHiddenEdgeKinds(hiddenEdgeKinds) {
  try {
    localStorage.setItem(HIDDEN_EDGE_KINDS_KEY, JSON.stringify([...hiddenEdgeKinds]));
  } catch { /* quota exceeded or private mode */ }
  scheduleFlushDiagramLayout();
}

function loadViewport() {
  try {
    const raw = localStorage.getItem(VIEWPORT_KEY);
    if (!raw) return null;
    return JSON.parse(raw);
  } catch { return null; }
}

function saveViewport(zoom, panX, panY) {
  try {
    const v = { zoom: Math.round(zoom * 1000) / 1000, panX: Math.round(panX * 10) / 10, panY: Math.round(panY * 10) / 10 };
    localStorage.setItem(VIEWPORT_KEY, JSON.stringify(v));
  } catch { /* quota exceeded or private mode */ }
  scheduleFlushDiagramLayout();
}

function clearViewport() {
  try {
    localStorage.removeItem(VIEWPORT_KEY);
  } catch { /* ignore */ }
  scheduleFlushDiagramLayout();
}

// ── Module state ─────────────────────────────────────
let dgState = null;

/** @type {Set<string>} */
let traceHighlightIds = new Set();
let showAliases = false;
let showLayers = false;

// ── Node sizing constants ────────────────────────────
const NODE_W = 200;
const PROP_H = 17;
const HEADER_H = 26;
const NAME_LINE_H = 18;
const NAME_PAD = 6;
const DIVIDER_H = 8;
const PAD = 12;
const MAX_NAME_CHARS = 22;

// Split a name into lines that fit within the node width.
// Splits PascalCase at uppercase boundaries, or on spaces.
function wrapName(text) {
  if (!text || text.length <= MAX_NAME_CHARS) return [text || ''];
  const words = text.includes(' ')
    ? text.split(' ')
    : text.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2').split(' ');
  const lines = [];
  let line = '';
  for (const w of words) {
    const candidate = line ? line + (text.includes(' ') ? ' ' : '') + w : w;
    if (candidate.length > MAX_NAME_CHARS && line) {
      lines.push(line);
      line = w;
    } else {
      line = candidate;
    }
  }
  if (line) lines.push(line);
  return lines.length ? lines : [text];
}

function nodeNameHeight(n) {
  const lines = wrapName(diagramDisplayName(n));
  return NAME_PAD + lines.length * NAME_LINE_H;
}

function nodeHeight(n) {
  let h = PAD + HEADER_H + nodeNameHeight(n);
  if (n.props.length > 0) h += DIVIDER_H + n.props.length * PROP_H;
  if (n.methods.length > 0) h += DIVIDER_H + n.methods.length * PROP_H;
  if (n.events.length > 0) h += DIVIDER_H + n.events.length * PROP_H;
  return h + PAD;
}

function parseEmitsMethodFromLabel(label) {
  if (!label || typeof label !== 'string') return null;
  const m = label.match(/^emits via ([^(]+)\(\)$/i);
  return m && m[1] ? m[1].trim() : null;
}

function methodNameFromSignature(signature) {
  if (!signature || typeof signature !== 'string') return '';
  const idx = signature.indexOf('(');
  return (idx >= 0 ? signature.slice(0, idx) : signature).trim();
}

function methodTextY(node, methodName) {
  if (!node || !Array.isArray(node.methods) || node.methods.length === 0) return null;
  if (!methodName) return null;

  let ty = 20;
  ty += NAME_PAD;
  ty += wrapName(diagramDisplayName(node)).length * NAME_LINE_H;

  if (node.props.length > 0) {
    ty += 8;
    ty += 4;
    ty += node.props.length * PROP_H;
  }

  ty += 8;
  ty += 4;

  for (const m of node.methods) {
    ty += PROP_H;
    if (methodNameFromSignature(m) === methodName) {
      return ty;
    }
  }

  return null;
}

function sourceAnchorForEdge(src, tgt, edge) {
  if (!src || !tgt || !edge) return null;
  if (edge.kind !== 'Emits') return null;
  const methodName = parseEmitsMethodFromLabel(edge.label || '');
  if (!methodName) return null;
  const methodYLocal = methodTextY(src, methodName);
  if (methodYLocal == null) return null;

  const anchorCy = src.y + methodYLocal;
  const tgtCx = tgt.x + tgt.w / 2;
  const tgtCy = tgt.y + tgt.h / 2;
  return rectEdge(src.x + src.w / 2, anchorCy, src.w + 8, src.h + 8, tgtCx, tgtCy);
}

try {
  showAliases = localStorage.getItem(SHOW_ALIASES_KEY) === 'true';
} catch { /* ignore */ }

try {
  showLayers = localStorage.getItem(SHOW_LAYERS_KEY) === 'true';
} catch { /* ignore */ }

export function getDiagramState() { return dgState; }

/**
 * Highlights diagram nodes by fullName (e.g. event + handler types). Used by the Trace tab.
 * @param {Iterable<string> | null | undefined} fullNames
 */
export function setDiagramTraceHighlights(fullNames) {
  traceHighlightIds = new Set(fullNames || []);
  renderSvg();
}

// ── Render the diagram wrapper HTML ──────────────────
/**
 * @param {{ traceLayout?: boolean }} [opts]
 */
export function renderDiagramView(opts = {}) {
  const traceLayout = opts.traceLayout === true;
  let html = renderTabBar(traceLayout ? 'trace' : 'diagram');

  const wrapClass = traceLayout ? 'diagram-wrap trace-diagram-inner' : 'diagram-wrap';
  html += `<div class="${wrapClass}" id="diagramWrap">`;

  // Toolbar (top-left)
  html += '<div class="diagram-toolbar">';
  html += '<button onclick="window.__diagram.resetLayout()" title="Reset to auto-layout">↻ Reset</button>';
  html += '<span class="diagram-toolbar-sep"></span>';
  html += `<button id="diagramAliasToggle" onclick="window.__diagram.toggleAliases()" title="Show aliases instead of original names" style="${showAliases ? 'background:var(--bg-hover)' : ''}">Aa Aliases</button>`;
  html += '<span class="diagram-toolbar-sep"></span>';
  html += `<button id="diagramLayerToggle" onclick="window.__diagram.toggleLayers()" title="Show architectural layers (Domain, Application, Infrastructure)" style="${showLayers ? 'background:var(--bg-hover)' : ''}">⊞ Layers</button>`;
  html += '<span class="diagram-toolbar-sep"></span>';
  html += '<div class="rel-dropdown" id="diagramKindFilterWrap"></div>';
  html += '<span class="diagram-toolbar-sep"></span>';
  html += '<div class="rel-dropdown" id="diagramEdgeFilterWrap"></div>';
  html += '</div>';

  // Zoom controls (top-right)
  html += '<div class="diagram-controls">';
  html += '<button onclick="window.__diagram.zoom(1.25)" title="Zoom in">+</button>';
  html += '<button onclick="window.__diagram.zoom(0.8)" title="Zoom out">−</button>';
  html += '<button onclick="window.__diagram.fit()" title="Fit to view">⊡</button>';
  html += '<button onclick="window.__diagram.downloadSvg()" title="Download as SVG">⬇ SVG</button>';
  html += '</div>';

  // Edge legend
  html += '<div class="diagram-edge-legend">';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line" style="color:#60a5fa"></span>Contains</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line dashed" style="color:#34d399"></span>References</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line dashed" style="color:#34d399"></span>References (by Id)</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line" style="color:#60a5fa"></span>Has</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line" style="color:#60a5fa"></span>Has Many</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line dashed" style="color:#fbbf24"></span>Emits</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line dashed" style="color:#f472b6"></span>Handles</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line dashed" style="color:#2dd4bf"></span>Publishes</div>';
  html += '<div class="diagram-edge-legend-item"><span class="diagram-edge-legend-line" style="color:#fb923c"></span>Manages</div>';
  html += '</div>';

  html += '<svg id="diagramSvg"></svg>';
  html += '</div>';
  return html;
}

// ── Build the diagram graph & start interaction ──────
export function initDiagram(ctx, boundedContexts) {
  if (!ctx) return;

  migrateLegacyDiagramLocalStorage();

  const nodes = [];
  const edges = [];
  const nMap = {};

  const kindCfg = KIND_CFG;

  // Build a lookup: fullName → bounded context name and layer
  const nodeContextMap = {};
  const nodeLayerMap = {};
  const allSections = ['aggregates', 'entities', 'valueObjects', 'subTypes', 'domainEvents', 'integrationEvents', 'commandHandlerTargets', 'eventHandlers', 'commandHandlers', 'queryHandlers', 'repositories', 'domainServices'];
  if (boundedContexts && boundedContexts.length > 1) {
    for (const bc of boundedContexts) {
      for (const sec of allSections) {
        for (const item of (bc[sec] || [])) {
          nodeContextMap[item.fullName] = bc.name;
          if (item.layer) nodeLayerMap[item.fullName] = item.layer;
        }
      }
    }
  } else if (boundedContexts && boundedContexts.length === 1) {
    for (const sec of allSections) {
      for (const item of (boundedContexts[0][sec] || [])) {
        if (item.layer) nodeLayerMap[item.fullName] = item.layer;
      }
    }
  }

  function addNode(item, kind) {
    if (nMap[item.fullName]) return;
    const cfg = kindCfg[kind];
    const n = {
      id: item.fullName, name: item.name, kind, cfg,
      contextName: nodeContextMap[item.fullName] || null,
      layerName: nodeLayerMap[item.fullName] || item.layer || null,
      props: (item.properties || []).slice(0, 5).map(p => p.name + ': ' + p.typeName),
      methods: (item.methods || []).map(m => m.name + '(' + (m.parameters || []).map(p => p.typeName).join(', ') + ')'),
      events: (item.emittedEvents || []).map(e => '\u26A1 ' + shortName(e)),
      x: 0, y: 0, vx: 0, vy: 0, w: NODE_W, h: 0
    };
    n.h = nodeHeight(n);
    nodes.push(n);
    nMap[item.fullName] = n;
  }

  (ctx.aggregates || []).forEach(a => addNode(a, 'aggregate'));
  (ctx.entities || []).forEach(e => addNode(e, 'entity'));
  (ctx.valueObjects || []).forEach(v => addNode(v, 'valueObject'));
  (ctx.subTypes || []).forEach(s => addNode(s, 'subType'));
  (ctx.domainEvents || []).forEach(e => addNode(e, 'event'));
  (ctx.integrationEvents || []).forEach(e => addNode(e, 'integrationEvent'));
  (ctx.commandHandlerTargets || []).forEach(c => addNode(c, 'commandHandlerTarget'));
  (ctx.eventHandlers || []).forEach(h => addNode(h, 'eventHandler'));
  (ctx.commandHandlers || []).forEach(h => addNode(h, 'commandHandler'));
  (ctx.queryHandlers || []).forEach(h => addNode(h, 'queryHandler'));
  (ctx.repositories || []).forEach(r => addNode(r, 'repository'));
  (ctx.domainServices || []).forEach(s => addNode(s, 'service'));

  for (const rel of (ctx.relationships || [])) {
    if (nMap[rel.sourceType] && nMap[rel.targetType]) {
      edges.push({ source: rel.sourceType, target: rel.targetType, kind: rel.kind, label: rel.label || '' });
    }
  }

  const contextName = ctx.name;
  let posSource = 'none';

  suppressDiagramLayoutFlush = true;
  try {
    const serverLayout = diagramLayoutBaseUrl ? serverLayoutDoc : null;

    const lsPos = loadPositions();
    let saved = null;
    if (serverLayout?.positions && typeof serverLayout.positions === 'object' && Object.keys(serverLayout.positions).length > 0) {
      saved = serverLayout.positions;
      posSource = 'server';
    } else if (lsPos && Object.keys(lsPos).length > 0) {
      saved = lsPos;
      posSource = 'local';
    }

    let hasSaved = false;
    if (saved) {
      let allFound = true;
      for (const n of nodes) {
        if (saved[n.id]) { n.x = saved[n.id].x; n.y = saved[n.id].y; }
        else { allFound = false; }
      }
      hasSaved = allFound;
    }

    if (!hasSaved) {
      applyAutoLayout(nodes, edges, nMap);
    }

    let hiddenKinds;
    if (serverLayout && Array.isArray(serverLayout.hiddenKinds)) {
      hiddenKinds = new Set(serverLayout.hiddenKinds);
    } else {
      hiddenKinds = loadHiddenKinds();
    }

    let hiddenEdgeKinds;
    if (serverLayout && Array.isArray(serverLayout.hiddenEdgeKinds)) {
      hiddenEdgeKinds = new Set(serverLayout.hiddenEdgeKinds);
    } else {
      hiddenEdgeKinds = loadHiddenEdgeKinds();
    }

    if (serverLayout && typeof serverLayout.showAliases === 'boolean') {
      showAliases = serverLayout.showAliases;
      try { localStorage.setItem(SHOW_ALIASES_KEY, showAliases ? 'true' : 'false'); } catch { /* ignore */ }
    }
    if (serverLayout && typeof serverLayout.showLayers === 'boolean') {
      showLayers = serverLayout.showLayers;
      try { localStorage.setItem(SHOW_LAYERS_KEY, showLayers); } catch { /* ignore */ }
    }

    dgState = {
      nodes, edges, nMap, allNodes: nodes, allEdges: edges,
      zoom: 1, panX: 0, panY: 0, contextName,
      hiddenKinds, hiddenEdgeKinds,
    };
    applyDiagramVisibility();

    const lsViewport = loadViewport();
    let savedViewport = null;
    if (hasSaved) {
      if (serverLayout?.viewport && typeof serverLayout.viewport.zoom === 'number') {
        savedViewport = serverLayout.viewport;
      } else {
        savedViewport = lsViewport;
      }
    }
    if (hasSaved && savedViewport) {
      dgState.zoom = savedViewport.zoom;
      dgState.panX = savedViewport.panX;
      dgState.panY = savedViewport.panY;
    }

    renderSvg();
    refreshDiagramKindFilters();
    syncDiagramToolbarToggles();

    if (!hasSaved || !savedViewport) {
      fitToView();
      saveViewport(dgState.zoom, dgState.panX, dgState.panY);
    }

    if (posSource === 'server') {
      const positions = {};
      for (const n of nodes) {
        positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
      }
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(positions));
      } catch { /* ignore */ }
      try {
        localStorage.setItem(HIDDEN_KINDS_KEY, JSON.stringify([...hiddenKinds]));
      } catch { /* ignore */ }
      try {
        localStorage.setItem(HIDDEN_EDGE_KINDS_KEY, JSON.stringify([...hiddenEdgeKinds]));
      } catch { /* ignore */ }
      if (savedViewport) {
        try {
          localStorage.setItem(VIEWPORT_KEY, JSON.stringify({
            zoom: Math.round(savedViewport.zoom * 1000) / 1000,
            panX: Math.round(savedViewport.panX * 10) / 10,
            panY: Math.round(savedViewport.panY * 10) / 10,
          }));
        } catch { /* ignore */ }
      }
    }
  } finally {
    suppressDiagramLayoutFlush = false;
  }

  if (posSource === 'local' && diagramLayoutBaseUrl && dgState) {
    void (async () => {
      const payload = buildLayoutPayloadFromState();
      if (!payload) return;
      try {
        const res = await fetch(`${diagramLayoutBaseUrl}/diagram-layout`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        });
        if (res.ok) serverLayoutDoc = payload;
      } catch { /* ignore */ }
    })();
  }

  setupInteraction();
}

// ── Kind filter pills ────────────────────────────────
function renderDiagramKindFilters() {
  if (!dgState) return '';
  const presentKinds = new Set(dgState.allNodes.map(n => n.kind));
  if (presentKinds.size === 0) return '';

  const visibleKinds = [...presentKinds].filter(kind => !dgState.hiddenKinds.has(kind)).length;
  let html = `<button class="rel-dropdown-trigger" id="diagramKindFilterTrigger" onclick="window.__diagram.toggleKindFilter()" title="Filter node types">`;
  html += '<span style="font-size:10px;opacity:.7">◈</span>';
  html += '<span>Node Types</span>';
  html += `<span class="rel-hidden-count">${visibleKinds}/${presentKinds.size}</span>`;
  html += '<span class="rel-chevron">▾</span>';
  html += '</button>';

  html += '<div class="rel-dropdown-menu" id="diagramKindFilterMenu">';
  html += '<div class="rel-dropdown-actions">';
  html += '<button type="button" onclick="window.__diagram.showAllKinds()">Show all</button>';
  html += '<button type="button" onclick="window.__diagram.hideAllKinds()">Hide all</button>';
  html += '</div>';

  for (const [kind, cfg] of Object.entries(KIND_CFG)) {
    if (!presentKinds.has(kind)) continue;
    const visible = !dgState.hiddenKinds.has(kind);
    const count = dgState.allNodes.filter(n => n.kind === kind).length;
    html += `<div class="rel-dropdown-item${visible ? ' checked' : ''}" onclick="window.__diagram.toggleKind(event, '${kind}')" data-node-kind="${kind}">`;
    html += `<span class="rel-check">${visible ? '✓' : ''}</span>`;
    html += `<span class="diagram-kind-dot" style="background:${cfg.color}"></span>`;
    html += `<span class="rel-kind-label">${esc(cfg.label)}</span>`;
    html += `<span class="diagram-kind-count">${count}</span>`;
    html += '</div>';
  }
  html += '</div>';
  return html;
}

function refreshDiagramKindFilters() {
  const el = document.getElementById('diagramKindFilterWrap');
  const prevMenu = document.getElementById('diagramKindFilterMenu');
  const prevTrigger = document.getElementById('diagramKindFilterTrigger');
  const wasVisible = !!prevMenu?.classList.contains('visible');
  const wasOpen = !!prevTrigger?.classList.contains('open');
  if (el) el.innerHTML = renderDiagramKindFilters();
  if (wasVisible || wasOpen) {
    const nextMenu = document.getElementById('diagramKindFilterMenu');
    const nextTrigger = document.getElementById('diagramKindFilterTrigger');
    if (nextMenu) nextMenu.classList.add('visible');
    if (nextTrigger) nextTrigger.classList.add('open');
  }
  refreshDiagramEdgeFilter();
}

function syncDiagramKindFilterUi() {
  if (!dgState) return;
  const trigger = document.getElementById('diagramKindFilterTrigger');
  const menu = document.getElementById('diagramKindFilterMenu');
  if (!trigger || !menu) {
    refreshDiagramKindFilters();
    return;
  }

  const presentKinds = new Set(dgState.allNodes.map(n => n.kind));
  const visibleKinds = [...presentKinds].filter(kind => !dgState.hiddenKinds.has(kind)).length;
  const badge = trigger.querySelector('.rel-hidden-count');
  if (badge) badge.textContent = `${visibleKinds}/${presentKinds.size}`;

  const rows = menu.querySelectorAll('[data-node-kind]');
  for (const row of rows) {
    const kind = row.getAttribute('data-node-kind');
    if (!kind) continue;
    const visible = !dgState.hiddenKinds.has(kind);
    row.classList.toggle('checked', visible);
    const check = row.querySelector('.rel-check');
    if (check) check.textContent = visible ? '✓' : '';
  }
}

// ── Edge-kind filter dropdown ────────────────────────
function renderDiagramEdgeFilter() {
  if (!dgState) return '';
  const presentEdgeKinds = new Set(dgState.allEdges.map(e => e.kind));
  if (presentEdgeKinds.size === 0) return '';

  const hiddenCount = dgState.hiddenEdgeKinds.size;
  let h = `<button class="rel-dropdown-trigger" id="diagramEdgeFilterTrigger" onclick="window.__diagram.toggleEdgeFilter()" title="Filter relation types">`;
  h += '<span style="font-size:10px;opacity:.7">⟜</span>';
  h += '<span>Relations</span>';
  if (hiddenCount > 0) h += `<span class="rel-hidden-count">${hiddenCount}</span>`;
  h += '<span class="rel-chevron">▾</span>';
  h += '</button>';

  h += '<div class="rel-dropdown-menu" id="diagramEdgeFilterMenu">';
  for (const [kind, cfg] of Object.entries(EDGE_CFG)) {
    if (!presentEdgeKinds.has(kind)) continue;
    const visible = !dgState.hiddenEdgeKinds.has(kind);
    h += `<div class="rel-dropdown-item${visible ? ' checked' : ''}" onclick="window.__diagram.toggleEdgeKind(event, '${kind}')" data-edge-kind="${kind}">`;
    h += `<span class="rel-check">${visible ? '✓' : ''}</span>`;
    h += `<span class="rel-line-sample${cfg.dashed ? ' dashed' : ''}" style="color:${cfg.color}"></span>`;
    h += `<span class="rel-kind-label">${esc(cfg.label)}</span>`;
    h += '</div>';
  }
  h += '</div>';
  return h;
}

function refreshDiagramEdgeFilter() {
  const el = document.getElementById('diagramEdgeFilterWrap');
  if (el) el.innerHTML = renderDiagramEdgeFilter();
}

// ── Visibility filtering ─────────────────────────────
function applyDiagramVisibility() {
  if (!dgState) return;
  const visibleIds = new Set();
  dgState.nodes = dgState.allNodes.filter(n => {
    if (dgState.hiddenKinds.has(n.kind)) return false;
    visibleIds.add(n.id);
    return true;
  });
  dgState.edges = dgState.allEdges.filter(e =>
    visibleIds.has(e.source) && visibleIds.has(e.target) && !dgState.hiddenEdgeKinds.has(e.kind)
  );
}

// ── Auto-layout (row-based + forces) ─────────────────
function applyAutoLayout(nodes, edges, nMap) {
  // Group nodes by bounded context for initial placement
  const ctxGroups = {};
  for (const n of nodes) {
    const key = n.contextName || '__default';
    (ctxGroups[key] = ctxGroups[key] || []).push(n);
  }
  const ctxNames = Object.keys(ctxGroups).sort();
  const hasMultipleContexts = ctxNames.length > 1 && !ctxNames.includes('__default');

  const kindRow = { aggregate: 0, entity: 1, valueObject: 1, subType: 1, event: 2, integrationEvent: 2, commandHandlerTarget: 2, eventHandler: 3, commandHandler: 3, queryHandler: 3, repository: 4, service: 4 };

  if (hasMultipleContexts) {
    // Layout each context as a separate group, offset horizontally
    let xOffset = 0;
    for (const ctxName of ctxNames) {
      const ctxNodes = ctxGroups[ctxName];
      const rowBuckets = {};
      for (const n of ctxNodes) { const r = kindRow[n.kind] || 0; (rowBuckets[r] = rowBuckets[r] || []).push(n); }
      let maxRowWidth = 0;
      for (const [row, rNodes] of Object.entries(rowBuckets)) {
        const y = parseInt(row) * 240;
        rNodes.forEach((n, i) => { n.x = xOffset + i * 270; n.y = y; });
        maxRowWidth = Math.max(maxRowWidth, rNodes.length * 270);
      }
      xOffset += maxRowWidth + 200; // gap between contexts
    }
  } else {
    const rowBuckets = {};
    for (const n of nodes) { const r = kindRow[n.kind] || 0; (rowBuckets[r] = rowBuckets[r] || []).push(n); }
    for (const [row, rNodes] of Object.entries(rowBuckets)) {
      const y = parseInt(row) * 240;
      rNodes.forEach((n, i) => { n.x = (i - (rNodes.length - 1) / 2) * 270; n.y = y; });
    }
  }

  // Force simulation
  for (let i = 0; i < 150; i++) {
    const alpha = 1 - i / 150;
    for (let a = 0; a < nodes.length; a++) {
      for (let b = a + 1; b < nodes.length; b++) {
        const na = nodes[a], nb = nodes[b];
        let dx = nb.x - na.x, dy = nb.y - na.y;
        const dist = Math.sqrt(dx * dx + dy * dy) || 1;
        // Stronger repulsion between nodes of different contexts
        const crossCtx = hasMultipleContexts && na.contextName !== nb.contextName;
        const repStrength = crossCtx ? 16000 : 8000;
        const force = (repStrength * alpha) / (dist * dist);
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
    for (const n of nodes) {
      n.vx *= 0.82; n.vy *= 0.82;
      n.x += n.vx; n.y += n.vy;
    }
  }
  for (const n of nodes) { n.vx = 0; n.vy = 0; }
}

// ── Bounded context boundary colors ──────────────────
const BC_COLORS = ['#6366f1', '#06b6d4', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6', '#ec4899', '#14b8a6'];

function computeContextBounds(nodes) {
  const groups = {};
  for (const n of nodes) {
    if (!n.contextName) continue;
    if (!groups[n.contextName]) groups[n.contextName] = [];
    groups[n.contextName].push(n);
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

// ── Layer boundary colors ────────────────────────────
const LAYER_COLORS = { Domain: '#a78bfa', Application: '#60a5fa', Infrastructure: '#fb923c' };

function computeLayerBounds(nodes) {
  // Group by (contextName or '__default', layerName) so each bounded context gets its own layer boundaries
  const groups = {};
  for (const n of nodes) {
    if (!n.layerName) continue;
    const key = (n.contextName || '__default') + '\0' + n.layerName;
    if (!groups[key]) groups[key] = { contextName: n.contextName, layerName: n.layerName, nodes: [] };
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
    const color = LAYER_COLORS[g.layerName] || '#888';
    bounds.push({ name: g.layerName, x: minX - pad, y: minY - pad - 24, w: maxX - minX + pad * 2, h: maxY - minY + pad * 2 + 24, color });
  }
  return bounds;
}

// ── SVG rendering ────────────────────────────────────
function renderSvg() {
  const svg = document.getElementById('diagramSvg');
  if (!svg || !dgState) return;
  const { nodes, edges, nMap } = dgState;

  const edgeColors = { Contains: '#60a5fa', References: '#34d399', ReferencesById: '#34d399', Has: '#60a5fa', HasMany: '#60a5fa', Emits: '#fbbf24', Handles: '#f472b6', Manages: '#fb923c', Publishes: '#2dd4bf' };

  let s = '<defs>';
  for (const [kind, color] of Object.entries(edgeColors)) {
    s += `<marker id="arrow-${kind}" viewBox="0 0 10 6" refX="10" refY="3" markerWidth="8" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,3 L0,6 Z" fill="${color}" /></marker>`;
  }
  s += `<marker id="diamond" viewBox="0 0 12 8" refX="0" refY="4" markerWidth="10" markerHeight="8" orient="auto-start-reverse"><path d="M0,4 L6,0 L12,4 L6,8 Z" fill="#60a5fa" /></marker>`;
  s += '</defs>';

  s += `<g id="diagramViewport" transform="translate(${dgState.panX},${dgState.panY}) scale(${dgState.zoom})">`;

  // Bounded context boundaries (drawn first, behind everything)
  const ctxBounds = computeContextBounds(nodes);
  dgState._ctxBounds = ctxBounds; // stash for hit-testing
  for (const b of ctxBounds) {
    s += `<g class="dg-ctx-boundary" data-ctx="${escAttr(b.name)}" style="cursor:move">`;
    s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="12" fill="rgba(255,255,255,.10)" stroke="${b.color}" stroke-width="1.5" stroke-dasharray="8,5" opacity="0.8" />`;
    s += `<text x="${b.x + 14}" y="${b.y + 24}" fill="${b.color}" font-size="20" font-weight="700" font-family="-apple-system,sans-serif" opacity="0.85">${esc(b.name)}</text>`;
    s += '</g>';
  }

  // Layer boundaries (behind nodes, in front of context boundaries)
  if (showLayers) {
    const layerBounds = computeLayerBounds(nodes);
    for (const b of layerBounds) {
      s += `<g class="dg-layer-boundary">`;
      s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="8" fill="none" stroke="${b.color}" stroke-width="1" stroke-dasharray="4,3" opacity="0.6" />`;
      s += `<text x="${b.x + 10}" y="${b.y + 18}" fill="${b.color}" font-size="13" font-weight="600" font-family="-apple-system,sans-serif" font-style="italic" opacity="0.7">${esc(b.name)}</text>`;
      s += '</g>';
    }
  }

  // Edges
  for (const e of edges) {
    const src = nMap[e.source], tgt = nMap[e.target];
    if (!src || !tgt) continue;
    const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
    const tgtCx = tgt.x + tgt.w / 2, tgtCy = tgt.y + tgt.h / 2;
    const p1 = sourceAnchorForEdge(src, tgt, e) || rectEdge(srcCx, srcCy, src.w + 8, src.h + 8, tgtCx, tgtCy);
    const p2 = rectEdge(tgtCx, tgtCy, tgt.w + 8, tgt.h + 8, srcCx, srcCy);
    const color = edgeColors[e.kind] || '#5c6070';
    const dashed = (e.kind === 'Emits' || e.kind === 'Handles' || e.kind === 'Publishes' || e.kind === 'References' || e.kind === 'ReferencesById') ? ' stroke-dasharray="6,4"' : '';
    const markerStart = e.kind === 'Contains' ? ' marker-start="url(#diamond)"' : '';
    const markerEnd = (e.kind === 'References' || e.kind === 'ReferencesById') ? '' : ` marker-end="url(#arrow-${e.kind})"`;
    s += `<line x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="${color}" stroke-width="1.5"${dashed}${markerStart}${markerEnd} opacity="0.65" />`;
    const mx = (p1.x + p2.x) / 2, my = (p1.y + p2.y) / 2;
    const label = e.label || e.kind;
    s += `<text x="${mx}" y="${my - 6}" text-anchor="middle" fill="${color}" font-size="9" font-family="-apple-system,sans-serif" opacity="0.7">${esc(label)}</text>`;
  }

  // Nodes
  for (const n of nodes) {
    n.h = nodeHeight(n);
    const c = n.cfg;
    const traceCls = traceHighlightIds.has(n.id) ? ' dg-node-trace' : '';
    s += `<g class="dg-node${traceCls}" data-id="${escAttr(n.id)}" transform="translate(${n.x},${n.y})" style="cursor:pointer">`;
    s += `<rect x="3" y="3" width="${n.w}" height="${n.h}" rx="8" fill="rgba(0,0,0,.3)" />`;
    s += `<rect width="${n.w}" height="${n.h}" rx="8" fill="${c.bg}" stroke="${c.border}" stroke-width="1.5" />`;
    let ty = 20;
    s += `<text x="${n.w/2}" y="${ty}" text-anchor="middle" fill="${c.color}" font-size="10" font-family="-apple-system,sans-serif" opacity="0.9">${c.stereotype}</text>`;
    ty += NAME_PAD;
    const nameLines = wrapName(diagramDisplayName(n));
    s += `<text x="${n.w/2}" text-anchor="middle" fill="#f0f2f7" font-size="14" font-weight="600" font-family="-apple-system,sans-serif">`;
    for (const ln of nameLines) {
      ty += NAME_LINE_H;
      s += `<tspan x="${n.w/2}" y="${ty}">${esc(ln)}</tspan>`;
    }
    s += '</text>';
    if (n.props.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w-12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const p of n.props) { ty += 17; s += `<text x="16" y="${ty}" fill="#a0a4b8" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(p)}</text>`; }
    }
    if (n.methods.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w-12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const m of n.methods) { ty += 17; s += `<text x="16" y="${ty}" fill="#a78bfa" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(m)}</text>`; }
    }
    if (n.events.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w-12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const ev of n.events) { ty += 17; s += `<text x="16" y="${ty}" fill="#fbbf24" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(ev)}</text>`; }
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
  if (!dgState || dgState.nodes.length === 0) return;
  const wrap = document.getElementById('diagramWrap');
  if (!wrap) return;
  const nodes = dgState.nodes;
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const n of nodes) {
    minX = Math.min(minX, n.x); minY = Math.min(minY, n.y);
    maxX = Math.max(maxX, n.x + n.w); maxY = Math.max(maxY, n.y + n.h);
  }
  const pad = 80;
  const gw = maxX - minX + pad * 2, gh = maxY - minY + pad * 2;
  const ww = wrap.clientWidth, wh = wrap.clientHeight;
  const zoom = Math.min(ww / gw, wh / gh, 1.5);
  dgState.zoom = zoom;
  dgState.panX = (ww - gw * zoom) / 2 - minX * zoom + pad * zoom;
  dgState.panY = (wh - gh * zoom) / 2 - minY * zoom + pad * zoom;
  renderSvg();
}

// ── Interaction (drag nodes, drag context groups, pan, zoom) ──
function setupInteraction() {
  const svg = document.getElementById('diagramSvg');
  if (!svg || !dgState) return;

  let dragNode = null, dragOffX = 0, dragOffY = 0;
  let dragCtx = null, dragCtxStartX = 0, dragCtxStartY = 0, dragCtxNodeStarts = null;
  let panning = false, panStartX = 0, panStartY = 0, panOrigX = 0, panOrigY = 0;

  svg.addEventListener('mousedown', function(ev) {
    const nodeEl = ev.target.closest('.dg-node');
    const ctxEl = ev.target.closest('.dg-ctx-boundary');

    if (nodeEl) {
      ev.preventDefault();
      const n = dgState.nMap[nodeEl.dataset.id];
      if (!n) return;
      dragNode = n;
      const pt = svgPoint(svg, ev);
      dragOffX = pt.x - n.x; dragOffY = pt.y - n.y;
      svg.classList.add('dragging-node');
    } else if (ctxEl) {
      ev.preventDefault();
      const ctxName = ctxEl.dataset.ctx;
      dragCtx = ctxName;
      const pt = svgPoint(svg, ev);
      dragCtxStartX = pt.x; dragCtxStartY = pt.y;
      // Snapshot starting positions of all nodes in this context (visible + hidden)
      dragCtxNodeStarts = new Map();
      for (const n of dgState.allNodes) {
        if (n.contextName === ctxName) {
          dragCtxNodeStarts.set(n.id, { x: n.x, y: n.y });
        }
      }
      svg.classList.add('dragging-node');
    } else {
      panning = true;
      panStartX = ev.clientX; panStartY = ev.clientY;
      panOrigX = dgState.panX; panOrigY = dgState.panY;
      svg.classList.add('dragging');
    }
  });

  svg.addEventListener('mousemove', function(ev) {
    if (dragNode) {
      const pt = svgPoint(svg, ev);
      dragNode.x = pt.x - dragOffX; dragNode.y = pt.y - dragOffY;
      renderSvg();
    } else if (dragCtx) {
      const pt = svgPoint(svg, ev);
      const dx = pt.x - dragCtxStartX, dy = pt.y - dragCtxStartY;
      for (const [id, start] of dragCtxNodeStarts) {
        const n = dgState.nMap[id];
        if (n) { n.x = start.x + dx; n.y = start.y + dy; }
      }
      renderSvg();
    } else if (panning) {
      dgState.panX = panOrigX + (ev.clientX - panStartX);
      dgState.panY = panOrigY + (ev.clientY - panStartY);
      renderSvg();
    }
  });

  function endDrag() {
    if (dragNode || dragCtx) {
      // Persist ALL node positions (including hidden) after drop
      savePositions(dgState.allNodes);
    }
    if (dragNode || dragCtx || panning) {
      saveViewport(dgState.zoom, dgState.panX, dgState.panY);
    }
    dragNode = null;
    dragCtx = null; dragCtxNodeStarts = null;
    panning = false;
    svg.classList.remove('dragging', 'dragging-node');
  }
  svg.addEventListener('mouseup', endDrag);
  svg.addEventListener('mouseleave', endDrag);

  svg.addEventListener('wheel', function(ev) {
    ev.preventDefault();
    const factor = ev.deltaY < 0 ? 1.1 : 0.9;
    const rect = svg.getBoundingClientRect();
    const mx = ev.clientX - rect.left, my = ev.clientY - rect.top;
    dgState.panX = mx - (mx - dgState.panX) * factor;
    dgState.panY = my - (my - dgState.panY) * factor;
    dgState.zoom *= factor;
    renderSvg();
    saveViewport(dgState.zoom, dgState.panX, dgState.panY);
  }, { passive: false });

  svg.addEventListener('dblclick', function(ev) {
    const nodeEl = ev.target.closest('.dg-node');
    if (nodeEl) window.__nav.navigateTo(nodeEl.dataset.id);
  });
}

function svgPoint(svg, ev) {
  const rect = svg.getBoundingClientRect();
  return {
    x: (ev.clientX - rect.left - dgState.panX) / dgState.zoom,
    y: (ev.clientY - rect.top - dgState.panY) / dgState.zoom
  };
}

// ── Public API (exposed on window.__diagram) ─────────
export function diagramZoom(factor) {
  if (!dgState) return;
  const wrap = document.getElementById('diagramWrap');
  if (!wrap) return;
  const cx = wrap.clientWidth / 2, cy = wrap.clientHeight / 2;
  dgState.panX = cx - (cx - dgState.panX) * factor;
  dgState.panY = cy - (cy - dgState.panY) * factor;
  dgState.zoom *= factor;
  renderSvg();
  saveViewport(dgState.zoom, dgState.panX, dgState.panY);
}

export function diagramFit() {
  fitToView();
  if (dgState) saveViewport(dgState.zoom, dgState.panX, dgState.panY);
}

export function diagramResetLayout(ctx) {
  if (!dgState || !ctx) return;
  clearPositions();
  clearViewport();
  applyAutoLayout(dgState.allNodes, dgState.allEdges, dgState.nMap);
  applyDiagramVisibility();
  renderSvg();
  fitToView();
  savePositions(dgState.allNodes);
  saveViewport(dgState.zoom, dgState.panX, dgState.panY);
}

export function diagramToggleKind(eventOrKind, maybeKind) {
  const hasEventArg = typeof eventOrKind === 'object' && eventOrKind !== null;
  const kind = hasEventArg ? maybeKind : eventOrKind;
  if (hasEventArg) {
    eventOrKind.stopPropagation();
    eventOrKind.preventDefault();
  }
  if (!dgState) return;
  if (typeof kind !== 'string') return;
  if (dgState.hiddenKinds.has(kind)) {
    dgState.hiddenKinds.delete(kind);
  } else {
    dgState.hiddenKinds.add(kind);
  }
  saveHiddenKinds(dgState.hiddenKinds);
  applyDiagramVisibility();
  renderSvg();
  syncDiagramKindFilterUi();
}

function setAllKindVisibility(visible) {
  if (!dgState) return;
  const presentKinds = new Set(dgState.allNodes.map(n => n.kind));
  if (visible) {
    dgState.hiddenKinds.clear();
  } else {
    for (const kind of presentKinds) {
      dgState.hiddenKinds.add(kind);
    }
  }
  saveHiddenKinds(dgState.hiddenKinds);
  applyDiagramVisibility();
  renderSvg();
  syncDiagramKindFilterUi();
}

export function diagramShowAllKinds() {
  setAllKindVisibility(true);
}

export function diagramHideAllKinds() {
  setAllKindVisibility(false);
}

export function diagramShowAll() {
  if (!dgState) return;
  dgState.hiddenKinds.clear();
  dgState.hiddenEdgeKinds.clear();
  saveHiddenKinds(dgState.hiddenKinds);
  saveHiddenEdgeKinds(dgState.hiddenEdgeKinds);
  applyDiagramVisibility();
  renderSvg();
  refreshDiagramKindFilters();
}

export function diagramToggleEdgeKind(eventOrKind, maybeKind) {
  const hasEventArg = typeof eventOrKind === 'object' && eventOrKind !== null;
  const kind = hasEventArg ? maybeKind : eventOrKind;
  if (hasEventArg) {
    eventOrKind.stopPropagation();
    eventOrKind.preventDefault();
  }
  if (!dgState) return;
  if (typeof kind !== 'string') return;
  if (dgState.hiddenEdgeKinds.has(kind)) {
    dgState.hiddenEdgeKinds.delete(kind);
  } else {
    dgState.hiddenEdgeKinds.add(kind);
  }
  saveHiddenEdgeKinds(dgState.hiddenEdgeKinds);
  applyDiagramVisibility();
  renderSvg();
  refreshDiagramEdgeFilter();
}

function toggleDropdown(menuId, triggerId) {
  const menu = document.getElementById(menuId);
  const trigger = document.getElementById(triggerId);
  if (!menu) return;
  const open = menu.classList.toggle('visible');
  if (trigger) trigger.classList.toggle('open', open);
  if (!open) return;

  const close = (ev) => {
    const clickedTrigger = trigger && (ev.target === trigger || trigger.contains(ev.target));
    const containsTarget = menu.contains(ev.target);
    if (!containsTarget && !clickedTrigger) {
      menu.classList.remove('visible');
      if (trigger) trigger.classList.remove('open');
      document.removeEventListener('click', close);
    }
  };
  setTimeout(() => document.addEventListener('click', close), 0);
}

export function diagramToggleKindFilter() {
  toggleDropdown('diagramKindFilterMenu', 'diagramKindFilterTrigger');
}

export function diagramToggleEdgeFilter() {
  toggleDropdown('diagramEdgeFilterMenu', 'diagramEdgeFilterTrigger');
}

export function diagramDownloadSvg() {
  const svg = document.getElementById('diagramSvg');
  if (!svg) return;
  const clone = svg.cloneNode(true);
  clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
  bg.setAttribute('width', '100%'); bg.setAttribute('height', '100%');
  bg.setAttribute('fill', '#0f1117');
  clone.insertBefore(bg, clone.firstChild);
  const blob = new Blob([clone.outerHTML], { type: 'image/svg+xml' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = 'domain-model-diagram.svg';
  document.body.appendChild(a); a.click();
  setTimeout(() => { document.body.removeChild(a); URL.revokeObjectURL(url); }, 100);
}

export function diagramToggleAliases() {
  showAliases = !showAliases;
  try { localStorage.setItem(SHOW_ALIASES_KEY, showAliases ? 'true' : 'false'); } catch { /* ignore */ }
  syncDiagramToolbarToggles();
  if (typeof window.__featureEditor?.onDiagramViewFlagsChanged === 'function') {
    window.__featureEditor.onDiagramViewFlagsChanged();
  }
  if (dgState) {
    scheduleFlushDiagramLayout();
    for (const n of dgState.allNodes) n.h = nodeHeight(n);
    renderSvg();
  }
}

export function diagramToggleLayers() {
  showLayers = !showLayers;
  try { localStorage.setItem(SHOW_LAYERS_KEY, showLayers); } catch { /* ignore */ }
  syncDiagramToolbarToggles();
  if (typeof window.__featureEditor?.onDiagramViewFlagsChanged === 'function') {
    window.__featureEditor.onDiagramViewFlagsChanged();
  }
  if (dgState) {
    scheduleFlushDiagramLayout();
    renderSvg();
  }
}

function diagramDisplayName(n) {
  if (!showAliases) return n.name;
  const meta = window.__metadata || {};
  const entry = meta[n.id];
  return (entry && entry.alias && entry.alias.trim()) ? entry.alias : n.name;
}
