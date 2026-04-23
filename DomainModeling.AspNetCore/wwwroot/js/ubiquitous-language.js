/**
 * Ubiquitous language reader tab — fetches structured doc from the API and renders by bounded context.
 */
import { esc, escAttr } from './helpers.js';
import { renderTabBar } from './tabs.js';

const UL_LANG_KEY = 'domainModelUbiquitousLanguageLang';
const UL_BC_KEY = 'domainModelUbiquitousLanguageBc';

const REL_COLORS = {
  Has: '#60a5fa',
  HasMany: '#60a5fa',
  Contains: '#60a5fa',
  References: '#34d399',
  ReferencesById: '#34d399',
};

function kindPillClass(kindLabel) {
  const k = (kindLabel || '').toLowerCase();
  if (k.includes('value') || k.includes('waarde')) return 'ul-kind-pill vo';
  if (k.includes('sub') && (k.includes('type') || k.includes('typ'))) return 'ul-kind-pill st';
  if (k.includes('aggreg')) return 'ul-kind-pill agg';
  return 'ul-kind-pill';
}

function getStoredUlBcFilter() {
  try {
    return localStorage.getItem(UL_BC_KEY) || '';
  } catch {
    return '';
  }
}

function setStoredUlBcFilter(name) {
  try {
    if (name) localStorage.setItem(UL_BC_KEY, name);
    else localStorage.removeItem(UL_BC_KEY);
  } catch { /* ignore */ }
}

