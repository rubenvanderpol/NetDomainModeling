/**
 * Reserved for future IPC (open folder, load graph JSON from disk).
 * Context-isolated; expose only explicit APIs here.
 */
import { contextBridge } from "electron";

contextBridge.exposeInMainWorld("diagramPoc", {
  version: "0.1.0",
});
