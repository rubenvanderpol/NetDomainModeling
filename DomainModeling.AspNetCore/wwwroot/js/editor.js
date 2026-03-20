/**
 * Developer editor — a read-only diagram viewer with visibility controls.
 *
 * The underlying domain model data is fixed / immutable.
 * The editor adds:
 *  - Bounded-context selector
 *  - Per-node hide / show
 *  - Per-type (kind) hide / show
 *  - Download visible-only JSON / SVG
 *  - Drag, pan, zoom (positions persisted in localStorage)
 *  - Selection shows read-only properties in a side panel
 */
import { esc, escAttr, shortName, SECTION_META } from './helpers.js';
import { renderTabBar } from './tabs.js';

// ── Constants ────────────────────────────────────────
const EDITOR_STORAGE_KEY = 'domain-model-editor-positions';
const EDITOR_HIDDEN_KINDS_KEY = 'domain-model-editor-hidden-kinds';
const NODE_W = 200;
const PROP_H = 17;
const HEADER_H = 26;
const NAME_H = 24;
const DIVIDER_H = 8;
const PAD = 12;

const KIND_CFG = {
  aggregate:      { stereotype: '«Aggregate»',       color: '#d4a0ff', bg: '#1f1828', border: '#7c5aa8' },
  entity:         { stereotype: '«Entity»',          color: '#7ab8ff', bg: '#161e2c', border: '#4a7bbf' },
  valueObject:    { stereotype: '«Value Object»',    color: '#4ee8ad', bg: '#142820', border: '#36a87a' },
  event:          { stereotype: '«Domain Event»',       color: '#fdd04e', bg: '#2a2418', border: '#b89530' },
  integrationEvent: { stereotype: '«Integration Event»', color: '#48e8d8', bg: '#14282a', border: '#30a89e' },
  commandHandlerTarget: { stereotype: '«Handles target»', color: '#f0a050', bg: '#2a2218', border: '#c07830' },
  eventHandler:   { stereotype: '«Event Handler»',       color: '#ff8ac8', bg: '#2a1824', border: '#b85888' },
  commandHandler: { stereotype: '«Command Handler»', color: '#ff8ac8', bg: '#2a1824', border: '#b85888' },
  queryHandler:   { stereotype: '«Query Handler»',   color: '#ff8ac8', bg: '#2a1824', border: '#b85888' },
  repository:     { stereotype: '«Repository»',      color: '#ffab5c', bg: '#2a2018', border: '#b87838' },
  service:        { stereotype: '«Service»',         color: '#bda0ff', bg: '#1e1828', border: '#7860b0' },
};

const EDGE_COLORS = { Contains: '#60a5fa', References: '#34d399', ReferencesById: '#34d399', Emits: '#fbbf24', Handles: '#f472b6', Manages: '#fb923c', Publishes: '#2dd4bf' };

/** Maps diagram kind → context section key */
const KIND_TO_SECTION = {
  aggregate: 'aggregates', entity: 'entities', valueObject: 'valueObjects',
  event: 'domainEvents', integrationEvent: 'integrationEvents', commandHandlerTarget: 'commandHandlerTargets', eventHandler: 'eventHandlers',
  commandHandler: 'commandHandlers', queryHandler: 'queryHandlers',
  repository: 'repositories', service: 'domainServices',
};

/** Label used in the type-filter UI */
const KIND_LABEL = {
  aggregate: 'Aggregates', entity: 'Entities', valueObject: 'Value Objects',
  event: 'Domain Events', integrationEvent: 'Integration Events', commandHandlerTarget: 'Cmd handler targets', eventHandler: 'Event Handlers',
  commandHandler: 'Cmd Handlers', queryHandler: 'Query Handlers',
  repository: 'Repositories', service: 'Services',
};

// ── Persistence ──────────────────────────────────────

function loadEditorPositions(contextName) {
  try {
    const raw = localStorage.getItem(EDITOR_STORAGE_KEY);
    if (!raw) return null;
    const all = JSON.parse(raw);
    return all[contextName] || null;
  } catch { return null; }
}

