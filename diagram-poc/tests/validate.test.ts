import { describe, expect, it } from "vitest";
import { isGraphValid, validateGraph } from "../src/validate.js";

describe("validateGraph", () => {
  it("accepts a minimal valid graph", () => {
    const g = {
      nodes: [
        { id: "a", kind: "x", label: "A" },
        { id: "b", kind: "y", label: "B" },
      ],
      edges: [{ id: "e1", kind: "rel", source: "a", target: "b" }],
    };
    expect(isGraphValid(g)).toBe(true);
    expect(validateGraph(g)).toEqual([]);
  });

  it("reports duplicate node ids", () => {
    const g = {
      nodes: [
        { id: "a", kind: "x", label: "A" },
        { id: "a", kind: "y", label: "B" },
      ],
      edges: [],
    };
    const issues = validateGraph(g);
    expect(issues.some((i) => i.code === "NODE_DUPLICATE_ID")).toBe(true);
  });

  it("reports dangling edge endpoints", () => {
    const g = {
      nodes: [{ id: "a", kind: "x", label: "A" }],
      edges: [{ id: "e1", kind: "rel", source: "a", target: "missing" }],
    };
    const issues = validateGraph(g);
    expect(issues.some((i) => i.code === "EDGE_UNKNOWN_TARGET")).toBe(true);
  });
});
