import { drag } from "d3-drag";
import {
  forceCenter,
  forceCollide,
  forceLink,
  forceManyBody,
  forceSimulation,
  type Simulation,
} from "d3-force";
import { select } from "d3-selection";
import {
  zoom,
  zoomIdentity,
  type D3ZoomEvent,
  type ZoomBehavior,
} from "d3-zoom";
import type { DiagramEdge, DiagramGraph, DiagramNode, DiagramTheme } from "./model.js";
import { resolveEdgeStyle, resolveNodeStyle } from "./theme.js";

export interface DiagramOptions {
  width?: number;
  height?: number;
  chargeStrength?: number;
  linkDistance?: number;
}

export interface DiagramHandle {
  destroy(): void;
}

interface SimNode extends DiagramNode {
  x?: number;
  y?: number;
  vx?: number;
  vy?: number;
  fx?: number | null;
  fy?: number | null;
}

type LinkDatum = Omit<DiagramEdge, "source" | "target"> & {
  source: SimNode;
  target: SimNode;
};

/** Initial layout seed (exported for tests). */
export function seedCirclePositions(
  nodes: readonly DiagramNode[],
  radius: number,
  centerX: number,
  centerY: number
): SimNode[] {
  const n = nodes.length || 1;
  return nodes.map((node, i) => {
    const angle = (2 * Math.PI * i) / n;
    return {
      ...node,
      x: centerX + radius * Math.cos(angle),
      y: centerY + radius * Math.sin(angle),
    };
  });
}

/**
 * Interactive SVG force-directed diagram. Kinds are styled via `theme`;
 * the graph model stays domain-agnostic.
 */
