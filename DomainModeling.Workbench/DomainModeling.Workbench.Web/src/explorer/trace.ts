/**
 * Trace tab: live domain event notifications via SignalR, diagram highlights (GitHub #34).
 */
import { esc } from './helpers';
import { renderDiagramView, initDiagram, setDiagramTraceHighlights } from './diagram';

const BASE_URL = (window.__config?.apiUrl || '/domain-model/json').replace(/\/json$/, '');
const HUB_URL = window.__config?.traceHubUrl || '';
const HIGHLIGHT_DURATION_MS = 5000;

let connection = null;
let reconnectTimer = null;
/** @type {ReturnType<typeof setTimeout> | null} */
let highlightClearTimer = null;
let manualDisconnect = false;
/** @type {object | null} */
let diagramCtx = null;
/** @type {object[] | null} */
let boundedContexts = null;

function setStatus(text, cls) {
  const el = document.getElementById('traceConnectionStatus');
  if (!el) return;
  el.textContent = text;
  el.className = 'trace-status ' + (cls || '');
}

function buildHighlightIds(msg) {
  const ids = new Set();
  if (msg.eventGraphKey) ids.add(msg.eventGraphKey);
  if (msg.eventTypeFullName && msg.eventTypeFullName !== msg.eventGraphKey) ids.add(msg.eventTypeFullName);
  for (const h of msg.handlerFullNames || []) ids.add(h);
  return [...ids];
}

function clearHighlightTimer() {
  if (highlightClearTimer) {
    clearTimeout(highlightClearTimer);
    highlightClearTimer = null;
  }
}

function applyHighlight(msg) {
  setDiagramTraceHighlights(buildHighlightIds(msg));
  clearHighlightTimer();
  highlightClearTimer = setTimeout(() => {
    highlightClearTimer = null;
    setDiagramTraceHighlights([]);
  }, HIGHLIGHT_DURATION_MS);
}

function appendEntry(msg) {
  const list = document.getElementById('traceEntries');
  if (!list) return;

  const empty = list.querySelector('.trace-empty');
  if (empty) empty.remove();

  const iso = msg.timestampUtc || msg.TimestampUtc;
  const time = iso ? new Date(iso).toLocaleString() : '—';
  const eventType = msg.eventTypeFullName || msg.EventTypeFullName || '';
  const payload = msg.payloadJson ?? msg.PayloadJson ?? '';
  const handlers = msg.handlerFullNames || msg.HandlerFullNames || [];
  const ctxs = msg.boundedContextsWithMatch || msg.BoundedContextsWithMatch || [];
  const filter = msg.boundedContextName || msg.BoundedContextName;

  const el = document.createElement('div');
  el.className = 'trace-entry';
  el.innerHTML = `
    <div class="trace-entry-time">${esc(time)}</div>
    <div class="trace-entry-type">${esc(eventType)}</div>
    <div class="trace-entry-meta">${filter ? 'Context filter: ' + esc(filter) : (ctxs.length ? 'Matched in: ' + esc(ctxs.join(', ')) : '')}</div>
    <div class="trace-entry-handlers">${handlers.length ? 'Handlers: ' + esc(handlers.join(', ')) : 'No matching handlers in graph'}</div>
    <pre class="trace-entry-json">${esc(typeof payload === 'string' ? payload : JSON.stringify(payload, null, 2))}</pre>
  `;
  list.insertBefore(el, list.firstChild);
}

function normalizeMessage(raw) {
  if (!raw || typeof raw !== 'object') return raw;
  return {
    timestampUtc: raw.timestampUtc ?? raw.TimestampUtc,
    eventTypeFullName: raw.eventTypeFullName ?? raw.EventTypeFullName,
    eventGraphKey: raw.eventGraphKey ?? raw.EventGraphKey,
    boundedContextName: raw.boundedContextName ?? raw.BoundedContextName,
    boundedContextsWithMatch: raw.boundedContextsWithMatch ?? raw.BoundedContextsWithMatch,
    handlerFullNames: raw.handlerFullNames ?? raw.HandlerFullNames,
    payloadJson: raw.payloadJson ?? raw.PayloadJson,
  };
}

