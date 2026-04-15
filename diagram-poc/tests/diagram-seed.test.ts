import { describe, expect, it } from "vitest";
import { seedCirclePositions } from "../src/diagram.js";

describe("seedCirclePositions", () => {
  it("places nodes on a circle", () => {
    const nodes = [
      { id: "a", kind: "t", label: "A" },
      { id: "b", kind: "t", label: "B" },
      { id: "c", kind: "t", label: "C" },
    ];
    const placed = seedCirclePositions(nodes, 10, 100, 200);
    expect(placed).toHaveLength(3);
    for (const p of placed) {
      const dx = (p.x ?? 0) - 100;
      const dy = (p.y ?? 0) - 200;
      const r = Math.sqrt(dx * dx + dy * dy);
      expect(r).toBeCloseTo(10, 5);
    }
  });
});