function renderRelations(relations, labels) {
  const items = relations?.items || [];
  const relTitle = labels.relationsHeadingLabel || 'Relations';
  const none = labels.noRelationsMessage || 'None from this concept.';
  const viaWord = labels.relationshipViaWord || 'via';

  let html = '<div class="ul-relations">';
  html += `<h4>${esc(relTitle)}</h4>`;
  if (items.length === 0) {
    html += `<p class="ul-empty" style="padding:0;margin:0 0 4px">${esc(none)}</p>`;
  } else {
    html += '<ul class="ul-rel-list">';
    for (const r of items) {
      const color = REL_COLORS[r.kind] || '#94a3b8';
      const via = r.viaLabel
        ? `<span class="ul-rel-via">${esc(viaWord)} <code>${esc(r.viaLabel)}</code></span>`
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

function renderConceptBlock(block, depth, labels) {
  const isRoot = depth === 0;
  const wrapClass = isRoot ? 'ul-root' : 'ul-nested-block';
  const headClass = isRoot ? 'ul-root-header' : 'ul-nested-head';
  const typePrefix = labels.typeLabelPrefix || 'Type';
  let html = `<div class="${wrapClass}" data-depth="${depth}">`;

  if (isRoot) {
    html += `<div class="${headClass}">`;
    html += `<h3 class="ul-root-title">${esc(block.displayName)}</h3>`;
    html += `<div class="ul-root-meta"><span>${esc(block.kindLabel || 'aggregate')}</span> · <code>${esc(block.typeName)}</code></div>`;
    html += '</div>';
  } else {
    html += `<div class="${headClass}">`;
    html += `<span class="ul-nested-name">${esc(block.displayName)}</span>`;
    html += `<span class="${kindPillClass(block.kindLabel)}">${esc(block.kindLabel)}</span>`;
    html += `<div class="ul-root-meta" style="margin-top:0;width:100%"><span>${esc(typePrefix)}</span> · <code>${esc(block.typeName)}</code></div>`;
    html += '</div>';
  }

  if (block.description) {
    html += `<p class="ul-desc">${esc(block.description)}</p>`;
  }

  html += renderRelations(block.relations, labels);

  const children = block.linkedConcepts || [];
  if (children.length > 0) {
    html += '<div class="ul-nested">';
    for (const ch of children) {
      html += renderConceptBlock(ch, depth + 1, labels);
    }
    html += '</div>';
  }

  html += '</div>';
  return html;
}

function renderBoundedContext(bc, labels) {
  const safeId = escAttr(bc.name || 'ctx');
  const aggSec = labels.aggregatesSectionLabel || 'Aggregates';
  const evSec = labels.domainEventsSectionLabel || 'Domain events';
  const typePrefix = labels.typeLabelPrefix || 'Type';

  let html = `<section class="ul-bc" id="ul-bc-${safeId.replace(/[^a-zA-Z0-9_-]/g, '_')}">`;
  html += '<header class="ul-bc-header">';
  html += '<div class="ul-bc-icon" aria-hidden="true">◇</div>';
  html += `<h2>${esc(bc.name)}</h2>`;
  html += '</header>';
  html += '<div class="ul-bc-body">';

  html += '<div class="ul-section">';
  html += `<h3>${esc(aggSec)}</h3>`;
  if (bc.aggregates?.emptyMessage) {
    html += `<p class="ul-empty">${esc(bc.aggregates.emptyMessage)}</p>`;
  } else {
    for (const root of bc.aggregates.roots || []) {
      html += renderConceptBlock(root, 0, labels);
    }
  }
  html += '</div>';

  html += '<div class="ul-section">';
  html += `<h3>${esc(evSec)}</h3>`;
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
      html += `<div class="ul-root-meta"><span>${esc(typePrefix)}</span> · <code>${esc(ev.typeName)}</code></div>`;
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
  html += '<div class="ul-top-toolbar" id="ulTopToolbar">';
  html += '<div class="ul-bc-filter-wrap" id="ulBcFilterWrap" style="display:none">';
  html += '<label class="ul-bc-filter-label" for="ulBcSelect">Bounded context</label>';
  html += '<select id="ulBcSelect" class="ul-bc-select" onchange="window.__ulOnBcChange(this.value)">';
  html += '</select>';
  html += '</div>';
  html += '<div class="ul-lang-toolbar-inner" id="ulLangToolbarInner" style="display:none">';
  html += '<label class="ul-lang-label" for="ulLangSelect">Language</label>';
  html += '<select id="ulLangSelect" class="ul-lang-select" onchange="window.__ulOnLangChange(this.value)">';
  html += '</select>';
  html += '</div>';
  html += '<div class="ul-toolbar-spacer"></div>';
  html += '<button type="button" class="ul-download-btn" id="ulDownloadBtn" disabled title="Download Markdown (same pipeline as server export)" onclick="window.__ulDownloadMarkdown()">⬇ Markdown</button>';
  html += '</div>';
  html += '<div class="ubiquitous-lang-page ubiquitous-lang-page--loading" id="ulPageRoot">';
  html += '<p class="ul-loading" id="ulStatus">Loading ubiquitous language…</p>';
  html += '</div>';
  return html;
}

function getStoredUlLang() {
  try {
    return localStorage.getItem(UL_LANG_KEY) || '';
  } catch {
    return '';
  }
}

function setStoredUlLang(lang) {
  try {
    if (lang) localStorage.setItem(UL_LANG_KEY, lang);
    else localStorage.removeItem(UL_LANG_KEY);
  } catch { /* ignore */ }
}

function buildUlQuery(lang, contextName) {
  const params = new URLSearchParams();
  if (lang) params.set('lang', lang);
  if (contextName) params.set('context', contextName);
  const s = params.toString();
  return s ? `?${s}` : '';
}

/**
 * @param {string} baseUrl
 * @param {Set<string> | null | undefined} sidebarSelectedContexts — from sidebar checkboxes (used as default when no Language-tab filter)
 * @param {string | undefined} requestedLang
 * @param {string[] | null | undefined} allContextNames — all bounded context names in the graph (for top dropdown)
 */
export async function mountUbiquitousLanguage(baseUrl, sidebarSelectedContexts, requestedLang, allContextNames) {
  const root = document.getElementById('ulPageRoot');
  const status = document.getElementById('ulStatus');
  const bcWrap = document.getElementById('ulBcFilterWrap');
  const bcSelect = document.getElementById('ulBcSelect');
  const toolbarInner = document.getElementById('ulLangToolbarInner');
  const select = document.getElementById('ulLangSelect');
  const downloadBtn = document.getElementById('ulDownloadBtn');
  if (!root) return;

  window.__ulDownloadMarkdown = () => {};
  if (downloadBtn) downloadBtn.disabled = true;

  const names = (allContextNames || []).filter(Boolean);
  const storedBc = getStoredUlBcFilter();
  let effectiveBc = '';
  if (names.length > 1) {
    if (storedBc && names.includes(storedBc)) {
      effectiveBc = storedBc;
    } else if (sidebarSelectedContexts && sidebarSelectedContexts.size === 1) {
      const only = [...sidebarSelectedContexts][0];
      effectiveBc = names.includes(only) ? only : '';
    }
  }

  const langParam = requestedLang !== undefined && requestedLang !== null && requestedLang !== ''
    ? requestedLang
    : getStoredUlLang();
  const qs = buildUlQuery(langParam, effectiveBc);
  const url = `${baseUrl.replace(/\/$/, '')}/ubiquitous-language${qs}`;
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const doc = await res.json();

    if (bcWrap && bcSelect && names.length > 1) {
      bcWrap.style.display = '';
      let optsHtml = '<option value="">All bounded contexts</option>';
      for (const n of names) {
        optsHtml += `<option value="${escAttr(n)}"${n === effectiveBc ? ' selected' : ''}>${esc(n)}</option>`;
      }
      bcSelect.innerHTML = optsHtml;
    } else if (bcWrap) {
      bcWrap.style.display = 'none';
    }

    window.__ulOnBcChange = (name) => {
      setStoredUlBcFilter(name);
      void mountUbiquitousLanguage(baseUrl, sidebarSelectedContexts, langParam, names);
    };

    const langs = doc.availableLanguages || [];
    if (toolbarInner && select && langs.length > 1) {
      toolbarInner.style.display = '';
      const current = doc.language || '';
      select.innerHTML = langs.map((l) =>
        `<option value="${escAttr(l)}"${l === current ? ' selected' : ''}>${esc(l)}</option>`,
      ).join('');
    } else if (toolbarInner) {
      toolbarInner.style.display = 'none';
    }

    const activeLang = doc.language || '';
    window.__ulDownloadMarkdown = async () => {
      const q = buildUlQuery(activeLang, effectiveBc);
      const mdUrl = `${baseUrl.replace(/\/$/, '')}/ubiquitous-language.md${q}`;
      try {
        const res = await fetch(mdUrl);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const blob = await res.blob();
        const cd = res.headers.get('Content-Disposition') || '';
        const m = cd.match(/filename="?([^";]+)"?/i);
        const filename = m ? m[1] : 'ubiquitous-language.md';
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(a.href);
      } catch (err) {
        console.error('Ubiquitous language Markdown download failed', err);
      }
    };
    if (downloadBtn) downloadBtn.disabled = false;

    window.__ulOnLangChange = (lang) => {
      setStoredUlLang(lang);
      void mountUbiquitousLanguage(baseUrl, sidebarSelectedContexts, lang, names);
    };

    let contexts = doc.boundedContexts || [];
    if (sidebarSelectedContexts && sidebarSelectedContexts.size > 0 && !effectiveBc) {
      contexts = contexts.filter((bc) => sidebarSelectedContexts.has(bc.name));
    }

    const labels = {
      aggregatesSectionLabel: doc.aggregatesSectionLabel,
      domainEventsSectionLabel: doc.domainEventsSectionLabel,
      relationsHeadingLabel: doc.relationsHeadingLabel,
      typeLabelPrefix: doc.typeLabelPrefix,
      noRelationsMessage: doc.noRelationsMessage,
      relationshipViaWord: doc.relationshipViaWord,
    };

    let html = '';
    html += '<div class="ul-hero">';
    html += `<h1>${esc(doc.title || 'Ubiquitous language')}</h1>`;
    if (doc.language && langs.length > 1) {
      html += `<p class="ul-lang-badge">${esc(doc.language)}</p>`;
    }
    if (doc.introduction) {
      html += `<p>${esc(doc.introduction)}</p>`;
    }
    html += '</div>';

    if (contexts.length === 0) {
      html += '<p class="ul-empty">No bounded contexts match the current selection (or the model is empty).</p>';
    } else {
      for (const bc of contexts) {
        html += renderBoundedContext(bc, labels);
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