function saveEditorPositions(contextName, nodes) {
  try {
    const raw = localStorage.getItem(EDITOR_STORAGE_KEY);
    const all = raw ? JSON.parse(raw) : {};
    const positions = {};
    for (const n of nodes) {
      positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
    }
    all[contextName] = positions;
    localStorage.setItem(EDITOR_STORAGE_KEY, JSON.stringify(all));
  } catch { /* quota exceeded or private mode */ }
}

function clearEditorPositions(contextName) {
  try {
    const raw = localStorage.getItem(EDITOR_STORAGE_KEY);
    if (!raw) return;
    const all = JSON.parse(raw);
    delete all[contextName];
    localStorage.setItem(EDITOR_STORAGE_KEY, JSON.stringify(all));
  } catch { /* ignore */ }
}

function loadEditorHiddenKinds(contextName) {
  try {
    const raw = localStorage.getItem(EDITOR_HIDDEN_KINDS_KEY);
    if (!raw) return new Set();
    const all = JSON.parse(raw);
    return new Set(all[contextName] || []);
  } catch { return new Set(); }
}

function saveEditorHiddenKinds(contextName, hiddenKinds) {
  try {
    const raw = localStorage.getItem(EDITOR_HIDDEN_KINDS_KEY);
    const all = raw ? JSON.parse(raw) : {};
    all[contextName] = [...hiddenKinds];
    localStorage.setItem(EDITOR_HIDDEN_KINDS_KEY, JSON.stringify(all));
  } catch { /* quota exceeded or private mode */ }
}

// ── State ────────────────────────────────────────────
let st = null;

// ── Public API ───────────────────────────────────────

/** Initialise the editor with immutable domain data. */
export function initEditor(data) {
  st = {
    data: JSON.parse(JSON.stringify(data)),
    ctx: null, ctxIdx: 0,
    allNodes: [], allEdges: [], nMap: {},
    nodes: [], edges: [],
    zoom: 1, panX: 0, panY: 0,
    selectedNode: null, selectedEdge: null,
    hiddenNodes: new Set(),
    hiddenKinds: new Set(),
  };
  st.ctx = st.data.boundedContexts?.[0] || null;
  if (st.ctx) {
    st.hiddenKinds = loadEditorHiddenKinds(st.ctx.name);
    buildGraph(); applyVisibility();
  }
}

export function getEditData() { return st?.data; }

/** Returns HTML string for the editor view. */
export function renderEditorView() {
  if (!st) return '<div class="empty-state"><h2>No data</h2></div>';

  let html = renderTabBar('editor');

  // ── Toolbar ──
  html += '<div class="editor-toolbar" id="editorToolbar">';
  html += '<div class="editor-toolbar-left">';

  // Bounded-context selector
  const ctxs = st.data.boundedContexts || [];
  if (ctxs.length > 1) {
    html += '<select class="editor-ctx-select" onchange="window.__editor.switchContext(parseInt(this.value))">';
    ctxs.forEach((c, i) => {
      html += `<option value="${i}"${i === st.ctxIdx ? ' selected' : ''}>${esc(c.name)}</option>`;
    });
    html += '</select>';
  } else if (ctxs.length === 1) {
    html += `<span class="editor-ctx-label">${esc(ctxs[0].name)}</span>`;
  }

  // Separator
  html += '<span class="editor-toolbar-sep"></span>';

  // Type visibility toggles
  html += '<div class="editor-kind-filters" id="editorKindFilters">';
  html += renderKindFilters();
  html += '</div>';

  html += '</div>'; // toolbar-left

  html += '<div class="editor-toolbar-right">';
  const anyHidden = st.hiddenNodes.size > 0 || st.hiddenKinds.size > 0;
  html += `<button class="editor-btn secondary" onclick="window.__editor.showAll()" ${anyHidden ? '' : 'disabled'} id="editorShowAllBtn" title="Show all hidden items">Show All</button>`;
  html += '<button class="editor-btn secondary" onclick="window.__editor.downloadJson()" title="Download visible items as JSON">⬇ JSON</button>';
  html += '<button class="editor-btn secondary" onclick="window.__editor.downloadSvg()" title="Download diagram as SVG">⬇ SVG</button>';
  html += '</div>';
  html += '</div>'; // toolbar

  // ── Canvas + Panel layout ──
  html += '<div class="editor-body">';

  // SVG diagram area
  html += '<div class="editor-canvas" id="editorCanvas">';
  html += '<div class="diagram-controls">';
  html += '<button onclick="window.__editor.zoom(1.25)" title="Zoom in">+</button>';
  html += '<button onclick="window.__editor.zoom(0.8)" title="Zoom out">−</button>';
  html += '<button onclick="window.__editor.fit()" title="Fit to view">⊡</button>';
  html += '</div>';
  html += '<svg id="editorSvg"></svg>';
  html += '</div>';

  // Properties panel (right side)
  html += '<div class="editor-panel" id="editorPanel">';
  html += renderPanel();
  html += '</div>';

  html += '</div>';
  return html;
}

