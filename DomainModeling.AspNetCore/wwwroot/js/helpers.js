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

/** Merge an array of bounded-context objects into one view (same shape as API nodes). */
export function mergeBoundedContextNodes(selected) {
  if (!selected || selected.length === 0) return null;
  if (selected.length === 1) return selected[0];
  const merged = { name: selected.map(c => c.name).join(' + ') };
  for (const key of ALL_SECTIONS) {
    merged[key] = selected.flatMap(c => c[key] || []);
  }
  merged.relationships = selected.flatMap(c => c.relationships || []);
  return merged;
}

/**
 * Inner HTML for a multi-select .rel-dropdown (Node Types / Relations pattern).
 * Caller places this inside an existing `<div class="rel-dropdown">`.
 */
export function renderMultiSelectDropdownInnerHtml(opts) {
  const {
    triggerId, menuId, triggerIconHtml, triggerLabel, badgeText, triggerTitle,
    toggleHandler, actionButtonsHtml, items,
  } = opts;

  let h = `<button type="button" class="rel-dropdown-trigger" id="${escAttr(triggerId)}" onclick="${toggleHandler}" title="${escAttr(triggerTitle || '')}">`;
  if (triggerIconHtml) h += triggerIconHtml;
  h += `<span>${esc(triggerLabel)}</span>`;
  if (badgeText) h += `<span class="rel-hidden-count">${esc(badgeText)}</span>`;
  h += '<span class="rel-chevron">▾</span></button>';
  h += `<div class="rel-dropdown-menu" id="${escAttr(menuId)}">`;
  if (actionButtonsHtml) {
    h += `<div class="rel-dropdown-actions">${actionButtonsHtml}</div>`;
  }
  for (const it of items) {
    const extra = it.dataAttr || '';
    h += `<div class="rel-dropdown-item${it.checked ? ' checked' : ''}" onclick="${it.rowClick}"${extra}>`;
    h += `<span class="rel-check">${it.checked ? '✓' : ''}</span>`;
    if (it.prefixHtml) h += it.prefixHtml;
    h += `<span class="rel-kind-label">${esc(it.label)}</span>`;
    if (it.suffixHtml) h += it.suffixHtml;
    h += '</div>';
  }
  h += '</div>';
  return h;
}

/** Open/close dropdown; closes on outside click (shared with diagram + feature editor). */
export function toggleDropdownMenu(menuId, triggerId) {
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

/**
 * Multi-select bounded-context dropdown (same UX as diagram Node Types).
 * @param {object} o
 * @param {{ name: string }[]} o.allContexts
 * @param {Set<string>|string[]} o.selectedSet
 * @param {string} o.triggerId
 * @param {string} o.menuId
 * @param {string} o.toggleMenuCall - onclick for trigger
 * @param {string} o.toggleContextCall - global fn name; invoked as toggleContextCall(event,'Name')
 * @param {string} o.showAllCall - onclick for Show all button
 * @param {string} [o.triggerLabel]
 * @param {string} [o.triggerTitle]
 */
export function renderBoundedContextMultiDropdownInner(o) {
  const all = o.allContexts || [];
  if (all.length <= 1) return '';
  const selected = o.selectedSet instanceof Set ? o.selectedSet : new Set(o.selectedSet || []);
  const badgeText = `${selected.size}/${all.length}`;
  const actionButtonsHtml =
    `<button type="button" onclick="${o.showAllCall}">Show all</button>`;
  const items = all.map((c) => ({
    label: c.name,
    checked: selected.has(c.name),
    dataAttr: ` data-bounded-context="${esc(c.name)}"`,
    rowClick: `${o.toggleContextCall}(event,'${escAttr(c.name)}')`,
    prefixHtml: '<span class="diagram-kind-dot" style="background:var(--accent)"></span>',
  }));
  return renderMultiSelectDropdownInnerHtml({
    triggerId: o.triggerId,
    menuId: o.menuId,
    triggerIconHtml: '<span style="font-size:10px;opacity:.7">◇</span>',
    triggerLabel: o.triggerLabel || 'Contexts',
    badgeText,
    triggerTitle: o.triggerTitle || 'Bounded contexts',
    toggleHandler: o.toggleMenuCall,
    actionButtonsHtml,
    items,
  });
}
