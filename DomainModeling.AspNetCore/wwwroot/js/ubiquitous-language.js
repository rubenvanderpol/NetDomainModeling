/**
 * Ubiquitous language reader tab — fetches structured doc from the API and renders by bounded context.
 */
import { esc, escAttr } from './helpers.js';
import { renderTabBar } from './tabs.js';

const REL_COLORS = {
  Has: '#60a5fa',
  HasMany: '#60a5fa',
  Contains: '#60a5fa',
  References: '#34d399',
  ReferencesById: '#34d399',
};

function kindPillClass(kindLabel) {
  const k = (kindLabel || '').toLowerCase();
  if (k.includes('value object')) return 'ul-kind-pill vo';
  if (k.includes('sub-type')) return 'ul-kind-pill st';
  if (k.includes('aggregate')) return 'ul-kind-pill agg';
  return 'ul-kind-pill';
}

function renderRelations(relations, depth) {
  const items = relations?.items || [];
  let html = '<div class="ul-relations">';
  html += '<h4>Relations</h4>';
  if (items.length === 0) {
    html += '<p class="ul-empty" style="padding:0;margin:0 0 4px">None from this concept.</p>';
  } else {
    html += '<ul class="ul-rel-list">';
    for (const r of items) {
      const color = REL_COLORS[r.kind] || '#94a3b8';
      const via = r.viaLabel
        ? `<span class="ul-rel-via">via <code>${esc(r.viaLabel)}</code></span>`
        : '';
      html += `<li>
        <span class="ul-rel-phrase" style="color:${color}">${esc(r.phrase)}</span>
        <span class="ul-rel-target">${esc(r.targetDisplayName)}</span>
        ${via}
      </li>`;
    }
    html += '</ul>';
  }
  html += '</div>';
  return html;
}

function renderConceptBlock(block, depth) {
  const isRoot = depth === 0;
  const wrapClass = isRoot ? 'ul-root' : 'ul-nested-block';
  const headClass = isRoot ? 'ul-root-header' : 'ul-nested-head';
  let html = `<div class="${wrapClass}" data-depth="${depth}">`;

  if (isRoot) {
    html += `<div class="${headClass}">`;
    html += `<h3 class="ul-root-title">${esc(block.displayName)}</h3>`;
    html += `<div class="ul-root-meta"><span>Aggregate</span> · <code>${esc(block.typeName)}</code></div>`;
    html += '</div>';
  } else {
    html += `<div class="${headClass}">`;
    html += `<span class="ul-nested-name">${esc(block.displayName)}</span>`;
    html += `<span class="${kindPillClass(block.kindLabel)}">${esc(block.kindLabel)}</span>`;
    html += `<div class="ul-root-meta" style="margin-top:0;width:100%"><code>${esc(block.typeName)}</code></div>`;
    html += '</div>';
  }

  if (block.description) {
    html += `<p class="ul-desc">${esc(block.description)}</p>`;
  }

  html += renderRelations(block.relations, depth);

  const children = block.linkedConcepts || [];
  if (children.length > 0) {
    html += '<div class="ul-nested">';
    for (const ch of children) {
      html += renderConceptBlock(ch, depth + 1);
    }
    html += '</div>';
  }

  html += '</div>';
  return html;
}

function renderBoundedContext(bc) {
  const safeId = escAttr(bc.name || 'ctx');
  let html = `<section class="ul-bc" id="ul-bc-${safeId.replace(/[^a-zA-Z0-9_-]/g, '_')}">`;
  html += '<header class="ul-bc-header">';
  html += '<div class="ul-bc-icon" aria-hidden="true">◇</div>';
  html += `<h2>${esc(bc.name)}</h2>`;
  html += '</header>';
  html += '<div class="ul-bc-body">';

  html += '<div class="ul-section">';
  html += '<h3>Aggregates</h3>';
  if (bc.aggregates?.emptyMessage) {
    html += `<p class="ul-empty">${esc(bc.aggregates.emptyMessage)}</p>`;
  } else {
    for (const root of bc.aggregates.roots || []) {
      html += renderConceptBlock(root, 0);
    }
  }
  html += '</div>';

  html += '<div class="ul-section">';
  html += '<h3>Domain events</h3>';
  if (bc.domainEvents?.emptyMessage) {
    html += `<p class="ul-empty">${esc(bc.domainEvents.emptyMessage)}</p>`;
  } else {
    html += '<div class="ul-events">';
    for (const ev of bc.domainEvents.items || []) {
      html += '<article class="ul-event-card">';
      html += `<h4>${esc(ev.displayName)}</h4>`;
      if (ev.description) {
        html += `<p class="ul-desc">${esc(ev.description)}</p>`;
      }
      html += `<div class="ul-root-meta"><code>${esc(ev.typeName)}</code></div>`;
      html += '</article>';
    }
    html += '</div>';
  }
  html += '</div>';

  html += '</div></section>';
  return html;
}

/**
 * @param {{ traceLayout?: boolean }} [opts]
 */
export function renderUbiquitousLanguageView(opts = {}) {
  const traceLayout = opts.traceLayout === true;
  const activeTab = opts.activeTab || (traceLayout ? 'trace' : 'ubiquitous-language');
  let html = renderTabBar(activeTab);
  html += '<div class="ubiquitous-lang-page ubiquitous-lang-page--loading" id="ulPageRoot">';
  html += '<p class="ul-loading" id="ulStatus">Loading ubiquitous language…</p>';
  html += '</div>';
  return html;
}

/**
 * @param {string} baseUrl — e.g. <code>/domain-model</code> (no trailing slash)
 * @param {Set<string> | null | undefined} selectedContextNames — when set, only these bounded contexts are shown
 */
export async function mountUbiquitousLanguage(baseUrl, selectedContextNames) {
  const root = document.getElementById('ulPageRoot');
  const status = document.getElementById('ulStatus');
  if (!root) return;

  const url = `${baseUrl.replace(/\/$/, '')}/ubiquitous-language`;
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const doc = await res.json();

    let contexts = doc.boundedContexts || [];
    if (selectedContextNames && selectedContextNames.size > 0) {
      contexts = contexts.filter((bc) => selectedContextNames.has(bc.name));
    }

    let html = '';
    html += '<div class="ul-hero">';
    html += `<h1>${esc(doc.title || 'Ubiquitous language')}</h1>`;
    if (doc.introduction) {
      html += `<p>${esc(doc.introduction)}</p>`;
    }
    html += '</div>';

    if (contexts.length === 0) {
      html += '<p class="ul-empty">No bounded contexts match the current selection (or the model is empty).</p>';
    } else {
      for (const bc of contexts) {
        html += renderBoundedContext(bc);
      }
    }

    root.classList.remove('ubiquitous-lang-page--loading');
    root.innerHTML = html;
  } catch (e) {
    console.error('Ubiquitous language load failed', e);
    if (status) {
      status.textContent = 'Could not load ubiquitous language. Is the app running the latest DomainModeling.AspNetCore?';
      status.className = 'ul-error';
    } else {
      root.innerHTML = `<p class="ul-error">Could not load ubiquitous language (${esc(String(e.message || e))}).</p>`;
    }
  }
}