export function createDiagram(
  container: HTMLElement,
  graph: DiagramGraph,
  theme: DiagramTheme,
  options: DiagramOptions = {}
): DiagramHandle {
  const width = options.width ?? (container.clientWidth || 800);
  const height = options.height ?? (container.clientHeight || 600);
  const chargeStrength = options.chargeStrength ?? -220;
  const linkDistance = options.linkDistance ?? 90;

  container.replaceChildren();

  const root = select(container)
    .append("div")
    .style("position", "relative")
    .style("width", "100%")
    .style("height", "100%")
    .style("min-height", `${height}px`)
    .style("overflow", "hidden")
    .style("background", "#0f172a");

  const svg = root
    .append("svg")
    .attr("width", "100%")
    .attr("height", "100%")
    .attr("viewBox", `0 0 ${width} ${height}`)
    .style("cursor", "grab");

  const g = svg.append("g");

  const simNodes = seedCirclePositions(
    graph.nodes,
    Math.min(width, height) * 0.35,
    width / 2,
    height / 2
  );

  const nodeById = new Map(simNodes.map((n) => [n.id, n]));
  const links: LinkDatum[] = [];
  for (const e of graph.edges) {
    const s = nodeById.get(e.source);
    const t = nodeById.get(e.target);
    if (s && t) {
      const ld: LinkDatum = { ...e, source: s, target: t };
      links.push(ld);
    }
  }

  const linkSel = g
    .append("g")
    .attr("class", "links")
    .selectAll<SVGLineElement, LinkDatum>("line")
    .data(links)
    .join("line")
    .attr("stroke-width", 1.5)
    .attr("stroke-linecap", "round");

  let simulation!: Simulation<SimNode, undefined>;

  const dragBehavior = drag<SVGGElement, SimNode>()
    .on("start", (event, d) => {
      if (!event.active) simulation.alphaTarget(0.35).restart();
      d.fx = d.x;
      d.fy = d.y;
      svg.style("cursor", "grabbing");
    })
    .on("drag", (event, d) => {
      d.fx = event.x;
      d.fy = event.y;
    })
    .on("end", (event, d) => {
      if (!event.active) simulation.alphaTarget(0);
      d.fx = null;
      d.fy = null;
      svg.style("cursor", "grab");
    });

  const nodeG = g
    .append("g")
    .attr("class", "nodes")
    .selectAll<SVGGElement, SimNode>("g")
    .data(simNodes)
    .join("g")
    .call(dragBehavior);

  nodeG
    .append("rect")
    .attr("rx", 6)
    .attr("ry", 6)
    .attr("width", 120)
    .attr("height", 44)
    .attr("x", -60)
    .attr("y", -22);

  nodeG
    .append("text")
    .attr("class", "node-label")
    .attr("text-anchor", "middle")
    .attr("dy", "0.35em")
    .attr("font-size", "11px")
    .attr("font-family", "system-ui, sans-serif")
    .attr("fill", "#f1f5f9");

  nodeG
    .append("text")
    .attr("class", "node-kind")
    .attr("text-anchor", "middle")
    .attr("y", -30)
    .attr("font-size", "9px")
    .attr("font-family", "system-ui, sans-serif")
    .attr("opacity", 0.85);

  const zoomFn: ZoomBehavior<SVGSVGElement, unknown> = zoom<SVGSVGElement, unknown>()
    .scaleExtent([0.2, 4])
    .on("zoom", (event: D3ZoomEvent<SVGSVGElement, unknown>) => {
      g.attr("transform", event.transform.toString());
    });

  svg.call(zoomFn);
  svg.call(zoomFn.transform, zoomIdentity);

  const linkForce = forceLink<SimNode, LinkDatum>(links)
    .id((d: SimNode) => d.id)
    .distance(linkDistance);

  simulation = forceSimulation<SimNode>(simNodes)
    .force("link", linkForce)
    .force("charge", forceManyBody().strength(chargeStrength))
    .force("center", forceCenter(width / 2, height / 2))
    .force("collide", forceCollide<SimNode>().radius(52))
    .on("tick", () => {
      linkSel.each(function (d) {
        const es = resolveEdgeStyle(theme, d.kind);
        select<SVGLineElement, LinkDatum>(this)
          .attr("x1", d.source.x ?? 0)
          .attr("y1", d.source.y ?? 0)
          .attr("x2", d.target.x ?? 0)
          .attr("y2", d.target.y ?? 0)
          .attr("stroke", es.color)
          .attr("stroke-dasharray", es.dashed ? "6 4" : "none");
      });

      nodeG.attr("transform", (d) => `translate(${d.x ?? 0},${d.y ?? 0})`);

      nodeG.select<SVGRectElement>("rect").each(function (d) {
        const st = resolveNodeStyle(theme, d.kind);
        select<SVGRectElement, SimNode>(this).attr("fill", st.background).attr("stroke", st.border);
      });

      nodeG.select<SVGTextElement>(".node-label").text((d) => {
        const label = d.label.length > 18 ? `${d.label.slice(0, 16)}…` : d.label;
        return label;
      });

      nodeG.select<SVGTextElement>(".node-kind").each(function (d) {
        const st = resolveNodeStyle(theme, d.kind);
        select<SVGTextElement, SimNode>(this).text(st.stereotype ?? d.kind).attr("fill", st.color);
      });
    });

  const legend = root
    .append("div")
    .style("position", "absolute")
    .style("left", "12px")
    .style("top", "12px")
    .style("padding", "10px 12px")
    .style("background", "rgba(15,23,42,0.88)")
    .style("border", "1px solid #334155")
    .style("border-radius", "8px")
    .style("color", "#e2e8f0")
    .style("font", "12px system-ui, sans-serif")
    .style("max-width", "240px")
    .style("pointer-events", "none");

  legend.append("div").style("font-weight", "600").style("margin-bottom", "8px").text("Node kinds");

  const kinds = [...new Set(graph.nodes.map((n) => n.kind))];
  for (const k of kinds) {
    const st = resolveNodeStyle(theme, k);
    const row = legend
      .append("div")
      .style("display", "flex")
      .style("align-items", "center")
      .style("gap", "8px")
      .style("margin-bottom", "4px");
    row
      .append("div")
      .style("width", "12px")
      .style("height", "12px")
      .style("border-radius", "2px")
      .style("background", st.background)
      .style("border", `1px solid ${st.border}`);
    row.append("span").text(`${st.label} · ${k}`);
  }

  const edgeKinds = [...new Set(graph.edges.map((e) => e.kind))];
  if (edgeKinds.length) {
    legend
      .append("div")
      .style("font-weight", "600")
      .style("margin-top", "10px")
      .style("margin-bottom", "6px")
      .text("Edge kinds");
    for (const k of edgeKinds) {
      const st = resolveEdgeStyle(theme, k);
      const row = legend
        .append("div")
        .style("display", "flex")
        .style("align-items", "center")
        .style("gap", "8px")
        .style("margin-bottom", "4px");
      row
        .append("div")
        .style("width", "24px")
        .style("height", "0")
        .style("border-top", `3px ${st.dashed ? "dashed" : "solid"} ${st.color}`);
      row.append("span").text(`${st.label} · ${k}`);
    }
  }

  return {
    destroy() {
      simulation.stop();
      root.remove();
    },
  };
}
