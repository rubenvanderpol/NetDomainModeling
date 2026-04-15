import { createDiagram } from "../src/diagram.js";
import { exampleGraph, exampleTheme } from "./example-graph.js";

const root = document.getElementById("diagram-root");
if (!root) throw new Error("Missing #diagram-root");

createDiagram(root, exampleGraph, exampleTheme, {
  width: root.clientWidth || 900,
  height: root.clientHeight || 560,
});
