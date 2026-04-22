/**
 * Tab bar rendering — generates a consistent tab bar across all views.
 */

export function renderTabBar(activeTab) {
  const tabs = [
    { id: 'diagram',       label: 'Diagram' },
  ];

  if (window.__config?.featureEditorMode) {
    tabs.push({ id: 'features', label: '⚙ Features' });
  }

  if (window.__config?.testingMode) {
    tabs.push({ id: 'testing', label: '🧪 Testing' });
  }

  if (window.__config?.traceViewMode) {
    tabs.push({ id: 'trace', label: 'Trace' });
  }

  let html = '<div class="tab-bar">';
  for (const t of tabs) {
    const cls = t.id === activeTab ? ' active' : '';
    html += `<div class="tab${cls}" onclick="window.__nav.switchTab('${t.id}')">${t.label}</div>`;
  }
  html += '</div>';
  return html;
}
