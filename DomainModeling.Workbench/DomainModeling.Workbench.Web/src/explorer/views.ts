/**
 * Overview + Cards + Detail views.
 */
import {
  esc, escAttr, shortName, kindMeta, relKindColor, syntaxHighlight, SECTION_META, SECTION_TO_DIAGRAM_KIND,
} from './helpers';
import { isDiagramNodeHidden, getDiagramState } from './diagram';
import { renderTabBar } from './tabs';

// ── Overview ─────────────────────────────────────────
export function renderOverview(ctx, exports) {
  const stats = [
    { label: 'Aggregates',          count: (ctx.aggregates||[]).length,            color: 'var(--clr-aggregate)' },
    { label: 'Entities',            count: (ctx.entities||[]).length,              color: 'var(--clr-entity)' },
    { label: 'Value Objects',       count: (ctx.valueObjects||[]).length,          color: 'var(--clr-value-object)' },
    { label: 'Sub Types',           count: (ctx.subTypes||[]).length,              color: 'var(--clr-sub-type)' },
    { label: 'Domain Events',       count: (ctx.domainEvents||[]).length,          color: 'var(--clr-event)' },
    { label: 'Integration Events',  count: (ctx.integrationEvents||[]).length,     color: 'var(--clr-integration-event)' },
    { label: 'Handlers',            count: ((ctx.eventHandlers||[]).length + (ctx.commandHandlers||[]).length + (ctx.queryHandlers||[]).length), color: 'var(--clr-handler)' },
    { label: 'Repositories',        count: (ctx.repositories||[]).length,          color: 'var(--clr-repository)' },
    { label: 'Services',            count: (ctx.domainServices||[]).length,        color: 'var(--clr-service)' },
    { label: 'Relationships',       count: (ctx.relationships||[]).length,         color: 'var(--clr-relationship)' },
  ];

  let html = renderTabBar('overview');

  if (exports && exports.length > 0) {
    html += `<div style="display:flex;justify-content:flex-end;gap:8px;padding:0 4px 8px;flex-wrap:wrap">`;
    for (const exp of exports) {
      html += `<button onclick="window.__downloadExport('${escAttr(exp.name)}')"
         style="text-decoration:none;padding:6px 14px;border-radius:6px;font-size:.82rem;
                border:1px solid var(--border);color:var(--text);background:var(--bg-card);cursor:pointer;
                display:inline-flex;align-items:center;gap:6px;transition:background .15s"
         onmouseover="this.style.background='var(--bg-hover)'"
         onmouseout="this.style.background='var(--bg-card)'"
      >\u2B07 ${esc(exp.name)} (.${esc(exp.extension)})</button>`;
    }
    html += '</div>';
  }

  html += '<div class="stat-grid">';
  for (const s of stats) {
    if (s.count === 0) continue;
    html += `<div class="stat-card">
      <div class="stat-value" style="color:${s.color}">${s.count}</div>
      <div class="stat-label">${s.label}</div>
    </div>`;
  }
  html += '</div>';

  for (const sec of SECTION_META) {
    const items = ctx[sec.key] || [];
    if (items.length === 0) continue;

    html += `<div class="section-title"><span class="dot" style="background:${sec.color}"></span>${sec.label}</div>`;
    html += '<div class="card-grid">';
    for (const item of items) {
      html += renderCard(item, sec);
    }
    html += '</div>';
  }

  return html;
}

function renderCard(item, sec) {
  let html = `<div class="card" onclick="window.__nav.showDetail('${sec.key}', '${escAttr(item.fullName)}')">`;
  html += `<div class="card-header">
    <span class="card-tag" style="color:${sec.color};background:${sec.bg}">${sec.tag}</span>
    <span class="card-name">${esc(item.name)}</span>
  </div>`;

  if (item.description) {
    html += `<div class="card-desc">${esc(item.description)}</div>`;
  }

  if (item.properties && item.properties.length > 0) {
    const preview = item.properties.slice(0, 4);
    html += '<div class="card-props"><table><tr><th>Property</th><th>Type</th></tr>';
    for (const p of preview) {
      html += `<tr><td>${esc(p.name)}</td><td>${esc(p.typeName)}</td></tr>`;
    }
    if (item.properties.length > 4) {
      html += `<tr><td colspan="2" style="color:var(--text-dim)">+${item.properties.length - 4} more…</td></tr>`;
    }
    html += '</table></div>';
  }

  if (item.emittedEvents && item.emittedEvents.length > 0) {
    html += '<div class="pill-list">';
    for (const e of item.emittedEvents) {
      html += `<span class="pill" style="border-color:var(--clr-event);color:var(--clr-event)">⚡ ${esc(shortName(e))}</span>`;
    }
    html += '</div>';
  }

  if (item.methods && item.methods.length > 0) {
    html += '<div class="pill-list">';
    for (const m of item.methods) {
      const sig = m.name + '(' + (m.parameters || []).map(p => p.typeName).join(', ') + ')';
      html += `<span class="pill" style="border-color:var(--clr-service);color:var(--clr-service)">${esc(sig)}</span>`;
    }
    html += '</div>';
  }

  if (item.handles && item.handles.length > 0) {
    html += '<div class="pill-list">';
    for (const h of item.handles) {
      html += `<span class="pill" style="border-color:var(--clr-handler);color:var(--clr-handler)">→ ${esc(shortName(h))}</span>`;
    }
    html += '</div>';
  }

  if (item.managesAggregate) {
    html += `<div class="pill-list">
      <span class="pill" style="border-color:var(--clr-aggregate);color:var(--clr-aggregate)">manages ${esc(shortName(item.managesAggregate))}</span>
    </div>`;
  }

  html += '</div>';
  return html;
}

