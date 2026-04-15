import type { DiagramTheme, EdgeKindStyle, NodeKindStyle } from "./model.js";

export function resolveNodeStyle(
  theme: DiagramTheme,
  kind: string
): NodeKindStyle {
  return theme.nodeKinds[kind] ?? theme.defaultNodeKind;
}

export function resolveEdgeStyle(
  theme: DiagramTheme,
  kind: string
): EdgeKindStyle {
  return theme.edgeKinds[kind] ?? theme.defaultEdgeKind;
}
