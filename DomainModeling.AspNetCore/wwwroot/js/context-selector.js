/**
 * Bounded context multi-select toolbar (diagram + feature editor).
 * Issue #20: selection lives with the canvas views instead of the explorer sidebar.
 */
import { esc, escAttr } from './helpers.js';

/**
 * @param {{ name: string }[] | null | undefined} allContexts
 * @param {Set<string> | null | undefined} selectedNames
 * @returns {string} HTML fragment (empty when there is only one context)
 */
export function renderBoundedContextToolbar(allContexts, selectedNames) {
  if (!allContexts || allContexts.length <= 1) return '';

  const sel = selectedNames || new Set();
  let html = '<div class="bc-toolbar" role="region" aria-label="Bounded context selection">';
  html += '<div class="nav-section ctx-selector bc-toolbar-inner">';
  html += `<div class="nav-section-header" onclick="window.__nav.toggleSection(this)">
      <span class="dot" style="width:6px;height:6px;border-radius:50%;background:var(--accent)"></span>
      Bounded Contexts
      <span class="badge">${sel.size}/${allContexts.length}</span>
      <span class="chevron">▼</span>
    </div>`;
  html += '<div class="nav-items">';
  for (const c of allContexts) {
    const checked = sel.has(c.name);
    html += `<label class="nav-item ctx-option${checked ? ' active' : ''}" onclick="event.stopPropagation()">
      <input type="checkbox" ${checked ? 'checked' : ''}
             onchange="window.__nav.toggleContext('${escAttr(c.name)}')" />
      <span class="ctx-name">${esc(c.name)}</span>
    </label>`;
  }
  html += '</div></div></div>';
  return html;
}
