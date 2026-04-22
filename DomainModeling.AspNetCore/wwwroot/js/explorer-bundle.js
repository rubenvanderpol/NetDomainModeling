var __defProp = Object.defineProperty;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __esm = (fn, res) => function __init() {
  return fn && (res = (0, fn[__getOwnPropNames(fn)[0]])(fn = 0)), res;
};
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};

// src/explorer/helpers.ts
function shortName(fullName) {
  if (!fullName) return "";
  const s = String(fullName);
  if (s.indexOf("`") >= 0 || s.indexOf("[[") >= 0) return displayShortName(s);
  const parts = s.split(".");
  return parts[parts.length - 1];
}
function displayShortName(fullName) {
  if (!fullName) return "";
  const s = String(fullName);
  const lastDot = s.lastIndexOf(".");
  const segment = lastDot >= 0 ? s.slice(lastDot + 1) : s;
  return formatClrTypeSegment(segment);
}
function formatClrTypeSegment(segment) {
  if (!segment) return "";
  const tick = segment.indexOf("`");
  if (tick < 0) return segment;
  const base = segment.slice(0, tick);
  let i = tick + 1;
  while (i < segment.length && segment[i] >= "0" && segment[i] <= "9") i++;
  if (i >= segment.length || segment.slice(i, i + 2) !== "[[") return base;
  const args = [];
  let pos = i;
  while (pos < segment.length && segment.slice(pos, pos + 2) === "[[") {
    const innerEnd = matchDoubleBracketContentEnd(segment, pos + 2);
    if (innerEnd < 0) break;
    const inner = segment.slice(pos + 2, innerEnd);
    args.push(parseAssemblyQualifiedTypeName(inner));
    pos = innerEnd + 2;
    while (pos < segment.length && (segment[pos] === "," || segment[pos] === " ")) pos++;
  }
  return args.length ? `${base}<${args.join(", ")}>` : base;
}
function matchDoubleBracketContentEnd(s, contentStart) {
  let depth = 1;
  let i = contentStart;
  while (i < s.length && depth > 0) {
    if (i + 1 < s.length && s[i] === "[" && s[i + 1] === "[") {
      depth++;
      i += 2;
    } else if (i + 1 < s.length && s[i] === "]" && s[i + 1] === "]") {
      depth--;
      i += 2;
    } else i++;
  }
  return depth === 0 ? i - 2 : -1;
}
function parseAssemblyQualifiedTypeName(inner) {
  const typePart = stripAssemblyQualifier(inner.trim());
  if (!typePart) return "";
  if (typePart.indexOf("`") >= 0) return formatClrTypeSegment(typePart);
  const parts = typePart.split(".");
  return parts[parts.length - 1] || typePart;
}
function stripAssemblyQualifier(s) {
  if (!s) return "";
  const v = s.indexOf(", Version=");
  if (v > 0) return s.slice(0, v).trim();
  const c = s.indexOf(", Culture=");
  if (c > 0) return s.slice(0, c).trim();
  const pk = s.indexOf(", PublicKeyToken=");
  if (pk > 0) return s.slice(0, pk).trim();
  const simple = s.indexOf(", ");
  if (simple > 0 && s.indexOf("`") < 0) return s.slice(0, simple).trim();
  return s.trim();
}
function stripGenericTypeArgs(typeName) {
  if (!typeName) return "";
  const s = String(typeName);
  const idx = s.indexOf("<");
  if (idx < 0) return s;
  let depth = 0;
  for (let i = idx; i < s.length; i++) {
    const ch = s[i];
    if (ch === "<") depth++;
    else if (ch === ">") {
      depth--;
      if (depth === 0) {
        return stripGenericTypeArgs(s.slice(0, idx) + s.slice(i + 1));
      }
    }
  }
  return s;
}
function truncateDiagramText(str, maxChars = DIAGRAM_NODE_TEXT_MAX_CHARS) {
  if (str == null || str === "") return "";
  const t = String(str);
  const n = typeof maxChars === "number" && maxChars > 0 ? maxChars : DIAGRAM_NODE_TEXT_MAX_CHARS;
  if (t.length <= n) return t;
  return t.slice(0, Math.max(0, n - 1)) + ELLIPSIS;
}
function formatDiagramPropertyLine(propName, typeName) {
  const t = formatDiagramTypeName(typeName || "");
  const raw = `${propName || ""}: ${t}`;
  return truncateDiagramText(raw);
}
function formatDiagramMethodLine(method) {
  if (!method) return "";
  const ret = formatDiagramTypeName(method.returnTypeName || "");
  const params = (method.parameters || []).map((p) => formatDiagramTypeName(p.typeName || "")).join(", ");
  const raw = `${ret} ${method.name || ""}(${params})`;
  return truncateDiagramText(raw);
}
function formatDiagramRuleLine(rule) {
  if (!rule) return "";
  const title = String(rule.name || "Rule").trim() || "Rule";
  const body = String(rule.text || "").trim();
  const raw = body ? `${title}: ${body}` : title;
  return truncateDiagramText(raw);
}
function formatDiagramTypeName(typeName) {
  if (!typeName) return "";
  if (typeName.indexOf("`") >= 0 || typeName.indexOf("[[") >= 0) return displayShortName(typeName);
  return stripGenericTypeArgs(typeName);
}
function formatDiagramEmittedEventLine(eventFullName) {
  const label = formatDiagramTypeName(eventFullName || "") || shortName(eventFullName || "");
  return formatDiagramEventBadgeLine(label);
}
function formatDiagramEventBadgeLine(displayLabel) {
  const raw = "\u26A1 " + String(displayLabel || "");
  return truncateDiagramText(raw);
}
function esc(str) {
  if (!str) return "";
  return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
function escAttr(str) {
  return (str || "").replace(/'/g, "\\'").replace(/"/g, "&quot;");
}
function kindMeta(kind) {
  const map = {
    aggregates: { tag: "AGGREGATE", color: "var(--clr-aggregate)", bg: "var(--clr-aggregate-bg)" },
    entities: { tag: "ENTITY", color: "var(--clr-entity)", bg: "var(--clr-entity-bg)" },
    valueObjects: { tag: "VALUE OBJECT", color: "var(--clr-value-object)", bg: "var(--clr-value-object-bg)" },
    subTypes: { tag: "SUB TYPE", color: "var(--clr-sub-type)", bg: "var(--clr-sub-type-bg)" },
    domainEvents: { tag: "DOMAIN EVENT", color: "var(--clr-event)", bg: "var(--clr-event-bg)" },
    integrationEvents: { tag: "INTEGRATION EVENT", color: "var(--clr-integration-event)", bg: "var(--clr-integration-event-bg)" },
    commandHandlerTargets: { tag: "HANDLES TARGET", color: "var(--clr-command)", bg: "var(--clr-command-bg)" },
    eventHandlers: { tag: "EVENT HANDLER", color: "var(--clr-handler)", bg: "var(--clr-handler-bg)" },
    commandHandlers: { tag: "COMMAND HANDLER", color: "var(--clr-handler)", bg: "var(--clr-handler-bg)" },
    queryHandlers: { tag: "QUERY HANDLER", color: "var(--clr-handler)", bg: "var(--clr-handler-bg)" },
    repositories: { tag: "REPOSITORY", color: "var(--clr-repository)", bg: "var(--clr-repository-bg)" },
    domainServices: { tag: "DOMAIN SERVICE", color: "var(--clr-service)", bg: "var(--clr-service-bg)" }
  };
  return map[kind] || { tag: kind.toUpperCase(), color: "var(--text-muted)", bg: "var(--bg-hover)" };
}
function relKindColor(kind) {
  const map = {
    "Contains": "var(--clr-entity)",
    "References": "var(--clr-value-object)",
    "ReferencesById": "var(--clr-value-object)",
    "Has": "var(--clr-entity)",
    "HasMany": "var(--clr-entity)",
    "Emits": "var(--clr-event)",
    "Handles": "var(--clr-handler)",
    "Manages": "var(--clr-repository)",
    "Publishes": "var(--clr-integration-event)"
  };
  return map[kind] || "var(--text-muted)";
}
var ELLIPSIS, DIAGRAM_NODE_TEXT_MAX_CHARS, ALL_SECTIONS, SECTION_TO_DIAGRAM_KIND, SECTION_META;
var init_helpers = __esm({
  "src/explorer/helpers.ts"() {
    ELLIPSIS = "\u2026";
    DIAGRAM_NODE_TEXT_MAX_CHARS = 28;
    ALL_SECTIONS = [
      "aggregates",
      "entities",
      "valueObjects",
      "subTypes",
      "domainEvents",
      "integrationEvents",
      "commandHandlerTargets",
      "eventHandlers",
      "commandHandlers",
      "queryHandlers",
      "repositories",
      "domainServices"
    ];
    SECTION_TO_DIAGRAM_KIND = {
      aggregates: "aggregate",
      entities: "entity",
      valueObjects: "valueObject",
      subTypes: "subType",
      domainEvents: "event",
      integrationEvents: "integrationEvent",
      commandHandlerTargets: "commandHandlerTarget",
      eventHandlers: "eventHandler",
      commandHandlers: "commandHandler",
      queryHandlers: "queryHandler",
      repositories: "repository",
      domainServices: "service"
    };
    SECTION_META = [
      { key: "aggregates", label: "Aggregates", color: "var(--clr-aggregate)", tag: "AGG", bg: "var(--clr-aggregate-bg)" },
      { key: "entities", label: "Entities", color: "var(--clr-entity)", tag: "ENT", bg: "var(--clr-entity-bg)" },
      { key: "valueObjects", label: "Value Objects", color: "var(--clr-value-object)", tag: "VO", bg: "var(--clr-value-object-bg)" },
      { key: "subTypes", label: "Sub Types", color: "var(--clr-sub-type)", tag: "SUB", bg: "var(--clr-sub-type-bg)" },
      { key: "domainEvents", label: "Domain Events", color: "var(--clr-event)", tag: "EVT", bg: "var(--clr-event-bg)" },
      { key: "integrationEvents", label: "Integration Events", color: "var(--clr-integration-event)", tag: "INT", bg: "var(--clr-integration-event-bg)" },
      { key: "commandHandlerTargets", label: "Cmd handler targets", color: "var(--clr-command)", tag: "CHT", bg: "var(--clr-command-bg)" },
      { key: "eventHandlers", label: "Event Handlers", color: "var(--clr-handler)", tag: "HDL", bg: "var(--clr-handler-bg)" },
      { key: "commandHandlers", label: "Command Handlers", color: "var(--clr-handler)", tag: "CMD", bg: "var(--clr-handler-bg)" },
      { key: "queryHandlers", label: "Query Handlers", color: "var(--clr-handler)", tag: "QRY", bg: "var(--clr-handler-bg)" },
      { key: "repositories", label: "Repositories", color: "var(--clr-repository)", tag: "REPO", bg: "var(--clr-repository-bg)" },
      { key: "domainServices", label: "Domain Services", color: "var(--clr-service)", tag: "SVC", bg: "var(--clr-service-bg)" }
    ];
  }
});

// src/explorer/tabs.ts
function renderTabBar(activeTab) {
  const tabs = [
    { id: "diagram", label: "Diagram" }
  ];
  if (window.__config?.featureEditorMode) {
    tabs.push({ id: "features", label: "\u2699 Features" });
  }
  if (window.__config?.testingMode) {
    tabs.push({ id: "testing", label: "\u{1F9EA} Testing" });
  }
  if (window.__config?.traceViewMode) {
    tabs.push({ id: "trace", label: "Trace" });
  }
  let html = '<div class="tab-bar">';
  for (const t of tabs) {
    const cls = t.id === activeTab ? " active" : "";
    html += `<div class="tab${cls}" onclick="window.__nav.switchTab('${t.id}')">${t.label}</div>`;
  }
  html += "</div>";
  return html;
}
var init_tabs = __esm({
  "src/explorer/tabs.ts"() {
  }
});

// src/explorer/diagram.ts
function setDiagramLayoutBaseUrl(baseUrl2) {
  diagramLayoutBaseUrl = baseUrl2 && baseUrl2.length ? baseUrl2.replace(/\/$/, "") : null;
}
function setServerDiagramLayoutCache(doc) {
  serverLayoutDoc = doc && typeof doc === "object" && !Array.isArray(doc) ? doc : null;
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
        if (bucket && typeof bucket === "object" && !Array.isArray(bucket)) {
          Object.assign(merged, bucket);
        }
      }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));
      localStorage.removeItem(LEGACY_POSITIONS_KEY);
    }
  } catch {
  }
  try {
    if (!localStorage.getItem(HIDDEN_KINDS_KEY) && localStorage.getItem(LEGACY_HIDDEN_KINDS_KEY)) {
      const raw = localStorage.getItem(LEGACY_HIDDEN_KINDS_KEY);
      const all = raw ? JSON.parse(raw) : {};
      const set = /* @__PURE__ */ new Set();
      for (const arr of Object.values(all || {})) {
        if (Array.isArray(arr)) for (const k of arr) set.add(k);
      }
      localStorage.setItem(HIDDEN_KINDS_KEY, JSON.stringify([...set]));
      localStorage.removeItem(LEGACY_HIDDEN_KINDS_KEY);
    }
  } catch {
  }
  try {
    if (!localStorage.getItem(HIDDEN_EDGE_KINDS_KEY) && localStorage.getItem(LEGACY_HIDDEN_EDGE_KINDS_KEY)) {
      const raw = localStorage.getItem(LEGACY_HIDDEN_EDGE_KINDS_KEY);
      const all = raw ? JSON.parse(raw) : {};
      const set = /* @__PURE__ */ new Set();
      for (const arr of Object.values(all || {})) {
        if (Array.isArray(arr)) for (const k of arr) set.add(k);
      }
      localStorage.setItem(HIDDEN_EDGE_KINDS_KEY, JSON.stringify([...set]));
      localStorage.removeItem(LEGACY_HIDDEN_EDGE_KINDS_KEY);
    }
  } catch {
  }
  try {
    if (!localStorage.getItem(VIEWPORT_KEY) && localStorage.getItem(LEGACY_VIEWPORT_KEY)) {
      const raw = localStorage.getItem(LEGACY_VIEWPORT_KEY);
      const all = raw ? JSON.parse(raw) : {};
      let picked = null;
      for (const v of Object.values(all || {})) {
        if (v && typeof v.zoom === "number") picked = v;
      }
      if (picked) {
        localStorage.setItem(VIEWPORT_KEY, JSON.stringify(picked));
      }
      localStorage.removeItem(LEGACY_VIEWPORT_KEY);
    }
  } catch {
  }
}
function buildLayoutPayloadFromState() {
  if (!dgState) return null;
  const positions = {};
  for (const n of dgState.allNodes) {
    positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
  }
  const edgeWaypoints = {};
  if (dgState.edgeWaypoints) {
    for (const [key, pts] of Object.entries(dgState.edgeWaypoints)) {
      if (pts && pts.length > 0) {
        edgeWaypoints[key] = pts.map((p) => ({ x: Math.round(p.x * 10) / 10, y: Math.round(p.y * 10) / 10 }));
      }
    }
  }
  return {
    positions,
    viewport: {
      zoom: Math.round(dgState.zoom * 1e3) / 1e3,
      panX: Math.round(dgState.panX * 10) / 10,
      panY: Math.round(dgState.panY * 10) / 10
    },
    hiddenKinds: [...dgState.hiddenKinds],
    hiddenNodeIds: [...dgState.hiddenNodeIds],
    hiddenEdgeKinds: [...dgState.hiddenEdgeKinds],
    showAliases,
    showLayers,
    edgeWaypoints: Object.keys(edgeWaypoints).length > 0 ? edgeWaypoints : void 0
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
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    if (res.ok) serverLayoutDoc = payload;
  } catch {
  }
}
function syncDiagramToolbarToggles() {
  const aliasBg = showAliases ? "var(--bg-hover)" : "";
  const layerBg = showLayers ? "var(--bg-hover)" : "";
  const aliasBtn = document.getElementById("diagramAliasToggle");
  if (aliasBtn) aliasBtn.style.background = aliasBg;
  const layerBtn = document.getElementById("diagramLayerToggle");
  if (layerBtn) layerBtn.style.background = layerBg;
  const feAlias = document.getElementById("feAliasToggle");
  if (feAlias) feAlias.style.background = aliasBg;
  const feLayer = document.getElementById("feLayerToggle");
  if (feLayer) feLayer.style.background = layerBg;
}
function reloadDiagramViewFlagsFromStorage() {
  try {
    showAliases = localStorage.getItem(SHOW_ALIASES_KEY) === "true";
  } catch {
    showAliases = false;
  }
  try {
    showLayers = localStorage.getItem(SHOW_LAYERS_KEY) === "true";
  } catch {
    showLayers = false;
  }
}
function getDiagramShowAliases() {
  return showAliases;
}
function getDiagramShowLayers() {
  return showLayers;
}
function loadDiagramHiddenKindsSet() {
  return new Set(loadHiddenKinds());
}
function loadDiagramHiddenEdgeKindsSet() {
  return new Set(loadHiddenEdgeKinds());
}
function saveDiagramHiddenKindsSet(hiddenKinds) {
  saveHiddenKinds(hiddenKinds instanceof Set ? hiddenKinds : new Set(hiddenKinds || []));
}
function saveDiagramHiddenEdgeKindsSet(hiddenEdgeKinds) {
  saveHiddenEdgeKinds(hiddenEdgeKinds instanceof Set ? hiddenEdgeKinds : new Set(hiddenEdgeKinds || []));
}
function loadHiddenNodeIds() {
  try {
    const raw = localStorage.getItem(HIDDEN_NODE_IDS_KEY);
    if (!raw) return /* @__PURE__ */ new Set();
    const arr = JSON.parse(raw);
    return new Set(Array.isArray(arr) ? arr : []);
  } catch {
    return /* @__PURE__ */ new Set();
  }
}
function saveHiddenNodeIds(hiddenNodeIds) {
  try {
    localStorage.setItem(HIDDEN_NODE_IDS_KEY, JSON.stringify([...hiddenNodeIds]));
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function metadataImpliesDiagramHiddenByDefault(meta) {
  if (!meta || typeof meta !== "object") return false;
  const a = typeof meta.alias === "string" ? meta.alias.trim() : "";
  const d = typeof meta.description === "string" ? meta.description.trim() : "";
  return a.length > 0 || d.length > 0;
}
function isDiagramNodeHidden(nodeId) {
  const meta = typeof window !== "undefined" && window.__metadata && window.__metadata[nodeId] || null;
  if (meta && meta.hiddenOnDiagram === false) return false;
  if (meta && meta.hiddenOnDiagram === true) return true;
  if (metadataImpliesDiagramHiddenByDefault(meta)) return true;
  if (dgState && dgState.hiddenNodeIds) return dgState.hiddenNodeIds.has(nodeId);
  return loadHiddenNodeIds().has(nodeId);
}
function reapplyDiagramVisibilityAfterMetadataChange() {
  if (!dgState) return;
  applyDiagramVisibility();
  renderSvg();
  syncDiagramKindFilterUi();
  if (typeof window.__onDiagramHiddenNodesChanged === "function") {
    window.__onDiagramHiddenNodesChanged();
  }
}
function removeLegacyHiddenNodeId(nodeId) {
  if (typeof nodeId !== "string" || !nodeId) return;
  if (dgState && dgState.hiddenNodeIds && dgState.hiddenNodeIds.has(nodeId)) {
    dgState.hiddenNodeIds.delete(nodeId);
    saveHiddenNodeIds(dgState.hiddenNodeIds);
  } else {
    const set = loadHiddenNodeIds();
    if (!set.has(nodeId)) return;
    set.delete(nodeId);
    saveHiddenNodeIds(set);
  }
}
function loadPositions() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const o = JSON.parse(raw);
    return o && typeof o === "object" && !Array.isArray(o) ? o : null;
  } catch {
    return null;
  }
}
function savePositions(nodes) {
  try {
    const positions = {};
    for (const n of nodes) {
      positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(positions));
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function clearPositions() {
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function loadHiddenKinds() {
  try {
    const raw = localStorage.getItem(HIDDEN_KINDS_KEY);
    if (!raw) return /* @__PURE__ */ new Set();
    const arr = JSON.parse(raw);
    return new Set(Array.isArray(arr) ? arr : []);
  } catch {
    return /* @__PURE__ */ new Set();
  }
}
function saveHiddenKinds(hiddenKinds) {
  try {
    localStorage.setItem(HIDDEN_KINDS_KEY, JSON.stringify([...hiddenKinds]));
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function loadHiddenEdgeKinds() {
  try {
    const raw = localStorage.getItem(HIDDEN_EDGE_KINDS_KEY);
    if (!raw) return /* @__PURE__ */ new Set();
    const arr = JSON.parse(raw);
    return new Set(Array.isArray(arr) ? arr : []);
  } catch {
    return /* @__PURE__ */ new Set();
  }
}
function saveHiddenEdgeKinds(hiddenEdgeKinds) {
  try {
    localStorage.setItem(HIDDEN_EDGE_KINDS_KEY, JSON.stringify([...hiddenEdgeKinds]));
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function loadViewport() {
  try {
    const raw = localStorage.getItem(VIEWPORT_KEY);
    if (!raw) return null;
    return JSON.parse(raw);
  } catch {
    return null;
  }
}
function saveViewport(zoom, panX, panY) {
  try {
    const v = { zoom: Math.round(zoom * 1e3) / 1e3, panX: Math.round(panX * 10) / 10, panY: Math.round(panY * 10) / 10 };
    localStorage.setItem(VIEWPORT_KEY, JSON.stringify(v));
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function clearViewport() {
  try {
    localStorage.removeItem(VIEWPORT_KEY);
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function loadEdgeWaypoints() {
  try {
    const raw = localStorage.getItem(EDGE_WAYPOINTS_KEY);
    if (!raw) return {};
    const o = JSON.parse(raw);
    return o && typeof o === "object" && !Array.isArray(o) ? o : {};
  } catch {
    return {};
  }
}
function saveEdgeWaypoints(edgeWaypoints) {
  try {
    const filtered = {};
    for (const [key, pts] of Object.entries(edgeWaypoints || {})) {
      if (pts && pts.length > 0) filtered[key] = pts;
    }
    localStorage.setItem(EDGE_WAYPOINTS_KEY, JSON.stringify(filtered));
  } catch {
  }
  scheduleFlushDiagramLayout();
}
function edgeKey(e) {
  return `${e.source}|${e.target}|${e.kind}`;
}
function wrapName(text) {
  if (!text || text.length <= MAX_NAME_CHARS) return [text || ""];
  const words = text.includes(" ") ? text.split(" ") : text.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2").split(" ");
  const lines = [];
  let line = "";
  for (const w of words) {
    const candidate = line ? line + (text.includes(" ") ? " " : "") + w : w;
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
  if (!label || typeof label !== "string") return null;
  const m = label.match(/^emits via ([^(]+)\(\)$/i);
  return m && m[1] ? m[1].trim() : null;
}
function methodNameFromSignature(signature) {
  if (!signature || typeof signature !== "string") return "";
  const idx = signature.indexOf("(");
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
  if (edge.kind !== "Emits") return null;
  const methodName = parseEmitsMethodFromLabel(edge.label || "");
  if (!methodName) return null;
  const methodYLocal = methodTextY(src, methodName);
  if (methodYLocal == null) return null;
  const anchorCy = src.y + methodYLocal;
  const tgtCx = tgt.x + tgt.w / 2;
  const tgtCy = tgt.y + tgt.h / 2;
  return rectEdge(src.x + src.w / 2, anchorCy, src.w + 8, src.h + 8, tgtCx, tgtCy);
}
function getDiagramState() {
  return dgState;
}
function setDiagramTraceHighlights(fullNames) {
  traceHighlightIds = new Set(fullNames || []);
  renderSvg();
}
function renderDiagramView(opts = {}) {
  const traceLayout = opts.traceLayout === true;
  let html = renderTabBar(traceLayout ? "trace" : "diagram");
  const wrapClass = traceLayout ? "diagram-wrap trace-diagram-inner" : "diagram-wrap";
  html += `<div class="${wrapClass}" id="diagramWrap">`;
  html += '<div class="diagram-toolbar">';
  html += '<button onclick="window.__diagram.resetLayout()" title="Reset to auto-layout">\u21BB Reset</button>';
  html += '<span class="diagram-toolbar-sep"></span>';
  html += `<button id="diagramAliasToggle" onclick="window.__diagram.toggleAliases()" title="Show aliases instead of original names" style="${showAliases ? "background:var(--bg-hover)" : ""}">Aa Aliases</button>`;
  html += '<span class="diagram-toolbar-sep"></span>';
  html += `<button id="diagramLayerToggle" onclick="window.__diagram.toggleLayers()" title="Show architectural layers (Domain, Application, Infrastructure)" style="${showLayers ? "background:var(--bg-hover)" : ""}">\u229E Layers</button>`;
  html += '<span class="diagram-toolbar-sep"></span>';
  html += '<div class="rel-dropdown" id="diagramKindFilterWrap"></div>';
  html += '<span class="diagram-toolbar-sep"></span>';
  html += '<div class="rel-dropdown" id="diagramEdgeFilterWrap"></div>';
  html += "</div>";
  html += '<div class="diagram-controls">';
  html += '<button onclick="window.__diagram.zoom(1.25)" title="Zoom in">+</button>';
  html += '<button onclick="window.__diagram.zoom(0.8)" title="Zoom out">\u2212</button>';
  html += '<button onclick="window.__diagram.fit()" title="Fit to view">\u22A1</button>';
  html += '<button onclick="window.__diagram.downloadSvg()" title="Download as SVG">\u2B07 SVG</button>';
  html += "</div>";
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
  html += "</div>";
  html += '<svg id="diagramSvg"></svg>';
  html += "</div>";
  return html;
}
function initDiagram(ctx, boundedContexts2) {
  if (!ctx) return;
  migrateLegacyDiagramLocalStorage();
  const nodes = [];
  const edges = [];
  const nMap = {};
  const kindCfg = KIND_CFG;
  const nodeContextMap = {};
  const nodeLayerMap = {};
  const allSections = ["aggregates", "entities", "valueObjects", "subTypes", "domainEvents", "integrationEvents", "commandHandlerTargets", "eventHandlers", "commandHandlers", "queryHandlers", "repositories", "domainServices"];
  if (boundedContexts2 && boundedContexts2.length > 1) {
    for (const bc of boundedContexts2) {
      for (const sec of allSections) {
        for (const item of bc[sec] || []) {
          nodeContextMap[item.fullName] = bc.name;
          if (item.layer) nodeLayerMap[item.fullName] = item.layer;
        }
      }
    }
  } else if (boundedContexts2 && boundedContexts2.length === 1) {
    for (const sec of allSections) {
      for (const item of boundedContexts2[0][sec] || []) {
        if (item.layer) nodeLayerMap[item.fullName] = item.layer;
      }
    }
  }
  function addNode(item, kind) {
    if (nMap[item.fullName]) return;
    const cfg = kindCfg[kind];
    const n = {
      id: item.fullName,
      name: item.name,
      kind,
      cfg,
      contextName: nodeContextMap[item.fullName] || null,
      layerName: nodeLayerMap[item.fullName] || item.layer || null,
      props: (item.properties || []).slice(0, 5).map((p) => formatDiagramPropertyLine(p.name, p.typeName)),
      methods: (item.methods || []).map((m) => formatDiagramMethodLine(m)),
      events: (item.emittedEvents || []).map((e) => formatDiagramEmittedEventLine(e)),
      x: 0,
      y: 0,
      vx: 0,
      vy: 0,
      w: NODE_W,
      h: 0
    };
    n.h = nodeHeight(n);
    nodes.push(n);
    nMap[item.fullName] = n;
  }
  (ctx.aggregates || []).forEach((a) => addNode(a, "aggregate"));
  (ctx.entities || []).forEach((e) => addNode(e, "entity"));
  (ctx.valueObjects || []).forEach((v) => addNode(v, "valueObject"));
  (ctx.subTypes || []).forEach((s) => addNode(s, "subType"));
  (ctx.domainEvents || []).forEach((e) => addNode(e, "event"));
  (ctx.integrationEvents || []).forEach((e) => addNode(e, "integrationEvent"));
  (ctx.commandHandlerTargets || []).forEach((c) => addNode(c, "commandHandlerTarget"));
  (ctx.eventHandlers || []).forEach((h) => addNode(h, "eventHandler"));
  (ctx.commandHandlers || []).forEach((h) => addNode(h, "commandHandler"));
  (ctx.queryHandlers || []).forEach((h) => addNode(h, "queryHandler"));
  (ctx.repositories || []).forEach((r) => addNode(r, "repository"));
  (ctx.domainServices || []).forEach((s) => addNode(s, "service"));
  for (const rel of ctx.relationships || []) {
    if (nMap[rel.sourceType] && nMap[rel.targetType]) {
      edges.push({ source: rel.sourceType, target: rel.targetType, kind: rel.kind, label: rel.label || "" });
    }
  }
  const contextName = ctx.name;
  let posSource = "none";
  suppressDiagramLayoutFlush = true;
  try {
    const serverLayout = diagramLayoutBaseUrl ? serverLayoutDoc : null;
    const lsPos = loadPositions();
    let saved = null;
    if (serverLayout?.positions && typeof serverLayout.positions === "object" && Object.keys(serverLayout.positions).length > 0) {
      saved = serverLayout.positions;
      posSource = "server";
    } else if (lsPos && Object.keys(lsPos).length > 0) {
      saved = lsPos;
      posSource = "local";
    }
    let appliedFromSaved = null;
    let hasSaved = false;
    let hasPartialSaved = false;
    if (saved) {
      appliedFromSaved = /* @__PURE__ */ new Set();
      for (const n of nodes) {
        const p = saved[n.id];
        if (p && typeof p.x === "number" && typeof p.y === "number") {
          n.x = p.x;
          n.y = p.y;
          appliedFromSaved.add(n.id);
        }
      }
      hasSaved = nodes.length > 0 && appliedFromSaved.size === nodes.length;
      hasPartialSaved = nodes.length > 0 && appliedFromSaved.size > 0 && appliedFromSaved.size < nodes.length;
    }
    if (hasPartialSaved) {
      applyAutoLayout(nodes, edges, nMap, appliedFromSaved);
    } else if (!hasSaved) {
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
    let hiddenNodeIds;
    if (serverLayout && Array.isArray(serverLayout.hiddenNodeIds)) {
      hiddenNodeIds = new Set(serverLayout.hiddenNodeIds.filter((id) => typeof id === "string"));
    } else {
      hiddenNodeIds = loadHiddenNodeIds();
    }
    if (serverLayout && typeof serverLayout.showAliases === "boolean") {
      showAliases = serverLayout.showAliases;
      try {
        localStorage.setItem(SHOW_ALIASES_KEY, showAliases ? "true" : "false");
      } catch {
      }
    }
    if (serverLayout && typeof serverLayout.showLayers === "boolean") {
      showLayers = serverLayout.showLayers;
      try {
        localStorage.setItem(SHOW_LAYERS_KEY, showLayers);
      } catch {
      }
    }
    let edgeWaypoints = {};
    if (serverLayout?.edgeWaypoints && typeof serverLayout.edgeWaypoints === "object") {
      edgeWaypoints = serverLayout.edgeWaypoints;
    } else {
      edgeWaypoints = loadEdgeWaypoints();
    }
    dgState = {
      nodes,
      edges,
      nMap,
      allNodes: nodes,
      allEdges: edges,
      zoom: 1,
      panX: 0,
      panY: 0,
      contextName,
      hiddenKinds,
      hiddenNodeIds,
      hiddenEdgeKinds,
      edgeWaypoints
    };
    applyDiagramVisibility();
    const lsViewport = loadViewport();
    let savedViewport = null;
    if (hasSaved || hasPartialSaved) {
      if (serverLayout?.viewport && typeof serverLayout.viewport.zoom === "number") {
        savedViewport = serverLayout.viewport;
      } else {
        savedViewport = lsViewport;
      }
    }
    if ((hasSaved || hasPartialSaved) && savedViewport) {
      dgState.zoom = savedViewport.zoom;
      dgState.panX = savedViewport.panX;
      dgState.panY = savedViewport.panY;
    }
    renderSvg();
    refreshDiagramKindFilters();
    syncDiagramToolbarToggles();
    if (!hasSaved && !hasPartialSaved || !savedViewport) {
      fitToView();
      saveViewport(dgState.zoom, dgState.panX, dgState.panY);
    }
    if (posSource === "server") {
      const positions = {};
      for (const n of nodes) {
        positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
      }
      try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(positions));
      } catch {
      }
      try {
        localStorage.setItem(HIDDEN_KINDS_KEY, JSON.stringify([...hiddenKinds]));
      } catch {
      }
      try {
        localStorage.setItem(HIDDEN_EDGE_KINDS_KEY, JSON.stringify([...hiddenEdgeKinds]));
      } catch {
      }
      try {
        localStorage.setItem(HIDDEN_NODE_IDS_KEY, JSON.stringify([...hiddenNodeIds]));
      } catch {
      }
      try {
        saveEdgeWaypoints(edgeWaypoints);
      } catch {
      }
      if (savedViewport) {
        try {
          localStorage.setItem(VIEWPORT_KEY, JSON.stringify({
            zoom: Math.round(savedViewport.zoom * 1e3) / 1e3,
            panX: Math.round(savedViewport.panX * 10) / 10,
            panY: Math.round(savedViewport.panY * 10) / 10
          }));
        } catch {
        }
      }
    }
  } finally {
    suppressDiagramLayoutFlush = false;
  }
  if (posSource === "local" && diagramLayoutBaseUrl && dgState) {
    void (async () => {
      const payload = buildLayoutPayloadFromState();
      if (!payload) return;
      try {
        const res = await fetch(`${diagramLayoutBaseUrl}/diagram-layout`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload)
        });
        if (res.ok) serverLayoutDoc = payload;
      } catch {
      }
    })();
  }
  setupInteraction();
}
function renderDiagramKindFilters() {
  if (!dgState) return "";
  const presentKinds = new Set(dgState.allNodes.map((n) => n.kind));
  if (presentKinds.size === 0) return "";
  const visibleKinds = [...presentKinds].filter((kind) => !dgState.hiddenKinds.has(kind)).length;
  let html = `<button class="rel-dropdown-trigger" id="diagramKindFilterTrigger" onclick="window.__diagram.toggleKindFilter()" title="Filter node types">`;
  html += '<span style="font-size:10px;opacity:.7">\u25C8</span>';
  html += "<span>Node Types</span>";
  html += `<span class="rel-hidden-count">${visibleKinds}/${presentKinds.size}</span>`;
  html += '<span class="rel-chevron">\u25BE</span>';
  html += "</button>";
  html += '<div class="rel-dropdown-menu" id="diagramKindFilterMenu">';
  html += '<div class="rel-dropdown-actions">';
  html += '<button type="button" onclick="window.__diagram.showAllKinds()">Show all</button>';
  html += '<button type="button" onclick="window.__diagram.hideAllKinds()">Hide all</button>';
  html += "</div>";
  for (const [kind, cfg] of Object.entries(KIND_CFG)) {
    if (!presentKinds.has(kind)) continue;
    const visible = !dgState.hiddenKinds.has(kind);
    const count = dgState.allNodes.filter((n) => n.kind === kind).length;
    html += `<div class="rel-dropdown-item${visible ? " checked" : ""}" onclick="window.__diagram.toggleKind(event, '${kind}')" data-node-kind="${kind}">`;
    html += `<span class="rel-check">${visible ? "\u2713" : ""}</span>`;
    html += `<span class="diagram-kind-dot" style="background:${cfg.color}"></span>`;
    html += `<span class="rel-kind-label">${esc(cfg.label)}</span>`;
    html += `<span class="diagram-kind-count">${count}</span>`;
    html += "</div>";
  }
  html += "</div>";
  return html;
}
function refreshDiagramKindFilters() {
  const el = document.getElementById("diagramKindFilterWrap");
  const prevMenu = document.getElementById("diagramKindFilterMenu");
  const prevTrigger = document.getElementById("diagramKindFilterTrigger");
  const wasVisible = !!prevMenu?.classList.contains("visible");
  const wasOpen = !!prevTrigger?.classList.contains("open");
  if (el) el.innerHTML = renderDiagramKindFilters();
  if (wasVisible || wasOpen) {
    const nextMenu = document.getElementById("diagramKindFilterMenu");
    const nextTrigger = document.getElementById("diagramKindFilterTrigger");
    if (nextMenu) nextMenu.classList.add("visible");
    if (nextTrigger) nextTrigger.classList.add("open");
  }
  refreshDiagramEdgeFilter();
}
function syncDiagramKindFilterUi() {
  if (!dgState) return;
  const trigger = document.getElementById("diagramKindFilterTrigger");
  const menu = document.getElementById("diagramKindFilterMenu");
  if (!trigger || !menu) {
    refreshDiagramKindFilters();
    return;
  }
  const presentKinds = new Set(dgState.allNodes.map((n) => n.kind));
  const visibleKinds = [...presentKinds].filter((kind) => !dgState.hiddenKinds.has(kind)).length;
  const badge = trigger.querySelector(".rel-hidden-count");
  if (badge) badge.textContent = `${visibleKinds}/${presentKinds.size}`;
  const rows = menu.querySelectorAll("[data-node-kind]");
  for (const row of rows) {
    const kind = row.getAttribute("data-node-kind");
    if (!kind) continue;
    const visible = !dgState.hiddenKinds.has(kind);
    row.classList.toggle("checked", visible);
    const check = row.querySelector(".rel-check");
    if (check) check.textContent = visible ? "\u2713" : "";
  }
}
function renderDiagramEdgeFilter() {
  if (!dgState) return "";
  const presentEdgeKinds = new Set(dgState.allEdges.map((e) => e.kind));
  if (presentEdgeKinds.size === 0) return "";
  const hiddenCount = dgState.hiddenEdgeKinds.size;
  let h = `<button class="rel-dropdown-trigger" id="diagramEdgeFilterTrigger" onclick="window.__diagram.toggleEdgeFilter()" title="Filter relation types">`;
  h += '<span style="font-size:10px;opacity:.7">\u27DC</span>';
  h += "<span>Relations</span>";
  if (hiddenCount > 0) h += `<span class="rel-hidden-count">${hiddenCount}</span>`;
  h += '<span class="rel-chevron">\u25BE</span>';
  h += "</button>";
  h += '<div class="rel-dropdown-menu" id="diagramEdgeFilterMenu">';
  for (const [kind, cfg] of Object.entries(EDGE_CFG)) {
    if (!presentEdgeKinds.has(kind)) continue;
    const visible = !dgState.hiddenEdgeKinds.has(kind);
    h += `<div class="rel-dropdown-item${visible ? " checked" : ""}" onclick="window.__diagram.toggleEdgeKind(event, '${kind}')" data-edge-kind="${kind}">`;
    h += `<span class="rel-check">${visible ? "\u2713" : ""}</span>`;
    h += `<span class="rel-line-sample${cfg.dashed ? " dashed" : ""}" style="color:${cfg.color}"></span>`;
    h += `<span class="rel-kind-label">${esc(cfg.label)}</span>`;
    h += "</div>";
  }
  h += "</div>";
  return h;
}
function refreshDiagramEdgeFilter() {
  const el = document.getElementById("diagramEdgeFilterWrap");
  if (el) el.innerHTML = renderDiagramEdgeFilter();
}
function applyDiagramVisibility() {
  if (!dgState) return;
  const visibleIds = /* @__PURE__ */ new Set();
  dgState.nodes = dgState.allNodes.filter((n) => {
    if (dgState.hiddenKinds.has(n.kind)) return false;
    if (isDiagramNodeHidden(n.id)) return false;
    visibleIds.add(n.id);
    return true;
  });
  dgState.edges = dgState.allEdges.filter(
    (e) => visibleIds.has(e.source) && visibleIds.has(e.target) && !dgState.hiddenEdgeKinds.has(e.kind)
  );
}
function applyAutoLayout(nodes, edges, nMap, fixedNodeIds) {
  const fixed = fixedNodeIds instanceof Set && fixedNodeIds.size > 0 ? fixedNodeIds : null;
  const isFixed = (n) => fixed && fixed.has(n.id);
  const ctxGroups = {};
  for (const n of nodes) {
    if (isFixed(n)) continue;
    const key = n.contextName || "__default";
    (ctxGroups[key] = ctxGroups[key] || []).push(n);
  }
  const ctxNames = Object.keys(ctxGroups).sort();
  const hasMultipleContexts = ctxNames.length > 1 && !ctxNames.includes("__default");
  const kindRow = { aggregate: 0, entity: 1, valueObject: 1, subType: 1, event: 2, integrationEvent: 2, commandHandlerTarget: 2, eventHandler: 3, commandHandler: 3, queryHandler: 3, repository: 4, service: 4 };
  if (hasMultipleContexts) {
    let xOffset = 0;
    for (const ctxName of ctxNames) {
      const ctxNodes = ctxGroups[ctxName];
      const rowBuckets = {};
      for (const n of ctxNodes) {
        const r = kindRow[n.kind] || 0;
        (rowBuckets[r] = rowBuckets[r] || []).push(n);
      }
      let maxRowWidth = 0;
      for (const [row, rNodes] of Object.entries(rowBuckets)) {
        const y = parseInt(row) * 240;
        rNodes.forEach((n, i) => {
          n.x = xOffset + i * 270;
          n.y = y;
        });
        maxRowWidth = Math.max(maxRowWidth, rNodes.length * 270);
      }
      xOffset += maxRowWidth + 200;
    }
  } else {
    const rowBuckets = {};
    for (const n of nodes) {
      if (isFixed(n)) continue;
      const r = kindRow[n.kind] || 0;
      (rowBuckets[r] = rowBuckets[r] || []).push(n);
    }
    for (const [row, rNodes] of Object.entries(rowBuckets)) {
      const y = parseInt(row) * 240;
      rNodes.forEach((n, i) => {
        n.x = (i - (rNodes.length - 1) / 2) * 270;
        n.y = y;
      });
    }
  }
  for (let i = 0; i < 150; i++) {
    const alpha = 1 - i / 150;
    for (let a = 0; a < nodes.length; a++) {
      for (let b = a + 1; b < nodes.length; b++) {
        const na = nodes[a], nb = nodes[b];
        const fa = isFixed(na), fb = isFixed(nb);
        if (fa && fb) continue;
        let dx = nb.x - na.x, dy = nb.y - na.y;
        const dist = Math.sqrt(dx * dx + dy * dy) || 1;
        const crossCtx = hasMultipleContexts && na.contextName !== nb.contextName;
        const repStrength = crossCtx ? 16e3 : 8e3;
        const force = repStrength * alpha / (dist * dist);
        const fx = dx / dist * force, fy = dy / dist * force;
        if (!fa) {
          na.vx -= fx;
          na.vy -= fy;
        }
        if (!fb) {
          nb.vx += fx;
          nb.vy += fy;
        }
      }
    }
    for (const e of edges) {
      const s = nMap[e.source], t = nMap[e.target];
      if (!s || !t) continue;
      const fs = isFixed(s), ft = isFixed(t);
      if (fs && ft) continue;
      const dx = t.x - s.x, dy = t.y - s.y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;
      const force = dist * 4e-3 * alpha;
      const fx = dx / dist * force, fy = dy / dist * force;
      if (!fs) {
        s.vx += fx;
        s.vy += fy;
      }
      if (!ft) {
        t.vx -= fx;
        t.vy -= fy;
      }
    }
    for (const n of nodes) {
      if (isFixed(n)) {
        n.vx = 0;
        n.vy = 0;
        continue;
      }
      n.vx *= 0.82;
      n.vy *= 0.82;
      n.x += n.vx;
      n.y += n.vy;
    }
  }
  for (const n of nodes) {
    n.vx = 0;
    n.vy = 0;
  }
}
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
function computeLayerBounds(nodes) {
  const groups = {};
  for (const n of nodes) {
    if (!n.layerName) continue;
    const key = (n.contextName || "__default") + "\0" + n.layerName;
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
    const color = LAYER_COLORS[g.layerName] || "#888";
    bounds.push({ name: g.layerName, x: minX - pad, y: minY - pad - 24, w: maxX - minX + pad * 2, h: maxY - minY + pad * 2 + 24, color });
  }
  return bounds;
}
function ensureDiagramNodeHeights(nodes) {
  for (const n of nodes) {
    n.h = nodeHeight(n);
  }
}
function renderSvg() {
  const svg = document.getElementById("diagramSvg");
  if (!svg || !dgState) return;
  const { nodes, edges, nMap } = dgState;
  ensureDiagramNodeHeights(nodes);
  const edgeColors = { Contains: "#60a5fa", References: "#34d399", ReferencesById: "#34d399", Has: "#60a5fa", HasMany: "#60a5fa", Emits: "#fbbf24", Handles: "#f472b6", Manages: "#fb923c", Publishes: "#2dd4bf" };
  let s = "<defs>";
  for (const [kind, color] of Object.entries(edgeColors)) {
    s += `<marker id="arrow-${kind}" viewBox="0 0 10 6" refX="10" refY="3" markerWidth="8" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,3 L0,6 Z" fill="${color}" /></marker>`;
  }
  s += `<marker id="diamond" viewBox="0 0 12 8" refX="0" refY="4" markerWidth="10" markerHeight="8" orient="auto-start-reverse"><path d="M0,4 L6,0 L12,4 L6,8 Z" fill="#60a5fa" /></marker>`;
  nodes.forEach((n, ni) => {
    s += `<clipPath id="dg-node-clip-${ni}"><rect x="0" y="0" width="${n.w}" height="${n.h}" rx="8" /></clipPath>`;
  });
  s += "</defs>";
  s += `<g id="diagramViewport" transform="translate(${dgState.panX},${dgState.panY}) scale(${dgState.zoom})">`;
  const ctxBounds = computeContextBounds(nodes);
  dgState._ctxBounds = ctxBounds;
  for (const b of ctxBounds) {
    s += `<g class="dg-ctx-boundary" data-ctx="${escAttr(b.name)}" style="cursor:move">`;
    s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="12" fill="rgba(255,255,255,.10)" stroke="${b.color}" stroke-width="1.5" stroke-dasharray="8,5" opacity="0.8" />`;
    s += `<text x="${b.x + 14}" y="${b.y + 24}" fill="${b.color}" font-size="20" font-weight="700" font-family="-apple-system,sans-serif" opacity="0.85">${esc(b.name)}</text>`;
    s += "</g>";
  }
  if (showLayers) {
    const layerBounds = computeLayerBounds(nodes);
    for (const b of layerBounds) {
      s += `<g class="dg-layer-boundary">`;
      s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="8" fill="none" stroke="${b.color}" stroke-width="1" stroke-dasharray="4,3" opacity="0.6" />`;
      s += `<text x="${b.x + 10}" y="${b.y + 18}" fill="${b.color}" font-size="13" font-weight="600" font-family="-apple-system,sans-serif" font-style="italic" opacity="0.7">${esc(b.name)}</text>`;
      s += "</g>";
    }
  }
  for (const e of edges) {
    const src = nMap[e.source], tgt = nMap[e.target];
    if (!src || !tgt) continue;
    const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
    const tgtCx = tgt.x + tgt.w / 2, tgtCy = tgt.y + tgt.h / 2;
    const color = edgeColors[e.kind] || "#5c6070";
    const dashed = e.kind === "Emits" || e.kind === "Handles" || e.kind === "Publishes" || e.kind === "References" || e.kind === "ReferencesById" ? ' stroke-dasharray="6,4"' : "";
    const markerStart = e.kind === "Contains" ? ' marker-start="url(#diamond)"' : "";
    const markerEnd = e.kind === "References" || e.kind === "ReferencesById" ? "" : ` marker-end="url(#arrow-${e.kind})"`;
    const ek = edgeKey(e);
    const waypoints = dgState.edgeWaypoints && dgState.edgeWaypoints[ek] || [];
    if (waypoints.length === 0) {
      const p1 = sourceAnchorForEdge(src, tgt, e) || rectEdge(srcCx, srcCy, src.w + 8, src.h + 8, tgtCx, tgtCy);
      const p2 = rectEdge(tgtCx, tgtCy, tgt.w + 8, tgt.h + 8, srcCx, srcCy);
      s += `<line class="dg-edge-hit" data-edge-key="${escAttr(ek)}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="transparent" stroke-width="12" fill="none" style="cursor:pointer" />`;
      s += `<line x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="${color}" stroke-width="1.5"${dashed}${markerStart}${markerEnd} opacity="0.65" pointer-events="none" />`;
      const mx = (p1.x + p2.x) / 2, my = (p1.y + p2.y) / 2;
      const label = e.label || e.kind;
      s += `<text x="${mx}" y="${my - 6}" text-anchor="middle" fill="${color}" font-size="9" font-family="-apple-system,sans-serif" opacity="0.7" pointer-events="none">${esc(label)}</text>`;
    } else {
      const firstWp = waypoints[0];
      const lastWp = waypoints[waypoints.length - 1];
      const p1 = sourceAnchorForEdge(src, { x: firstWp.x - 1, y: firstWp.y - 1, w: 2, h: 2 }, e) || rectEdge(srcCx, srcCy, src.w + 8, src.h + 8, firstWp.x, firstWp.y);
      const p2 = rectEdge(tgtCx, tgtCy, tgt.w + 8, tgt.h + 8, lastWp.x, lastWp.y);
      const allPts = [p1, ...waypoints, p2];
      let pathD = `M${allPts[0].x},${allPts[0].y}`;
      for (let i = 1; i < allPts.length; i++) {
        pathD += ` L${allPts[i].x},${allPts[i].y}`;
      }
      s += `<path class="dg-edge-hit" data-edge-key="${escAttr(ek)}" d="${pathD}" stroke="transparent" stroke-width="12" fill="none" style="cursor:pointer" />`;
      s += `<path d="${pathD}" stroke="${color}" stroke-width="1.5"${dashed}${markerStart}${markerEnd} opacity="0.65" fill="none" pointer-events="none" />`;
      const midIdx = Math.floor(allPts.length / 2);
      const mPrev = allPts[midIdx - 1] || allPts[0];
      const mNext = allPts[midIdx] || allPts[allPts.length - 1];
      const mx = (mPrev.x + mNext.x) / 2, my = (mPrev.y + mNext.y) / 2;
      const label = e.label || e.kind;
      s += `<text x="${mx}" y="${my - 6}" text-anchor="middle" fill="${color}" font-size="9" font-family="-apple-system,sans-serif" opacity="0.7" pointer-events="none">${esc(label)}</text>`;
      for (let wi = 0; wi < waypoints.length; wi++) {
        const wp = waypoints[wi];
        s += `<circle class="dg-waypoint" data-edge-key="${escAttr(ek)}" data-wp-idx="${wi}" cx="${wp.x}" cy="${wp.y}" r="5" fill="${color}" stroke="#0f1117" stroke-width="1.5" opacity="0.85" style="cursor:grab" />`;
      }
    }
  }
  nodes.forEach((n, ni) => {
    const c = n.cfg;
    const traceCls = traceHighlightIds.has(n.id) ? " dg-node-trace" : "";
    s += `<g class="dg-node${traceCls}" data-id="${escAttr(n.id)}" transform="translate(${n.x},${n.y})" style="cursor:pointer">`;
    s += `<rect x="3" y="3" width="${n.w}" height="${n.h}" rx="8" fill="rgba(0,0,0,.3)" />`;
    s += `<rect width="${n.w}" height="${n.h}" rx="8" fill="${c.bg}" stroke="${c.border}" stroke-width="1.5" />`;
    s += `<g clip-path="url(#dg-node-clip-${ni})">`;
    let ty = 20;
    s += `<text x="${n.w / 2}" y="${ty}" text-anchor="middle" fill="${c.color}" font-size="10" font-family="-apple-system,sans-serif" opacity="0.9">${c.stereotype}</text>`;
    ty += NAME_PAD;
    const nameLines = wrapName(diagramDisplayName(n));
    s += `<text x="${n.w / 2}" text-anchor="middle" fill="#f0f2f7" font-size="14" font-weight="600" font-family="-apple-system,sans-serif">`;
    for (const ln of nameLines) {
      ty += NAME_LINE_H;
      s += `<tspan x="${n.w / 2}" y="${ty}">${esc(ln)}</tspan>`;
    }
    s += "</text>";
    if (n.props.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const p of n.props) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#a0a4b8" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(p)}</text>`;
      }
    }
    if (n.methods.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const m of n.methods) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#a78bfa" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(m)}</text>`;
      }
    }
    if (n.events.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const ev of n.events) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#fbbf24" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(ev)}</text>`;
      }
    }
    s += "</g></g>";
  });
  s += "</g>";
  svg.innerHTML = s;
}
function rectEdge(cx, cy, w, h, px, py) {
  const dx = px - cx, dy = py - cy;
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const absDx = Math.abs(dx), absDy = Math.abs(dy);
  const hw = w / 2, hh = h / 2;
  const t = absDx * hh > absDy * hw ? hw / (absDx || 1) : hh / (absDy || 1);
  return { x: cx + dx * t, y: cy + dy * t };
}
function fitToView() {
  if (!dgState || dgState.nodes.length === 0) return;
  const wrap = document.getElementById("diagramWrap");
  if (!wrap) return;
  const nodes = dgState.nodes;
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const n of nodes) {
    minX = Math.min(minX, n.x);
    minY = Math.min(minY, n.y);
    maxX = Math.max(maxX, n.x + n.w);
    maxY = Math.max(maxY, n.y + n.h);
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
function setupInteraction() {
  const svg = document.getElementById("diagramSvg");
  if (!svg || !dgState) return;
  let dragNode = null, dragOffX = 0, dragOffY = 0;
  let dragCtx = null, dragCtxStartX = 0, dragCtxStartY = 0, dragCtxNodeStarts = null;
  let dragWp = null;
  let panning = false, panStartX = 0, panStartY = 0, panOrigX = 0, panOrigY = 0;
  svg.addEventListener("mousedown", function(ev) {
    const wpEl = ev.target.closest(".dg-waypoint");
    const nodeEl = ev.target.closest(".dg-node");
    const ctxEl = ev.target.closest(".dg-ctx-boundary");
    if (wpEl) {
      ev.preventDefault();
      ev.stopPropagation();
      dragWp = { edgeKey: wpEl.dataset.edgeKey, wpIdx: parseInt(wpEl.dataset.wpIdx, 10) };
      svg.classList.add("dragging-node");
    } else if (nodeEl) {
      ev.preventDefault();
      const n = dgState.nMap[nodeEl.dataset.id];
      if (!n) return;
      dragNode = n;
      const pt = svgPoint(svg, ev);
      dragOffX = pt.x - n.x;
      dragOffY = pt.y - n.y;
      svg.classList.add("dragging-node");
    } else if (ctxEl) {
      ev.preventDefault();
      const ctxName = ctxEl.dataset.ctx;
      dragCtx = ctxName;
      const pt = svgPoint(svg, ev);
      dragCtxStartX = pt.x;
      dragCtxStartY = pt.y;
      dragCtxNodeStarts = /* @__PURE__ */ new Map();
      for (const n of dgState.allNodes) {
        if (n.contextName === ctxName) {
          dragCtxNodeStarts.set(n.id, { x: n.x, y: n.y });
        }
      }
      svg.classList.add("dragging-node");
    } else {
      panning = true;
      panStartX = ev.clientX;
      panStartY = ev.clientY;
      panOrigX = dgState.panX;
      panOrigY = dgState.panY;
      svg.classList.add("dragging");
    }
  });
  svg.addEventListener("mousemove", function(ev) {
    if (dragWp) {
      const pt = svgPoint(svg, ev);
      const wps = dgState.edgeWaypoints[dragWp.edgeKey];
      if (wps && wps[dragWp.wpIdx]) {
        wps[dragWp.wpIdx] = { x: pt.x, y: pt.y };
        renderSvg();
      }
    } else if (dragNode) {
      const pt = svgPoint(svg, ev);
      dragNode.x = pt.x - dragOffX;
      dragNode.y = pt.y - dragOffY;
      renderSvg();
    } else if (dragCtx) {
      const pt = svgPoint(svg, ev);
      const dx = pt.x - dragCtxStartX, dy = pt.y - dragCtxStartY;
      for (const [id, start] of dragCtxNodeStarts) {
        const n = dgState.nMap[id];
        if (n) {
          n.x = start.x + dx;
          n.y = start.y + dy;
        }
      }
      renderSvg();
    } else if (panning) {
      dgState.panX = panOrigX + (ev.clientX - panStartX);
      dgState.panY = panOrigY + (ev.clientY - panStartY);
      renderSvg();
    }
  });
  function endDrag() {
    if (dragWp) {
      saveEdgeWaypoints(dgState.edgeWaypoints);
    }
    if (dragNode || dragCtx) {
      savePositions(dgState.allNodes);
    }
    if (dragWp || dragNode || dragCtx || panning) {
      saveViewport(dgState.zoom, dgState.panX, dgState.panY);
    }
    dragWp = null;
    dragNode = null;
    dragCtx = null;
    dragCtxNodeStarts = null;
    panning = false;
    svg.classList.remove("dragging", "dragging-node");
  }
  svg.addEventListener("mouseup", endDrag);
  svg.addEventListener("mouseleave", endDrag);
  svg.addEventListener("wheel", function(ev) {
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
  svg.addEventListener("dblclick", function(ev) {
    const wpEl = ev.target.closest(".dg-waypoint");
    if (wpEl) {
      ev.preventDefault();
      ev.stopPropagation();
      const ek = wpEl.dataset.edgeKey;
      const idx = parseInt(wpEl.dataset.wpIdx, 10);
      const wps = dgState.edgeWaypoints[ek];
      if (wps) {
        wps.splice(idx, 1);
        if (wps.length === 0) delete dgState.edgeWaypoints[ek];
        saveEdgeWaypoints(dgState.edgeWaypoints);
        renderSvg();
      }
      return;
    }
    const edgeHit = ev.target.closest(".dg-edge-hit");
    if (edgeHit) {
      ev.preventDefault();
      ev.stopPropagation();
      const ek = edgeHit.dataset.edgeKey;
      const pt = svgPoint(svg, ev);
      if (!dgState.edgeWaypoints[ek]) dgState.edgeWaypoints[ek] = [];
      const wps = dgState.edgeWaypoints[ek];
      const edge = dgState.edges.find((e) => edgeKey(e) === ek);
      if (edge) {
        const src = dgState.nMap[edge.source], tgt = dgState.nMap[edge.target];
        if (src && tgt) {
          const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
          const tgtCx = tgt.x + tgt.w / 2, tgtCy = tgt.y + tgt.h / 2;
          const firstWp = wps.length > 0 ? wps[0] : { x: tgtCx, y: tgtCy };
          const lastWp = wps.length > 0 ? wps[wps.length - 1] : { x: srcCx, y: srcCy };
          const p1 = sourceAnchorForEdge(src, { x: firstWp.x - 1, y: firstWp.y - 1, w: 2, h: 2 }, edge) || rectEdge(srcCx, srcCy, src.w + 8, src.h + 8, firstWp.x, firstWp.y);
          const p2 = rectEdge(tgtCx, tgtCy, tgt.w + 8, tgt.h + 8, lastWp.x, lastWp.y);
          const allPts = [p1, ...wps, p2];
          let bestDist = Infinity, bestIdx = wps.length;
          for (let i = 0; i < allPts.length - 1; i++) {
            const d = distToSegment(pt, allPts[i], allPts[i + 1]);
            if (d < bestDist) {
              bestDist = d;
              bestIdx = i;
            }
          }
          wps.splice(bestIdx, 0, { x: pt.x, y: pt.y });
        } else {
          wps.push({ x: pt.x, y: pt.y });
        }
      } else {
        wps.push({ x: pt.x, y: pt.y });
      }
      saveEdgeWaypoints(dgState.edgeWaypoints);
      renderSvg();
      return;
    }
    const nodeEl = ev.target.closest(".dg-node");
    if (nodeEl) window.__nav.navigateTo(nodeEl.dataset.id);
  });
  svg.addEventListener("contextmenu", function(ev) {
    const wpEl = ev.target.closest(".dg-waypoint");
    if (wpEl) {
      ev.preventDefault();
      ev.stopPropagation();
      const ek = wpEl.dataset.edgeKey;
      const idx = parseInt(wpEl.dataset.wpIdx, 10);
      const wps = dgState.edgeWaypoints[ek];
      if (wps) {
        wps.splice(idx, 1);
        if (wps.length === 0) delete dgState.edgeWaypoints[ek];
        saveEdgeWaypoints(dgState.edgeWaypoints);
        renderSvg();
      }
    }
  });
}
function distToSegment(p, a, b) {
  const dx = b.x - a.x, dy = b.y - a.y;
  const lenSq = dx * dx + dy * dy;
  if (lenSq === 0) return Math.sqrt((p.x - a.x) ** 2 + (p.y - a.y) ** 2);
  let t = ((p.x - a.x) * dx + (p.y - a.y) * dy) / lenSq;
  t = Math.max(0, Math.min(1, t));
  const px = a.x + t * dx, py = a.y + t * dy;
  return Math.sqrt((p.x - px) ** 2 + (p.y - py) ** 2);
}
function svgPoint(svg, ev) {
  const rect = svg.getBoundingClientRect();
  return {
    x: (ev.clientX - rect.left - dgState.panX) / dgState.zoom,
    y: (ev.clientY - rect.top - dgState.panY) / dgState.zoom
  };
}
function diagramZoom(factor) {
  if (!dgState) return;
  const wrap = document.getElementById("diagramWrap");
  if (!wrap) return;
  const cx = wrap.clientWidth / 2, cy = wrap.clientHeight / 2;
  dgState.panX = cx - (cx - dgState.panX) * factor;
  dgState.panY = cy - (cy - dgState.panY) * factor;
  dgState.zoom *= factor;
  renderSvg();
  saveViewport(dgState.zoom, dgState.panX, dgState.panY);
}
function diagramFit() {
  fitToView();
  if (dgState) saveViewport(dgState.zoom, dgState.panX, dgState.panY);
}
function diagramResetLayout(ctx) {
  if (!dgState || !ctx) return;
  clearPositions();
  clearViewport();
  dgState.hiddenNodeIds = /* @__PURE__ */ new Set();
  saveHiddenNodeIds(dgState.hiddenNodeIds);
  dgState.edgeWaypoints = {};
  saveEdgeWaypoints(dgState.edgeWaypoints);
  applyAutoLayout(dgState.allNodes, dgState.allEdges, dgState.nMap);
  applyDiagramVisibility();
  renderSvg();
  fitToView();
  savePositions(dgState.allNodes);
  saveViewport(dgState.zoom, dgState.panX, dgState.panY);
  if (typeof window.__onDiagramHiddenNodesChanged === "function") {
    window.__onDiagramHiddenNodesChanged();
  }
}
function diagramToggleKind(eventOrKind, maybeKind) {
  const hasEventArg = typeof eventOrKind === "object" && eventOrKind !== null;
  const kind = hasEventArg ? maybeKind : eventOrKind;
  if (hasEventArg) {
    eventOrKind.stopPropagation();
    eventOrKind.preventDefault();
  }
  if (!dgState) return;
  if (typeof kind !== "string") return;
  if (dgState.hiddenKinds.has(kind)) {
    dgState.hiddenKinds.delete(kind);
  } else {
    dgState.hiddenKinds.add(kind);
  }
  saveHiddenKinds(dgState.hiddenKinds);
  applyDiagramVisibility();
  renderSvg();
  syncDiagramKindFilterUi();
  if (typeof window.__onDiagramHiddenNodesChanged === "function") {
    window.__onDiagramHiddenNodesChanged();
  }
}
function setAllKindVisibility(visible) {
  if (!dgState) return;
  const presentKinds = new Set(dgState.allNodes.map((n) => n.kind));
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
  if (typeof window.__onDiagramHiddenNodesChanged === "function") {
    window.__onDiagramHiddenNodesChanged();
  }
}
function diagramShowAllKinds() {
  setAllKindVisibility(true);
}
function diagramHideAllKinds() {
  setAllKindVisibility(false);
}
function diagramShowAll() {
  if (!dgState) return;
  dgState.hiddenKinds.clear();
  dgState.hiddenNodeIds.clear();
  dgState.hiddenEdgeKinds.clear();
  saveHiddenKinds(dgState.hiddenKinds);
  saveHiddenNodeIds(dgState.hiddenNodeIds);
  saveHiddenEdgeKinds(dgState.hiddenEdgeKinds);
  applyDiagramVisibility();
  renderSvg();
  refreshDiagramKindFilters();
  if (typeof window.__onDiagramHiddenNodesChanged === "function") {
    window.__onDiagramHiddenNodesChanged();
  }
}
function diagramToggleEdgeKind(eventOrKind, maybeKind) {
  const hasEventArg = typeof eventOrKind === "object" && eventOrKind !== null;
  const kind = hasEventArg ? maybeKind : eventOrKind;
  if (hasEventArg) {
    eventOrKind.stopPropagation();
    eventOrKind.preventDefault();
  }
  if (!dgState) return;
  if (typeof kind !== "string") return;
  if (dgState.hiddenEdgeKinds.has(kind)) {
    dgState.hiddenEdgeKinds.delete(kind);
  } else {
    dgState.hiddenEdgeKinds.add(kind);
  }
  saveHiddenEdgeKinds(dgState.hiddenEdgeKinds);
  applyDiagramVisibility();
  renderSvg();
  refreshDiagramEdgeFilter();
  if (typeof window.__onDiagramHiddenNodesChanged === "function") {
    window.__onDiagramHiddenNodesChanged();
  }
}
function toggleDropdown(menuId, triggerId) {
  const menu = document.getElementById(menuId);
  const trigger = document.getElementById(triggerId);
  if (!menu) return;
  const open = menu.classList.toggle("visible");
  if (trigger) trigger.classList.toggle("open", open);
  if (!open) return;
  const close = (ev) => {
    const clickedTrigger = trigger && (ev.target === trigger || trigger.contains(ev.target));
    const containsTarget = menu.contains(ev.target);
    if (!containsTarget && !clickedTrigger) {
      menu.classList.remove("visible");
      if (trigger) trigger.classList.remove("open");
      document.removeEventListener("click", close);
    }
  };
  setTimeout(() => document.addEventListener("click", close), 0);
}
function diagramToggleKindFilter() {
  toggleDropdown("diagramKindFilterMenu", "diagramKindFilterTrigger");
}
function diagramToggleEdgeFilter() {
  toggleDropdown("diagramEdgeFilterMenu", "diagramEdgeFilterTrigger");
}
function diagramDownloadSvg() {
  const svg = document.getElementById("diagramSvg");
  if (!svg) return;
  const clone = svg.cloneNode(true);
  clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");
  const bg = document.createElementNS("http://www.w3.org/2000/svg", "rect");
  bg.setAttribute("width", "100%");
  bg.setAttribute("height", "100%");
  bg.setAttribute("fill", "#0f1117");
  clone.insertBefore(bg, clone.firstChild);
  const blob = new Blob([clone.outerHTML], { type: "image/svg+xml" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "domain-model-diagram.svg";
  document.body.appendChild(a);
  a.click();
  setTimeout(() => {
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }, 100);
}
function diagramToggleAliases() {
  showAliases = !showAliases;
  try {
    localStorage.setItem(SHOW_ALIASES_KEY, showAliases ? "true" : "false");
  } catch {
  }
  syncDiagramToolbarToggles();
  if (typeof window.__featureEditor?.onDiagramViewFlagsChanged === "function") {
    window.__featureEditor.onDiagramViewFlagsChanged();
  }
  if (dgState) {
    scheduleFlushDiagramLayout();
    renderSvg();
  }
}
function diagramToggleLayers() {
  showLayers = !showLayers;
  try {
    localStorage.setItem(SHOW_LAYERS_KEY, showLayers);
  } catch {
  }
  syncDiagramToolbarToggles();
  if (typeof window.__featureEditor?.onDiagramViewFlagsChanged === "function") {
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
  return entry && entry.alias && entry.alias.trim() ? entry.alias : n.name;
}
var STORAGE_KEY, HIDDEN_KINDS_KEY, HIDDEN_NODE_IDS_KEY, HIDDEN_EDGE_KINDS_KEY, VIEWPORT_KEY, SHOW_ALIASES_KEY, SHOW_LAYERS_KEY, LEGACY_POSITIONS_KEY, LEGACY_HIDDEN_KINDS_KEY, LEGACY_HIDDEN_EDGE_KINDS_KEY, LEGACY_VIEWPORT_KEY, FLUSH_MS, EDGE_WAYPOINTS_KEY, diagramLayoutBaseUrl, serverLayoutDoc, diagramLayoutFlushTimer, suppressDiagramLayoutFlush, diagramLocalStorageMigrated, EDGE_CFG, KIND_CFG, dgState, traceHighlightIds, showAliases, showLayers, NODE_W, PROP_H, HEADER_H, NAME_LINE_H, NAME_PAD, DIVIDER_H, PAD, MAX_NAME_CHARS, BC_COLORS, LAYER_COLORS;
var init_diagram = __esm({
  "src/explorer/diagram.ts"() {
    init_helpers();
    init_tabs();
    STORAGE_KEY = "domain-model-diagram-positions-global";
    HIDDEN_KINDS_KEY = "domain-model-diagram-hidden-kinds-global";
    HIDDEN_NODE_IDS_KEY = "domain-model-diagram-hidden-node-ids-global";
    HIDDEN_EDGE_KINDS_KEY = "domain-model-diagram-hidden-edge-kinds-global";
    VIEWPORT_KEY = "domain-model-diagram-viewport-global";
    SHOW_ALIASES_KEY = "domain-model-diagram-show-aliases";
    SHOW_LAYERS_KEY = "domain-model-diagram-show-layers";
    LEGACY_POSITIONS_KEY = "domain-model-diagram-positions";
    LEGACY_HIDDEN_KINDS_KEY = "domain-model-diagram-hidden-kinds";
    LEGACY_HIDDEN_EDGE_KINDS_KEY = "domain-model-diagram-hidden-edge-kinds";
    LEGACY_VIEWPORT_KEY = "domain-model-diagram-viewport";
    FLUSH_MS = 450;
    EDGE_WAYPOINTS_KEY = "domain-model-diagram-edge-waypoints-global";
    diagramLayoutBaseUrl = null;
    serverLayoutDoc = null;
    diagramLayoutFlushTimer = null;
    suppressDiagramLayoutFlush = false;
    diagramLocalStorageMigrated = false;
    EDGE_CFG = {
      Contains: { label: "Contains", color: "#60a5fa", dashed: false },
      References: { label: "References", color: "#34d399", dashed: true },
      ReferencesById: { label: "References (by Id)", color: "#34d399", dashed: true },
      Has: { label: "Has", color: "#60a5fa", dashed: false },
      HasMany: { label: "Has Many", color: "#60a5fa", dashed: false },
      Emits: { label: "Emits", color: "#fbbf24", dashed: true },
      Handles: { label: "Handles", color: "#f472b6", dashed: true },
      Manages: { label: "Manages", color: "#fb923c", dashed: false },
      Publishes: { label: "Publishes", color: "#2dd4bf", dashed: true }
    };
    KIND_CFG = {
      aggregate: { label: "Aggregates", color: "#d4a0ff", bg: "#1f1828", border: "#7c5aa8", stereotype: "\xABAggregate\xBB" },
      entity: { label: "Entities", color: "#7ab8ff", bg: "#161e2c", border: "#4a7bbf", stereotype: "\xABEntity\xBB" },
      valueObject: { label: "Value Objects", color: "#4ee8ad", bg: "#142820", border: "#36a87a", stereotype: "\xABValue Object\xBB" },
      subType: { label: "Sub Types", color: "#a0b4c8", bg: "#1a1e24", border: "#6880a0", stereotype: "\xABSub Type\xBB" },
      event: { label: "Domain Events", color: "#fdd04e", bg: "#2a2418", border: "#b89530", stereotype: "\xABDomain Event\xBB" },
      integrationEvent: { label: "Integration Events", color: "#48e8d8", bg: "#14282a", border: "#30a89e", stereotype: "\xABIntegration Event\xBB" },
      commandHandlerTarget: { label: "Cmd handler targets", color: "#f0a050", bg: "#2a2218", border: "#c07830", stereotype: "\xABHandles target\xBB" },
      eventHandler: { label: "Event Handlers", color: "#ff8ac8", bg: "#2a1824", border: "#b85888", stereotype: "\xABEvent Handler\xBB" },
      commandHandler: { label: "Command Handlers", color: "#ff8ac8", bg: "#2a1824", border: "#b85888", stereotype: "\xABCommand Handler\xBB" },
      queryHandler: { label: "Query Handlers", color: "#ff8ac8", bg: "#2a1824", border: "#b85888", stereotype: "\xABQuery Handler\xBB" },
      repository: { label: "Repositories", color: "#ffab5c", bg: "#2a2018", border: "#b87838", stereotype: "\xABRepository\xBB" },
      service: { label: "Services", color: "#bda0ff", bg: "#1e1828", border: "#7860b0", stereotype: "\xABService\xBB" }
    };
    dgState = null;
    traceHighlightIds = /* @__PURE__ */ new Set();
    showAliases = false;
    showLayers = false;
    NODE_W = 200;
    PROP_H = 17;
    HEADER_H = 26;
    NAME_LINE_H = 18;
    NAME_PAD = 6;
    DIVIDER_H = 8;
    PAD = 12;
    MAX_NAME_CHARS = 22;
    try {
      showAliases = localStorage.getItem(SHOW_ALIASES_KEY) === "true";
    } catch {
    }
    try {
      showLayers = localStorage.getItem(SHOW_LAYERS_KEY) === "true";
    } catch {
    }
    BC_COLORS = ["#6366f1", "#06b6d4", "#f59e0b", "#10b981", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6"];
    LAYER_COLORS = { Domain: "#a78bfa", Application: "#60a5fa", Infrastructure: "#fb923c" };
  }
});

// src/explorer/testing.ts
var testing_exports = {};
__export(testing_exports, {
  cancelEdit: () => cancelEdit,
  cancelInvoke: () => cancelInvoke,
  create: () => create,
  deleteInstance: () => deleteInstance,
  editInstance: () => editInstance,
  initTesting: () => initTesting,
  invokeInstanceMethod: () => invokeInstanceMethod,
  mountTesting: () => mountTesting,
  renderTestingView: () => renderTestingView,
  saveInstance: () => saveInstance,
  selectMethod: () => selectMethod,
  selectType: () => selectType,
  startInvoke: () => startInvoke,
  toggleInstance: () => toggleInstance
});
async function initTesting(url) {
  apiUrl = url;
  try {
    const [aggRes, instRes] = await Promise.all([
      fetch(`${apiUrl}/testing/aggregates`),
      fetch(`${apiUrl}/testing/instances`)
    ]);
    aggregates = await aggRes.json();
    instances = await instRes.json();
    if (aggregates.length > 0) {
      selectedAggregate = aggregates[0];
      autoSelectMethod();
    }
  } catch (e) {
    console.error("Failed to load testing data:", e);
  }
}
function renderTestingView() {
  let html = renderTabBar("testing");
  html += '<div class="testing-body">';
  html += '<div class="testing-create-panel">';
  html += renderCreateForm();
  html += "</div>";
  html += '<div class="testing-instances-panel">';
  html += renderInstanceList();
  html += "</div>";
  html += "</div>";
  return html;
}
function mountTesting() {
}
function renderCreateForm() {
  let html = '<div class="testing-section-title">Create Aggregate</div>';
  if (aggregates.length === 0) {
    html += '<div class="testing-empty">No aggregate types found in the domain graph.</div>';
    return html;
  }
  html += '<div class="testing-field">';
  html += "<label>Aggregate Type</label>";
  html += '<select class="testing-select" onchange="window.__testing.selectType(this.value)">';
  for (const agg of aggregates) {
    const sel = agg.fullName === selectedAggregate?.fullName ? " selected" : "";
    html += `<option value="${escAttr(agg.fullName)}"${sel}>${esc(agg.name)}</option>`;
  }
  html += "</select>";
  html += "</div>";
  if (!selectedAggregate) return html;
  if (selectedAggregate.description) {
    html += `<div class="testing-desc">${esc(selectedAggregate.description)}</div>`;
  }
  html += '<div class="testing-field">';
  html += "<label>Creation Method</label>";
  html += '<select class="testing-select" onchange="window.__testing.selectMethod(this.value)">';
  html += `<option value="properties"${creationMethod === "properties" ? " selected" : ""}>Properties (JSON deserialization)</option>`;
  (selectedAggregate.constructors || []).forEach((c, i) => {
    if (c.parameters.length > 0) {
      const sig = c.parameters.map((p) => p.typeName).join(", ");
      const val = `constructor:${i}`;
      html += `<option value="${val}"${creationMethod === val ? " selected" : ""}>Constructor(${esc(sig)})</option>`;
    }
  });
  for (const f of selectedAggregate.factoryMethods || []) {
    const sig = f.parameters.map((p) => p.typeName).join(", ");
    const val = `factory:${f.name}`;
    const star = f.name === selectedAggregate.configuredFactory ? " \u2605" : "";
    html += `<option value="${val}"${creationMethod === val ? " selected" : ""}>${esc(f.name)}(${esc(sig)})${star}</option>`;
  }
  html += "</select>";
  html += "</div>";
  html += '<div class="testing-params" id="testingParams">';
  html += renderParameterFields();
  html += "</div>";
  if (error) {
    html += `<div class="testing-error">${esc(error)}</div>`;
  }
  html += `<button class="testing-create-btn" onclick="window.__testing.create()" ${creating ? "disabled" : ""}>`;
  html += creating ? "Creating\u2026" : "Create & Store";
  html += "</button>";
  return html;
}
function renderParameterFields() {
  let params = [];
  if (creationMethod === "properties") {
    params = selectedAggregate.properties || [];
  } else if (creationMethod.startsWith("constructor:")) {
    const idx = parseInt(creationMethod.split(":")[1]);
    params = selectedAggregate.constructors[idx]?.parameters || [];
  } else if (creationMethod.startsWith("factory:")) {
    const name = creationMethod.split(":").slice(1).join(":");
    const factory = (selectedAggregate.factoryMethods || []).find((f) => f.name === name);
    params = factory?.parameters || [];
  }
  if (params.length === 0) {
    return '<div class="testing-hint">No parameters required. Click Create to instantiate with default values.</div>';
  }
  let html = "";
  for (const p of params) {
    html += renderSingleField(p, "");
  }
  return html;
}
function renderSingleField(p, prefix) {
  const fullName = prefix ? `${prefix}.${p.name}` : p.name;
  const req = p.isRequired ? ' <span class="testing-required">*</span>' : "";
  const complex = p.isComplex || false;
  const hasSubProps = complex && Array.isArray(p.subProperties) && p.subProperties.length > 0;
  let html = "";
  if (hasSubProps) {
    html += '<div class="testing-field testing-object-group">';
    html += `<div class="testing-object-header">`;
    html += `<span class="testing-object-label">${esc(p.name)}${req}</span>`;
    html += `<span class="testing-type-hint">${esc(p.typeName)}</span>`;
    html += "</div>";
    html += '<div class="testing-object-fields">';
    for (const sub of p.subProperties) {
      html += renderSingleField(sub, fullName);
    }
    html += "</div>";
    html += "</div>";
  } else if (p.isCollection) {
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" placeholder='[ ]' spellcheck="false"></textarea>`;
    html += "</div>";
  } else if (complex) {
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" placeholder='{ }' spellcheck="false"></textarea>`;
    html += "</div>";
  } else if (p.typeName === "bool") {
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<select class="testing-select testing-input" data-param="${escAttr(fullName)}">`;
    html += '<option value="">\u2014 select \u2014</option>';
    html += '<option value="true">true</option>';
    html += '<option value="false">false</option>';
    html += "</select>";
    html += "</div>";
  } else {
    const ph = p.defaultValue || placeholder(p.typeName);
    const inputType = inputTypeFor(p.typeName);
    const step = ["decimal", "double", "float"].includes(p.typeName) ? ' step="any"' : "";
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<input class="testing-input" type="${inputType}" data-param="${escAttr(fullName)}" placeholder="${escAttr(ph)}"${step} />`;
    html += "</div>";
  }
  return html;
}
function inputTypeFor(typeName) {
  if (["int", "long", "decimal", "double", "float"].includes(typeName)) return "number";
  return "text";
}
function placeholder(typeName) {
  const map = {
    "string": "Enter text\u2026",
    "int": "0",
    "long": "0",
    "decimal": "0.00",
    "double": "0.0",
    "float": "0.0",
    "bool": "true / false",
    "Guid": "00000000-0000-0000-0000-000000000000",
    "DateTime": "2026-01-01T00:00:00"
  };
  return map[typeName] || "";
}
function collectParameters() {
  const inputs = document.querySelectorAll("#testingParams .testing-input");
  const params = {};
  for (const input of inputs) {
    const path = input.dataset.param;
    let value = (input.value || "").trim();
    if (!value) continue;
    let parsed;
    if (input.dataset.complex === "true") {
      try {
        parsed = JSON.parse(value);
      } catch (e) {
        throw new Error(`Invalid JSON for "${path}": ${e.message}`);
      }
    } else if (input.type === "number") {
      parsed = value.includes(".") ? parseFloat(value) : parseInt(value);
    } else if (value === "true" || value === "false") {
      parsed = value === "true";
    } else {
      parsed = value;
    }
    setNested(params, path, parsed);
  }
  return params;
}
function setNested(obj, path, value) {
  const parts = path.split(".");
  let cur = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    if (!(parts[i] in cur)) cur[parts[i]] = {};
    cur = cur[parts[i]];
  }
  cur[parts[parts.length - 1]] = value;
}
function renderInstanceList() {
  let html = `<div class="testing-section-title">Stored Instances <span class="testing-count">${instances.length}</span></div>`;
  if (instances.length === 0) {
    html += '<div class="testing-empty">No instances created yet.<br>Use the form on the left to create and store aggregates.</div>';
    return html;
  }
  for (const inst of instances) {
    const expanded = expandedInstances.has(inst.id);
    const isEditing = editingInstance === inst.id;
    html += '<div class="testing-instance">';
    html += '<div class="testing-instance-header">';
    html += `<span class="testing-instance-type">${esc(inst.typeName)}</span>`;
    html += `<span class="testing-instance-id" title="${escAttr(inst.id)}">${esc(inst.id.substring(0, 8))}\u2026</span>`;
    html += `<button class="testing-expand-btn" onclick="window.__testing.toggleInstance('${escAttr(inst.id)}')" title="${expanded ? "Collapse" : "Expand"}">${expanded ? "\u25BE" : "\u25B8"}</button>`;
    html += `<button class="testing-edit-btn${isEditing ? " active" : ""}" onclick="window.__testing.editInstance('${escAttr(inst.id)}')" title="${isEditing ? "Close editor" : "Edit & invoke methods"}">\u270E</button>`;
    html += `<button class="testing-delete-btn" onclick="window.__testing.deleteInstance('${escAttr(inst.id)}')" title="Delete instance">\u2715</button>`;
    html += "</div>";
    if (isEditing) {
      html += renderInstanceEditor(inst);
    } else if (expanded) {
      html += '<div class="testing-instance-props">';
      html += "<pre>" + highlight(JSON.stringify(inst.properties, null, 2)) + "</pre>";
      html += "</div>";
    }
    html += "</div>";
  }
  return html;
}
function renderInstanceEditor(inst) {
  const agg = aggregates.find((a) => a.fullName === inst.typeFullName);
  const methods = agg?.methods || [];
  let html = '<div class="testing-instance-editor">';
  html += '<div class="testing-editor-section">';
  html += '<div class="testing-editor-section-title">Properties</div>';
  html += `<div class="testing-editor-props" id="editProps-${escAttr(inst.id)}">`;
  const props = agg?.properties || [];
  if (props.length > 0) {
    for (const p of props) {
      const currentVal = getNestedValue(inst.properties, p.name);
      html += renderEditField(p, "", inst.id, currentVal);
    }
  } else {
    html += `<textarea class="testing-input testing-textarea testing-edit-area" id="editJson-${escAttr(inst.id)}" spellcheck="false">${esc(JSON.stringify(inst.properties, null, 2))}</textarea>`;
  }
  html += "</div>";
  html += '<div class="testing-edit-actions">';
  html += `<button class="testing-save-btn" onclick="window.__testing.saveInstance('${escAttr(inst.id)}')">Update</button>`;
  html += `<button class="testing-cancel-btn" onclick="window.__testing.cancelEdit('${escAttr(inst.id)}')">Cancel</button>`;
  html += "</div>";
  html += "</div>";
  if (methods.length > 0) {
    html += '<div class="testing-editor-section">';
    html += '<div class="testing-editor-section-title">Methods</div>';
    html += '<div class="testing-methods-list">';
    for (const m of methods) {
      const isInvoking = invokeMethod?.id === inst.id && invokeMethod?.methodName === m.name;
      const sig = m.parameters.map((p) => `${p.typeName} ${p.name}`).join(", ");
      html += '<div class="testing-method-card">';
      html += '<div class="testing-method-header">';
      html += `<span class="testing-method-name">${esc(m.name)}</span>`;
      html += `<span class="testing-method-sig">(${esc(sig)})</span>`;
      html += `<span class="testing-method-return">${esc(m.returnTypeName)}</span>`;
      html += "</div>";
      if (isInvoking) {
        html += `<div class="testing-method-params" id="methodParams-${escAttr(inst.id)}-${escAttr(m.name)}">`;
        if (m.parameters.length > 0) {
          for (const p of m.parameters) {
            html += renderSingleField(p, "", `methodParam-${inst.id}-${m.name}`);
          }
        }
        html += '<div class="testing-method-actions">';
        html += `<button class="testing-invoke-btn" onclick="window.__testing.invokeMethod('${escAttr(inst.id)}', '${escAttr(m.name)}')">Invoke</button>`;
        html += `<button class="testing-cancel-btn" onclick="window.__testing.cancelInvoke()">Cancel</button>`;
        html += "</div>";
        html += "</div>";
        if (invokeResult && invokeResult.methodName === m.name && invokeResult.instanceId === inst.id) {
          html += renderInvokeResult(invokeResult);
        }
      } else {
        html += `<button class="testing-expand-method-btn" onclick="window.__testing.startInvoke('${escAttr(inst.id)}', '${escAttr(m.name)}')">\u25B6 Invoke</button>`;
      }
      html += "</div>";
    }
    html += "</div>";
    html += "</div>";
  }
  html += "</div>";
  return html;
}
function renderEditField(p, prefix, instanceId, currentValue) {
  const fullName = prefix ? `${prefix}.${p.name}` : p.name;
  const req = p.isRequired ? ' <span class="testing-required">*</span>' : "";
  const complex = p.isComplex || false;
  const hasSubProps = complex && Array.isArray(p.subProperties) && p.subProperties.length > 0;
  let html = "";
  if (hasSubProps) {
    html += '<div class="testing-field testing-object-group">';
    html += '<div class="testing-object-header">';
    html += `<span class="testing-object-label">${esc(p.name)}${req}</span>`;
    html += `<span class="testing-type-hint">${esc(p.typeName)}</span>`;
    html += "</div>";
    html += '<div class="testing-object-fields">';
    for (const sub of p.subProperties) {
      const subVal = currentValue != null ? getNestedValue(currentValue, sub.name) : void 0;
      html += renderEditField(sub, fullName, instanceId, subVal);
    }
    html += "</div>";
    html += "</div>";
  } else if (p.isCollection) {
    const val = currentValue != null ? JSON.stringify(currentValue, null, 2) : "";
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" spellcheck="false">${esc(val)}</textarea>`;
    html += "</div>";
  } else if (complex) {
    const val = currentValue != null ? JSON.stringify(currentValue, null, 2) : "";
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" spellcheck="false">${esc(val)}</textarea>`;
    html += "</div>";
  } else if (p.typeName === "bool") {
    const val = currentValue != null ? String(currentValue) : "";
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<select class="testing-select testing-input" data-param="${escAttr(fullName)}">`;
    html += `<option value="">\u2014 select \u2014</option>`;
    html += `<option value="true"${val === "true" ? " selected" : ""}>true</option>`;
    html += `<option value="false"${val === "false" ? " selected" : ""}>false</option>`;
    html += "</select>";
    html += "</div>";
  } else {
    const val = currentValue != null ? String(currentValue) : "";
    const inputType = inputTypeFor(p.typeName);
    const step = ["decimal", "double", "float"].includes(p.typeName) ? ' step="any"' : "";
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<input class="testing-input" type="${inputType}" data-param="${escAttr(fullName)}" value="${escAttr(val)}"${step} />`;
    html += "</div>";
  }
  return html;
}
function renderInvokeResult(result) {
  let html = '<div class="testing-invoke-result">';
  if (result.error) {
    html += `<div class="testing-error">${esc(result.error)}</div>`;
  } else {
    if (result.raisedEvents && result.raisedEvents.length > 0) {
      html += '<div class="testing-events-raised">';
      html += '<div class="testing-events-title">\u26A1 Events raised:</div>';
      for (const evt of result.raisedEvents) {
        html += '<div class="testing-event-card">';
        html += `<span class="testing-event-name">${esc(evt.typeName)}</span>`;
        html += "<pre>" + highlight(JSON.stringify(evt.properties, null, 2)) + "</pre>";
        html += "</div>";
      }
      html += "</div>";
    }
    html += '<div class="testing-invoke-success">\u2713 Method invoked \u2014 instance updated.</div>';
  }
  html += "</div>";
  return html;
}
function getNestedValue(obj, name) {
  if (obj == null || typeof obj !== "object") return void 0;
  if (name in obj) return obj[name];
  const camel = name[0].toLowerCase() + name.slice(1);
  if (camel in obj) return obj[camel];
  const pascal = name[0].toUpperCase() + name.slice(1);
  if (pascal in obj) return obj[pascal];
  return void 0;
}
function collectScopedParams(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return {};
  const inputs = container.querySelectorAll(".testing-input");
  const params = {};
  for (const input of inputs) {
    const path = input.dataset.param;
    if (!path) continue;
    let value = (input.value || "").trim();
    if (!value) continue;
    let parsed;
    if (input.dataset.complex === "true") {
      try {
        parsed = JSON.parse(value);
      } catch (e) {
        throw new Error(`Invalid JSON for "${path}": ${e.message}`);
      }
    } else if (input.type === "number") {
      parsed = value.includes(".") ? parseFloat(value) : parseInt(value);
    } else if (value === "true" || value === "false") {
      parsed = value === "true";
    } else {
      parsed = value;
    }
    setNested(params, path, parsed);
  }
  return params;
}
function highlight(json) {
  return json.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"([^"]+)":/g, '<span class="json-key">"$1"</span>:').replace(/: "([^"]*)"/g, ': <span class="json-string">"$1"</span>').replace(/: (\d+\.?\d*)/g, ': <span class="json-number">$1</span>').replace(/: (true|false)/g, ': <span class="json-bool">$1</span>').replace(/: (null)/g, ': <span class="json-null">$1</span>');
}
function autoSelectMethod() {
  if (selectedAggregate?.configuredFactory) {
    creationMethod = `factory:${selectedAggregate.configuredFactory}`;
  } else {
    creationMethod = "properties";
  }
}
function refresh() {
  const main = document.getElementById("mainContent");
  if (main) main.innerHTML = renderTestingView();
}
function selectType(fullName) {
  selectedAggregate = aggregates.find((a) => a.fullName === fullName) || null;
  error = null;
  autoSelectMethod();
  refresh();
}
function selectMethod(method) {
  creationMethod = method;
  error = null;
  refresh();
}
async function create() {
  if (!selectedAggregate || creating) return;
  error = null;
  creating = true;
  refresh();
  try {
    const params = collectParameters();
    let factoryMethod = null;
    if (creationMethod.startsWith("factory:")) {
      factoryMethod = creationMethod.split(":").slice(1).join(":");
    }
    const body = {
      typeFullName: selectedAggregate.fullName,
      factoryMethod,
      parameters: Object.keys(params).length > 0 ? params : null
    };
    const res = await fetch(`${apiUrl}/testing/create`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (!res.ok) {
      const errData = await res.json().catch(() => null);
      throw new Error(errData?.error || `HTTP ${res.status}`);
    }
    const instance = await res.json();
    instances.unshift(instance);
    expandedInstances.add(instance.id);
  } catch (e) {
    error = e.message;
  } finally {
    creating = false;
    refresh();
  }
}
async function deleteInstance(id) {
  try {
    await fetch(`${apiUrl}/testing/instances/${id}`, { method: "DELETE" });
    instances = instances.filter((i) => i.id !== id);
    expandedInstances.delete(id);
    editingInstances.delete(id);
    refresh();
  } catch (e) {
    error = e.message;
    refresh();
  }
}
function editInstance(id) {
  if (editingInstance === id) {
    editingInstance = null;
    invokeMethod = null;
    invokeResult = null;
    refresh();
    return;
  }
  editingInstance = id;
  invokeMethod = null;
  invokeResult = null;
  expandedInstances.delete(id);
  refresh();
}
function cancelEdit() {
  editingInstance = null;
  invokeMethod = null;
  invokeResult = null;
  refresh();
}
async function saveInstance(id) {
  try {
    const agg = aggregates.find((a) => {
      const inst = instances.find((i) => i.id === id);
      return inst && a.fullName === inst.typeFullName;
    });
    let params;
    if (agg?.properties?.length > 0) {
      params = collectScopedParams(`editProps-${id}`);
    } else {
      const textarea = document.getElementById(`editJson-${id}`);
      if (!textarea) return;
      params = JSON.parse(textarea.value);
    }
    const res = await fetch(`${apiUrl}/testing/instances/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ parameters: params })
    });
    if (!res.ok) {
      const errData = await res.json().catch(() => null);
      throw new Error(errData?.error || `HTTP ${res.status}`);
    }
    const updated = await res.json();
    instances = instances.map((i) => i.id === id ? updated : i);
    error = null;
  } catch (e) {
    error = e.message;
  }
  refresh();
}
function toggleInstance(id) {
  if (expandedInstances.has(id)) expandedInstances.delete(id);
  else expandedInstances.add(id);
  refresh();
}
function startInvoke(instanceId, methodName) {
  invokeMethod = { id: instanceId, methodName };
  invokeResult = null;
  refresh();
}
function cancelInvoke() {
  invokeMethod = null;
  invokeResult = null;
  refresh();
}
async function invokeInstanceMethod(instanceId, methodName) {
  try {
    const agg = aggregates.find((a) => {
      const inst = instances.find((i) => i.id === instanceId);
      return inst && a.fullName === inst.typeFullName;
    });
    const method = agg?.methods?.find((m) => m.name === methodName);
    let params = {};
    if (method?.parameters?.length > 0) {
      params = collectScopedParams(`methodParams-${instanceId}-${methodName}`);
    }
    const res = await fetch(`${apiUrl}/testing/instances/${instanceId}/invoke`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ methodName, parameters: params })
    });
    if (!res.ok) {
      const errData = await res.json().catch(() => null);
      throw new Error(errData?.error || `HTTP ${res.status}`);
    }
    const result = await res.json();
    if (result.instance) {
      instances = instances.map((i) => i.id === instanceId ? result.instance : i);
    }
    invokeResult = {
      instanceId,
      methodName,
      raisedEvents: result.raisedEvents || []
    };
    error = null;
  } catch (e) {
    invokeResult = {
      instanceId,
      methodName,
      error: e.message
    };
  }
  refresh();
}
var apiUrl, aggregates, selectedAggregate, creationMethod, instances, creating, error, expandedInstances, editingInstance, invokeMethod, invokeResult;
var init_testing = __esm({
  "src/explorer/testing.ts"() {
    init_helpers();
    init_tabs();
    apiUrl = "";
    aggregates = [];
    selectedAggregate = null;
    creationMethod = "properties";
    instances = [];
    creating = false;
    error = null;
    expandedInstances = /* @__PURE__ */ new Set();
    editingInstance = null;
    invokeMethod = null;
    invokeResult = null;
  }
});

// src/explorer/feature-editor.ts
var feature_editor_exports = {};
__export(feature_editor_exports, {
  addAllFromBoundedContext: () => addAllFromBoundedContext,
  addExistingType: () => addExistingType,
  addMethod: () => addMethod,
  addNewType: () => addNewType,
  addProperty: () => addProperty,
  addRule: () => addRule,
  changeAlias: () => changeAlias,
  changeBoundedContext: () => changeBoundedContext,
  changeDescription: () => changeDescription,
  changeEdgeKind: () => changeEdgeKind,
  changeLayer: () => changeLayer,
  createFeature: () => createFeature,
  deleteFeature: () => deleteFeature,
  downloadExport: () => downloadExport,
  featureEditorFit: () => featureEditorFit,
  featureEditorZoom: () => featureEditorZoom,
  filterPalette: () => filterPalette,
  hideAllFeKinds: () => hideAllFeKinds,
  initFeatureEditor: () => initFeatureEditor,
  isFeatureEditorViewModeLayoutActive: () => isFeatureEditorViewModeLayoutActive,
  loadFeature: () => loadFeature,
  mountFeatureEditor: () => mountFeatureEditor,
  onDiagramViewFlagsChanged: () => onDiagramViewFlagsChanged,
  removeEdge: () => removeEdge,
  removeMethod: () => removeMethod,
  removeNode: () => removeNode,
  removeProperty: () => removeProperty,
  removeRule: () => removeRule,
  renameCustomType: () => renameCustomType,
  renderFeatureEditorView: () => renderFeatureEditorView,
  saveFeature: () => saveFeature,
  showAllFeKinds: () => showAllFeKinds,
  startConnect: () => startConnect,
  toggleBcDropdown: () => toggleBcDropdown,
  toggleFeEdgeFilter: () => toggleFeEdgeFilter,
  toggleFeEdgeKind: () => toggleFeEdgeKind,
  toggleFeKind: () => toggleFeKind,
  toggleFeKindFilter: () => toggleFeKindFilter,
  toggleFeatureEditorAliases: () => toggleFeatureEditorAliases,
  toggleFeatureEditorLayers: () => toggleFeatureEditorLayers,
  toggleFeatureEditorViewMode: () => toggleFeatureEditorViewMode,
  toggleLayerDropdown: () => toggleLayerDropdown,
  toggleRelDropdown: () => toggleRelDropdown
});
function wrapName2(text) {
  if (!text || text.length <= MAX_NAME_CHARS2) return [text || ""];
  const words = text.includes(" ") ? text.split(" ") : text.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2").split(" ");
  const lines = [];
  let line = "";
  for (const w of words) {
    const candidate = line ? line + (text.includes(" ") ? " " : "") + w : w;
    if (candidate.length > MAX_NAME_CHARS2 && line) {
      lines.push(line);
      line = w;
    } else {
      line = candidate;
    }
  }
  if (line) lines.push(line);
  return lines.length ? lines : [text];
}
function nodeNameHeight2(n) {
  const lines = wrapName2(feDisplayName(n));
  return NAME_PAD2 + lines.length * NAME_LINE_H2;
}
function isViewModeOnly() {
  return viewModeOnly === true;
}
function feLoadLastBoundedContext() {
  try {
    const v = localStorage.getItem(FE_LAST_BC_KEY);
    return v && v.trim() ? v.trim() : "";
  } catch {
    return "";
  }
}
function feLoadLastLayer() {
  try {
    const v = localStorage.getItem(FE_LAST_LAYER_KEY);
    return v && v.trim() ? v.trim() : "";
  } catch {
    return "";
  }
}
function feRememberLastContext(boundedContext, layer) {
  try {
    if (boundedContext && String(boundedContext).trim()) {
      localStorage.setItem(FE_LAST_BC_KEY, String(boundedContext).trim());
    }
    if (layer && String(layer).trim()) {
      localStorage.setItem(FE_LAST_LAYER_KEY, String(layer).trim());
    }
  } catch {
  }
}
function feParseMethodSignatureInput(sig) {
  const s = String(sig || "").trim();
  if (!s) return { returnTypeName: "void", name: "Method", parameters: [] };
  const paren = s.indexOf("(");
  const head = paren >= 0 ? s.slice(0, paren).trim() : s;
  const tail = paren >= 0 ? s.slice(paren) : "";
  const headParts = head.split(/\s+/).filter(Boolean);
  let returnTypeName = "void";
  let name = "Method";
  if (headParts.length === 1) {
    name = headParts[0];
  } else if (headParts.length >= 2) {
    returnTypeName = headParts.slice(0, -1).join(" ");
    name = headParts[headParts.length - 1];
  }
  const parameters = [];
  if (tail.startsWith("(") && tail.endsWith(")")) {
    const inner = tail.slice(1, -1).trim();
    if (inner) {
      inner.split(",").forEach((part, i) => {
        const p = part.trim();
        if (!p) return;
        const ps = p.split(/\s+/);
        if (ps.length >= 2) {
          const pName = ps[ps.length - 1];
          const pType = ps.slice(0, -1).join(" ");
          parameters.push({ name: pName, typeName: pType });
        } else {
          parameters.push({ name: `arg${i + 1}`, typeName: p });
        }
      });
    }
  }
  return { returnTypeName, name, parameters };
}
function feRebuildMethodDisplayLines(n) {
  const list = n.structuredMethods || [];
  n.methods = list.map((m) => formatDiagramMethodLine(m));
}
function feRebuildRuleDisplayLines(n) {
  const list = n.structuredRules || [];
  n.ruleLines = list.map((r) => formatDiagramRuleLine(r));
}
function feEnsureMethodRuleStructures(n) {
  if (!n.structuredMethods) n.structuredMethods = [];
  if (!n.structuredRules) n.structuredRules = [];
  if (!Array.isArray(n.methods)) n.methods = [];
  if (!Array.isArray(n.ruleLines)) n.ruleLines = [];
}
function feDomainMethodsToStructured(methods) {
  if (!methods || !methods.length) return [];
  return methods.map((m) => {
    if (m && typeof m === "object" && m.name) {
      return {
        returnTypeName: m.returnTypeName || "void",
        name: m.name,
        parameters: Array.isArray(m.parameters) ? m.parameters.map((p) => ({ name: p.name || "", typeName: p.typeName || "" })) : []
      };
    }
    return feParseMethodSignatureInput(String(m || ""));
  });
}
async function initFeatureEditor(apiBaseUrl, data2) {
  baseUrl = apiBaseUrl;
  domainData = data2;
  await loadFeatureList();
  await loadFeatureExports();
}
function toggleFeatureEditorViewMode() {
  viewModeOnly = !viewModeOnly;
  try {
    if (viewModeOnly) sessionStorage.setItem(FEATURE_EDITOR_VIEW_MODE_KEY, "1");
    else sessionStorage.removeItem(FEATURE_EDITOR_VIEW_MODE_KEY);
  } catch {
  }
  connecting = null;
  if (st) {
    st.selectedNode = null;
    st.selectedEdge = null;
  }
  rerender();
}
function isFeatureEditorViewModeLayoutActive() {
  return isViewModeOnly() === true && !!currentFeatureName;
}
function onDiagramViewFlagsChanged() {
  if (!isViewModeOnly() || !st) return;
  reloadDiagramViewFlagsFromStorage();
  syncDiagramToolbarToggles();
  for (const n of st.nodes) n.h = nodeHeight2(n);
  renderSvg2();
}
function feToggleDropdown(menuId, triggerId) {
  const menu = document.getElementById(menuId);
  const trigger = document.getElementById(triggerId);
  if (!menu) return;
  const open = menu.classList.toggle("visible");
  if (trigger) trigger.classList.toggle("open", open);
  if (!open) return;
  const close = (ev) => {
    const clickedTrigger = trigger && (ev.target === trigger || trigger.contains(ev.target));
    if (!menu.contains(ev.target) && !clickedTrigger) {
      menu.classList.remove("visible");
      if (trigger) trigger.classList.remove("open");
      document.removeEventListener("click", close);
    }
  };
  setTimeout(() => document.addEventListener("click", close), 0);
}
function renderFeKindFilters() {
  if (!st) return "";
  const presentKinds = new Set(st.nodes.map((n) => n.kind));
  if (presentKinds.size === 0) return "";
  const visibleKinds = [...presentKinds].filter((k) => !st.hiddenKinds.has(k)).length;
  let html = `<button type="button" class="rel-dropdown-trigger" id="feKindFilterTrigger" onclick="window.__featureEditor.toggleFeKindFilter()" title="Filter node types">`;
  html += '<span style="font-size:10px;opacity:.7">\u25C8</span>';
  html += "<span>Node Types</span>";
  html += `<span class="rel-hidden-count">${visibleKinds}/${presentKinds.size}</span>`;
  html += '<span class="rel-chevron">\u25BE</span>';
  html += "</button>";
  html += '<div class="rel-dropdown-menu" id="feKindFilterMenu">';
  html += '<div class="rel-dropdown-actions">';
  html += '<button type="button" onclick="window.__featureEditor.showAllFeKinds()">Show all</button>';
  html += '<button type="button" onclick="window.__featureEditor.hideAllFeKinds()">Hide all</button>';
  html += "</div>";
  for (const kind of Object.keys(KIND_CFG2)) {
    if (!presentKinds.has(kind)) continue;
    const cfg = KIND_CFG2[kind];
    const visible = !st.hiddenKinds.has(kind);
    const count = st.nodes.filter((n) => n.kind === kind).length;
    const label = FE_KIND_DROPDOWN_LABEL[kind] || kind;
    html += `<div class="rel-dropdown-item${visible ? " checked" : ""}" onclick="window.__featureEditor.toggleFeKind(event, '${kind}')" data-node-kind="${escAttr(kind)}">`;
    html += `<span class="rel-check">${visible ? "\u2713" : ""}</span>`;
    html += `<span class="diagram-kind-dot" style="background:${cfg.color}"></span>`;
    html += `<span class="rel-kind-label">${esc(label)}</span>`;
    html += `<span class="diagram-kind-count">${count}</span>`;
    html += "</div>";
  }
  html += "</div>";
  return html;
}
function renderFeEdgeFilter() {
  if (!st) return "";
  const present = new Set(st.edges.map((e) => e.kind));
  if (present.size === 0) return "";
  const hiddenCount = st.hiddenEdgeKinds.size;
  let h = `<button type="button" class="rel-dropdown-trigger" id="feEdgeFilterTrigger" onclick="window.__featureEditor.toggleFeEdgeFilter()" title="Filter relation types">`;
  h += '<span style="font-size:10px;opacity:.7">\u27DC</span>';
  h += "<span>Relations</span>";
  if (hiddenCount > 0) h += `<span class="rel-hidden-count">${hiddenCount}</span>`;
  h += '<span class="rel-chevron">\u25BE</span>';
  h += "</button>";
  h += '<div class="rel-dropdown-menu" id="feEdgeFilterMenu">';
  for (const kind of Object.keys(FE_EDGE_CFG)) {
    if (!present.has(kind)) continue;
    const cfg = FE_EDGE_CFG[kind];
    const visible = !st.hiddenEdgeKinds.has(kind);
    h += `<div class="rel-dropdown-item${visible ? " checked" : ""}" onclick="window.__featureEditor.toggleFeEdgeKind(event, '${kind}')" data-edge-kind="${escAttr(kind)}">`;
    h += `<span class="rel-check">${visible ? "\u2713" : ""}</span>`;
    h += `<span class="rel-line-sample${cfg.dashed ? " dashed" : ""}" style="color:${cfg.color}"></span>`;
    h += `<span class="rel-kind-label">${esc(cfg.label)}</span>`;
    h += "</div>";
  }
  h += "</div>";
  return h;
}
function refreshFeViewFilters() {
  if (!isViewModeOnly() || !st) return;
  const kindEl = document.getElementById("feKindFilterWrap");
  const prevMenu = document.getElementById("feKindFilterMenu");
  const prevTrigger = document.getElementById("feKindFilterTrigger");
  const wasVisible = !!prevMenu?.classList.contains("visible");
  const wasOpen = !!prevTrigger?.classList.contains("open");
  if (kindEl) kindEl.innerHTML = renderFeKindFilters();
  if (wasVisible || wasOpen) {
    document.getElementById("feKindFilterMenu")?.classList.add("visible");
    document.getElementById("feKindFilterTrigger")?.classList.add("open");
  }
  refreshFeEdgeFilterOnly();
}
function refreshFeEdgeFilterOnly() {
  if (!isViewModeOnly() || !st) return;
  const edgeEl = document.getElementById("feEdgeFilterWrap");
  if (edgeEl) edgeEl.innerHTML = renderFeEdgeFilter();
}
function syncFeKindFilterUi() {
  if (!st) return;
  const trigger = document.getElementById("feKindFilterTrigger");
  const menu = document.getElementById("feKindFilterMenu");
  if (!trigger || !menu) {
    refreshFeViewFilters();
    return;
  }
  const presentKinds = new Set(st.nodes.map((n) => n.kind));
  const visibleKinds = [...presentKinds].filter((k) => !st.hiddenKinds.has(k)).length;
  const badge = trigger.querySelector(".rel-hidden-count");
  if (badge) badge.textContent = `${visibleKinds}/${presentKinds.size}`;
  for (const row of menu.querySelectorAll("[data-node-kind]")) {
    const kind = row.getAttribute("data-node-kind");
    if (!kind) continue;
    const visible = !st.hiddenKinds.has(kind);
    row.classList.toggle("checked", visible);
    const check = row.querySelector(".rel-check");
    if (check) check.textContent = visible ? "\u2713" : "";
  }
}
function clearFeSelectionIfHidden() {
  if (!st || !isViewModeOnly()) return;
  if (st.selectedNode && st.hiddenKinds.has(st.nMap[st.selectedNode]?.kind)) {
    st.selectedNode = null;
  }
  if (st.selectedEdge !== null) {
    const e = st.edges[st.selectedEdge];
    if (!e || st.hiddenEdgeKinds.has(e.kind)) st.selectedEdge = null;
    else {
      const a = st.nMap[e.source], b = st.nMap[e.target];
      if (!a || !b || st.hiddenKinds.has(a.kind) || st.hiddenKinds.has(b.kind)) st.selectedEdge = null;
    }
  }
}
function toggleFeatureEditorAliases() {
  diagramToggleAliases();
}
function toggleFeatureEditorLayers() {
  diagramToggleLayers();
}
function toggleFeKindFilter() {
  feToggleDropdown("feKindFilterMenu", "feKindFilterTrigger");
}
function toggleFeEdgeFilter() {
  feToggleDropdown("feEdgeFilterMenu", "feEdgeFilterTrigger");
}
function toggleFeKind(ev, kind) {
  if (ev) {
    ev.stopPropagation();
    ev.preventDefault();
  }
  if (!st || !isViewModeOnly()) return;
  if (st.hiddenKinds.has(kind)) st.hiddenKinds.delete(kind);
  else st.hiddenKinds.add(kind);
  saveDiagramHiddenKindsSet(st.hiddenKinds);
  clearFeSelectionIfHidden();
  for (const n of st.nodes) n.h = nodeHeight2(n);
  renderSvg2();
  syncFeKindFilterUi();
}
function showAllFeKinds() {
  if (!st || !isViewModeOnly()) return;
  st.hiddenKinds.clear();
  saveDiagramHiddenKindsSet(st.hiddenKinds);
  clearFeSelectionIfHidden();
  for (const n of st.nodes) n.h = nodeHeight2(n);
  renderSvg2();
  syncFeKindFilterUi();
}
function hideAllFeKinds() {
  if (!st || !isViewModeOnly()) return;
  st.hiddenKinds.clear();
  for (const n of st.nodes) st.hiddenKinds.add(n.kind);
  saveDiagramHiddenKindsSet(st.hiddenKinds);
  clearFeSelectionIfHidden();
  for (const n of st.nodes) n.h = nodeHeight2(n);
  renderSvg2();
  syncFeKindFilterUi();
}
function toggleFeEdgeKind(ev, kind) {
  if (ev) {
    ev.stopPropagation();
    ev.preventDefault();
  }
  if (!st || !isViewModeOnly()) return;
  if (st.hiddenEdgeKinds.has(kind)) st.hiddenEdgeKinds.delete(kind);
  else st.hiddenEdgeKinds.add(kind);
  saveDiagramHiddenEdgeKindsSet(st.hiddenEdgeKinds);
  clearFeSelectionIfHidden();
  for (const n of st.nodes) n.h = nodeHeight2(n);
  renderSvg2();
  refreshFeEdgeFilterOnly();
}
function isReadOnlyFeature() {
  return currentFeatureReadOnly === true;
}
function renderFeatureEditorView() {
  let html = renderTabBar("features");
  let vm = isViewModeOnly();
  if (vm && !currentFeatureName) {
    viewModeOnly = false;
    try {
      sessionStorage.removeItem(FEATURE_EDITOR_VIEW_MODE_KEY);
    } catch {
    }
    vm = false;
  }
  html += `<div class="fe-layout${vm ? " fe-view-mode" : ""}">`;
  if (!vm) {
    html += '<div class="fe-sidebar" id="feSidebar">';
    html += renderFeatureListPanel();
    html += renderPalettePanel();
    html += "</div>";
  }
  html += '<div class="fe-canvas-wrap" id="feCanvasWrap">';
  if (!currentFeatureName) {
    html += '<div class="fe-empty-state">Select or create a feature to start editing.</div>';
  } else {
    html += '<div class="fe-canvas-toolbar">';
    html += `<span class="fe-feature-name">${esc(currentFeatureName)}</span>`;
    if (isReadOnlyFeature()) {
      html += '<span class="fe-mode-badge" title="Only existing domain items can be included">read-only feature mode</span>';
    }
    if (vm) {
      html += '<span class="fe-mode-badge fe-view-badge" title="Pan and zoom only; exit to edit">view mode</span>';
    }
    html += `<span class="fe-dirty-indicator" id="feDirtyIndicator" style="display:${dirty && !vm ? "inline" : "none"}">\u25CF unsaved</span>`;
    html += '<span class="fe-toolbar-spacer"></span>';
    html += `<button type="button" class="fe-btn${vm ? " primary" : ""}" onclick="window.__featureEditor.toggleViewMode()" title="${vm ? "Show sidebars and editing tools" : "Full-width canvas like the main diagram"}">${vm ? "\u270E Edit" : "\u{1F441} View"}</button>`;
    if (!vm) {
      html += '<button class="fe-btn" onclick="window.__featureEditor.fit()" title="Fit to view">\u22A1 Fit</button>';
      if (featureExports.length > 0) {
        html += '<label class="fe-export-opt" title="Append command-handler DI scaffold (ICommandHandler&lt;T&gt;) to text exports">';
        html += '<input type="checkbox" id="feRegisterCommands" /> Register commands';
        html += "</label>";
      }
      for (const exp of featureExports) {
        html += `<button class="fe-btn" onclick="window.__featureEditor.downloadExport('${escAttr(exp.name)}')" title="Download ${esc(exp.name)}">\u2B07 ${esc(exp.name)}</button>`;
      }
      html += `<button class="fe-btn primary" onclick="window.__featureEditor.save()" title="Save feature" id="feSaveBtn">Save</button>`;
      html += `<button class="fe-btn danger" onclick="window.__featureEditor.deleteFeature()" title="Delete feature">Delete</button>`;
    } else {
      html += '<span class="fe-toolbar-sep fe-toolbar-sep-bar"></span>';
      html += `<button type="button" class="fe-btn" id="feAliasToggle" onclick="window.__featureEditor.toggleAliases()" title="Show aliases instead of original names (same as Diagram)" style="${getDiagramShowAliases() ? "background:var(--bg-hover)" : ""}">Aa Aliases</button>`;
      html += '<span class="fe-toolbar-sep fe-toolbar-sep-bar"></span>';
      html += `<button type="button" class="fe-btn" id="feLayerToggle" onclick="window.__featureEditor.toggleLayers()" title="Show architectural layers (same as Diagram)" style="${getDiagramShowLayers() ? "background:var(--bg-hover)" : ""}">\u229E Layers</button>`;
      html += '<span class="fe-toolbar-sep fe-toolbar-sep-bar"></span>';
      html += '<div class="rel-dropdown" id="feKindFilterWrap"></div>';
      html += '<span class="fe-toolbar-sep fe-toolbar-sep-bar"></span>';
      html += '<div class="rel-dropdown" id="feEdgeFilterWrap"></div>';
      html += '<span class="fe-toolbar-spacer"></span>';
      html += '<button class="fe-btn" onclick="window.__featureEditor.fit()" title="Fit to view">\u22A1 Fit</button>';
    }
    html += "</div>";
    html += `<div class="fe-canvas${vm ? " fe-view-canvas" : ""}" id="feCanvas">`;
    html += '<div class="diagram-controls">';
    html += '<button onclick="window.__featureEditor.zoom(1.25)" title="Zoom in">+</button>';
    html += '<button onclick="window.__featureEditor.zoom(0.8)" title="Zoom out">\u2212</button>';
    html += "</div>";
    if (vm) {
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
      html += "</div>";
    }
    html += '<svg id="feSvg"></svg>';
    html += "</div>";
  }
  html += "</div>";
  if (!vm) {
    html += '<div class="fe-panel" id="fePanel">';
    html += renderPropertiesPanel();
    html += "</div>";
  }
  html += "</div>";
  return html;
}
function mountFeatureEditor() {
  if (!st || !currentFeatureName) return;
  if (isViewModeOnly()) {
    st.hiddenKinds = loadDiagramHiddenKindsSet();
    st.hiddenEdgeKinds = loadDiagramHiddenEdgeKindsSet();
    reloadDiagramViewFlagsFromStorage();
  }
  renderSvg2();
  if (isViewModeOnly()) {
    refreshFeViewFilters();
    syncDiagramToolbarToggles();
  }
  fitToView2();
  if (isViewModeOnly()) setupViewModeInteraction();
  else setupInteraction2();
}
async function loadFeatureList() {
  try {
    const res = await fetch(`${baseUrl}/features`);
    if (res.ok) featureList = await res.json();
  } catch {
    featureList = [];
  }
}
async function loadFeatureExports() {
  try {
    const res = await fetch(`${baseUrl}/features/exports`);
    if (res.ok) featureExports = await res.json();
  } catch {
    featureExports = [];
  }
}
function renderFeatureListPanel() {
  let html = '<div class="fe-section">';
  html += '<div class="fe-section-header">Features</div>';
  html += '<div class="fe-feature-list" id="feFeatureList">';
  for (const name of featureList) {
    const active = name === currentFeatureName ? " active" : "";
    html += `<div class="fe-feature-item${active}" onclick="window.__featureEditor.loadFeature('${escAttr(name)}')">${esc(name)}</div>`;
  }
  html += "</div>";
  html += '<div class="fe-new-feature">';
  html += '<input type="text" class="fe-input" id="feNewFeatureName" placeholder="New feature name\u2026" />';
  html += '<label class="fe-checkbox-row"><input type="checkbox" id="feNewFeatureReadOnly" /> Read-only</label>';
  html += '<button class="fe-btn primary" onclick="window.__featureEditor.createFeature()">+ Create</button>';
  html += "</div>";
  html += "</div>";
  return html;
}
function renderPalettePanel() {
  let html = '<div class="fe-section">';
  html += `<div class="fe-section-header">${isReadOnlyFeature() ? "Add Existing Types" : "Add Types"}</div>`;
  if (currentFeatureName) {
    html += '<div class="fe-bulk-bc">';
    html += '<label class="fe-bulk-bc-label" for="feBulkBcSelect">Add entire context</label>';
    html += '<div class="fe-bulk-bc-row">';
    html += '<select class="fe-input fe-bulk-bc-select" id="feBulkBcSelect" title="Add every discovered type from this bounded context to the diagram">';
    html += '<option value="">Select context\u2026</option>';
    for (const name of getBoundedContextNames()) {
      html += `<option value="${escAttr(name)}">${esc(name)}</option>`;
    }
    html += "</select>";
    html += '<button type="button" class="fe-btn primary fe-bulk-bc-btn" onclick="window.__featureEditor.addAllFromBoundedContext()" title="Place all types from the selected bounded context on the canvas">Add all</button>';
    html += "</div></div>";
  }
  html += '<input type="text" class="fe-input fe-search" id="fePaletteSearch" placeholder="Search types\u2026" oninput="window.__featureEditor.filterPalette()" />';
  html += '<div class="fe-palette" id="fePalette">';
  html += renderPaletteItems("");
  html += "</div>";
  if (!isReadOnlyFeature()) {
    html += '<div class="fe-section-header" style="margin-top:12px">Create New Type</div>';
    html += '<input type="text" class="fe-input" id="feNewTypeName" placeholder="Type name\u2026" />';
    html += '<select class="fe-input" id="feNewTypeKind">';
    for (const [kind, label] of Object.entries(KIND_LABELS)) {
      html += `<option value="${kind}">${label}</option>`;
    }
    html += "</select>";
    html += '<button class="fe-btn" onclick="window.__featureEditor.addNewType()" style="margin-top:4px">+ Add Custom Type</button>';
  } else {
    html += '<div class="fe-readonly-hint">Custom type creation is disabled in read-only feature mode.</div>';
  }
  html += "</div>";
  return html;
}
function renderPaletteItems(filter) {
  if (!domainData) return '<div class="fe-palette-empty">No domain data loaded</div>';
  const lowerFilter = filter.toLowerCase();
  const addedIds = st ? new Set(st.nodes.map((n) => n.id)) : /* @__PURE__ */ new Set();
  let html = "";
  for (const ctx of domainData.boundedContexts || []) {
    for (const sec of ALL_SECTIONS) {
      const kind = SECTION_TO_KIND[sec];
      if (!kind) continue;
      const items = ctx[sec] || [];
      for (const item of items) {
        if (addedIds.has(item.fullName)) continue;
        if (lowerFilter && !item.name.toLowerCase().includes(lowerFilter) && !item.fullName.toLowerCase().includes(lowerFilter)) continue;
        const cfg = KIND_CFG2[kind];
        html += `<div class="fe-palette-item" onclick="window.__featureEditor.addExistingType('${escAttr(item.fullName)}', '${kind}')" title="${esc(item.fullName)}">`;
        html += `<span class="fe-palette-dot" style="background:${cfg.color}"></span>`;
        html += `<span class="fe-palette-name">${esc(item.name)}</span>`;
        html += `<span class="fe-palette-kind">${KIND_LABELS[kind]}</span>`;
        html += "</div>";
      }
    }
  }
  if (!html) {
    html = '<div class="fe-palette-empty">No matching types found</div>';
  }
  return html;
}
function renderPropertiesPanel() {
  if (!st) return '<div class="fe-panel-empty">No feature loaded.</div>';
  const readOnly = isReadOnlyFeature();
  if (st.selectedNode) {
    const n = st.nMap[st.selectedNode];
    if (!n) return renderPanelInstructions();
    const cfg = KIND_CFG2[n.kind];
    let h = `<div class="fe-panel-title" style="color:${cfg.color}">${cfg.stereotype}</div>`;
    h += `<div class="fe-panel-field"><label>Name</label>`;
    if (readOnly) {
      h += `<div class="fe-panel-value">${esc(n.name)}</div></div>`;
    } else if (n.isCustom) {
      h += `<input type="text" class="fe-input" value="${escAttr(n.name)}" placeholder="Short type name\u2026" `;
      h += `onchange="window.__featureEditor.renameCustomType('${escAttr(n.id)}', this.value)" /></div>`;
    } else {
      h += `<div class="fe-panel-value">${esc(n.name)}</div></div>`;
    }
    h += `<div class="fe-panel-field"><label>Full Name</label>`;
    if (readOnly || !n.isCustom) {
      h += `<div class="fe-panel-value">${esc(n.id)}</div></div>`;
    } else {
      h += `<div class="fe-panel-value" style="opacity:0.9"><span style="color:var(--text-dim)">${esc("Custom.")}</span>${esc(n.name)}</div></div>`;
    }
    h += `<div class="fe-panel-field"><label>Alias</label>`;
    if (readOnly) {
      h += `<div class="fe-panel-value">${esc(n.alias || "\u2014")}</div>`;
    } else {
      h += `<input type="text" class="fe-input" value="${escAttr(n.alias || "")}" placeholder="Display name override\u2026" `;
      h += `onchange="window.__featureEditor.changeAlias('${escAttr(n.id)}', this.value)" /></div>`;
    }
    if (readOnly) h += "</div>";
    h += `<div class="fe-panel-field"><label>Description</label>`;
    if (readOnly) {
      h += `<div class="fe-panel-value">${esc(n.description || "\u2014")}</div></div>`;
    } else {
      h += `<textarea class="fe-input" rows="3" placeholder="Custom description\u2026" `;
      h += `onchange="window.__featureEditor.changeDescription('${escAttr(n.id)}', this.value)">${esc(n.description || "")}</textarea></div>`;
    }
    h += `<div class="fe-panel-field"><label>Bounded Context</label>`;
    h += readOnly ? `<div class="fe-panel-value">${esc(n.boundedContext || "\u2014")}</div>` : renderBoundedContextDropdown(n);
    h += "</div>";
    h += `<div class="fe-panel-field"><label>Layer</label>`;
    h += readOnly ? `<div class="fe-panel-value">${esc(n.layer || "\u2014")}</div>` : renderLayerDropdown(n);
    h += "</div>";
    h += '<div class="fe-panel-section">Properties</div>';
    if (n.structuredProps && n.structuredProps.length > 0) {
      for (let i = 0; i < n.structuredProps.length; i++) {
        const p = n.structuredProps[i];
        h += `<div class="fe-panel-prop-row">`;
        h += `<span class="fe-panel-prop-text">${esc(p.name)}: ${esc(p.type)}</span>`;
        if (!readOnly) {
          h += `<button class="fe-btn-icon" onclick="window.__featureEditor.removeProperty('${escAttr(n.id)}', ${i})" title="Remove property">\u2715</button>`;
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
      h += "</div>";
    }
    if (FE_METHOD_RULE_KINDS.has(n.kind)) {
      feEnsureMethodRuleStructures(n);
      h += '<div class="fe-panel-section">Methods</div>';
      if (n.structuredMethods.length > 0) {
        for (let i = 0; i < n.structuredMethods.length; i++) {
          const line = n.methods[i] || formatDiagramMethodLine(n.structuredMethods[i]);
          h += `<div class="fe-panel-prop-row">`;
          h += `<span class="fe-panel-prop-text">${esc(line)}</span>`;
          if (!readOnly) {
            h += `<button class="fe-btn-icon" onclick="window.__featureEditor.removeMethod('${escAttr(n.id)}', ${i})" title="Remove method">\u2715</button>`;
          }
          h += `</div>`;
        }
      } else {
        h += '<div class="fe-panel-prop-empty">No methods</div>';
      }
      if (!readOnly) {
        h += '<div class="fe-add-prop-row">';
        h += '<input type="text" class="fe-input" id="feNewMethodSig" placeholder="void DoSomething(int x) or DoSomething()" />';
        h += `<button class="fe-btn-icon fe-btn-add" onclick="window.__featureEditor.addMethod('${escAttr(n.id)}')" title="Add method">+</button>`;
        h += "</div>";
      }
      h += '<div class="fe-panel-section">Rules</div>';
      if (n.structuredRules.length > 0) {
        for (let i = 0; i < n.structuredRules.length; i++) {
          const r = n.structuredRules[i];
          const line = n.ruleLines[i] || formatDiagramRuleLine(r);
          h += `<div class="fe-panel-prop-row">`;
          h += `<span class="fe-panel-prop-text">${esc(line)}</span>`;
          if (!readOnly) {
            h += `<button class="fe-btn-icon" onclick="window.__featureEditor.removeRule('${escAttr(n.id)}', ${i})" title="Remove rule">\u2715</button>`;
          }
          h += `</div>`;
        }
      } else {
        h += '<div class="fe-panel-prop-empty">No rules</div>';
      }
      if (!readOnly) {
        h += '<div class="fe-add-prop-row">';
        h += '<input type="text" class="fe-input fe-input-sm" id="feNewRuleName" placeholder="name" />';
        h += '<input type="text" class="fe-input" id="feNewRuleText" placeholder="description / invariant" />';
        h += `<button class="fe-btn-icon fe-btn-add" onclick="window.__featureEditor.addRule('${escAttr(n.id)}')" title="Add rule">+</button>`;
        h += "</div>";
      }
    }
    const emittedEvents = getEmittedEventsForNode(n.id);
    if (emittedEvents.length > 0) {
      h += '<div class="fe-panel-section">Emitted Events</div>';
      for (const evName of emittedEvents) {
        h += `<div class="fe-panel-prop">\u26A1 ${esc(evName)}</div>`;
      }
    }
    const rels = st.edges.filter((e) => e.source === n.id || e.target === n.id);
    if (rels.length > 0) {
      h += '<div class="fe-panel-section">Relationships</div>';
      for (const e of rels) {
        const other = e.source === n.id ? shortName(e.target) : shortName(e.source);
        const dir = e.source === n.id ? "\u2192" : "\u2190";
        h += `<div class="fe-panel-rel">${dir} <span style="color:${EDGE_COLORS[e.kind] || "#888"}">${e.kind}</span> ${esc(other)}</div>`;
      }
    }
    h += '<div class="fe-panel-section">Actions</div>';
    if (!readOnly) {
      h += `<button class="fe-btn fe-connect-btn" onclick="window.__featureEditor.startConnect('${escAttr(n.id)}')" title="Drag to another node to create a relation">\u27F6 Draw Relation</button>`;
    }
    h += `<button class="fe-btn danger" onclick="window.__featureEditor.removeNode('${escAttr(n.id)}')" style="margin-top:4px">\u2715 Remove from Feature</button>`;
    return h;
  }
  if (st.selectedEdge !== null) {
    const e = st.edges[st.selectedEdge];
    if (!e) return renderPanelInstructions();
    let h = `<div class="fe-panel-title" style="color:${EDGE_COLORS[e.kind] || "#888"}">Relationship</div>`;
    h += `<div class="fe-panel-field"><label>Source</label><div class="fe-panel-value">${esc(shortName(e.source))}</div></div>`;
    h += `<div class="fe-panel-field"><label>Target</label><div class="fe-panel-value">${esc(shortName(e.target))}</div></div>`;
    h += `<div class="fe-panel-field"><label>Kind</label>`;
    h += readOnly ? `<div class="fe-panel-value">${esc(e.kind)}</div>` : renderRelKindDropdown(e.kind, st.selectedEdge);
    h += "</div>";
    if (e.label) h += `<div class="fe-panel-field"><label>Label</label><div class="fe-panel-value">${esc(e.label)}</div></div>`;
    if (!readOnly) {
      h += '<div class="fe-panel-section">Actions</div>';
      h += `<button class="fe-btn danger" onclick="window.__featureEditor.removeEdge(${st.selectedEdge})">\u2715 Remove Relation</button>`;
    }
    return h;
  }
  return renderPanelInstructions();
}
function renderPanelInstructions() {
  let h = '<div class="fe-panel-empty">';
  if (isReadOnlyFeature()) {
    h += "<p>Read-only mode: add existing types from the palette.</p>";
    h += "<p>Select a node to inspect details.</p>";
    h += "</div>";
    return h;
  }
  h += "<p>Click a node to inspect it.</p>";
  h += '<p>Click a node then <strong>"Draw Relation"</strong>, then click another node to connect them.</p>';
  h += "<p>Or drag from a node's <strong>connector port</strong> (\u2B24) to another node.</p>";
  h += "</div>";
  return h;
}
function refreshPanel() {
  const el = document.getElementById("fePanel");
  if (el) el.innerHTML = renderPropertiesPanel();
}
function getEmittedEventsForNode(nodeId) {
  if (!st) return [];
  return st.edges.filter((e) => {
    if (e.source !== nodeId || e.kind !== "Emits") return false;
    if (!isViewModeOnly()) return true;
    if (st.hiddenEdgeKinds.has("Emits")) return false;
    const tgt = st.nMap[e.target];
    if (!tgt || st.hiddenKinds.has(tgt.kind)) return false;
    return true;
  }).map((e) => {
    const tgt = st.nMap[e.target];
    return tgt ? tgt.name : shortName(e.target);
  });
}
function feDisplayName(n) {
  if (!getDiagramShowAliases()) return n.name;
  const meta = typeof window !== "undefined" && window.__metadata ? window.__metadata[n.id] : null;
  if (meta && meta.alias && String(meta.alias).trim()) return String(meta.alias).trim();
  if (n.alias && String(n.alias).trim()) return String(n.alias).trim();
  return n.name;
}
function addProperty(nodeId) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  const nameInput = document.getElementById("feNewPropName");
  const typeInput = document.getElementById("feNewPropType");
  if (!nameInput || !typeInput) return;
  const name = nameInput.value.trim();
  const type = typeInput.value.trim() || "string";
  if (!name) return;
  if (!n.structuredProps) n.structuredProps = [];
  n.structuredProps.push({ name, type });
  rebuildDisplayProps(n);
  nameInput.value = "";
  typeInput.value = "";
  markDirty();
  renderSvg2();
  refreshPanel();
}
function removeProperty(nodeId, idx) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !n.structuredProps) return;
  n.structuredProps.splice(idx, 1);
  rebuildDisplayProps(n);
  markDirty();
  renderSvg2();
  refreshPanel();
}
function addMethod(nodeId) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !FE_METHOD_RULE_KINDS.has(n.kind)) return;
  const input = document.getElementById("feNewMethodSig");
  if (!input) return;
  const sig = input.value.trim();
  if (!sig) return;
  feEnsureMethodRuleStructures(n);
  n.structuredMethods.push(feParseMethodSignatureInput(sig));
  feRebuildMethodDisplayLines(n);
  n.h = nodeHeight2(n);
  input.value = "";
  markDirty();
  renderSvg2();
  refreshPanel();
}
function removeMethod(nodeId, idx) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !FE_METHOD_RULE_KINDS.has(n.kind) || !n.structuredMethods) return;
  n.structuredMethods.splice(idx, 1);
  feRebuildMethodDisplayLines(n);
  n.h = nodeHeight2(n);
  markDirty();
  renderSvg2();
  refreshPanel();
}
function addRule(nodeId) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !FE_METHOD_RULE_KINDS.has(n.kind)) return;
  const nameInput = document.getElementById("feNewRuleName");
  const textInput = document.getElementById("feNewRuleText");
  if (!nameInput || !textInput) return;
  const ruleName = nameInput.value.trim() || "Rule";
  const ruleText = textInput.value.trim();
  if (!ruleText) return;
  feEnsureMethodRuleStructures(n);
  n.structuredRules.push({ name: ruleName, text: ruleText });
  feRebuildRuleDisplayLines(n);
  n.h = nodeHeight2(n);
  nameInput.value = "";
  textInput.value = "";
  markDirty();
  renderSvg2();
  refreshPanel();
}
function removeRule(nodeId, idx) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !FE_METHOD_RULE_KINDS.has(n.kind) || !n.structuredRules) return;
  n.structuredRules.splice(idx, 1);
  feRebuildRuleDisplayLines(n);
  n.h = nodeHeight2(n);
  markDirty();
  renderSvg2();
  refreshPanel();
}
function rebuildDisplayProps(n) {
  n.props = (n.structuredProps || []).map((p) => formatDiagramPropertyLine(p.name, p.type));
  if (FE_METHOD_RULE_KINDS.has(n.kind)) {
    feEnsureMethodRuleStructures(n);
    feRebuildMethodDisplayLines(n);
    feRebuildRuleDisplayLines(n);
  }
  n.h = nodeHeight2(n);
}
async function createFeature() {
  const input = document.getElementById("feNewFeatureName");
  const readOnlyInput = document.getElementById("feNewFeatureReadOnly");
  if (!input) return;
  const name = input.value.trim();
  if (!name) return;
  const readOnly = readOnlyInput?.checked === true;
  const feature = { readOnly, nodes: [], edges: [], positions: {} };
  try {
    const res = await fetch(`${baseUrl}/features/${encodeURIComponent(name)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(feature)
    });
    if (!res.ok) {
      console.error("Failed to create feature");
      return;
    }
  } catch (e) {
    console.error("Failed to create feature", e);
    return;
  }
  await loadFeatureList();
  currentFeatureName = name;
  currentFeatureReadOnly = readOnly;
  loadFeatureState(feature);
  dirty = false;
  rerender();
}
async function loadFeature(name) {
  if (dirty && !confirm("You have unsaved changes. Discard them?")) return;
  try {
    const res = await fetch(`${baseUrl}/features/${encodeURIComponent(name)}`);
    if (!res.ok) {
      console.error("Feature not found");
      return;
    }
    const feature = await res.json();
    currentFeatureName = name;
    currentFeatureReadOnly = feature?.readOnly === true;
    loadFeatureState(feature);
    dirty = false;
    rerender();
  } catch (e) {
    console.error("Failed to load feature", e);
  }
}
async function saveFeature() {
  if (!st || !currentFeatureName) return;
  const feature = serializeFeature();
  try {
    const res = await fetch(`${baseUrl}/features/${encodeURIComponent(currentFeatureName)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(feature)
    });
    if (res.ok) {
      dirty = false;
      const indicator = document.getElementById("feDirtyIndicator");
      if (indicator) indicator.style.display = "none";
    }
  } catch (e) {
    console.error("Failed to save feature", e);
  }
}
async function deleteFeature() {
  if (!currentFeatureName) return;
  if (!confirm(`Delete feature "${currentFeatureName}"?`)) return;
  try {
    await fetch(`${baseUrl}/features/${encodeURIComponent(currentFeatureName)}`, { method: "DELETE" });
  } catch {
  }
  currentFeatureName = null;
  currentFeatureReadOnly = false;
  st = null;
  dirty = false;
  await loadFeatureList();
  rerender();
}
async function downloadExport(exportName) {
  if (!currentFeatureName) return;
  try {
    let url = `${baseUrl}/features/${encodeURIComponent(currentFeatureName)}/exports/${encodeURIComponent(exportName)}`;
    const regCmd = document.getElementById("feRegisterCommands");
    if (regCmd && regCmd.checked) {
      url += (url.includes("?") ? "&" : "?") + "registerCommands=true";
    }
    const res = await fetch(url);
    if (!res.ok) {
      alert("Export failed: " + res.statusText);
      return;
    }
    const blob = await res.blob();
    const disposition = res.headers.get("content-disposition") || "";
    const match = disposition.match(/filename="?([^"]+)"?/);
    const fileName = match ? match[1] : `${currentFeatureName}-${exportName}`;
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(a.href);
  } catch (e) {
    console.error("Export download failed", e);
  }
}
function rerender() {
  const main = document.getElementById("mainContent");
  if (!main) return;
  main.innerHTML = renderFeatureEditorView();
  requestAnimationFrame(() => {
    mountFeatureEditor();
    if (typeof window.__syncFeatureEditorViewBodyClass === "function") {
      window.__syncFeatureEditorViewBodyClass();
    }
  });
}
function serializeFeature() {
  if (!st) return { readOnly: currentFeatureReadOnly, nodes: [], edges: [], positions: {} };
  const positions = {};
  for (const n of st.nodes) {
    positions[n.id] = { x: Math.round(n.x * 10) / 10, y: Math.round(n.y * 10) / 10 };
  }
  return {
    readOnly: currentFeatureReadOnly,
    nodes: st.nodes.map((n) => ({
      id: n.id,
      name: n.name,
      kind: n.kind,
      isCustom: n.isCustom || false,
      alias: n.alias || null,
      description: n.description || null,
      boundedContext: n.boundedContext || "",
      layer: n.layer || "",
      props: n.props,
      structuredProps: n.structuredProps || [],
      methods: FE_METHOD_RULE_KINDS.has(n.kind) ? n.structuredMethods || [] : n.methods,
      rules: FE_METHOD_RULE_KINDS.has(n.kind) ? n.structuredRules || [] : void 0,
      events: n.events
    })),
    edges: st.edges.map((e) => ({
      source: e.source,
      target: e.target,
      kind: e.kind,
      label: e.label || ""
    })),
    positions
  };
}
function loadFeatureState(feature) {
  currentFeatureReadOnly = feature?.readOnly === true;
  st = {
    nodes: [],
    edges: [],
    nMap: {},
    zoom: 1,
    panX: 0,
    panY: 0,
    selectedNode: null,
    selectedEdge: null,
    hiddenKinds: loadDiagramHiddenKindsSet(),
    hiddenEdgeKinds: loadDiagramHiddenEdgeKindsSet()
  };
  for (const saved of feature.nodes || []) {
    const cfg = KIND_CFG2[saved.kind];
    if (!cfg) continue;
    const n = {
      id: saved.id,
      name: saved.name,
      kind: saved.kind,
      isCustom: saved.isCustom || false,
      alias: saved.alias || null,
      description: saved.description || null,
      boundedContext: saved.boundedContext || "",
      layer: saved.layer || "",
      cfg,
      structuredProps: saved.structuredProps || [],
      props: saved.props || [],
      methods: [],
      structuredMethods: [],
      structuredRules: [],
      ruleLines: [],
      events: saved.events || [],
      x: 0,
      y: 0,
      vx: 0,
      vy: 0,
      w: NODE_W2,
      h: 0
    };
    if (n.structuredProps.length > 0 && n.props.length === 0) {
      n.props = n.structuredProps.map((p) => formatDiagramPropertyLine(p.name, p.type));
    }
    if (FE_METHOD_RULE_KINDS.has(n.kind)) {
      const rawMethods = saved.structuredMethods || saved.methods || [];
      n.structuredMethods = feDomainMethodsToStructured(rawMethods);
      feRebuildMethodDisplayLines(n);
      const rawRules = saved.rules || [];
      n.structuredRules = rawRules.map((r) => {
        if (r && typeof r === "object") {
          return { name: r.name && String(r.name).trim() ? String(r.name).trim() : "Rule", text: r.text != null ? String(r.text) : "" };
        }
        return { name: "Rule", text: String(r || "") };
      }).filter((r) => r.name || r.text);
      feRebuildRuleDisplayLines(n);
    } else {
      n.methods = Array.isArray(saved.methods) ? saved.methods : [];
    }
    n.h = nodeHeight2(n);
    const pos = feature.positions?.[n.id];
    if (pos && typeof pos.x === "number" && typeof pos.y === "number") {
      n.x = pos.x;
      n.y = pos.y;
    }
    st.nodes.push(n);
    st.nMap[n.id] = n;
  }
  for (const saved of feature.edges || []) {
    if (st.nMap[saved.source] && st.nMap[saved.target]) {
      st.edges.push({ source: saved.source, target: saved.target, kind: saved.kind, label: saved.label || "" });
    }
  }
  const fixedFromSaved = /* @__PURE__ */ new Set();
  for (const n of st.nodes) {
    const pos = feature.positions?.[n.id];
    if (pos && typeof pos.x === "number" && typeof pos.y === "number") {
      fixedFromSaved.add(n.id);
    }
  }
  const needsLayout = st.nodes.filter((n) => !fixedFromSaved.has(n.id));
  if (needsLayout.length === st.nodes.length) {
    applyAutoLayout2(st.nodes, st.edges, st.nMap);
  } else if (needsLayout.length > 0) {
    applyAutoLayout2(st.nodes, st.edges, st.nMap, fixedFromSaved);
  }
}
function getGlobalTypeMetadata(fullName) {
  const meta = typeof window !== "undefined" && window.__metadata ? window.__metadata[fullName] : null;
  if (!meta) return { alias: null, description: null };
  const alias = meta.alias && String(meta.alias).trim() ? String(meta.alias).trim() : null;
  const description = meta.description && String(meta.description).trim() ? String(meta.description).trim() : null;
  return { alias, description };
}
function buildFeatureNodeFromDomain(fullName, kind) {
  if (!st || st.nMap[fullName]) return null;
  const cfg = KIND_CFG2[kind];
  if (!cfg) return null;
  const item = findDomainItem(fullName, kind);
  const globalMeta = getGlobalTypeMetadata(fullName);
  const n = {
    id: fullName,
    name: item ? item.name : shortName(fullName),
    kind,
    isCustom: item?.isCustom === true,
    alias: globalMeta.alias,
    description: globalMeta.description || item && item.description || null,
    boundedContext: findDomainContext(fullName) || "",
    layer: item && item.layer || "",
    cfg,
    structuredProps: item ? (item.properties || []).map((p) => ({ name: p.name, type: p.typeName })) : [],
    props: item ? (item.properties || []).map((p) => formatDiagramPropertyLine(p.name, p.typeName)) : [],
    structuredMethods: [],
    structuredRules: [],
    methods: [],
    ruleLines: [],
    events: [],
    x: 0,
    y: 0,
    vx: 0,
    vy: 0,
    w: NODE_W2,
    h: 0
  };
  if (FE_METHOD_RULE_KINDS.has(kind)) {
    n.structuredMethods = feDomainMethodsToStructured(item ? item.methods || [] : []);
    feRebuildMethodDisplayLines(n);
    const dr = item && item.rules ? item.rules : [];
    n.structuredRules = dr.map((r) => {
      if (r && typeof r === "object" && "name" in r) {
        return { name: r.name && String(r.name).trim() ? String(r.name).trim() : "Rule", text: r.text != null ? String(r.text) : "" };
      }
      return { name: "Rule", text: String(r || "") };
    }).filter((r) => r.name || r.text);
    feRebuildRuleDisplayLines(n);
  } else {
    n.methods = item ? (item.methods || []).map((m) => formatDiagramMethodLine(m)) : [];
  }
  n.h = nodeHeight2(n);
  return n;
}
function addExistingType(fullName, kind) {
  if (!st) return;
  const n = buildFeatureNodeFromDomain(fullName, kind);
  if (!n) return;
  placeNewNode(n);
  feRememberLastContext(n.boundedContext, n.layer);
  st.nodes.push(n);
  st.nMap[n.id] = n;
  importAllRelationshipsFromDomain();
  markDirty();
  renderSvg2();
  if (isViewModeOnly()) refreshFeViewFilters();
  refreshPalette();
  refreshPanel();
}
function placeNewNodeAtBulkIndex(n, index) {
  if (!st) return;
  const canvas = document.getElementById("feCanvas");
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
function addAllFromBoundedContext() {
  if (!st || !domainData) return;
  const sel = document.getElementById("feBulkBcSelect");
  const ctxName = sel && sel.value ? String(sel.value).trim() : "";
  if (!ctxName) return;
  const ctx = (domainData.boundedContexts || []).find((c) => c.name === ctxName);
  if (!ctx) return;
  let bulkIndex = 0;
  let added = 0;
  for (const sec of ALL_SECTIONS) {
    const kind = SECTION_TO_KIND[sec];
    if (!kind) continue;
    for (const item of ctx[sec] || []) {
      const n = buildFeatureNodeFromDomain(item.fullName, kind);
      if (!n) continue;
      placeNewNodeAtBulkIndex(n, bulkIndex++);
      feRememberLastContext(ctxName, n.layer);
      st.nodes.push(n);
      st.nMap[n.id] = n;
      added++;
    }
  }
  if (added === 0) return;
  importAllRelationshipsFromDomain();
  recalcNodeHeights();
  markDirty();
  renderSvg2();
  if (isViewModeOnly()) refreshFeViewFilters();
  refreshPalette();
  refreshPanel();
  fitToView2();
}
function addNewType() {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const nameInput = document.getElementById("feNewTypeName");
  const kindSelect = document.getElementById("feNewTypeKind");
  if (!nameInput || !kindSelect) return;
  const name = nameInput.value.trim();
  const kind = kindSelect.value;
  if (!name) return;
  const fullName = "Custom." + name;
  if (st.nMap[fullName]) {
    alert("A type with that name already exists in this feature.");
    return;
  }
  const cfg = KIND_CFG2[kind];
  let lastBc = "";
  let lastLayer = "";
  if (st.selectedNode) {
    const sel = st.nMap[st.selectedNode];
    if (sel) {
      lastBc = sel.boundedContext || "";
      lastLayer = sel.layer || "";
    }
  }
  if (!lastBc) lastBc = feLoadLastBoundedContext();
  if (!lastLayer) lastLayer = feLoadLastLayer();
  const n = {
    id: fullName,
    name,
    kind,
    isCustom: true,
    alias: null,
    description: null,
    boundedContext: lastBc,
    layer: lastLayer,
    cfg,
    structuredProps: [],
    props: [],
    structuredMethods: [],
    structuredRules: [],
    methods: [],
    ruleLines: [],
    events: [],
    x: 0,
    y: 0,
    vx: 0,
    vy: 0,
    w: NODE_W2,
    h: 0
  };
  if (FE_METHOD_RULE_KINDS.has(kind)) {
    feRebuildMethodDisplayLines(n);
    feRebuildRuleDisplayLines(n);
  }
  n.h = nodeHeight2(n);
  placeNewNode(n);
  feRememberLastContext(n.boundedContext, n.layer);
  st.nodes.push(n);
  st.nMap[n.id] = n;
  nameInput.value = "";
  markDirty();
  renderSvg2();
  if (isViewModeOnly()) refreshFeViewFilters();
  refreshPanel();
}
function placeNewNode(n) {
  if (!st) return;
  const canvas = document.getElementById("feCanvas");
  if (canvas) {
    const cx = (canvas.clientWidth / 2 - st.panX) / st.zoom;
    const cy = (canvas.clientHeight / 2 - st.panY) / st.zoom;
    n.x = cx - n.w / 2 + (Math.random() - 0.5) * 100;
    n.y = cy - n.h / 2 + (Math.random() - 0.5) * 100;
  }
}
function removeNode(id) {
  if (!st) return;
  st.nodes = st.nodes.filter((n) => n.id !== id);
  delete st.nMap[id];
  st.edges = st.edges.filter((e) => e.source !== id && e.target !== id);
  if (st.selectedNode === id) st.selectedNode = null;
  markDirty();
  renderSvg2();
  if (isViewModeOnly()) refreshFeViewFilters();
  refreshPalette();
  refreshPanel();
}
function startConnect(sourceId) {
  if (isReadOnlyFeature()) return;
  connecting = { sourceId, mouseX: 0, mouseY: 0 };
  const canvas = document.getElementById("feCanvas");
  if (canvas) canvas.style.cursor = "crosshair";
}
function finishConnect(targetId) {
  if (isReadOnlyFeature()) return;
  if (!connecting || !st) return;
  const { sourceId } = connecting;
  connecting = null;
  const canvas = document.getElementById("feCanvas");
  if (canvas) canvas.style.cursor = "";
  if (sourceId === targetId) return;
  if (st.edges.some((e) => e.source === sourceId && e.target === targetId)) return;
  showRelationKindPicker((kind) => {
    st.edges.push({ source: sourceId, target: targetId, kind, label: "" });
    recalcNodeHeights();
    markDirty();
    renderSvg2();
    if (isViewModeOnly()) refreshFeViewFilters();
    refreshPanel();
  });
}
function renderRelKindDropdown(currentKind, edgeIdx) {
  const DASHED = /* @__PURE__ */ new Set(["Emits", "Handles", "Publishes", "References", "ReferencesById"]);
  let h = `<div class="rel-dropdown" id="relKindDropdown">`;
  h += `<button class="rel-dropdown-trigger" onclick="window.__featureEditor.toggleRelDropdown()" type="button">`;
  h += `<span class="rel-line-sample${DASHED.has(currentKind) ? " dashed" : ""}" style="color:${EDGE_COLORS[currentKind] || "#888"};width:16px;height:0;border-top:2px solid currentColor;${DASHED.has(currentKind) ? "border-top-style:dashed;" : ""}"></span>`;
  h += `<span>${esc(currentKind)}</span>`;
  h += '<span class="rel-chevron">\u25BE</span>';
  h += "</button>";
  h += '<div class="rel-dropdown-menu single-select" id="relKindMenu">';
  for (const k of RELATION_KINDS) {
    const color = EDGE_COLORS[k] || "#888";
    const dashed = DASHED.has(k);
    const sel = k === currentKind ? " selected" : "";
    h += `<div class="rel-dropdown-item${sel}" onclick="window.__featureEditor.changeEdgeKind(${edgeIdx}, '${k}')">`;
    h += `<span class="rel-line-sample${dashed ? " dashed" : ""}" style="color:${color}"></span>`;
    h += `<span class="rel-kind-label">${esc(k)}</span>`;
    if (k === currentKind) h += '<span style="color:var(--accent);font-size:11px">\u2713</span>';
    h += "</div>";
  }
  h += "</div></div>";
  return h;
}
function showRelationKindPicker(callback) {
  const DASHED = /* @__PURE__ */ new Set(["Emits", "Handles", "Publishes", "References", "ReferencesById"]);
  const overlay = document.createElement("div");
  overlay.className = "rel-picker-overlay";
  let h = '<div class="rel-picker-card">';
  h += '<div class="rel-picker-title">Select Relationship Kind</div>';
  for (const k of RELATION_KINDS) {
    const color = EDGE_COLORS[k] || "#888";
    const dashed = DASHED.has(k);
    h += `<div class="rel-picker-item" data-kind="${escAttr(k)}">`;
    h += `<span class="rel-line-sample${dashed ? " dashed" : ""}" style="color:${color}"></span>`;
    h += `<span>${esc(k)}</span>`;
    h += "</div>";
  }
  h += '<button class="rel-picker-cancel">Cancel</button>';
  h += "</div>";
  overlay.innerHTML = h;
  document.body.appendChild(overlay);
  function cleanup() {
    overlay.remove();
  }
  overlay.querySelector(".rel-picker-cancel").addEventListener("click", cleanup);
  overlay.addEventListener("click", (ev) => {
    if (ev.target === overlay) cleanup();
  });
  overlay.querySelectorAll(".rel-picker-item").forEach((item) => {
    item.addEventListener("click", () => {
      const kind = item.dataset.kind;
      cleanup();
      callback(kind);
    });
  });
}
function toggleRelDropdown() {
  const menu = document.getElementById("relKindMenu");
  const trigger = menu?.previousElementSibling;
  if (!menu) return;
  const open = menu.classList.toggle("visible");
  if (trigger) trigger.classList.toggle("open", open);
  if (open) {
    const close = (ev) => {
      if (!menu.contains(ev.target) && ev.target !== trigger && !trigger.contains(ev.target)) {
        menu.classList.remove("visible");
        trigger.classList.remove("open");
        document.removeEventListener("click", close);
      }
    };
    setTimeout(() => document.addEventListener("click", close), 0);
  }
}
function renderBoundedContextDropdown(node) {
  const ctxNames = getBoundedContextNames();
  const current = node.boundedContext || "";
  let h = `<div class="rel-dropdown" id="bcDropdown">`;
  h += `<button class="rel-dropdown-trigger" onclick="window.__featureEditor.toggleBcDropdown()" type="button">`;
  h += `<span class="fe-bc-dot" style="background:var(--accent);width:7px;height:7px;border-radius:50%"></span>`;
  h += `<span>${current ? esc(current) : '<span style="color:var(--text-dim)">None</span>'}</span>`;
  h += '<span class="rel-chevron">\u25BE</span>';
  h += "</button>";
  h += '<div class="rel-dropdown-menu single-select" id="bcDropdownMenu">';
  h += `<div class="rel-dropdown-item${!current ? " selected" : ""}" onclick="window.__featureEditor.changeBoundedContext('${escAttr(node.id)}', '')">`;
  h += `<span class="rel-kind-label" style="color:var(--text-dim);font-style:italic">None</span>`;
  if (!current) h += '<span style="color:var(--accent);font-size:11px">\u2713</span>';
  h += "</div>";
  for (const name of ctxNames) {
    const sel = name === current;
    h += `<div class="rel-dropdown-item${sel ? " selected" : ""}" onclick="window.__featureEditor.changeBoundedContext('${escAttr(node.id)}', '${escAttr(name)}')">`;
    h += `<span class="fe-bc-dot" style="background:var(--accent);width:7px;height:7px;border-radius:50%;flex-shrink:0"></span>`;
    h += `<span class="rel-kind-label">${esc(name)}</span>`;
    if (sel) h += '<span style="color:var(--accent);font-size:11px">\u2713</span>';
    h += "</div>";
  }
  h += "</div></div>";
  return h;
}
function renameCustomType(nodeId, newShortName) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n || !n.isCustom) return;
  const short = String(newShortName ?? "").trim();
  if (!short) {
    alert("Name cannot be empty.");
    refreshPanel();
    return;
  }
  const newId = `Custom.${short}`;
  if (newId === nodeId) return;
  if (st.nMap[newId]) {
    alert("A type with that name already exists in this feature.");
    refreshPanel();
    return;
  }
  delete st.nMap[nodeId];
  n.id = newId;
  n.name = short;
  st.nMap[newId] = n;
  for (const e of st.edges) {
    if (e.source === nodeId) e.source = newId;
    if (e.target === nodeId) e.target = newId;
  }
  if (connecting && connecting.sourceId === nodeId) connecting.sourceId = newId;
  if (st.selectedNode === nodeId) st.selectedNode = newId;
  markDirty();
  renderSvg2();
  refreshPalette();
  refreshPanel();
}
function changeAlias(nodeId, value) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.alias = value && value.trim() ? value.trim() : null;
  markDirty();
  renderSvg2();
  refreshPanel();
}
function changeDescription(nodeId, value) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.description = value && value.trim() ? value.trim() : null;
  markDirty();
  refreshPanel();
}
function changeBoundedContext(nodeId, ctxName) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.boundedContext = ctxName;
  if (st.selectedNode === nodeId) feRememberLastContext(n.boundedContext, n.layer);
  markDirty();
  refreshPanel();
}
function toggleBcDropdown() {
  toggleDropdownById("bcDropdownMenu", "bcDropdown");
}
function renderLayerDropdown(node) {
  const current = node.layer || "";
  const currentColor = LAYER_COLORS2[current] || "var(--text-dim)";
  let h = `<div class="rel-dropdown" id="layerDropdown">`;
  h += `<button class="rel-dropdown-trigger" onclick="window.__featureEditor.toggleLayerDropdown()" type="button">`;
  h += `<span style="width:14px;height:3px;border-radius:1px;background:${currentColor};flex-shrink:0"></span>`;
  h += `<span>${current ? esc(current) : '<span style="color:var(--text-dim)">None</span>'}</span>`;
  h += '<span class="rel-chevron">\u25BE</span>';
  h += "</button>";
  h += '<div class="rel-dropdown-menu single-select" id="layerDropdownMenu">';
  h += `<div class="rel-dropdown-item${!current ? " selected" : ""}" onclick="window.__featureEditor.changeLayer('${escAttr(node.id)}', '')">`;
  h += `<span class="rel-kind-label" style="color:var(--text-dim);font-style:italic">None</span>`;
  if (!current) h += '<span style="color:var(--accent);font-size:11px">\u2713</span>';
  h += "</div>";
  for (const layer of LAYERS) {
    const sel = layer === current;
    const color = LAYER_COLORS2[layer];
    h += `<div class="rel-dropdown-item${sel ? " selected" : ""}" onclick="window.__featureEditor.changeLayer('${escAttr(node.id)}', '${escAttr(layer)}')">`;
    h += `<span style="width:14px;height:3px;border-radius:1px;background:${color};flex-shrink:0"></span>`;
    h += `<span class="rel-kind-label">${esc(layer)}</span>`;
    if (sel) h += '<span style="color:var(--accent);font-size:11px">\u2713</span>';
    h += "</div>";
  }
  h += "</div></div>";
  return h;
}
function changeLayer(nodeId, layer) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  const n = st.nMap[nodeId];
  if (!n) return;
  n.layer = layer;
  if (st.selectedNode === nodeId) feRememberLastContext(n.boundedContext, n.layer);
  markDirty();
  refreshPanel();
}
function toggleLayerDropdown() {
  toggleDropdownById("layerDropdownMenu", "layerDropdown");
}
function toggleDropdownById(menuId, wrapperId) {
  const menu = document.getElementById(menuId);
  const wrapper = document.getElementById(wrapperId);
  const trigger = wrapper?.querySelector(".rel-dropdown-trigger");
  if (!menu) return;
  const open = menu.classList.toggle("visible");
  if (trigger) trigger.classList.toggle("open", open);
  if (open) {
    const close = (ev) => {
      if (!menu.contains(ev.target) && ev.target !== trigger && !trigger.contains(ev.target)) {
        menu.classList.remove("visible");
        if (trigger) trigger.classList.remove("open");
        document.removeEventListener("click", close);
      }
    };
    setTimeout(() => document.addEventListener("click", close), 0);
  }
}
function removeEdge(idx) {
  if (isReadOnlyFeature()) return;
  if (!st) return;
  st.edges.splice(idx, 1);
  st.selectedEdge = null;
  recalcNodeHeights();
  markDirty();
  renderSvg2();
  if (isViewModeOnly()) refreshFeViewFilters();
  refreshPanel();
}
function changeEdgeKind(idx, kind) {
  if (isReadOnlyFeature()) return;
  if (!st || !st.edges[idx]) return;
  st.edges[idx].kind = kind;
  recalcNodeHeights();
  markDirty();
  renderSvg2();
  if (isViewModeOnly()) refreshFeViewFilters();
}
function recalcNodeHeights() {
  if (!st) return;
  for (const n of st.nodes) {
    n.h = nodeHeight2(n);
  }
}
function importAllRelationshipsFromDomain() {
  if (!st || !domainData) return;
  for (const c of domainData.boundedContexts || []) {
    for (const rel of c.relationships || []) {
      const hasSource = st.nMap[rel.sourceType];
      const hasTarget = st.nMap[rel.targetType];
      if (!hasSource || !hasTarget) continue;
      if (st.edges.some((e) => e.source === rel.sourceType && e.target === rel.targetType && e.kind === rel.kind)) continue;
      st.edges.push({ source: rel.sourceType, target: rel.targetType, kind: rel.kind, label: rel.label || "" });
    }
  }
}
function findDomainItem(fullName, kind) {
  if (!domainData) return null;
  const secKey = KIND_TO_SECTION[kind];
  for (const ctx of domainData.boundedContexts || []) {
    const items = ctx[secKey] || [];
    const found = items.find((i) => i.fullName === fullName);
    if (found) return found;
  }
  return null;
}
function findDomainContext(fullName) {
  if (!domainData) return null;
  for (const ctx of domainData.boundedContexts || []) {
    for (const sec of ALL_SECTIONS) {
      if ((ctx[sec] || []).some((i) => i.fullName === fullName)) return ctx.name;
    }
  }
  return null;
}
function getBoundedContextNames() {
  if (!domainData) return [];
  return (domainData.boundedContexts || []).map((c) => c.name);
}
function markDirty() {
  dirty = true;
  const indicator = document.getElementById("feDirtyIndicator");
  if (indicator) indicator.style.display = "inline";
}
function filterPalette() {
  const input = document.getElementById("fePaletteSearch");
  const container = document.getElementById("fePalette");
  if (input && container) container.innerHTML = renderPaletteItems(input.value);
}
function refreshPalette() {
  const input = document.getElementById("fePaletteSearch");
  const container = document.getElementById("fePalette");
  if (container) container.innerHTML = renderPaletteItems(input?.value || "");
}
function applyAutoLayout2(nodes, edges, nMap, fixedNodeIds) {
  const fixed = fixedNodeIds instanceof Set && fixedNodeIds.size > 0 ? fixedNodeIds : null;
  const isFixed = (n) => fixed && fixed.has(n.id);
  const kindRow = {
    aggregate: 0,
    entity: 1,
    valueObject: 1,
    subType: 1,
    event: 2,
    integrationEvent: 2,
    eventHandler: 3,
    commandHandlerTarget: 2,
    commandHandler: 3,
    queryHandler: 3,
    repository: 4,
    service: 4
  };
  const rowBuckets = {};
  for (const n of nodes) {
    if (isFixed(n)) continue;
    const r = kindRow[n.kind] || 0;
    (rowBuckets[r] = rowBuckets[r] || []).push(n);
  }
  for (const [row, rNodes] of Object.entries(rowBuckets)) {
    const y = parseInt(row) * 240;
    rNodes.forEach((n, i) => {
      n.x = (i - (rNodes.length - 1) / 2) * 270;
      n.y = y;
    });
  }
  for (let i = 0; i < 150; i++) {
    const alpha = 1 - i / 150;
    for (let a = 0; a < nodes.length; a++) {
      for (let b = a + 1; b < nodes.length; b++) {
        const na = nodes[a], nb = nodes[b];
        const fa = isFixed(na), fb = isFixed(nb);
        if (fa && fb) continue;
        let dx = nb.x - na.x, dy = nb.y - na.y;
        const dist = Math.sqrt(dx * dx + dy * dy) || 1;
        const force = 8e3 * alpha / (dist * dist);
        const fx = dx / dist * force, fy = dy / dist * force;
        if (!fa) {
          na.vx -= fx;
          na.vy -= fy;
        }
        if (!fb) {
          nb.vx += fx;
          nb.vy += fy;
        }
      }
    }
    for (const e of edges) {
      const s = nMap[e.source], t = nMap[e.target];
      if (!s || !t) continue;
      const fs = isFixed(s), ft = isFixed(t);
      if (fs && ft) continue;
      const dx = t.x - s.x, dy = t.y - s.y;
      const dist = Math.sqrt(dx * dx + dy * dy) || 1;
      const force = dist * 4e-3 * alpha;
      const fx = dx / dist * force, fy = dy / dist * force;
      if (!fs) {
        s.vx += fx;
        s.vy += fy;
      }
      if (!ft) {
        t.vx -= fx;
        t.vy -= fy;
      }
    }
    for (const n of nodes) {
      if (isFixed(n)) {
        n.vx = 0;
        n.vy = 0;
        continue;
      }
      n.vx *= 0.82;
      n.vy *= 0.82;
      n.x += n.vx;
      n.y += n.vy;
    }
  }
  for (const n of nodes) {
    n.vx = 0;
    n.vy = 0;
  }
}
function nodeHeight2(n) {
  const derivedEvents = getEmittedEventsForNode(n.id);
  const ruleLines = n.ruleLines && n.ruleLines.length ? n.ruleLines : [];
  let h = PAD2 + HEADER_H2 + nodeNameHeight2(n);
  if (n.props.length > 0) h += DIVIDER_H2 + n.props.length * PROP_H2;
  if (n.methods.length > 0) h += DIVIDER_H2 + n.methods.length * PROP_H2;
  if (ruleLines.length > 0) h += DIVIDER_H2 + ruleLines.length * PROP_H2;
  if (derivedEvents.length > 0) h += DIVIDER_H2 + derivedEvents.length * PROP_H2;
  return h + PAD2;
}
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
    const color = BC_COLORS2[i % BC_COLORS2.length];
    bounds.push({ name, x: minX - pad, y: minY - pad - 32, w: maxX - minX + pad * 2, h: maxY - minY + pad * 2 + 32, color });
  }
  return bounds;
}
function computeFeatureLayerBounds(nodes) {
  const groups = {};
  for (const n of nodes) {
    if (!n.layer) continue;
    const key = (n.boundedContext || "__default") + "\0" + n.layer;
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
    const color = LAYER_COLORS2[g.layer] || "#888";
    bounds.push({ name: g.layer, x: minX - pad, y: minY - pad - 24, w: maxX - minX + pad * 2, h: maxY - minY + pad * 2 + 24, color });
  }
  return bounds;
}
function renderSvg2() {
  const svg = document.getElementById("feSvg");
  if (!svg || !st) return;
  const vm = isViewModeOnly();
  const visibleNodeIds = /* @__PURE__ */ new Set();
  if (vm) {
    for (const n of st.nodes) {
      if (!st.hiddenKinds.has(n.kind)) visibleNodeIds.add(n.id);
    }
  } else {
    for (const n of st.nodes) visibleNodeIds.add(n.id);
  }
  let s = "<defs>";
  for (const [kind, color] of Object.entries(EDGE_COLORS)) {
    s += `<marker id="fe-arrow-${kind}" viewBox="0 0 10 6" refX="10" refY="3" markerWidth="8" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,3 L0,6 Z" fill="${color}" /></marker>`;
  }
  s += '<marker id="fe-diamond" viewBox="0 0 12 8" refX="0" refY="4" markerWidth="10" markerHeight="8" orient="auto-start-reverse"><path d="M0,4 L6,0 L12,4 L6,8 Z" fill="#60a5fa" /></marker>';
  const feNodesForClip = vm ? st.nodes.filter((n) => visibleNodeIds.has(n.id)) : st.nodes;
  feNodesForClip.forEach((n, ni) => {
    s += `<clipPath id="fe-node-clip-${ni}"><rect x="0" y="0" width="${n.w}" height="${n.h}" rx="8" /></clipPath>`;
  });
  s += "</defs>";
  s += `<g id="feViewport" transform="translate(${st.panX},${st.panY}) scale(${st.zoom})">`;
  const ctxNodes = vm ? st.nodes.filter((n) => visibleNodeIds.has(n.id)) : st.nodes;
  const ctxBounds = computeFeatureContextBounds(ctxNodes);
  const ctxDragCursor = vm ? "default" : "move";
  for (const b of ctxBounds) {
    s += `<g class="dg-ctx-boundary" data-ctx="${escAttr(b.name)}" style="cursor:${ctxDragCursor}">`;
    s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="12" fill="rgba(255,255,255,.10)" stroke="${b.color}" stroke-width="1.5" stroke-dasharray="8,5" opacity="0.8" />`;
    s += `<text x="${b.x + 14}" y="${b.y + 24}" fill="${b.color}" font-size="20" font-weight="700" font-family="-apple-system,sans-serif" opacity="0.85">${esc(b.name)}</text>`;
    s += "</g>";
  }
  let layerBounds = [];
  if (!vm) {
    layerBounds = computeFeatureLayerBounds(st.nodes);
  } else if (getDiagramShowLayers()) {
    layerBounds = computeFeatureLayerBounds(st.nodes.filter((n) => visibleNodeIds.has(n.id)));
  }
  for (const b of layerBounds) {
    s += `<g class="dg-layer-boundary">`;
    s += `<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="8" fill="none" stroke="${b.color}" stroke-width="1" stroke-dasharray="4,3" opacity="0.6" />`;
    s += `<text x="${b.x + 10}" y="${b.y + 18}" fill="${b.color}" font-size="13" font-weight="600" font-family="-apple-system,sans-serif" font-style="italic" opacity="0.7">${esc(b.name)}</text>`;
    s += "</g>";
  }
  for (let ei = 0; ei < st.edges.length; ei++) {
    const e = st.edges[ei];
    const src = st.nMap[e.source], tgt = st.nMap[e.target];
    if (!src || !tgt) continue;
    if (vm) {
      if (st.hiddenEdgeKinds.has(e.kind)) continue;
      if (!visibleNodeIds.has(src.id) || !visibleNodeIds.has(tgt.id)) continue;
    }
    const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
    const tgtCx = tgt.x + tgt.w / 2, tgtCy = tgt.y + tgt.h / 2;
    const p1 = rectEdge2(srcCx, srcCy, src.w + 8, src.h + 8, tgtCx, tgtCy);
    const p2 = rectEdge2(tgtCx, tgtCy, tgt.w + 8, tgt.h + 8, srcCx, srcCy);
    const color = EDGE_COLORS[e.kind] || "#5c6070";
    const dashed = e.kind === "Emits" || e.kind === "Handles" || e.kind === "Publishes" || e.kind === "References" || e.kind === "ReferencesById" ? ' stroke-dasharray="6,4"' : "";
    const markerStart = e.kind === "Contains" || e.kind === "Has" || e.kind === "HasMany" ? ' marker-start="url(#fe-diamond)"' : "";
    const markerEnd = e.kind === "References" || e.kind === "ReferencesById" ? "" : ` marker-end="url(#fe-arrow-${e.kind})"`;
    const selected = !isViewModeOnly() && st.selectedEdge === ei;
    const sw = selected ? 3 : 1.5;
    const op = selected ? 1 : isViewModeOnly() ? 0.88 : 0.65;
    if (!isViewModeOnly()) {
      s += `<line class="fe-edge" data-idx="${ei}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="transparent" stroke-width="12" style="cursor:pointer" />`;
    }
    s += `<line class="fe-edge-vis" data-idx="${ei}" x1="${p1.x}" y1="${p1.y}" x2="${p2.x}" y2="${p2.y}" stroke="${color}" stroke-width="${sw}"${dashed}${markerStart}${markerEnd} opacity="${op}" style="pointer-events:none" />`;
    const mx = (p1.x + p2.x) / 2, my = (p1.y + p2.y) / 2;
    s += `<text x="${mx}" y="${my - 6}" text-anchor="middle" fill="${color}" font-size="9" font-family="-apple-system,sans-serif" opacity="0.7" style="pointer-events:none">${esc(e.label || e.kind)}</text>`;
  }
  if (connecting && st.nMap[connecting.sourceId]) {
    const src = st.nMap[connecting.sourceId];
    const srcCx = src.x + src.w / 2, srcCy = src.y + src.h / 2;
    s += `<line id="feConnectLine" x1="${srcCx}" y1="${srcCy}" x2="${connecting.mouseX}" y2="${connecting.mouseY}" stroke="#6366f1" stroke-width="2" stroke-dasharray="6,4" opacity="0.8" style="pointer-events:none" />`;
    s += `<circle cx="${connecting.mouseX}" cy="${connecting.mouseY}" r="4" fill="#6366f1" opacity="0.8" style="pointer-events:none" />`;
  }
  let feClipIdx = 0;
  for (const n of st.nodes) {
    if (vm && !visibleNodeIds.has(n.id)) continue;
    const c = n.cfg;
    const selected = !isViewModeOnly() && st.selectedNode === n.id;
    const strokeW = selected ? 2.5 : 1.5;
    const stroke = selected ? "#6366f1" : c.border;
    const nodeCursor = isViewModeOnly() ? "default" : "pointer";
    const clipId = `fe-node-clip-${feClipIdx++}`;
    s += `<g class="fe-node" data-id="${escAttr(n.id)}" transform="translate(${n.x},${n.y})" style="cursor:${nodeCursor}">`;
    s += `<rect x="3" y="3" width="${n.w}" height="${n.h}" rx="8" fill="rgba(0,0,0,.3)" />`;
    s += `<rect width="${n.w}" height="${n.h}" rx="8" fill="${c.bg}" stroke="${stroke}" stroke-width="${strokeW}" />`;
    s += `<g clip-path="url(#${clipId})">`;
    if (!isReadOnlyFeature() && !isViewModeOnly()) {
      s += `<circle class="fe-port" cx="${n.w - 8}" cy="12" r="5" fill="${c.color}" opacity="0.6" style="cursor:crosshair" />`;
    }
    if (n.isCustom) {
      s += `<text x="${n.w - 20}" y="14" text-anchor="end" fill="#6366f1" font-size="8" font-family="-apple-system,sans-serif" opacity="0.7">NEW</text>`;
    }
    let ty = 20;
    s += `<text x="${n.w / 2}" y="${ty}" text-anchor="middle" fill="${c.color}" font-size="10" font-family="-apple-system,sans-serif" opacity="0.85">${c.stereotype}</text>`;
    ty += NAME_PAD2;
    const nameLines = wrapName2(feDisplayName(n));
    s += `<text class="fe-name" x="${n.w / 2}" text-anchor="middle" fill="#f0f2f7" font-size="14" font-weight="600" font-family="-apple-system,sans-serif">`;
    for (const ln of nameLines) {
      ty += NAME_LINE_H2;
      s += `<tspan x="${n.w / 2}" y="${ty}">${esc(ln)}</tspan>`;
    }
    s += "</text>";
    if (n.props.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const p of n.props) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#a0a4b8" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(p)}</text>`;
      }
    }
    if (n.methods.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const m of n.methods) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#a78bfa" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(m)}</text>`;
      }
    }
    const ruleLines = n.ruleLines || [];
    if (ruleLines.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const rl of ruleLines) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#94a3b8" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(rl)}</text>`;
      }
    }
    const derivedEvents = getEmittedEventsForNode(n.id);
    if (derivedEvents.length > 0) {
      ty += 8;
      s += `<line x1="12" y1="${ty}" x2="${n.w - 12}" y2="${ty}" stroke="${c.border}" stroke-width="0.5" />`;
      ty += 4;
      for (const ev of derivedEvents) {
        ty += 17;
        s += `<text x="16" y="${ty}" fill="#fbbf24" font-size="11" font-family="'SF Mono','Cascadia Code','Fira Code',monospace">${esc(formatDiagramEventBadgeLine(ev))}</text>`;
      }
    }
    s += "</g></g>";
  }
  s += "</g>";
  svg.innerHTML = s;
}
function rectEdge2(cx, cy, w, h, px, py) {
  const dx = px - cx, dy = py - cy;
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const absDx = Math.abs(dx), absDy = Math.abs(dy);
  const hw = w / 2, hh = h / 2;
  const t = absDx * hh > absDy * hw ? hw / (absDx || 1) : hh / (absDy || 1);
  return { x: cx + dx * t, y: cy + dy * t };
}
function fitToView2() {
  if (!st || st.nodes.length === 0) return;
  const wrap = document.getElementById("feCanvas");
  if (!wrap) return;
  const nodes = isViewModeOnly() ? st.nodes.filter((n) => !st.hiddenKinds.has(n.kind)) : st.nodes;
  if (nodes.length === 0) return;
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const n of nodes) {
    minX = Math.min(minX, n.x);
    minY = Math.min(minY, n.y);
    maxX = Math.max(maxX, n.x + n.w);
    maxY = Math.max(maxY, n.y + n.h);
  }
  const pad = 80;
  const gw = maxX - minX + pad * 2, gh = maxY - minY + pad * 2;
  const ww = wrap.clientWidth, wh = wrap.clientHeight;
  const zoom = Math.min(ww / gw, wh / gh, 1.5);
  st.zoom = zoom;
  st.panX = (ww - gw * zoom) / 2 - minX * zoom + pad * zoom;
  st.panY = (wh - gh * zoom) / 2 - minY * zoom + pad * zoom;
  renderSvg2();
}
function featureEditorZoom(factor) {
  if (!st) return;
  const wrap = document.getElementById("feCanvas");
  if (!wrap) return;
  const cx = wrap.clientWidth / 2, cy = wrap.clientHeight / 2;
  st.panX = cx - (cx - st.panX) * factor;
  st.panY = cy - (cy - st.panY) * factor;
  st.zoom *= factor;
  renderSvg2();
}
function featureEditorFit() {
  fitToView2();
}
function setupViewModeInteraction() {
  const svg = document.getElementById("feSvg");
  if (!svg || !st) return;
  if (feInteractionAbort) feInteractionAbort.abort();
  feInteractionAbort = new AbortController();
  const sig = feInteractionAbort.signal;
  let panning = false, panStartX = 0, panStartY = 0, panOrigX = 0, panOrigY = 0;
  svg.addEventListener("mousedown", function viewModeMouseDown(ev) {
    if (!document.getElementById("feSvg") || !isViewModeOnly()) return;
    ev.preventDefault();
    panning = true;
    panStartX = ev.clientX;
    panStartY = ev.clientY;
    panOrigX = st.panX;
    panOrigY = st.panY;
    svg.classList.add("dragging");
  }, { signal: sig });
  svg.addEventListener("mousemove", function viewModeMouseMove(ev) {
    if (!document.getElementById("feSvg") || !isViewModeOnly()) return;
    if (!panning) return;
    st.panX = panOrigX + (ev.clientX - panStartX);
    st.panY = panOrigY + (ev.clientY - panStartY);
    renderSvg2();
  }, { signal: sig });
  function endViewPan() {
    panning = false;
    svg.classList.remove("dragging");
  }
  svg.addEventListener("mouseup", endViewPan, { signal: sig });
  svg.addEventListener("mouseleave", endViewPan, { signal: sig });
  svg.addEventListener("wheel", function viewModeWheel(ev) {
    if (!document.getElementById("feSvg") || !isViewModeOnly()) return;
    ev.preventDefault();
    const factor = ev.deltaY < 0 ? 1.1 : 0.9;
    const rect = svg.getBoundingClientRect();
    const mx = ev.clientX - rect.left, my = ev.clientY - rect.top;
    st.panX = mx - (mx - st.panX) * factor;
    st.panY = my - (my - st.panY) * factor;
    st.zoom *= factor;
    renderSvg2();
  }, { passive: false, signal: sig });
}
function setupInteraction2() {
  const svg = document.getElementById("feSvg");
  if (!svg || !st) return;
  if (feInteractionAbort) feInteractionAbort.abort();
  feInteractionAbort = new AbortController();
  const sig = feInteractionAbort.signal;
  let dragNode = null, dragOffX = 0, dragOffY = 0;
  let dragCtx = null, dragCtxStartX = 0, dragCtxStartY = 0, dragCtxNodeStarts = null;
  let panning = false, panStartX = 0, panStartY = 0, panOrigX = 0, panOrigY = 0;
  let portDrag = false;
  svg.addEventListener("mousedown", function(ev) {
    const portEl = ev.target.closest(".fe-port");
    const nodeEl = ev.target.closest(".fe-node");
    const edgeEl = ev.target.closest(".fe-edge");
    const ctxEl = ev.target.closest(".dg-ctx-boundary");
    if (!isReadOnlyFeature() && portEl && nodeEl) {
      ev.preventDefault();
      const id = nodeEl.dataset.id;
      connecting = { sourceId: id, mouseX: 0, mouseY: 0 };
      portDrag = true;
      svg.style.cursor = "crosshair";
      const pt = svgPoint2(svg, ev);
      connecting.mouseX = pt.x;
      connecting.mouseY = pt.y;
      renderSvg2();
      return;
    }
    if (!isReadOnlyFeature() && connecting && nodeEl) {
      ev.preventDefault();
      finishConnect(nodeEl.dataset.id);
      renderSvg2();
      return;
    }
    if (!isReadOnlyFeature() && connecting && !nodeEl) {
      connecting = null;
      svg.style.cursor = "";
      renderSvg2();
      return;
    }
    if (nodeEl) {
      ev.preventDefault();
      const n = st.nMap[nodeEl.dataset.id];
      if (!n) return;
      st.selectedNode = n.id;
      st.selectedEdge = null;
      dragNode = n;
      const pt = svgPoint2(svg, ev);
      dragOffX = pt.x - n.x;
      dragOffY = pt.y - n.y;
      svg.classList.add("dragging-node");
      renderSvg2();
      refreshPanel();
    } else if (edgeEl) {
      ev.preventDefault();
      const idx = parseInt(edgeEl.dataset.idx);
      st.selectedEdge = idx;
      st.selectedNode = null;
      renderSvg2();
      refreshPanel();
    } else if (ctxEl) {
      ev.preventDefault();
      const ctxName = ctxEl.dataset.ctx;
      dragCtx = ctxName;
      const pt = svgPoint2(svg, ev);
      dragCtxStartX = pt.x;
      dragCtxStartY = pt.y;
      dragCtxNodeStarts = /* @__PURE__ */ new Map();
      for (const n of st.nodes) {
        if (n.boundedContext === ctxName) {
          dragCtxNodeStarts.set(n.id, { x: n.x, y: n.y });
        }
      }
      svg.classList.add("dragging-node");
    } else {
      if (st.selectedNode || st.selectedEdge !== null) {
        st.selectedNode = null;
        st.selectedEdge = null;
        renderSvg2();
        refreshPanel();
      }
      panning = true;
      panStartX = ev.clientX;
      panStartY = ev.clientY;
      panOrigX = st.panX;
      panOrigY = st.panY;
      svg.classList.add("dragging");
    }
  }, { signal: sig });
  svg.addEventListener("mousemove", function(ev) {
    if (connecting && portDrag) {
      const pt = svgPoint2(svg, ev);
      connecting.mouseX = pt.x;
      connecting.mouseY = pt.y;
      renderSvg2();
    } else if (dragNode) {
      const pt = svgPoint2(svg, ev);
      dragNode.x = pt.x - dragOffX;
      dragNode.y = pt.y - dragOffY;
      markDirty();
      renderSvg2();
    } else if (dragCtx) {
      const pt = svgPoint2(svg, ev);
      const dx = pt.x - dragCtxStartX, dy = pt.y - dragCtxStartY;
      for (const [id, start] of dragCtxNodeStarts) {
        const n = st.nMap[id];
        if (n) {
          n.x = start.x + dx;
          n.y = start.y + dy;
        }
      }
      markDirty();
      renderSvg2();
    } else if (panning) {
      st.panX = panOrigX + (ev.clientX - panStartX);
      st.panY = panOrigY + (ev.clientY - panStartY);
      renderSvg2();
    }
  }, { signal: sig });
  function endDrag(ev) {
    if (portDrag && connecting) {
      const nodeEl = ev.target?.closest?.(".fe-node");
      if (nodeEl && nodeEl.dataset.id !== connecting.sourceId) {
        finishConnect(nodeEl.dataset.id);
      }
      connecting = null;
      portDrag = false;
      svg.style.cursor = "";
      renderSvg2();
    }
    if (dragCtx) markDirty();
    dragNode = null;
    dragCtx = null;
    dragCtxNodeStarts = null;
    panning = false;
    svg.classList.remove("dragging", "dragging-node");
  }
  svg.addEventListener("mouseup", endDrag, { signal: sig });
  svg.addEventListener("mouseleave", function() {
    if (portDrag) {
      connecting = null;
      portDrag = false;
      svg.style.cursor = "";
      renderSvg2();
    }
    if (dragCtx) markDirty();
    dragNode = null;
    dragCtx = null;
    dragCtxNodeStarts = null;
    panning = false;
    svg.classList.remove("dragging", "dragging-node");
  }, { signal: sig });
  svg.addEventListener("wheel", function(ev) {
    ev.preventDefault();
    const factor = ev.deltaY < 0 ? 1.1 : 0.9;
    const rect = svg.getBoundingClientRect();
    const mx = ev.clientX - rect.left, my = ev.clientY - rect.top;
    st.panX = mx - (mx - st.panX) * factor;
    st.panY = my - (my - st.panY) * factor;
    st.zoom *= factor;
    renderSvg2();
  }, { passive: false, signal: sig });
  document.addEventListener("keydown", function handler(ev) {
    if (!document.getElementById("feSvg")) {
      document.removeEventListener("keydown", handler);
      return;
    }
    if (ev.target.tagName === "INPUT" || ev.target.tagName === "TEXTAREA" || ev.target.tagName === "SELECT") return;
    if (ev.key === "Escape") {
      if (connecting) {
        connecting = null;
        const canvas = document.getElementById("feCanvas");
        if (canvas) canvas.style.cursor = "";
        renderSvg2();
      } else {
        st.selectedNode = null;
        st.selectedEdge = null;
        renderSvg2();
        refreshPanel();
      }
    }
    if (ev.key === "Delete" || ev.key === "Backspace") {
      if (st.selectedNode) {
        removeNode(st.selectedNode);
      } else if (st.selectedEdge !== null) {
        removeEdge(st.selectedEdge);
      }
    }
  }, { signal: sig });
}
function svgPoint2(svg, ev) {
  const rect = svg.getBoundingClientRect();
  return {
    x: (ev.clientX - rect.left - st.panX) / st.zoom,
    y: (ev.clientY - rect.top - st.panY) / st.zoom
  };
}
var NODE_W2, PROP_H2, HEADER_H2, NAME_LINE_H2, NAME_PAD2, DIVIDER_H2, PAD2, MAX_NAME_CHARS2, KIND_CFG2, EDGE_COLORS, RELATION_KINDS, KIND_TO_SECTION, SECTION_TO_KIND, KIND_LABELS, LAYERS, LAYER_COLORS2, FE_EDGE_CFG, FE_KIND_DROPDOWN_LABEL, FEATURE_EDITOR_VIEW_MODE_KEY, FE_LAST_BC_KEY, FE_LAST_LAYER_KEY, FE_METHOD_RULE_KINDS, baseUrl, domainData, featureList, currentFeatureName, currentFeatureReadOnly, st, dirty, connecting, featureExports, viewModeOnly, feInteractionAbort, BC_COLORS2;
var init_feature_editor = __esm({
  "src/explorer/feature-editor.ts"() {
    init_helpers();
    init_tabs();
    init_diagram();
    NODE_W2 = 200;
    PROP_H2 = 17;
    HEADER_H2 = 26;
    NAME_LINE_H2 = 18;
    NAME_PAD2 = 6;
    DIVIDER_H2 = 8;
    PAD2 = 12;
    MAX_NAME_CHARS2 = 22;
    KIND_CFG2 = {
      aggregate: { stereotype: "\xABAggregate\xBB", color: "#d4a0ff", bg: "#1f1828", border: "#7c5aa8" },
      entity: { stereotype: "\xABEntity\xBB", color: "#7ab8ff", bg: "#161e2c", border: "#4a7bbf" },
      valueObject: { stereotype: "\xABValue Object\xBB", color: "#4ee8ad", bg: "#142820", border: "#36a87a" },
      subType: { stereotype: "\xABSub Type\xBB", color: "#a0b4c8", bg: "#1a1e24", border: "#6880a0" },
      event: { stereotype: "\xABDomain Event\xBB", color: "#fdd04e", bg: "#2a2418", border: "#b89530" },
      integrationEvent: { stereotype: "\xABIntegration Event\xBB", color: "#48e8d8", bg: "#14282a", border: "#30a89e" },
      commandHandlerTarget: { stereotype: "\xABHandles target\xBB", color: "#f0a050", bg: "#2a2218", border: "#c07830" },
      eventHandler: { stereotype: "\xABEvent Handler\xBB", color: "#ff8ac8", bg: "#2a1824", border: "#b85888" },
      commandHandler: { stereotype: "\xABCommand Handler\xBB", color: "#ff8ac8", bg: "#2a1824", border: "#b85888" },
      queryHandler: { stereotype: "\xABQuery Handler\xBB", color: "#ff8ac8", bg: "#2a1824", border: "#b85888" },
      repository: { stereotype: "\xABRepository\xBB", color: "#ffab5c", bg: "#2a2018", border: "#b87838" },
      service: { stereotype: "\xABService\xBB", color: "#bda0ff", bg: "#1e1828", border: "#7860b0" }
    };
    EDGE_COLORS = {
      Contains: "#60a5fa",
      References: "#34d399",
      ReferencesById: "#34d399",
      Has: "#60a5fa",
      HasMany: "#60a5fa",
      Emits: "#fbbf24",
      Handles: "#f472b6",
      Manages: "#fb923c",
      Publishes: "#2dd4bf"
    };
    RELATION_KINDS = ["Contains", "References", "ReferencesById", "Has", "HasMany", "Emits", "Handles", "Manages", "Publishes"];
    KIND_TO_SECTION = {
      aggregate: "aggregates",
      entity: "entities",
      valueObject: "valueObjects",
      subType: "subTypes",
      event: "domainEvents",
      integrationEvent: "integrationEvents",
      commandHandlerTarget: "commandHandlerTargets",
      eventHandler: "eventHandlers",
      commandHandler: "commandHandlers",
      queryHandler: "queryHandlers",
      repository: "repositories",
      service: "domainServices"
    };
    SECTION_TO_KIND = {};
    for (const [k, v] of Object.entries(KIND_TO_SECTION)) SECTION_TO_KIND[v] = k;
    KIND_LABELS = {
      aggregate: "Aggregate",
      entity: "Entity",
      valueObject: "Value Object",
      subType: "Sub Type",
      event: "Domain Event",
      integrationEvent: "Integration Event",
      commandHandlerTarget: "Cmd handler target",
      eventHandler: "Event Handler",
      commandHandler: "Command Handler",
      queryHandler: "Query Handler",
      repository: "Repository",
      service: "Domain Service"
    };
    LAYERS = ["Domain", "Application", "Infrastructure"];
    LAYER_COLORS2 = { Domain: "#a78bfa", Application: "#60a5fa", Infrastructure: "#fb923c" };
    FE_EDGE_CFG = {
      Contains: { label: "Contains", color: "#60a5fa", dashed: false },
      References: { label: "References", color: "#34d399", dashed: true },
      ReferencesById: { label: "References (by Id)", color: "#34d399", dashed: true },
      Has: { label: "Has", color: "#60a5fa", dashed: false },
      HasMany: { label: "Has Many", color: "#60a5fa", dashed: false },
      Emits: { label: "Emits", color: "#fbbf24", dashed: true },
      Handles: { label: "Handles", color: "#f472b6", dashed: true },
      Manages: { label: "Manages", color: "#fb923c", dashed: false },
      Publishes: { label: "Publishes", color: "#2dd4bf", dashed: true }
    };
    FE_KIND_DROPDOWN_LABEL = {
      aggregate: "Aggregates",
      entity: "Entities",
      valueObject: "Value Objects",
      subType: "Sub Types",
      event: "Domain Events",
      integrationEvent: "Integration Events",
      commandHandlerTarget: "Cmd handler targets",
      eventHandler: "Event Handlers",
      commandHandler: "Command Handlers",
      queryHandler: "Query Handlers",
      repository: "Repositories",
      service: "Services"
    };
    FEATURE_EDITOR_VIEW_MODE_KEY = "domain-model-feature-editor-view-mode";
    FE_LAST_BC_KEY = "domain-model-feature-editor-last-bc";
    FE_LAST_LAYER_KEY = "domain-model-feature-editor-last-layer";
    FE_METHOD_RULE_KINDS = /* @__PURE__ */ new Set(["aggregate", "entity", "valueObject", "subType"]);
    baseUrl = "";
    domainData = null;
    featureList = [];
    currentFeatureName = null;
    currentFeatureReadOnly = false;
    st = null;
    dirty = false;
    connecting = null;
    featureExports = [];
    viewModeOnly = false;
    feInteractionAbort = null;
    try {
      viewModeOnly = sessionStorage.getItem(FEATURE_EDITOR_VIEW_MODE_KEY) === "1";
    } catch {
      viewModeOnly = false;
    }
    BC_COLORS2 = ["#6366f1", "#06b6d4", "#f59e0b", "#10b981", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6"];
  }
});

// src/explorer/trace.ts
var trace_exports = {};
__export(trace_exports, {
  clearTracePanel: () => clearTracePanel,
  disconnectTraceHub: () => disconnectTraceHub,
  mountTrace: () => mountTrace,
  reconnectTraceHub: () => reconnectTraceHub,
  remountTraceDiagram: () => remountTraceDiagram,
  renderTraceView: () => renderTraceView
});
function setStatus(text, cls) {
  const el = document.getElementById("traceConnectionStatus");
  if (!el) return;
  el.textContent = text;
  el.className = "trace-status " + (cls || "");
}
function buildHighlightIds(msg) {
  const ids = /* @__PURE__ */ new Set();
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
  const list = document.getElementById("traceEntries");
  if (!list) return;
  const empty = list.querySelector(".trace-empty");
  if (empty) empty.remove();
  const iso = msg.timestampUtc || msg.TimestampUtc;
  const time = iso ? new Date(iso).toLocaleString() : "\u2014";
  const eventType = msg.eventTypeFullName || msg.EventTypeFullName || "";
  const payload = msg.payloadJson ?? msg.PayloadJson ?? "";
  const handlers = msg.handlerFullNames || msg.HandlerFullNames || [];
  const ctxs = msg.boundedContextsWithMatch || msg.BoundedContextsWithMatch || [];
  const filter = msg.boundedContextName || msg.BoundedContextName;
  const el = document.createElement("div");
  el.className = "trace-entry";
  el.innerHTML = `
    <div class="trace-entry-time">${esc(time)}</div>
    <div class="trace-entry-type">${esc(eventType)}</div>
    <div class="trace-entry-meta">${filter ? "Context filter: " + esc(filter) : ctxs.length ? "Matched in: " + esc(ctxs.join(", ")) : ""}</div>
    <div class="trace-entry-handlers">${handlers.length ? "Handlers: " + esc(handlers.join(", ")) : "No matching handlers in graph"}</div>
    <pre class="trace-entry-json">${esc(typeof payload === "string" ? payload : JSON.stringify(payload, null, 2))}</pre>
  `;
  list.insertBefore(el, list.firstChild);
}
function normalizeMessage(raw) {
  if (!raw || typeof raw !== "object") return raw;
  return {
    timestampUtc: raw.timestampUtc ?? raw.TimestampUtc,
    eventTypeFullName: raw.eventTypeFullName ?? raw.EventTypeFullName,
    eventGraphKey: raw.eventGraphKey ?? raw.EventGraphKey,
    boundedContextName: raw.boundedContextName ?? raw.BoundedContextName,
    boundedContextsWithMatch: raw.boundedContextsWithMatch ?? raw.BoundedContextsWithMatch,
    handlerFullNames: raw.handlerFullNames ?? raw.HandlerFullNames,
    payloadJson: raw.payloadJson ?? raw.PayloadJson
  };
}
async function startHub() {
  const hub = window.signalR;
  if (!HUB_URL || !hub) {
    setStatus("SignalR unavailable", "disconnected");
    return;
  }
  manualDisconnect = false;
  setStatus("Connecting\u2026", "connecting");
  if (connection) {
    try {
      await connection.stop();
    } catch {
    }
    connection = null;
  }
  connection = new hub.HubConnectionBuilder().withUrl(HUB_URL).withAutomaticReconnect([0, 2e3, 5e3, 1e4]).build();
  connection.on("trace", (payload) => {
    const msg = normalizeMessage(payload);
    appendEntry(msg);
    applyHighlight(msg);
  });
  connection.onreconnecting(() => setStatus("Reconnecting\u2026", "connecting"));
  connection.onreconnected(() => setStatus("Connected", "connected"));
  connection.onclose(() => {
    setStatus("Disconnected", "disconnected");
    if (!manualDisconnect) scheduleReconnect();
  });
  try {
    await connection.start();
    setStatus("Connected", "connected");
  } catch (e) {
    console.error("Trace hub connection failed", e);
    setStatus("Disconnected", "disconnected");
    scheduleReconnect();
  }
}
function scheduleReconnect() {
  if (manualDisconnect) return;
  if (reconnectTimer) return;
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    void startHub();
  }, 3e3);
}
async function disconnectTraceHub() {
  manualDisconnect = true;
  clearHighlightTimer();
  setDiagramTraceHighlights([]);
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  if (connection) {
    try {
      await connection.stop();
    } catch {
    }
    connection = null;
  }
  setStatus("Paused", "disconnected");
}
async function reconnectTraceHub() {
  await disconnectTraceHub();
  manualDisconnect = false;
  await startHub();
}
function clearTracePanel() {
  const list = document.getElementById("traceEntries");
  if (!list) return;
  list.innerHTML = '<div class="trace-empty">Waiting for events\u2026 Call <code>DomainModelTracing.NotifyAsync</code> from your app or hit the demo endpoint.</div>';
  clearHighlightTimer();
  setDiagramTraceHighlights([]);
}
function renderTraceView() {
  let html = '<div class="trace-layout">';
  html += '<div class="trace-diagram-pane">';
  html += renderDiagramView({ traceLayout: true });
  html += "</div>";
  html += '<aside class="trace-panel" id="tracePanel">';
  html += '<div class="trace-panel-header">';
  html += "<h2>Event trace</h2>";
  html += '<div class="trace-panel-actions">';
  html += '<span class="trace-status disconnected" id="traceConnectionStatus">\u2026</span>';
  html += '<button type="button" onclick="window.__trace.clear()">Clear</button>';
  html += '<button type="button" onclick="window.__trace.reconnect()">Reconnect</button>';
  html += "</div></div>";
  html += '<div class="trace-entries" id="traceEntries">';
  html += '<div class="trace-empty">Waiting for events\u2026</div>';
  html += "</div></aside></div>";
  return html;
}
function mountTrace(mergedCtx, contexts) {
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
    const list = document.getElementById("traceEntries");
    if (!list) return;
    list.innerHTML = "";
    for (let i = items.length - 1; i >= 0; i--) {
      appendEntry(normalizeMessage(items[i]));
    }
    applyHighlight(normalizeMessage(items[0]));
  } catch {
  }
}
function remountTraceDiagram() {
  if (!diagramCtx || !boundedContexts) return;
  requestAnimationFrame(() => initDiagram(diagramCtx, boundedContexts));
}
var BASE_URL, HUB_URL, HIGHLIGHT_DURATION_MS, connection, reconnectTimer, highlightClearTimer, manualDisconnect, diagramCtx, boundedContexts;
var init_trace = __esm({
  "src/explorer/trace.ts"() {
    init_helpers();
    init_diagram();
    BASE_URL = (window.__config?.apiUrl || "/domain-model/json").replace(/\/json$/, "");
    HUB_URL = window.__config?.traceHubUrl || "";
    HIGHLIGHT_DURATION_MS = 5e3;
    connection = null;
    reconnectTimer = null;
    highlightClearTimer = null;
    manualDisconnect = false;
    diagramCtx = null;
    boundedContexts = null;
  }
});

