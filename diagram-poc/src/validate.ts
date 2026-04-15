import type { DiagramEdge, DiagramGraph, DiagramNode } from "./model.js";

export interface ValidationIssue {
  code: string;
  message: string;
  detail?: string;
}

export function validateGraph(graph: DiagramGraph): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const nodeIds = new Set<string>();
  const seenNodeIds = new Map<string, DiagramNode>();

  for (const n of graph.nodes) {
    if (!n.id?.length) {
      issues.push({ code: "NODE_EMPTY_ID", message: "Node has empty id" });
      continue;
    }
    if (nodeIds.has(n.id)) {
      issues.push({
        code: "NODE_DUPLICATE_ID",
        message: `Duplicate node id: ${n.id}`,
        detail: seenNodeIds.get(n.id)?.label,
      });
    } else {
      nodeIds.add(n.id);
      seenNodeIds.set(n.id, n);
    }
    if (!n.kind?.length) {
      issues.push({ code: "NODE_EMPTY_KIND", message: `Node ${n.id} has empty kind` });
    }
  }

  const edgeIds = new Set<string>();
  for (const e of graph.edges) {
    if (!e.id?.length) {
      issues.push({ code: "EDGE_EMPTY_ID", message: "Edge has empty id" });
      continue;
    }
    if (edgeIds.has(e.id)) {
      issues.push({ code: "EDGE_DUPLICATE_ID", message: `Duplicate edge id: ${e.id}` });
    } else {
      edgeIds.add(e.id);
    }
    if (!e.kind?.length) {
      issues.push({ code: "EDGE_EMPTY_KIND", message: `Edge ${e.id} has empty kind` });
    }
    if (!nodeIds.has(e.source)) {
      issues.push({
        code: "EDGE_UNKNOWN_SOURCE",
        message: `Edge ${e.id} references unknown source: ${e.source}`,
      });
    }
    if (!nodeIds.has(e.target)) {
      issues.push({
        code: "EDGE_UNKNOWN_TARGET",
        message: `Edge ${e.id} references unknown target: ${e.target}`,
      });
    }
  }

  return issues;
}

export function isGraphValid(graph: DiagramGraph): boolean {
  return validateGraph(graph).length === 0;
}
