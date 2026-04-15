import { describe, expect, it } from "vitest";
import type { DiagramTheme } from "../src/model.js";
import { resolveEdgeStyle, resolveNodeStyle } from "../src/theme.js";

describe("theme resolution", () => {
  const theme: DiagramTheme = {
    defaultNodeKind: {
      label: "Default node",
      color: "#fff",
      background: "#111",
      border: "#222",
    },
    defaultEdgeKind: {
      label: "Default edge",
      color: "#999",
      dashed: true,
    },
    nodeKinds: {
      custom: {
        label: "Custom",
        color: "#abc",
        background: "#def",
        border: "#000",
      },
    },
    edgeKinds: {
      uses: { label: "Uses", color: "#0f0", dashed: false },
    },
  };

  it("falls back to default node kind", () => {
    const st = resolveNodeStyle(theme, "unknown");
    expect(st.label).toBe("Default node");
  });

  it("uses configured node kind", () => {
    const st = resolveNodeStyle(theme, "custom");
    expect(st.label).toBe("Custom");
  });

  it("falls back to default edge kind", () => {
    const st = resolveEdgeStyle(theme, "other");
    expect(st.dashed).toBe(true);
  });
});