// ── Detail ───────────────────────────────────────────
export function renderDetailView(kind, item, ctx, metadata, saveMetadataFn) {
  const meta = kindMeta(kind);
  const existing = (metadata || {})[item.fullName] || {};
  const diagramKind = SECTION_TO_DIAGRAM_KIND[kind];
  const st = getDiagramState();
  const kindFiltered = diagramKind && st && st.hiddenKinds.has(diagramKind);
  const perTypeHidden = diagramKind && isDiagramNodeHidden(item.fullName);
  const diagramHidden = kindFiltered || perTypeHidden;
  const detailCbDisabled = kindFiltered ? ' disabled' : '';

  let html = '<div class="detail-panel">';
  html += `<div class="detail-back" onclick="window.__nav.switchTab('diagram')">← Back to diagram</div>`;
  html += `<div style="margin-bottom:4px"><span class="card-tag" style="color:${meta.color};background:${meta.bg}">${meta.tag}</span></div>`;
  html += `<h2 class="detail-title">${esc(item.name)}</h2>`;
  html += `<div class="detail-fullname">${esc(item.fullName)}</div>`;

  if (diagramKind) {
    html += `<div class="detail-section detail-diagram-visibility">
      <label class="detail-diagram-visibility-label">
        <input type="checkbox" id="detailDiagramVisible"${diagramHidden ? '' : ' checked'}${detailCbDisabled}
               onchange="window.__nav.toggleDiagramVisibility('${escAttr(item.fullName)}', this.checked)" />
        <span>Show on main diagram</span>
      </label>
      ${kindFiltered ? '<p class="detail-diagram-visibility-note">This node type is hidden via the diagram toolbar; turn the type back on to show this item.</p>' : ''}
    </div>`;
  }

  if (item.description) {
    html += `<div class="detail-desc">${esc(item.description)}</div>`;
  }

  // Editable alias & description
  html += `<div class="detail-section detail-metadata-edit">
    <h3>Custom Metadata</h3>
    <label style="display:block;margin-bottom:8px;color:var(--text-muted);font-size:.82rem">
      Alias
      <input id="metaAlias" type="text" value="${escAttr(existing.alias || '')}"
             placeholder="Display name override…"
             style="display:block;width:100%;margin-top:4px;padding:6px 10px;border-radius:6px;
                    border:1px solid var(--border);background:var(--bg-card);color:var(--text);font-size:.88rem" />
    </label>
    <label style="display:block;margin-bottom:8px;color:var(--text-muted);font-size:.82rem">
      Description
      <textarea id="metaDescription" rows="3" placeholder="Custom description…"
                style="display:block;width:100%;margin-top:4px;padding:6px 10px;border-radius:6px;
                       border:1px solid var(--border);background:var(--bg-card);color:var(--text);
                       font-size:.88rem;resize:vertical">${esc(existing.description || '')}</textarea>
    </label>
    <button onclick="(async()=>{
      const alias=document.getElementById('metaAlias').value;
      const desc=document.getElementById('metaDescription').value;
      await window.__saveMetadata('${escAttr(item.fullName)}',alias,desc);
      this.textContent='Saved ✓';setTimeout(()=>this.textContent='Save',1500);
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
      const refHtml = p.referenceTypeName
        ? `<span class="rel-link" onclick="window.__nav.navigateTo('${escAttr(p.referenceTypeName)}')">${esc(shortName(p.referenceTypeName))}</span>`
        : '<span style="color:var(--text-dim)">—</span>';
      html += `<tr><td>${esc(p.name)}</td><td>${esc(p.typeName)}${p.isCollection ? ' <span style="color:var(--clr-value-object)">[∗]</span>' : ''}</td><td>${refHtml}</td></tr>`;
    }
    html += '</table></div></div>';
  }

  if (item.childEntities && item.childEntities.length > 0) {
    html += '<div class="detail-section"><h3>Child Entities</h3>';
    for (const c of item.childEntities) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(c)}')"><span class="rel-arrow">◆</span> ${esc(shortName(c))}</div><br/>`;
    }
    html += '</div>';
  }

  if (item.emittedEvents && item.emittedEvents.length > 0) {
    html += '<div class="detail-section"><h3>Emitted Events</h3>';
    for (const e of item.emittedEvents) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(e)}')"><span class="rel-arrow">⚡</span> ${esc(shortName(e))}</div><br/>`;
    }
    html += '</div>';
  }

  if (item.methods && item.methods.length > 0) {
    html += '<div class="detail-section"><h3>Methods</h3>';
    html += '<div class="detail-props"><table><tr><th>Method</th><th>Parameters</th><th>Returns</th></tr>';
    for (const m of item.methods) {
      const params = (m.parameters || []).map(p => esc(p.typeName) + ' ' + esc(p.name)).join(', ') || '<span style="color:var(--text-dim)">—</span>';
      html += `<tr><td>${esc(m.name)}</td><td>${params}</td><td>${esc(m.returnTypeName)}</td></tr>`;
    }
    html += '</table></div></div>';
  }

  if (item.emittedBy && item.emittedBy.length > 0) {
    html += '<div class="detail-section"><h3>Emitted By</h3>';
    for (const e of item.emittedBy) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(e)}')"><span class="rel-arrow">←</span> ${esc(shortName(e))}</div><br/>`;
    }
    html += '</div>';
  }

  if (item.handledBy && item.handledBy.length > 0) {
    html += '<div class="detail-section"><h3>Handled By</h3>';
    for (const e of item.handledBy) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(e)}')"><span class="rel-arrow">→</span> ${esc(shortName(e))}</div><br/>`;
    }
    html += '</div>';
  }

  if (item.handles && item.handles.length > 0) {
    html += '<div class="detail-section"><h3>Handles</h3>';
    for (const h of item.handles) {
      html += `<div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(h)}')"><span class="rel-arrow">→</span> ${esc(shortName(h))}</div><br/>`;
    }
    html += '</div>';
  }

  if (item.managesAggregate) {
    html += `<div class="detail-section"><h3>Manages</h3>
      <div class="rel-link" onclick="window.__nav.navigateTo('${escAttr(item.managesAggregate)}')"><span class="rel-arrow">◆</span> ${esc(shortName(item.managesAggregate))}</div>
    </div>`;
  }

  const rels = (ctx.relationships || []).filter(r =>
    r.sourceType === item.fullName || r.targetType === item.fullName
  );
  if (rels.length > 0) {
    html += '<div class="detail-section"><h3>Relationships</h3>';
    html += '<table class="rel-table"><tr><th>Direction</th><th>Kind</th><th>Related Type</th><th>Label</th></tr>';
    for (const r of rels) {
      const isSource = r.sourceType === item.fullName;
      const other = isSource ? r.targetType : r.sourceType;
      const dir = isSource ? '→ outgoing' : '← incoming';
      const kindColor = relKindColor(r.kind);
      html += `<tr>
        <td style="color:var(--text-muted)">${dir}</td>
        <td><span class="rel-kind" style="color:${kindColor};background:${kindColor}18">${esc(r.kind)}</span></td>
        <td><span class="rel-link" onclick="window.__nav.navigateTo('${escAttr(other)}')">${esc(shortName(other))}</span></td>
        <td style="color:var(--text-muted)">${r.label ? esc(r.label) : '—'}</td>
      </tr>`;
    }
    html += '</table></div>';
  }

  html += '</div>';
  return html;
}

