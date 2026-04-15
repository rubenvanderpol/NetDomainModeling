import type { DiagramGraph, DiagramTheme } from "../src/model.js";

/** Example: fake “solution structure” — not DDD-specific; any scanner can emit this shape. */
export const exampleGraph: DiagramGraph = {
  nodes: [
    { id: "sln", kind: "artifact", label: "App.sln", data: { path: "/repo/App.sln" } },
    { id: "proj-a", kind: "project", label: "Services.csproj", data: { path: "/repo/src/Services" } },
    { id: "proj-b", kind: "project", label: "Web.csproj", data: { path: "/repo/src/Web" } },
    { id: "pkg", kind: "folder", label: "packages/", data: { path: "/repo/packages" } },
  ],
  edges: [
    { id: "e1", kind: "contains", source: "sln", target: "proj-a", directed: true },
    { id: "e2", kind: "contains", source: "sln", target: "proj-b", directed: true },
    { id: "e3", kind: "references", source: "proj-b", target: "proj-a", directed: true },
    { id: "e4", kind: "dependsOn", source: "proj-a", target: "pkg", directed: true },
  ],
};

export const exampleTheme: DiagramTheme = {
  defaultNodeKind: {
    label: "Node",
    color: "#cbd5e1",
    background: "#1e293b",
    border: "#64748b",
  },
  defaultEdgeKind: {
    label: "Link",
    color: "#64748b",
    dashed: false,
  },
  nodeKinds: {
    artifact: {
      label: "Solution / file",
      color: "#fde68a",
      background: "#422006",
      border: "#d97706",
      stereotype: "«Solution»",
    },
    project: {
      label: "Project",
      color: "#93c5fd",
      background: "#172554",
      border: "#3b82f6",
      stereotype: "«Project»",
    },
    folder: {
      label: "Folder",
      color: "#86efac",
      background: "#14532d",
      border: "#22c55e",
      stereotype: "«Folder»",
    },
  },
  edgeKinds: {
    contains: { label: "Contains", color: "#38bdf8", dashed: false },
    references: { label: "References", color: "#4ade80", dashed: true },
    dependsOn: { label: "Depends on", color: "#f472b6", dashed: true },
  },
};