/** Called after the HTML is in the DOM. */
export function mountEditor() {
  if (!st) return;
  renderSvg();
  fitToView();
  setupInteraction();
}

// ── Kind filter pills ────────────────────────────────
function renderKindFilters() {
  // Only show kinds that exist in the current context
  const presentKinds = new Set(st.allNodes.map(n => n.kind));
  let html = '';
  for (const [kind, cfg] of Object.entries(KIND_CFG)) {
    if (!presentKinds.has(kind)) continue;
    const hidden = st.hiddenKinds.has(kind);
    const count = st.allNodes.filter(n => n.kind === kind).length;
    html += `<button class="editor-kind-pill${hidden ? ' hidden-kind' : ''}" onclick="window.__editor.toggleKind('${kind}')" title="${hidden ? 'Show' : 'Hide'} ${KIND_LABEL[kind] || kind}">`;
    html += `<span class="dot" style="background:${cfg.color}${hidden ? ';opacity:.35' : ''}"></span>`;
    html += `<span>${KIND_LABEL[kind] || kind}</span>`;
    html += `<span class="editor-kind-count">${count}</span>`;
    if (hidden) html += '<span class="editor-kind-hidden-icon">⊘</span>';
    html += '</button>';
  }
  return html;
}

function refreshKindFilters() {
  const el = document.getElementById('editorKindFilters');
  if (el) el.innerHTML = renderKindFilters();
  const showAll = document.getElementById('editorShowAllBtn');
  if (showAll) showAll.disabled = st.hiddenNodes.size === 0 && st.hiddenKinds.size === 0;
}

// ── Graph building ───────────────────────────────────
function buildGraph() {
  const ctx = st.ctx;
  st.allNodes = []; st.allEdges = []; st.nMap = {};
  st.selectedNode = null; st.selectedEdge = null;

  function addNode(item, kind) {
    if (st.nMap[item.fullName]) return;
    const cfg = KIND_CFG[kind];
    const n = {
      id: item.fullName, name: item.name, kind, cfg,
      props: (item.properties || []).slice(0, 5).map(p => p.name + ': ' + p.typeName),
      methods: (item.methods || []).map(m => m.name + '(' + (m.parameters || []).map(p => p.typeName).join(', ') + ')'),
      events: (item.emittedEvents || []).map(e => '\u26A1 ' + shortName(e)),
      x: 0, y: 0, vx: 0, vy: 0, w: NODE_W, h: 0,
    };
    n.h = nodeHeight(n);
    st.allNodes.push(n);
    st.nMap[item.fullName] = n;
  }

  (ctx.aggregates || []).forEach(a => addNode(a, 'aggregate'));
  (ctx.entities || []).forEach(e => addNode(e, 'entity'));
  (ctx.valueObjects || []).forEach(v => addNode(v, 'valueObject'));
  (ctx.domainEvents || []).forEach(e => addNode(e, 'event'));
  (ctx.integrationEvents || []).forEach(e => addNode(e, 'integrationEvent'));
  (ctx.commandHandlerTargets || []).forEach(c => addNode(c, 'commandHandlerTarget'));
  (ctx.eventHandlers || []).forEach(h => addNode(h, 'eventHandler'));
  (ctx.commandHandlers || []).forEach(h => addNode(h, 'commandHandler'));
  (ctx.queryHandlers || []).forEach(h => addNode(h, 'queryHandler'));
  (ctx.repositories || []).forEach(r => addNode(r, 'repository'));
  (ctx.domainServices || []).forEach(s => addNode(s, 'service'));

  for (const rel of (ctx.relationships || [])) {
    if (st.nMap[rel.sourceType] && st.nMap[rel.targetType]) {
      st.allEdges.push({ source: rel.sourceType, target: rel.targetType, kind: rel.kind, label: rel.label || '' });
    }
  }

  applyAutoLayout(st.allNodes, st.allEdges, st.nMap);

  // Restore saved positions if available
  const saved = loadEditorPositions(ctx.name);
  if (saved) {
    for (const n of st.allNodes) {
      if (saved[n.id]) { n.x = saved[n.id].x; n.y = saved[n.id].y; }
    }
  }
}

