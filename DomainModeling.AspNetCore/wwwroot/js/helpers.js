/**
 * Shared helpers used across all explorer modules.
 */

export function shortName(fullName) {
  if (!fullName) return '';
  const s = String(fullName);
  if (s.indexOf('`') >= 0 || s.indexOf('[[') >= 0) return displayShortName(s);
  const parts = s.split('.');
  return parts[parts.length - 1];
}

/**
 * Human-readable short name for a CLR type string (reflection FullName / relationship keys).
 * e.g. Namespace.EntityDeletedEvent`1[[Ns.User, Asm,...]] → EntityDeletedEvent<User>
 */
export function displayShortName(fullName) {
  if (!fullName) return '';
  const s = String(fullName);
  const lastDot = s.lastIndexOf('.');
  const segment = lastDot >= 0 ? s.slice(lastDot + 1) : s;
  return formatClrTypeSegment(segment);
}

/**
 * Formats one namespace-less CLR type segment (may include `arity and [[assembly-qualified args]]`).
 */
export function formatClrTypeSegment(segment) {
  if (!segment) return '';
  const tick = segment.indexOf('`');
  if (tick < 0) return segment;

  const base = segment.slice(0, tick);
  let i = tick + 1;
  while (i < segment.length && segment[i] >= '0' && segment[i] <= '9') i++;
  if (i >= segment.length || segment.slice(i, i + 2) !== '[[') return base;

  const args = [];
  let pos = i;
  while (pos < segment.length && segment.slice(pos, pos + 2) === '[[') {
    const innerEnd = matchDoubleBracketContentEnd(segment, pos + 2);
    if (innerEnd < 0) break;
    const inner = segment.slice(pos + 2, innerEnd);
    args.push(parseAssemblyQualifiedTypeName(inner));
    pos = innerEnd + 2;
    while (pos < segment.length && (segment[pos] === ',' || segment[pos] === ' ')) pos++;
  }
  return args.length ? `${base}<${args.join(', ')}>` : base;
}

/**
 * Content starts after opening `[[`. Returns index of first `]` of the matching closing `]]`.
 */
function matchDoubleBracketContentEnd(s, contentStart) {
  let depth = 1;
  let i = contentStart;
  while (i < s.length && depth > 0) {
    if (i + 1 < s.length && s[i] === '[' && s[i + 1] === '[') {
      depth++;
      i += 2;
    } else if (i + 1 < s.length && s[i] === ']' && s[i + 1] === ']') {
      depth--;
      i += 2;
    } else i++;
  }
  return depth === 0 ? i - 2 : -1;
}

function parseAssemblyQualifiedTypeName(inner) {
  const typePart = stripAssemblyQualifier(inner.trim());
  if (!typePart) return '';
  if (typePart.indexOf('`') >= 0) return formatClrTypeSegment(typePart);
  const parts = typePart.split('.');
  return parts[parts.length - 1] || typePart;
}

/** Removes `, Assembly` / `, Version=` suffix from a CLR assembly-qualified type string. */
function stripAssemblyQualifier(s) {
  if (!s) return '';
  const v = s.indexOf(', Version=');
  if (v > 0) return s.slice(0, v).trim();
  const c = s.indexOf(', Culture=');
  if (c > 0) return s.slice(0, c).trim();
  const pk = s.indexOf(', PublicKeyToken=');
  if (pk > 0) return s.slice(0, pk).trim();
  const simple = s.indexOf(', ');
  if (simple > 0 && s.indexOf('`') < 0) return s.slice(0, simple).trim();
  return s.trim();
}

/**
 * Removes the first balanced generic argument list from a type fragment, recursively
 * (e.g. IReadonlyList<T> → IReadonlyList, Dictionary<K,List<V>> → Dictionary).
 */
export function stripGenericTypeArgs(typeName) {
  if (!typeName) return '';
  const s = String(typeName);
  const idx = s.indexOf('<');
  if (idx < 0) return s;
  let depth = 0;
  for (let i = idx; i < s.length; i++) {
    const ch = s[i];
    if (ch === '<') depth++;
    else if (ch === '>') {
      depth--;
      if (depth === 0) {
        return stripGenericTypeArgs(s.slice(0, idx) + s.slice(i + 1));
      }
    }
  }
  return s;
}

