/**
 * Neutral graph model: kinds are plain strings; styling comes from DiagramTheme.
 */
export interface DiagramNode {
  id: string;
  kind: string;
  label: string;
  /** Optional payload for tooltips / future inspectors */
  data?: Record<string, unknown>;
}

export interface DiagramEdge {
  id: string;
  kind: string;
  source: string;
  target: string;
  label?: string;
  directed?: boolean;
  data?: Record<string, unknown>;
}

export interface DiagramGraph {
  nodes: DiagramNode[];
  edges: DiagramEdge[];
}

export interface NodeKindStyle {
  label: string;
  color: string;
  background: string;
  border: string;
  /** Short tag shown on the card (e.g. «Module») */
  stereotype?: string;
}

export interface EdgeKindStyle {
  label: string;
  color: string;
  dashed?: boolean;
}

export interface DiagramTheme {
  nodeKinds: Record<string, NodeKindStyle>;
  edgeKinds: Record<string, EdgeKindStyle>;
  /** Fallback when a kind is missing from the map */
  defaultNodeKind: NodeKindStyle;
  defaultEdgeKind: EdgeKindStyle;
}

export const defaultDiagramTheme: DiagramTheme = {
  nodeKinds: {},
  edgeKinds: {},
  defaultNodeKind: {
    label: "Node",
    color: "#e2e8f0",
    background: "#1e293b",
    border: "#64748b",
  },
  defaultEdgeKind: {
    label: "Link",
    color: "#94a3b8",
    dashed: false,
  },
};
