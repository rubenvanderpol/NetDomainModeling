/**
 * Global hooks and config used by the domain model explorer (legacy onclick wiring).
 */
export {};

declare global {
  interface Window {
    /** Optional SignalR client (Trace tab when enabled; loaded from CDN in embedded HTML). */
    signalR?: SignalRNamespace;
    __syncFeatureEditorViewBodyClass?: () => void;
    __config?: DomainModelExplorerConfig;
    /** Custom metadata map keyed by type fullName (alias, description, hiddenOnDiagram). */
    __metadata?: Record<string, TypeMetadataEntry>;
    __nav?: DomainModelNavApi;
    __saveMetadata?: (fullName: string, alias: string | null | undefined, description: string | null | undefined) => void | Promise<void>;
    __downloadExport?: (name: string) => void | Promise<void>;
    __diagram?: DomainModelDiagramApi;
    __testing?: DomainModelTestingApi;
    __featureEditor?: DomainModelFeatureEditorApi;
    __trace?: DomainModelTraceApi;
    __onDiagramHiddenNodesChanged?: () => void;
    __editor?: DomainModelEditorApi;
  }
}

export interface SignalRHubConnection {
  on(eventName: string, handler: (...args: unknown[]) => void): void;
  onreconnecting(cb: () => void): void;
  onreconnected(cb: () => void): void;
  onclose(cb: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

export interface SignalRHubConnectionBuilder {
  withUrl(url: string): SignalRHubConnectionBuilder;
  withAutomaticReconnect(delays: number[]): { build(): SignalRHubConnection };
}

export interface SignalRNamespace {
  HubConnectionBuilder: new () => SignalRHubConnectionBuilder;
}

export interface DomainModelExplorerConfig {
  apiUrl: string;
  developerMode: boolean;
  testingMode: boolean;
  featureEditorMode: boolean;
  traceViewMode: boolean;
  traceHubUrl: string;
}

export interface TypeMetadataEntry {
  alias?: string | null;
  description?: string | null;
  hiddenOnDiagram?: boolean | null;
}

export interface DomainModelNavApi {
  switchTab: (tab: string) => void;
  showDetail: (kind: string, fullName: string) => void;
  navigateTo: (fullName: string) => void;
  toggleSection: (el: HTMLElement) => void;
  toggleContext: (name: string) => void;
  toggleDiagramVisibility: (fullName: string, visible: boolean) => void;
}

export interface DomainModelDiagramApi {
  zoom: (delta: number) => void;
  fit: () => void;
  resetLayout: () => void;
  toggleKind: (kind: string) => void;
  showAll: () => void;
  downloadSvg: () => void;
  toggleAliases: () => void;
  toggleLayers: () => void;
  toggleEdgeKind: (kind: string) => void;
  toggleEdgeFilter: () => void;
  toggleKindFilter: () => void;
  showAllKinds: () => void;
  hideAllKinds: () => void;
}

/** Loose API surface for dynamically loaded modules (testing / feature editor / trace). */
export type DomainModelTestingApi = Record<string, unknown>;
export type DomainModelFeatureEditorApi = Record<string, unknown>;
export type DomainModelTraceApi = Record<string, unknown>;
export type DomainModelEditorApi = Record<string, unknown>;
