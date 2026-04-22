import './style.css';

const explorerSrc = '/domain-model#diagram';
const featuresSrc = '/domain-model#features';

function mount(): void {
  const root = document.querySelector<HTMLDivElement>('#app');
  if (!root) return;

  root.innerHTML = `
    <header class="top">
      <div class="brand">
        <strong>Domain Modeling Workbench</strong>
        <span class="sub">Aspire + Vite — graph and feature editor against the API</span>
      </div>
      <nav class="tabs" role="tablist" aria-label="Workbench views">
        <button type="button" class="tab active" data-tab="explorer" role="tab" aria-selected="true">Graph</button>
        <button type="button" class="tab" data-tab="features" role="tab" aria-selected="false">Features</button>
      </nav>
    </header>
    <main class="frame-wrap">
      <iframe id="workbench-frame" class="frame" title="Domain model explorer" src="${explorerSrc}"></iframe>
    </main>
  `;

  const frame = root.querySelector<HTMLIFrameElement>('#workbench-frame');
  const tabButtons = root.querySelectorAll<HTMLButtonElement>('.tab');

  const setTab = (tab: 'explorer' | 'features') => {
    const src = tab === 'features' ? featuresSrc : explorerSrc;
    if (frame) frame.src = src;
    tabButtons.forEach((btn) => {
      const isActive = btn.dataset.tab === tab;
      btn.classList.toggle('active', isActive);
      btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
    });
  };

  tabButtons.forEach((btn) => {
    btn.addEventListener('click', () => {
      const tab = btn.dataset.tab;
      if (tab === 'features' || tab === 'explorer') {
        const url = tab === 'features' ? '/app/features' : '/app/';
        window.history.pushState(null, '', url);
        setTab(tab);
      }
    });
  });

  window.addEventListener('popstate', () => {
    const path = window.location.pathname;
    if (path.endsWith('/features')) setTab('features');
    else setTab('explorer');
  });

  const path = window.location.pathname;
  if (path.endsWith('/features')) setTab('features');
}

mount();