// src/explorer/domain-model-main.ts
init_helpers();

// src/explorer/views.ts
init_helpers();
init_diagram();
init_tabs();
function renderDetailView(kind, item, ctx, metadata2, saveMetadataFn) {
  const meta = kindMeta(kind);
  const existing = (metadata2 || {})[item.fullName] || {};
  const diagramKind = SECTION_TO_DIAGRAM_KIND[kind];
  const st2 = getDiagramState();
  const kindFiltered = diagramKind && st2 && st2.hiddenKinds.has(diagramKind);
  const perTypeHidden = diagramKind && isDiagramNodeHidden(item.fullName);
  const diagramHidden = kindFiltered || perTypeHidden;
  const detailCbDisabled = kindFiltered ? " disabled" : "";
  let html = '<div class="detail-panel">';
  html += `<div class="detail-back" onclick="window.__nav.switchTab('diagram')">\u2190 Back to diagram</div>`;
  html += `<div style="margin-bottom:4px"><span class="card-tag" style="color:${meta.color};background:${meta.bg}">${meta.tag}</span></div>`;
  html += `<h2 class="detail-title">${esc(item.name)}</h2>`;
  html += `<div class="detail-fullname">${esc(item.fullName)}</div>`;
  if (diagramKind) {
    html += `<div class="detail-section detail-diagram-visibility">
      <label class="detail-diagram-visibility-label">
        <input type="checkbox" id="detailDiagramVisible"${diagramHidden ? "" : " checked"}${detailCbDisabled}
               onchange="window.__nav.toggleDiagramVisibility('${escAttr(item.fullName)}', this.checked)" />
        <span>Show on main diagram</span>
      </label>
      ${kindFiltered ? '<p class="detail-diagram-visibility-note">This node type is hidden via the diagram toolbar; turn the type back on to show this item.</p>' : ""}
    </div>`;
  }
  if (item.description) {
    html += `<div class="detail-desc">${esc(item.description)}</div>`;
  }
  html += `<div class="detail-section detail-metadata-edit">
    <h3>Custom Metadata</h3>
    <label style="display:block;margin-bottom:8px;color:var(--text-muted);font-size:.82rem">
      Alias
      <input id="metaAlias" type="text" value="${escAttr(existing.alias || "")}"
             placeholder="Display name override\u2026"
             style="display:block;width:100%;margin-top:4px;padding:6px 10px;border-radius:6px;
                    border:1px solid var(--border);background:var(--bg-card);color:var(--text);font-size:.88rem" />
    </label>
    <label style="display:block;margin-bottom:8px;color:var(--text-muted);font-size:.82rem">
      Description
      <textarea id="metaDescription" rows="3" placeholder="Custom description\u2026"
                style="display:block;width:100%;margin-top:4px;padding:6px 10px;border-radius:6px;
                       border:1px solid var(--border);background:var(--bg-card);color:var(--text);
                       font-size:.88rem;resize:vertical">${esc(existing.description || "")}</textarea>
    </label>
    <button onclick="(async()=>{
      const alias=document.getElementById('metaAlias').value;
      const desc=document.getElementById('metaDescription').value;
      await window.__saveMetadata('${escAttr(item.fullName)}',alias,desc);
      this.textContent='Saved \u2713';setTimeout(()=>this.textContent='Save',1500);
    })()"
    style="padding:6px 18px;border-radius:6px;border:1px solid var(--border);background:var(--bg-card);
           color:var(--text);cursor:pointer;font-size:.82rem;transition:background .15s"
    onmouseover="this.style.background='var(--bg-hover)'"
    onmouseout="this.style.background='var(--bg-card)'"
    >Save</button>
  </div>`;
  if (item.properties && item.properties.length > 0) {
    html += '<div class="detail-section"><h3>Properties</h3>';
    html += '<div class="detail-props"><table><tr><th>Name</th><th>Type</th><th>Ref</th></tr>';
    for (const p of item.properties) {
      const refHtml = p.referenceTypeName ? `<span class="rel-link" onclick="window.__nav.navigateTo('${escAttr(p.referenceTypeName)}')">${esc(shortName(p.referenceTypeName))}</span>` : '<span style="color:var(--text-dim)">\u2014</span>';
      html += `<tr><td>${esc(p.name)}</td><td>${esc(p.typeName)}${p.isCollection ? ' <span style="color:var(--clr-value-object)">[\u2217]</span>' : ""}</td><td>${refHtml}</td></tr>`;
    }
    html += "</table></div></div>";
  }
  if (item.childEntities && item.childEntities.length > 0) {
    html += '<div class="detail-section"><h3>Child Entities</h3>';
    for (const c of item.childEntities) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(c)}')"><span class="rel-arrow">\u25C6</span> ${esc(shortName(c))}</div><br/>`;
    }
    html += "</div>";
  }
  if (item.emittedEvents && item.emittedEvents.length > 0) {
    html += '<div class="detail-section"><h3>Emitted Events</h3>';
    for (const e of item.emittedEvents) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(e)}')"><span class="rel-arrow">\u26A1</span> ${esc(shortName(e))}</div><br/>`;
    }
    html += "</div>";
  }
  if (item.methods && item.methods.length > 0) {
    html += '<div class="detail-section"><h3>Methods</h3>';
    html += '<div class="detail-props"><table><tr><th>Method</th><th>Parameters</th><th>Returns</th></tr>';
    for (const m of item.methods) {
      const params = (m.parameters || []).map((p) => esc(p.typeName) + " " + esc(p.name)).join(", ") || '<span style="color:var(--text-dim)">\u2014</span>';
      html += `<tr><td>${esc(m.name)}</td><td>${params}</td><td>${esc(m.returnTypeName)}</td></tr>`;
    }
    html += "</table></div></div>";
  }
  if (item.emittedBy && item.emittedBy.length > 0) {
    html += '<div class="detail-section"><h3>Emitted By</h3>';
    for (const e of item.emittedBy) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(e)}')"><span class="rel-arrow">\u2190</span> ${esc(shortName(e))}</div><br/>`;
    }
    html += "</div>";
  }
  if (item.handledBy && item.handledBy.length > 0) {
    html += '<div class="detail-section"><h3>Handled By</h3>';
    for (const e of item.handledBy) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(e)}')"><span class="rel-arrow">\u2192</span> ${esc(shortName(e))}</div><br/>`;
    }
    html += "</div>";
  }
  if (item.handles && item.handles.length > 0) {
    html += '<div class="detail-section"><h3>Handles</h3>';
    for (const h of item.handles) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(h)}')"><span class="rel-arrow">\u2192</span> ${esc(shortName(h))}</div><br/>`;
    }
    html += "</div>";
  }
  if (item.managesAggregate) {
    html += `<div class="detail-section"><h3>Manages</h3>
      <div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(item.managesAggregate)}')"><span class="rel-arrow">\u25C6</span> ${esc(shortName(item.managesAggregate))}</div>
    </div>`;
  }
  const rels = (ctx.relationships || []).filter(
    (r) => r.sourceType === item.fullName || r.targetType === item.fullName
  );
  if (rels.length > 0) {
    html += '<div class="detail-section"><h3>Relationships</h3>';
    html += '<table class="rel-table"><tr><th>Direction</th><th>Kind</th><th>Related Type</th><th>Label</th></tr>';
    for (const r of rels) {
      const isSource = r.sourceType === item.fullName;
      const other = isSource ? r.targetType : r.sourceType;
      const dir = isSource ? "\u2192 outgoing" : "\u2190 incoming";
      const kindColor = relKindColor(r.kind);
      html += `<tr>
        <td style="color:var(--text-muted)">${dir}</td>
        <td><span class="rel-kind" style="color:${kindColor};background:${kindColor}18">${esc(r.kind)}</span></td>
        <td><span class="rel-link" onclick="window.__nav.navigateTo('${escAttr(other)}')">${esc(shortName(other))}</span></td>
        <td style="color:var(--text-muted)">${r.label ? esc(r.label) : "\u2014"}</td>
      </tr>`;
    }
    html += "</table></div>";
  }
  html += "</div>";
  return html;
}