/** Strip generics anywhere in a diagram line (property types, method params, etc.). */
export function stripGenericsForDiagramLine(line) {
  if (!line) return '';
  let s = String(line);
  let prev;
  do {
    prev = s;
    s = stripGenericTypeArgs(s);
  } while (s !== prev);
  return s;
}

const ELLIPSIS = '\u2026';

/** Max characters for a single line inside diagram nodes (~200px wide, ~11px mono). */
export const DIAGRAM_NODE_TEXT_MAX_CHARS = 28;

export function truncateDiagramText(str, maxChars = DIAGRAM_NODE_TEXT_MAX_CHARS) {
  if (str == null || str === '') return '';
  const t = String(str);
  const n = typeof maxChars === 'number' && maxChars > 0 ? maxChars : DIAGRAM_NODE_TEXT_MAX_CHARS;
  if (t.length <= n) return t;
  return t.slice(0, Math.max(0, n - 1)) + ELLIPSIS;
}

/** Display string for a property row on the diagram canvas (frontend only). */
export function formatDiagramPropertyLine(propName, typeName) {
  const t = formatDiagramTypeName(typeName || '');
  const raw = `${propName || ''}: ${t}`;
  return truncateDiagramText(raw);
}

/** Display string for a method signature row on the diagram canvas (frontend only). */
export function formatDiagramMethodLine(method) {
  if (!method) return '';
  const ret = formatDiagramTypeName(method.returnTypeName || '');
  const params = (method.parameters || []).map(p => formatDiagramTypeName(p.typeName || '')).join(', ');
  const raw = `${ret} ${method.name || ''}(${params})`;
  return truncateDiagramText(raw);
}

function formatDiagramTypeName(typeName) {
  if (!typeName) return '';
  if (typeName.indexOf('`') >= 0 || typeName.indexOf('[[') >= 0) return displayShortName(typeName);
  return stripGenericTypeArgs(typeName);
}

/** Event row from domain `emittedEvents` full names (shortName + ellipsis). */
export function formatDiagramEmittedEventLine(eventFullName) {
  const label = formatDiagramTypeName(eventFullName || '') || shortName(eventFullName || '');
  return formatDiagramEventBadgeLine(label);
}

/** Event row when the label is already a display name (feature editor derived events). */
export function formatDiagramEventBadgeLine(displayLabel) {
  const raw = '\u26A1 ' + String(displayLabel || '');
  return truncateDiagramText(raw);
}

export function esc(str) {
  if (!str) return '';
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

export function escAttr(str) {
  return (str || '').replace(/'/g, "\\'").replace(/"/g, '&quot;');
}

export function kindMeta(kind) {
  const map = {
    aggregates:          { tag: 'AGGREGATE',            color: 'var(--clr-aggregate)',          bg: 'var(--clr-aggregate-bg)' },
    entities:            { tag: 'ENTITY',               color: 'var(--clr-entity)',             bg: 'var(--clr-entity-bg)' },
    valueObjects:        { tag: 'VALUE OBJECT',         color: 'var(--clr-value-object)',       bg: 'var(--clr-value-object-bg)' },
    subTypes:            { tag: 'SUB TYPE',             color: 'var(--clr-sub-type)',           bg: 'var(--clr-sub-type-bg)' },
    domainEvents:        { tag: 'DOMAIN EVENT',         color: 'var(--clr-event)',              bg: 'var(--clr-event-bg)' },
    integrationEvents:   { tag: 'INTEGRATION EVENT',    color: 'var(--clr-integration-event)',  bg: 'var(--clr-integration-event-bg)' },
    commandHandlerTargets: { tag: 'HANDLES TARGET',       color: 'var(--clr-command)',            bg: 'var(--clr-command-bg)' },
    eventHandlers:       { tag: 'EVENT HANDLER',        color: 'var(--clr-handler)',            bg: 'var(--clr-handler-bg)' },
    commandHandlers:     { tag: 'COMMAND HANDLER',      color: 'var(--clr-handler)',            bg: 'var(--clr-handler-bg)' },
    queryHandlers:       { tag: 'QUERY HANDLER',        color: 'var(--clr-handler)',            bg: 'var(--clr-handler-bg)' },
    repositories:        { tag: 'REPOSITORY',           color: 'var(--clr-repository)',         bg: 'var(--clr-repository-bg)' },
    domainServices:      { tag: 'DOMAIN SERVICE',       color: 'var(--clr-service)',            bg: 'var(--clr-service-bg)' },
  };
  return map[kind] || { tag: kind.toUpperCase(), color: 'var(--text-muted)', bg: 'var(--bg-hover)' };
}

export function relKindColor(kind) {
  const map = {
    'Contains':       'var(--clr-entity)',
    'References':     'var(--clr-value-object)',
    'ReferencesById': 'var(--clr-value-object)',
    'Has':            'var(--clr-entity)',
    'HasMany':        'var(--clr-entity)',
    'Emits':          'var(--clr-event)',
    'Handles':        'var(--clr-handler)',
    'Manages':        'var(--clr-repository)',
    'Publishes':      'var(--clr-integration-event)',
  };
  return map[kind] || 'var(--text-muted)';
}

export function syntaxHighlight(json) {
  return json
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"([^"]+)":/g, '<span class="json-key">"$1"</span>:')
    .replace(/: "([^"]*)"/g, ': <span class="json-string">"$1"</span>')
    .replace(/: (\d+)/g, ': <span class="json-number">$1</span>')
    .replace(/: (true|false)/g, ': <span class="json-bool">$1</span>')
    .replace(/: (null)/g, ': <span class="json-null">$1</span>');
}