function nodeHeight(n) {
  let h = PAD + HEADER_H + NAME_H;
  if (n.props.length > 0) h += DIVIDER_H + n.props.length * PROP_H;
  if (n.methods.length > 0) h += DIVIDER_H + n.methods.length * PROP_H;
  if (n.events.length > 0) h += DIVIDER_H + n.events.length * PROP_H;
  return h + PAD;
}

// ── Visibility filtering ─────────────────────────────
function applyVisibility() {
  const visibleIds = new Set();
  st.nodes = st.allNodes.filter(n => {
    if (st.hiddenKinds.has(n.kind)) return false;
    if (st.hiddenNodes.has(n.id)) return false;
    visibleIds.add(n.id);
    return true;
  });
  st.edges = st.allEdges.filter(e => visibleIds.has(e.source) && visibleIds.has(e.target));

  // Deselect if hidden
  if (st.selectedNode && !visibleIds.has(st.selectedNode)) st.selectedNode = null;
  if (st.selectedEdge !== null && !st.edges[st.selectedEdge]) st.selectedEdge = null;
}

// ── Auto-layout ──────────────────────────────────────
function applyAutoLayout(nodes, edges, nMap) {
  const kindRow = { aggregate: 0, entity: 1, valueObject: 1, event: 2, integrationEvent: 2, commandHandlerTarget: 2, eventHandler: 3, commandHandler: 3, queryHandler: 3, repository: 4, service: 4 };
  const rowBuckets = {};
  for (const n of nodes) { const r = kindRow[n.kind] || 0; (rowBuckets[r] = rowBuckets[r] || []).push(n); }
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

// ── SVG rendering ────────────────────────────────────
function renderSvg() {
  const svg = document.getElementById('editorSvg');
  if (!svg || !st) return;

  let s = '<defs>';
  for (const [kind, color] of Object.entries(EDGE_COLORS)) {
    s += `<marker id="ed-arrow-${kind}" viewBox="0 0 10 6" refX="10" refY="3" markerWidth="8" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,3 L0,6 Z" fill="${color}" /></marker>`;
  }
  s += '<marker id="ed-diamond" viewBox="0 0 12 8" refX="0" refY="4" markerWidth="10" markerHeight="8" orient="auto-start-reverse"><path d="M0,4 L6,0 L12,4 L6,8 Z" fill="#60a5fa" /></marker>';
  s += '</defs>';

  s += `<g id="editorViewport" transform="translate(${st.panX},${st.panY}) scale(${st.zoom})">`;

  // Edges (only visible)
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
    const markerStart = e.kind === 'Contains' ? ' marker-start="url(#ed-diamond)"' : '';
    const markerEnd = (e.kind === 'References' || e.kind === 'ReferencesById') ? '' : ` marker-end="url(#ed-arrow-${e.kind})"`;
    const selected = st.selectedEdge === ei;
    const sw = selected ? 3 : 1.5;
    const op = selected ? 1 : 0.65;
    s += `<line class="dg-edge" data-idx="${ei}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="transparent" stroke-width="12" style="cursor:pointer" />`;
    s += `<line class="dg-edge-vis" data-idx="${ei}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="${color}" stroke-width="${sw}"${dashed}${markerStart}${markerEnd} opacity="${op}" style="pointer-events:none" />`;
    const mx = (p1.x + p2.x) / 2, my = (p1.y + p2.y) / 2;
    s += `<text x="${mx}" y="${my - 6}" text-anchor="middle" fill="${color}" font-size="9" font-family="-apple-system,sans-serif" opacity="0.7" style="pointer-events:none">${esc(e.label || e.kind)}</text>`;
  }

  // Nodes (only visible)
  for (const n of st.nodes) {
    const c = n.cfg;
    const selected = st.selectedNode === n.id;
    const strokeW = selected ? 2.5 : 1.5;
    const stroke = selected ? '#6366f1' : c.border;
    s += `<g class="dg-node" data-id="${escAttr(n.id)}" transform="translate(${n.x},${n.y})" style="cursor:pointer">`;
    s += `<rect x="3" y="3" width="${n.w}" height="${n.h}" rx="8" fill="rgba(0,0,0,.3)" />`;
    s += `<rect width="${n.w}" height="${n.h}" rx="8" fill="${c.bg}" stroke="${stroke}" stroke-width="${strokeW}" />`;
    let ty = 20;
    s += `<text x="${n.w / 2}" y="${ty}" text-anchor="middle" fill="${c.color}" font-size="10" font-family="-apple-system,sans-serif" opacity="0.85">${c.stereotype}</text>`;
    ty += 22;
    s += `<text class="dg-name" x="${n.w / 2}" y="${ty}" text-anchor="middle" fill="#f0f2f7" font-size="14" font-weight="600" font-family="-apple-system,sans-serif">${esc(n.name)}</text>`;
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
    if (n.events.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
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
  if (!st || st.nodes.length === 0) return;
  const wrap = document.getElementById('editorCanvas');
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

export function editorZoom(factor) {
  if (!st) return;
  const wrap = document.getElementById('editorCanvas');
  if (!wrap) return;
  const cx = wrap.clientWidth / 2, cy = wrap.clientHeight / 2;
  st.panX = cx - (cx - st.panX) * factor;
  st.panY = cy - (cy - st.panY) * factor;
  st.zoom *= factor;
  renderSvg();
}

export function editorFit() { fitToView(); }

// ── Interaction: drag, pan, zoom, select ─────────────
function setupInteraction() {
  const svg = document.getElementById('editorSvg');
  if (!svg || !st) return;

  let dragNode = null, dragOffX = 0, dragOffY = 0;
  let panning = false, panStartX = 0, panStartY = 0, panOrigX = 0, panOrigY = 0;

  svg.addEventListener('mousedown', function (ev) {
    const nodeEl = ev.target.closest('.dg-node');
    const edgeEl = ev.target.closest('.dg-edge');

    if (nodeEl) {
      ev.preventDefault();
      const n = st.nMap[nodeEl.dataset.id];
      if (!n) return;
      st.selectedNode = n.id; st.selectedEdge = null;
      dragNode = n;
      const pt = svgPoint(svg, ev);
      dragOffX = pt.x - n.x; dragOffY = pt.y - n.y;
      svg.classList.add('dragging-node');
      renderSvg(); refreshPanel();
    } else if (edgeEl) {
      ev.preventDefault();
      const idx = parseInt(edgeEl.dataset.idx);
      st.selectedEdge = idx; st.selectedNode = null;
      renderSvg(); refreshPanel();
    } else {
      // Deselect on background click
      if (st.selectedNode || st.selectedEdge !== null) {
        st.selectedNode = null; st.selectedEdge = null;
        renderSvg(); refreshPanel();
      }
      panning = true;
      panStartX = ev.clientX; panStartY = ev.clientY;
      panOrigX = st.panX; panOrigY = st.panY;
      svg.classList.add('dragging');
    }
  });

  svg.addEventListener('mousemove', function (ev) {
    if (dragNode) {
      const pt = svgPoint(svg, ev);
      dragNode.x = pt.x - dragOffX; dragNode.y = pt.y - dragOffY;
      renderSvg();
    } else if (panning) {
      st.panX = panOrigX + (ev.clientX - panStartX);
      st.panY = panOrigY + (ev.clientY - panStartY);
      renderSvg();
    }
  });

  function endDrag() {
    if (dragNode && st?.ctx) saveEditorPositions(st.ctx.name, st.allNodes);
    dragNode = null; panning = false;
    svg.classList.remove('dragging', 'dragging-node');
  }
  svg.addEventListener('mouseup', endDrag);
  svg.addEventListener('mouseleave', endDrag);

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

  // Keyboard: Escape to deselect
  document.addEventListener('keydown', function handler(ev) {
    if (!document.getElementById('editorSvg')) {
      document.removeEventListener('keydown', handler);
      return;
    }
    if (ev.target.tagName === 'INPUT' || ev.target.tagName === 'TEXTAREA' || ev.target.tagName === 'SELECT') return;
    if (ev.key === 'Escape') {
      st.selectedNode = null; st.selectedEdge = null;
      renderSvg(); refreshPanel();
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

// ── Properties panel (read-only) ─────────────────────
function renderPanel() {
  // Selected node
  if (st.selectedNode) {
    const n = st.nMap[st.selectedNode];
    if (!n) return renderPanelEmpty();
    const item = findDataItem(n.id, n.kind);

    let h = `<div class="editor-panel-title" style="color:${n.cfg.color}">${n.cfg.stereotype}</div>`;
    h += panelField('Name', n.name);
    h += panelField('Full Name', n.id);
    if (item?.description) h += panelField('Description', item.description);

    // Properties
    if (item?.properties?.length) {
      h += '<div class="editor-panel-section">Properties</div>';
      for (const p of item.properties) {
        h += `<div class="editor-panel-prop"><span class="editor-panel-prop-name">${esc(p.name)}</span> <span class="editor-panel-prop-type">${esc(p.typeName)}</span></div>`;
      }
    }

    // Relationships
    const relatedEdges = st.allEdges.filter(e => e.source === n.id || e.target === n.id);
    if (relatedEdges.length > 0) {
      h += '<div class="editor-panel-section">Relationships</div>';
      for (const e of relatedEdges) {
        const other = e.source === n.id ? shortName(e.target) : shortName(e.source);
        const dir = e.source === n.id ? '→' : '←';
        h += `<div class="editor-panel-rel">${dir} <span style="color:${EDGE_COLORS[e.kind] || '#888'}">${e.kind}</span> ${esc(other)}</div>`;
      }
    }

    // Hide button
    h += '<div class="editor-panel-section">Visibility</div>';
    h += `<button class="editor-btn secondary editor-panel-hide-btn" onclick="window.__editor.hideNode('${escAttr(n.id)}')">⊘ Hide this item</button>`;

    return h;
  }

  // Selected edge
  if (st.selectedEdge !== null) {
    const e = st.edges[st.selectedEdge];
    if (!e) return renderPanelEmpty();
    let h = `<div class="editor-panel-title" style="color:${EDGE_COLORS[e.kind] || '#888'}">Relationship</div>`;
    h += panelField('Source', shortName(e.source));
    h += panelField('Target', shortName(e.target));
    h += panelField('Kind', e.kind);
    if (e.label) h += panelField('Label', e.label);
    return h;
  }

  return renderPanelEmpty();
}

function renderPanelEmpty() {
  let h = '<div class="editor-panel-empty">Click a node or edge to inspect its properties.</div>';

  // Show hidden items list
  const hiddenItems = st.allNodes.filter(n => st.hiddenNodes.has(n.id));
  if (hiddenItems.length > 0) {
    h += '<div class="editor-panel-section">Hidden Items</div>';
    for (const n of hiddenItems) {
      h += `<div class="editor-panel-hidden-item">`;
      h += `<span class="dot" style="background:${n.cfg.color};width:6px;height:6px;border-radius:50%;flex-shrink:0"></span>`;
      h += `<span class="editor-panel-hidden-name">${esc(n.name)}</span>`;
      h += `<button class="editor-btn-inline" onclick="window.__editor.showNode('${escAttr(n.id)}')" title="Show">Show</button>`;
      h += '</div>';
    }
  }

  return h;
}

function panelField(label, value) {
  return `<div class="editor-panel-field"><label>${esc(label)}</label><div class="editor-panel-value">${esc(value)}</div></div>`;
}

function refreshPanel() {
  const panel = document.getElementById('editorPanel');
  if (panel) panel.innerHTML = renderPanel();
}

function findDataItem(fullName, kind) {
  const secKey = KIND_TO_SECTION[kind];
  const items = st.ctx[secKey] || [];
  return items.find(i => i.fullName === fullName) || null;
}

// ── Visibility actions ───────────────────────────────

/** Hide a single node by fullName. */
export function hideNode(fullName) {
  if (!st) return;
  st.hiddenNodes.add(fullName);
  st.selectedNode = null; st.selectedEdge = null;
  applyVisibility();
  renderSvg(); refreshPanel(); refreshKindFilters();
}

/** Show a previously hidden node. */
export function showNode(fullName) {
  if (!st) return;
  st.hiddenNodes.delete(fullName);
  applyVisibility();
  renderSvg(); refreshPanel(); refreshKindFilters();
}

/** Toggle visibility for an entire kind (type category). */
export function toggleKind(kind) {
  if (!st) return;
  if (st.hiddenKinds.has(kind)) {
    st.hiddenKinds.delete(kind);
  } else {
    st.hiddenKinds.add(kind);
  }
  saveEditorHiddenKinds(st.ctx.name, st.hiddenKinds);
  applyVisibility();
  renderSvg(); refreshPanel(); refreshKindFilters();
}

/** Show all hidden items and kinds. */
export function showAll() {
  if (!st) return;
  st.hiddenNodes.clear();
  st.hiddenKinds.clear();
  saveEditorHiddenKinds(st.ctx.name, st.hiddenKinds);
  applyVisibility();
  renderSvg(); refreshPanel(); refreshKindFilters();
}

// ── Context switching ────────────────────────────────

export function switchContext(idx) {
  if (!st) return;
  const ctxs = st.data.boundedContexts || [];
  if (idx < 0 || idx >= ctxs.length) return;
  st.ctxIdx = idx;
  st.ctx = ctxs[idx];
  st.hiddenNodes.clear();
  st.hiddenKinds = loadEditorHiddenKinds(st.ctx.name);
  st.selectedNode = null; st.selectedEdge = null;
  buildGraph(); // buildGraph restores saved positions if available
  applyVisibility();
  renderSvg(); refreshPanel(); refreshKindFilters();
  fitToView();
}

// ── Download ─────────────────────────────────────────

/** Download JSON containing only the visible items. */
export function downloadJson() {
  if (!st) return;
  const visibleIds = new Set(st.nodes.map(n => n.id));
  const ctx = st.ctx;

  function filterList(items) {
    return (items || []).filter(i => visibleIds.has(i.fullName));
  }

  const exported = {
    boundedContexts: [{
      name: ctx.name,
      aggregates: filterList(ctx.aggregates),
      entities: filterList(ctx.entities),
      valueObjects: filterList(ctx.valueObjects),
      domainEvents: filterList(ctx.domainEvents),
      integrationEvents: filterList(ctx.integrationEvents),
      commandHandlerTargets: filterList(ctx.commandHandlerTargets),
      eventHandlers: filterList(ctx.eventHandlers),
      commandHandlers: filterList(ctx.commandHandlers),
      queryHandlers: filterList(ctx.queryHandlers),
      repositories: filterList(ctx.repositories),
      domainServices: filterList(ctx.domainServices),
      relationships: (ctx.relationships || []).filter(r => visibleIds.has(r.sourceType) && visibleIds.has(r.targetType)),
    }],
  };
  const blob = new Blob([JSON.stringify(exported, null, 2)], { type: 'application/json' });
  downloadBlob(blob, 'domain-model.json');
}

export function downloadSvg() {
  const svg = document.getElementById('editorSvg');
  if (!svg) return;
  const clone = svg.cloneNode(true);
  clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
  const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
  bg.setAttribute('width', '100%'); bg.setAttribute('height', '100%');
  bg.setAttribute('fill', '#0f1117');
  clone.insertBefore(bg, clone.firstChild);
  const blob = new Blob([clone.outerHTML], { type: 'image/svg+xml' });
  downloadBlob(blob, 'domain-model-diagram.svg');
}

function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = filename;
  document.body.appendChild(a); a.click();
  setTimeout(() => { document.body.removeChild(a); URL.revokeObjectURL(url); }, 100);
}
