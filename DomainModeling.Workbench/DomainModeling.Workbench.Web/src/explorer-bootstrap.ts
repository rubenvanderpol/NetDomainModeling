import './explorer-styles.css';

declare global {
  interface Window {
    __config?: {
      apiUrl: string;
      developerMode: boolean;
      testingMode: boolean;
      featureEditorMode: boolean;
      traceViewMode: boolean;
      traceHubUrl: string;
    };
  }
}

function syncRouteToHash(): void {
  const path = window.location.pathname;
  if (path.endsWith('/features')) {
    window.location.hash = '#features';
  } else {
    window.location.hash = '#diagram';
  }
}

async function bootExplorer(): Promise<void> {
  // Same origin: Vite dev proxies /domain-model; API hosts /domain-model in production.
  window.__config = {
    apiUrl: '/domain-model/json',
    developerMode: true,
    testingMode: false,
    featureEditorMode: true,
    traceViewMode: false,
    traceHubUrl: '',
  };

  syncRouteToHash();

  await import('@explorer/js/main.js');

  window.addEventListener('popstate', () => {
    syncRouteToHash();
    const h = (window.location.hash || '').slice(1).toLowerCase();
    const nav = (window as unknown as { __nav?: { switchTab: (t: string) => void } }).__nav;
    if (!nav) return;
    if (h === 'features') nav.switchTab('features');
    else nav.switchTab('diagram');
  });
}

function start(): void {
  void bootExplorer();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', start);
} else {
  start();
}