/** The canonical list of browsable section keys. */
export const ALL_SECTIONS = [
  'aggregates', 'entities', 'valueObjects', 'subTypes', 'domainEvents', 'integrationEvents', 'commandHandlerTargets',
  'eventHandlers', 'commandHandlers', 'queryHandlers',
  'repositories', 'domainServices'
];

/** Maps explorer section keys to main-diagram node `kind` strings (see `diagram.js` KIND_CFG). */
export const SECTION_TO_DIAGRAM_KIND = {
  aggregates: 'aggregate',
  entities: 'entity',
  valueObjects: 'valueObject',
  subTypes: 'subType',
  domainEvents: 'event',
  integrationEvents: 'integrationEvent',
  commandHandlerTargets: 'commandHandlerTarget',
  eventHandlers: 'eventHandler',
  commandHandlers: 'commandHandler',
  queryHandlers: 'queryHandler',
  repositories: 'repository',
  domainServices: 'service',
};

/** Section metadata used in sidebar + overview. */
export const SECTION_META = [
  { key: 'aggregates',           label: 'Aggregates',           color: 'var(--clr-aggregate)',          tag: 'AGG',  bg: 'var(--clr-aggregate-bg)' },
  { key: 'entities',             label: 'Entities',             color: 'var(--clr-entity)',             tag: 'ENT',  bg: 'var(--clr-entity-bg)' },
  { key: 'valueObjects',         label: 'Value Objects',        color: 'var(--clr-value-object)',       tag: 'VO',   bg: 'var(--clr-value-object-bg)' },
  { key: 'subTypes',              label: 'Sub Types',            color: 'var(--clr-sub-type)',           tag: 'SUB',  bg: 'var(--clr-sub-type-bg)' },
  { key: 'domainEvents',         label: 'Domain Events',        color: 'var(--clr-event)',              tag: 'EVT',  bg: 'var(--clr-event-bg)' },
  { key: 'integrationEvents',    label: 'Integration Events',   color: 'var(--clr-integration-event)',  tag: 'INT',  bg: 'var(--clr-integration-event-bg)' },
  { key: 'commandHandlerTargets', label: 'Cmd handler targets', color: 'var(--clr-command)',            tag: 'CHT',  bg: 'var(--clr-command-bg)' },
  { key: 'eventHandlers',        label: 'Event Handlers',       color: 'var(--clr-handler)',            tag: 'HDL',  bg: 'var(--clr-handler-bg)' },
  { key: 'commandHandlers',      label: 'Command Handlers',     color: 'var(--clr-handler)',            tag: 'CMD',  bg: 'var(--clr-handler-bg)' },
  { key: 'queryHandlers',        label: 'Query Handlers',       color: 'var(--clr-handler)',            tag: 'QRY',  bg: 'var(--clr-handler-bg)' },
  { key: 'repositories',         label: 'Repositories',         color: 'var(--clr-repository)',         tag: 'REPO', bg: 'var(--clr-repository-bg)' },
  { key: 'domainServices',       label: 'Domain Services',      color: 'var(--clr-service)',            tag: 'SVC',  bg: 'var(--clr-service-bg)' },
];
