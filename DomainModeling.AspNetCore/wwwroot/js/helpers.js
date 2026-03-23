/**
 * Shared helpers used across all explorer modules.
 */

export function shortName(fullName) {
  if (!fullName) return '';
  const parts = fullName.split('.');
  return parts[parts.length - 1];
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

/**
 * Checkbox row for choosing which bounded contexts are merged into the explorer.
 * Used on the diagram and feature editor (issue #20); toggles via window.__nav.toggleContext.
 */
export function renderBoundedContextSelectorHtml(allContexts, selectedNames) {
  if (!allContexts || allContexts.length <= 1) return '';
  const set = selectedNames instanceof Set
    ? selectedNames
    : new Set(Array.isArray(selectedNames) ? selectedNames : []);
  let html = '<div class="bc-selector-inline" role="group" aria-label="Bounded contexts">';
  html += '<span class="bc-selector-label">Bounded contexts</span>';
  html += `<span class="bc-selector-badge">${set.size}/${allContexts.length}</span>`;
  for (const c of allContexts) {
    const checked = set.has(c.name);
    html += `<label class="ctx-option bc-selector-chip${checked ? ' active' : ''}" onclick="event.stopPropagation()">`;
    html += `<input type="checkbox" ${checked ? 'checked' : ''} onchange="window.__nav.toggleContext('${escAttr(c.name)}')" />`;
    html += `<span class="ctx-name">${esc(c.name)}</span></label>`;
  }
  html += '</div>';
  return html;
}