// src/explorer/domain-model-main.ts
init_diagram();
var API_URL = window.__config?.apiUrl || "/domain-model/json";
var BASE_URL2 = API_URL.replace(/\/json$/, "");
var TESTING_MODE = window.__config?.testingMode === true;
var FEATURE_EDITOR_MODE = window.__config?.featureEditorMode === true;
var TRACE_VIEW_MODE = window.__config?.traceViewMode === true;
var testingModule = null;
var featureEditorModule = null;
var traceModule = null;
var metadata = {};
var STORAGE_KEY2 = "domainModelSelectedContexts";
var METADATA_STORAGE_KEY = "domainModelMetadata";
var data = null;
var selectedContextNames = /* @__PURE__ */ new Set();
var currentCtx = null;
var currentView = "diagram";
var currentDetail = null;
var availableExports = [];
function saveSelection() {
  localStorage.setItem(STORAGE_KEY2, JSON.stringify([...selectedContextNames]));
}
function loadSelection() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY2);
    if (stored) return new Set(JSON.parse(stored));
  } catch {
  }
  return null;
}
async function init() {
  try {
    const res = await fetch(API_URL);
    data = await res.json();
    let legacyMetadata = {};
    try {
      const stored = localStorage.getItem(METADATA_STORAGE_KEY);
      if (stored) legacyMetadata = JSON.parse(stored);
    } catch {
    }
    try {
      const metaRes = await fetch(`${BASE_URL2}/metadata`);
      let serverMeta = {};
      if (metaRes.ok) {
        serverMeta = await metaRes.json();
      }
      metadata = { ...legacyMetadata, ...serverMeta };
      const keysToMigrate = Object.keys(legacyMetadata || {}).filter((k) => !serverMeta || !serverMeta[k]);
      if (keysToMigrate.length > 0) {
        await Promise.allSettled(keysToMigrate.map(async (fullName) => {
          const entry = legacyMetadata[fullName];
          return fetch(`${BASE_URL2}/metadata/${encodeURIComponent(fullName)}`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(entry)
          });
        }));
        try {
          localStorage.removeItem(METADATA_STORAGE_KEY);
        } catch {
        }
      }
    } catch {
      metadata = legacyMetadata;
    }
    window.__metadata = metadata;
    setDiagramLayoutBaseUrl(BASE_URL2);
    try {
      const layoutRes = await fetch(`${BASE_URL2}/diagram-layout`);
      if (layoutRes.ok) {
        setServerDiagramLayoutCache(await layoutRes.json());
      }
    } catch {
    }
    try {
      const exportsRes = await fetch(`${BASE_URL2}/exports`);
      if (exportsRes.ok) {
        availableExports = await exportsRes.json();
      }
    } catch {
    }
    if (data.boundedContexts && data.boundedContexts.length > 0) {
      const saved = loadSelection();
      const validNames = new Set(data.boundedContexts.map((c) => c.name));
      if (saved && [...saved].some((n) => validNames.has(n))) {
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
    if (TESTING_MODE) {
      testingModule = await Promise.resolve().then(() => (init_testing(), testing_exports));
      await testingModule.initTesting(API_URL.replace("/json", ""));
      wireTestingGlobals();
    }
    if (FEATURE_EDITOR_MODE) {
      featureEditorModule = await Promise.resolve().then(() => (init_feature_editor(), feature_editor_exports));
      await featureEditorModule.initFeatureEditor(BASE_URL2, data);
      wireFeatureEditorGlobals();
    }
    if (TRACE_VIEW_MODE) {
      traceModule = await Promise.resolve().then(() => (init_trace(), trace_exports));
      wireTraceGlobals();
    }
    const hashTab = (typeof location !== "undefined" && location.hash ? location.hash.slice(1) : "").toLowerCase();
    if (hashTab === "features" && FEATURE_EDITOR_MODE) {
      currentView = "features";
    } else if (hashTab === "diagram") {
      currentView = "diagram";
    }
    render();
    initSidebarToggle();
    window.__onDiagramHiddenNodesChanged = syncExplorerDiagramHideCheckboxes;
  } catch (e) {
    document.getElementById("loadingState").textContent = "Failed to load domain model. Check the console.";
    console.error("Failed to load domain model:", e);
  }
}
function mergeContexts() {
  const selected = (data.boundedContexts || []).filter((c) => selectedContextNames.has(c.name));
  if (selected.length === 0) return null;
  if (selected.length === 1) return selected[0];
  const merged = { name: selected.map((c) => c.name).join(" + ") };
  for (const key of ALL_SECTIONS) {
    merged[key] = selected.flatMap((c) => c[key] || []);
  }
  merged.relationships = selected.flatMap((c) => c.relationships || []);
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
var SIDEBAR_COLLAPSED_KEY = "domainModelSidebarCollapsed";
function initSidebarToggle() {
  const sidebar = document.getElementById("sidebar");
  const btn = document.getElementById("sidebarToggle");
  if (!sidebar || !btn) return;
  const collapsed = localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === "true";
  if (collapsed) {
    sidebar.classList.add("collapsed");
    btn.textContent = "\u276F";
    btn.title = "Expand sidebar";
  }
  btn.addEventListener("click", () => {
    const isCollapsed = sidebar.classList.toggle("collapsed");
    btn.textContent = isCollapsed ? "\u276F" : "\u276E";
    btn.title = isCollapsed ? "Expand sidebar" : "Collapse sidebar";
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, isCollapsed);
  });
}
function render() {
  renderSidebar();
  renderMain();
  syncFeatureEditorViewBodyClass();
  requestAnimationFrame(() => syncExplorerDiagramHideCheckboxes());
}
function syncFeatureEditorViewBodyClass() {
  try {
    const active = FEATURE_EDITOR_MODE && featureEditorModule && currentView === "features" && featureEditorModule.isFeatureEditorViewModeLayoutActive();
    document.body.classList.toggle("feature-editor-view-mode", !!active);
  } catch {
    document.body.classList.remove("feature-editor-view-mode");
  }
}
window.__syncFeatureEditorViewBodyClass = syncFeatureEditorViewBodyClass;
function renderSidebar() {
  const nav = document.getElementById("sidebarNav");
  if (!currentCtx) {
    nav.innerHTML = "";
    return;
  }
  const allContexts = data.boundedContexts || [];
  const contextLabel = selectedContextNames.size === allContexts.length ? "All Bounded Contexts" : [...selectedContextNames].join(", ");
  document.getElementById("contextName").textContent = contextLabel;
  let html = "";
  if (allContexts.length > 1) {
    html += `<div class="nav-section ctx-selector">
      <div class="nav-section-header" onclick="window.__nav.toggleSection(this)">
        <span class="dot" style="width:6px;height:6px;border-radius:50%;background:var(--accent)"></span>
        Bounded Contexts
        <span class="badge">${selectedContextNames.size}/${allContexts.length}</span>
        <span class="chevron">\u25BC</span>
      </div>
      <div class="nav-items">
        ${allContexts.map((c) => {
      const checked = selectedContextNames.has(c.name);
      return `<label class="nav-item ctx-option${checked ? " active" : ""}" onclick="event.stopPropagation()">
            <input type="checkbox" ${checked ? "checked" : ""}
                   onchange="window.__nav.toggleContext('${escAttr(c.name)}')" />
            <span class="ctx-name">${esc(c.name)}</span>
          </label>`;
    }).join("")}
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
        <span class="chevron">\u25BC</span>
      </div>
      <div class="nav-items">
        ${items.map((item) => {
      const dk = SECTION_TO_DIAGRAM_KIND[sec.key];
      const hidden = dk && isNavDiagramHidden(dk, item.fullName);
      const visTitle = hidden ? "Show on main diagram" : "Hide from main diagram";
      return `
          <div class="nav-item nav-item-with-diagram-toggle${isActive(sec.key, item) ? " active" : ""}"
               data-fullname="${escAttr(item.fullName)}">
            ${dk ? `<label class="nav-diagram-visibility" title="${escAttr(visTitle)}" onclick="event.stopPropagation()">
              <input type="checkbox" ${hidden ? "" : "checked"}
                     data-nav-kind="${escAttr(sec.key)}"
                     onchange="window.__nav.toggleDiagramVisibility('${escAttr(item.fullName)}', this.checked)" />
            </label>` : '<span class="nav-diagram-visibility-spacer"></span>'}
            <span class="nav-item-label" onclick="window.__nav.showDetail('${sec.key}', '${escAttr(item.fullName)}')">
              <span class="nav-dot" style="background:${sec.color}"></span>
              ${esc(item.name)}
            </span>
          </div>`;
    }).join("")}
      </div>
    </div>`;
  }
  html += `<div class="nav-section" style="margin-top:12px;border-top:1px solid var(--border);padding-top:8px;">
    <div class="nav-item${currentView === "diagram" ? " active" : ""}" onclick="window.__nav.switchTab('diagram')">
      <span class="nav-dot" style="background:var(--clr-relationship)"></span>
      Diagram
    </div>`;
  if (FEATURE_EDITOR_MODE) {
    html += `<div class="nav-item${currentView === "features" ? " active" : ""}" onclick="window.__nav.switchTab('features')">
      <span class="nav-dot" style="background:#f59e0b"></span>
      \u2699 Features
    </div>`;
  }
  if (TESTING_MODE) {
    html += `<div class="nav-item${currentView === "testing" ? " active" : ""}" onclick="window.__nav.switchTab('testing')">
      <span class="nav-dot" style="background:#c084fc"></span>
      \u{1F9EA} Testing
    </div>`;
  }
  if (TRACE_VIEW_MODE) {
    html += `<div class="nav-item${currentView === "trace" ? " active" : ""}" onclick="window.__nav.switchTab('trace')">
      <span class="nav-dot" style="background:#22d3ee"></span>
      Trace
    </div>`;
  }
  html += `</div>`;
  nav.innerHTML = html;
}
function isActive(key, item) {
  if (currentView === "detail" && currentDetail) {
    return currentDetail.kind === key && currentDetail.item.fullName === item.fullName;
  }
  return false;
}
function isNavDiagramHidden(diagramKind, fullName) {
  const st2 = getDiagramState();
  if (st2 && st2.hiddenKinds.has(diagramKind)) return true;
  return isDiagramNodeHidden(fullName);
}
function syncExplorerDiagramHideCheckboxes() {
  for (const row of document.querySelectorAll(".nav-item-with-diagram-toggle")) {
    const fullName = row.getAttribute("data-fullname");
    const input = row.querySelector('input[type="checkbox"][data-nav-kind]');
    if (!fullName || !input) continue;
    const kindKey = input.getAttribute("data-nav-kind");
    const dk = kindKey ? SECTION_TO_DIAGRAM_KIND[kindKey] : null;
    if (!dk) continue;
    const hidden = isNavDiagramHidden(dk, fullName);
    input.checked = !hidden;
    const lab = row.querySelector(".nav-diagram-visibility");
    if (lab) {
      lab.title = hidden ? "Show on main diagram" : "Hide from main diagram";
    }
  }
}
function metadataEntryIsEmpty(entry) {
  if (!entry || typeof entry !== "object") return true;
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
      await fetch(`${BASE_URL2}/metadata/${encodeURIComponent(fullName)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ alias: null, description: null, hiddenOnDiagram: null })
      });
    } else {
      await fetch(`${BASE_URL2}/metadata/${encodeURIComponent(fullName)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(entry)
      });
    }
  } catch {
  }
}
async function toggleDiagramVisibility(fullName, visible) {
  const prev = metadata[fullName] || {};
  const next = { ...prev, hiddenOnDiagram: visible ? false : true };
  removeLegacyHiddenNodeId(fullName);
  await persistMetadata(fullName, next);
  reapplyDiagramVisibilityAfterMetadataChange();
}
function renderMain() {
  const main = document.getElementById("mainContent");
  if (!currentCtx) {
    main.innerHTML = '<div class="empty-state"><h2>No Bounded Contexts</h2><p>The domain graph is empty.</p></div>';
    return;
  }
  if (currentView === "features" && FEATURE_EDITOR_MODE && featureEditorModule) {
    main.innerHTML = featureEditorModule.renderFeatureEditorView();
    requestAnimationFrame(() => featureEditorModule.mountFeatureEditor());
    return;
  }
  if (currentView === "testing" && TESTING_MODE && testingModule) {
    main.innerHTML = testingModule.renderTestingView();
    requestAnimationFrame(() => testingModule.mountTesting());
    return;
  }
  if (currentView === "trace" && TRACE_VIEW_MODE && traceModule) {
    main.innerHTML = traceModule.renderTraceView();
    const selectedCtxs2 = (data.boundedContexts || []).filter((c) => selectedContextNames.has(c.name));
    traceModule.mountTrace(currentCtx, selectedCtxs2);
    return;
  }
  if (currentView === "detail" && currentDetail) {
    main.innerHTML = renderDetailView(currentDetail.kind, currentDetail.item, currentCtx, metadata, saveMetadata);
    return;
  }
  main.innerHTML = renderDiagramView();
  const selectedCtxs = (data.boundedContexts || []).filter((c) => selectedContextNames.has(c.name));
  requestAnimationFrame(() => initDiagram(currentCtx, selectedCtxs));
}
async function saveMetadata(fullName, alias, description) {
  const prev = metadata[fullName] || {};
  const next = {
    ...prev,
    alias: alias && String(alias).trim() ? alias : null,
    description: description && String(description).trim() ? description : null
  };
  if (!metadataImpliesDiagramHiddenByDefault(next)) {
    delete next.hiddenOnDiagram;
  } else if (next.hiddenOnDiagram === void 0) {
    next.hiddenOnDiagram = true;
  }
  await persistMetadata(fullName, next);
  reapplyDiagramVisibilityAfterMetadataChange();
}
function switchTab(tab) {
  currentView = tab;
  currentDetail = null;
  render();
  document.getElementById("mainContent").scrollTop = 0;
}
function showDetail(kind, fullName) {
  const items = currentCtx[kind] || [];
  const item = items.find((i) => i.fullName === fullName);
  if (!item) return;
  currentView = "detail";
  currentDetail = { kind, item };
  render();
  document.getElementById("mainContent").scrollTop = 0;
}
function navigateTo(fullName) {
  for (const sec of ALL_SECTIONS) {
    const items = currentCtx[sec] || [];
    const item = items.find((i) => i.fullName === fullName);
    if (item) {
      showDetail(sec, fullName);
      return;
    }
  }
}
function toggleSection(el) {
  el.parentElement.classList.toggle("collapsed");
}
window.__nav = {
  switchTab,
  showDetail,
  navigateTo,
  toggleSection,
  toggleContext,
  toggleDiagramVisibility
};
window.__saveMetadata = saveMetadata;
window.__downloadExport = async function(name) {
  try {
    const url = `${BASE_URL2}/exports/${encodeURIComponent(name)}`;
    const res = await fetch(url);
    if (!res.ok) throw new Error("Download failed");
    const blob = await res.blob();
    const disposition = res.headers.get("Content-Disposition") || "";
    const match = disposition.match(/filename="?([^"]+)"?/);
    const filename = match ? match[1] : `${name}.txt`;
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(a.href);
  } catch (err) {
    console.error("Export download failed", err);
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
  hideAllKinds: diagramHideAllKinds
};
window.__metadata = metadata;
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
    invokeMethod: testingModule.invokeInstanceMethod
  };
}
function wireTraceGlobals() {
  if (!traceModule) return;
  window.__trace = {
    clear: () => {
      traceModule.clearTracePanel();
    },
    reconnect: () => {
      void traceModule.reconnectTraceHub();
    }
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
    onDiagramViewFlagsChanged: featureEditorModule.onDiagramViewFlagsChanged
  };
}
init();