async function startHub() {
  const hub = window.signalR;
  if (!HUB_URL || !hub) {
    setStatus('SignalR unavailable', 'disconnected');
    return;
  }

  manualDisconnect = false;
  setStatus('Connecting…', 'connecting');

  if (connection) {
    try { await connection.stop(); } catch { /* ignore */ }
    connection = null;
  }

  connection = new hub.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .build();

  connection.on('trace', (payload) => {
    const msg = normalizeMessage(payload);
    appendEntry(msg);
    applyHighlight(msg);
  });

  connection.onreconnecting(() => setStatus('Reconnecting…', 'connecting'));
  connection.onreconnected(() => setStatus('Connected', 'connected'));
  connection.onclose(() => {
    setStatus('Disconnected', 'disconnected');
    if (!manualDisconnect) scheduleReconnect();
  });

  try {
    await connection.start();
    setStatus('Connected', 'connected');
  } catch (e) {
    console.error('Trace hub connection failed', e);
    setStatus('Disconnected', 'disconnected');
    scheduleReconnect();
  }
}

function scheduleReconnect() {
  if (manualDisconnect) return;
  if (reconnectTimer) return;
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    void startHub();
  }, 3000);
}

export async function disconnectTraceHub() {
  manualDisconnect = true;
  clearHighlightTimer();
  setDiagramTraceHighlights([]);
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  if (connection) {
    try { await connection.stop(); } catch { /* ignore */ }
    connection = null;
  }
  setStatus('Paused', 'disconnected');
}

export async function reconnectTraceHub() {
  await disconnectTraceHub();
  manualDisconnect = false;
  await startHub();
}

export function clearTracePanel() {
  const list = document.getElementById('traceEntries');
  if (!list) return;
  list.innerHTML = '<div class="trace-empty">Waiting for events… Call <code>DomainModelTracing.NotifyAsync</code> from your app or hit the demo endpoint.</div>';
  clearHighlightTimer();
  setDiagramTraceHighlights([]);
}

export function renderTraceView() {
  let html = '<div class="trace-layout">';
  html += '<div class="trace-diagram-pane">';
  html += renderDiagramView({ traceLayout: true });
  html += '</div>';
  html += '<aside class="trace-panel" id="tracePanel">';
  html += '<div class="trace-panel-header">';
  html += '<h2>Event trace</h2>';
  html += '<div class="trace-panel-actions">';
  html += '<span class="trace-status disconnected" id="traceConnectionStatus">…</span>';
  html += '<button type="button" onclick="window.__trace.clear()">Clear</button>';
  html += '<button type="button" onclick="window.__trace.reconnect()">Reconnect</button>';
  html += '</div></div>';
  html += '<div class="trace-entries" id="traceEntries">';
  html += '<div class="trace-empty">Waiting for events…</div>';
  html += '</div></aside></div>';
  return html;
}

/**
 * @param {object} mergedCtx
 * @param {object[]} contexts
 */
export function mountTrace(mergedCtx, contexts) {
  diagramCtx = mergedCtx;
  boundedContexts = contexts;
  requestAnimationFrame(() => {
    initDiagram(mergedCtx, contexts);
    void startHub();
    void fetchRecent();
  });
}

async function fetchRecent() {
  try {
    const res = await fetch(`${BASE_URL}/trace/recent?limit=20`);
    if (!res.ok) return;
    const items = await res.json();
    if (!Array.isArray(items) || items.length === 0) return;
    const list = document.getElementById('traceEntries');
    if (!list) return;
    list.innerHTML = '';
    for (let i = items.length - 1; i >= 0; i--) {
      appendEntry(normalizeMessage(items[i]));
    }
    applyHighlight(normalizeMessage(items[0]));
  } catch { /* optional */ }
}

export function remountTraceDiagram() {
  if (!diagramCtx || !boundedContexts) return;
  requestAnimationFrame(() => initDiagram(diagramCtx, boundedContexts));
}
