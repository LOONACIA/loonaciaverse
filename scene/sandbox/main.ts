/// <reference types="vite/client" />

import "../../src/Loonaciaverse.Web/wwwroot/css/app.css";
import { clearFocus, dispose, focus, highlight, init, warp } from "../src/scene/scene";
import type { UniverseDto } from "../src/scene/types";

// Scene fields from content/universe.yml; this fixture is only used by the sandbox.
const universe: UniverseDto = {
  bodies: [
    {
      id: "about", name: "LOONACIA", subtitle: "ABOUT", locked: false,
      visual: { baseColor: "#fff2cc", atmosphereColor: "#ffd780", noiseScale: 3.5, glowIntensity: 1.8, surface: null },
      orbit: null,
    },
    {
      id: "projects", name: "ARTIFEX", subtitle: "PROJECTS", locked: false,
      visual: { baseColor: "#236b8e", atmosphereColor: "#5fd4ff", noiseScale: 4.0, glowIntensity: 0.4, surface: "terran" },
      orbit: { radius: 8.0, periodSeconds: 75.0, phase: 0.5 },
    },
    {
      id: "blog", name: "ARCHIVA", subtitle: "BLOG", locked: false,
      visual: { baseColor: "#6956a8", atmosphereColor: "#bda5ff", noiseScale: 5.0, glowIntensity: 0.3, surface: "gas" },
      orbit: { radius: 12.0, periodSeconds: 100.0, phase: 2.5 },
    },
    {
      id: "playground", name: "ARCADIA", subtitle: "PLAYGROUND", locked: true,
      visual: { baseColor: "#a65a35", atmosphereColor: "#e8a46a", noiseScale: 6.0, glowIntensity: 0.25, surface: "desert" },
      orbit: { radius: 16.0, periodSeconds: 120.0, phase: 4.5 },
    },
  ],
};

const container = document.getElementById("scene")!;
const immediate = document.getElementById("immediate") as HTMLInputElement;
const highlightToggles: HTMLInputElement[] = [];
let warping = false;
let disposed = false;
let restartTimer: ReturnType<typeof setTimeout> | null = null;
for (const body of universe.bodies) {
  const row = document.createElement("div");
  const button = document.createElement("button");
  button.type = "button";
  button.textContent = `FOCUS ${body.name}`;
  button.addEventListener("click", () => focus(body.id, immediate.checked));
  const warpButton = document.createElement("button");
  warpButton.type = "button";
  warpButton.textContent = `WARP ${body.name}`;
  warpButton.addEventListener("click", async () => {
    if (warping) return;
    warping = true;
    await warp(body.id);
    if (disposed) return;
    restartTimer = setTimeout(() => {
      restartTimer = null;
      highlightToggles.forEach((toggle) => { toggle.checked = false; });
      initialize();
      warping = false;
    }, 1000);
  });
  const label = document.createElement("label");
  const toggle = document.createElement("input");
  toggle.type = "checkbox";
  toggle.setAttribute("aria-label", `HIGHLIGHT ${body.name}`);
  toggle.addEventListener("change", () => {
    highlightToggles.forEach((other) => { if (other !== toggle) other.checked = false; });
    highlight(toggle.checked ? body.id : null);
  });
  highlightToggles.push(toggle);
  label.append(toggle, "HIGHLIGHT");
  row.append(button, warpButton, label);
  document.getElementById("body-controls")!.append(row);
}
document.getElementById("clear")!.addEventListener("click", clearFocus);
function initialize() {
  const initialized = init(container, universe, {
    async invokeMethodAsync(methodName, ...args) {
      console.log(methodName, ...args);
      if (methodName === "OnBodyClicked") {
        const id = args[0] as string | null;
        if (id === null) clearFocus();
        else focus(id, immediate.checked);
      }
    },
  });
  if (!initialized) {
    container.textContent = "Scene unavailable: WebGL2 is required and reduced motion must be off.";
  }
}
initialize();

if (import.meta.hot) {
  import.meta.hot.dispose(() => {
    disposed = true;
    if (restartTimer !== null) clearTimeout(restartTimer);
    dispose();
  });
}