// ── Relationships ────────────────────────────────────
export function renderRelationshipsView(ctx) {
  const rels = ctx.relationships || [];
  if (rels.length === 0) return '<div class="empty-state"><h2>No Relationships</h2></div>';

  let html = renderTabBar('relationships');

  html += `<div class="section-title"><span class="dot" style="background:var(--clr-relationship)"></span>All Relationships (${rels.length})</div>`;
  html += '<table class="rel-table"><tr><th>Source</th><th>Kind</th><th>Target</th><th>Label</th></tr>';
  for (const r of rels) {
    const kindColor = relKindColor(r.kind);
    html += `<tr>
      <td><span class="rel-link" onclick="window.__nav.navigateTo('${escAttr(r.sourceType)}')">${esc(shortName(r.sourceType))}</span></td>
      <td><span class="rel-kind" style="color:${kindColor};background:${kindColor}18">${esc(r.kind)}</span></td>
      <td><span class="rel-link" onclick="window.__nav.navigateTo('${escAttr(r.targetType)}')">${esc(shortName(r.targetType))}</span></td>
      <td style="color:var(--text-muted)">${r.label ? esc(r.label) : '—'}</td>
    </tr>`;
  }
  html += '</table>';
  return html;
}

// ── JSON ─────────────────────────────────────────────
export function renderJsonView(data) {
  let html = renderTabBar('json');
  html += '<div class="json-view"><pre>' + syntaxHighlight(JSON.stringify(data, null, 2)) + '</pre></div>';
  return html;
}
