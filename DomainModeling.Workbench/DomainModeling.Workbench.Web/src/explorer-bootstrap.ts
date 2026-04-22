import './explorer/css/explorer.css';
import './explorer/css/diagram.css';
import './explorer/css/feature-editor.css';
import './explorer/css/testing.css';
import './explorer/css/trace.css';

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
  window.__config = {
    apiUrl: '/domain-model/json',
    developerMode: true,
    testingMode: false,
    featureEditorMode: true,
    traceViewMode: false,
    traceHubUrl: '',
  };

  syncRouteToHash();

  await import('virtual:explorer-main');

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
