import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { Mesh, PerspectiveCamera, Scene, SphereGeometry, WebGLRenderer } from "three";
import { focusPose, overviewPose } from "./camera";
import { clearFocus, dispose, focus, highlight, init } from "./scene";
import type { DotNetCallbacks, UniverseDto } from "./types";

vi.mock("three", async (importOriginal) => {
  const actual = await importOriginal<typeof import("three")>();
  return {
    ...actual,
    WebGLRenderer: vi.fn(function () {
      return {
        domElement: document.createElement("canvas"),
        setPixelRatio: vi.fn(), setSize: vi.fn(), render: vi.fn(),
        dispose: vi.fn(), forceContextLoss: vi.fn(),
      };
    }),
  };
});

const universe: UniverseDto = {
  bodies: [{
    id: "planet", name: "PLANET", subtitle: "TEST", locked: false,
    visual: { baseColor: "#236b8e", atmosphereColor: "#5fd4ff", noiseScale: 4, glowIntensity: 0.4 },
    orbit: { radius: 16, periodSeconds: 80, phase: 0 },
  }],
};
const callbacks: DotNetCallbacks = { invokeMethodAsync: vi.fn(async () => undefined) };
let container: HTMLDivElement;
let resize: ResizeObserverCallback;
let frames: Map<number, FrameRequestCallback>;
let nextFrameId: number;
let disconnect: ReturnType<typeof vi.fn>;

function renderer() {
  return vi.mocked(WebGLRenderer).mock.results.at(-1)!.value as WebGLRenderer;
}

function tick(timestamp: number) {
  const [id, callback] = frames.entries().next().value!;
  frames.delete(id);
  callback(timestamp);
}

beforeEach(() => {
  container = document.createElement("div");
  document.body.append(container);
  Object.defineProperties(container, { clientWidth: { configurable: true, value: 1200 }, clientHeight: { value: 800 } });
  vi.spyOn(window, "matchMedia").mockReturnValue({ matches: false } as MediaQueryList);
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue({ getExtension: () => null } as unknown as WebGL2RenderingContext);
  vi.spyOn(document, "hidden", "get").mockReturnValue(false);
  frames = new Map();
  nextFrameId = 0;
  vi.stubGlobal("requestAnimationFrame", vi.fn((callback: FrameRequestCallback) => { frames.set(++nextFrameId, callback); return nextFrameId; }));
  vi.stubGlobal("cancelAnimationFrame", vi.fn((id: number) => frames.delete(id)));
  disconnect = vi.fn();
  vi.stubGlobal("ResizeObserver", class {
    constructor(callback: ResizeObserverCallback) { resize = callback; }
    observe = vi.fn();
    disconnect = disconnect;
  });
});

afterEach(() => {
  dispose();
  document.body.innerHTML = "";
  vi.restoreAllMocks();
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe("scene lifecycle", () => {
  it("ignores camera and highlight commands before initialization and after disposal", () => {
    const commands = () => { focus("planet", false); focus("planet", true); clearFocus(); highlight("planet"); highlight(null); };
    expect(commands).not.toThrow();
    init(container, universe, callbacks);
    dispose();
    expect(commands).not.toThrow();
    expect(frames.size).toBe(0);
  });

  it("returns false without adding DOM or probing WebGL when reduced motion is requested", () => {
    vi.mocked(window.matchMedia).mockReturnValue({ matches: true } as MediaQueryList);
    expect(init(container, universe, callbacks)).toBe(false);
    expect(window.matchMedia).toHaveBeenCalledWith("(prefers-reduced-motion: reduce)");
    expect(HTMLCanvasElement.prototype.getContext).not.toHaveBeenCalled();
    expect(WebGLRenderer).not.toHaveBeenCalled();
    expect(container.children).toHaveLength(0);
  });

  it("returns false without a renderer or attached canvas if WebGL2 is unavailable", () => {
    vi.mocked(HTMLCanvasElement.prototype.getContext).mockReturnValue(null);
    expect(init(container, universe, callbacks)).toBe(false);
    expect(HTMLCanvasElement.prototype.getContext).toHaveBeenCalledExactlyOnceWith("webgl2");
    expect(WebGLRenderer).not.toHaveBeenCalled();
    expect(container.children).toHaveLength(0);
  });

  it("returns false if constructing the renderer throws", () => {
    vi.mocked(WebGLRenderer).mockImplementationOnce(() => { throw new Error("Context unavailable"); });
    expect(init(container, universe, callbacks)).toBe(false);
    expect(container.children).toHaveLength(0);
  });

  it("safely disposes before initialization or more than once", () => {
    expect(() => { dispose(); dispose(); }).not.toThrow();
  });

  it("creates its own canvas, caps pixel ratio, resizes and frames the outer orbit in portrait", () => {
    vi.spyOn(window, "devicePixelRatio", "get").mockReturnValue(3);
    expect(init(container, universe, callbacks)).toBe(true);
    const active = renderer();
    expect(container.firstElementChild).toBe(active.domElement);
    expect(active.setPixelRatio).toHaveBeenCalledWith(2);
    expect(active.setSize).toHaveBeenCalledWith(1200, 800);
    Object.defineProperty(container, "clientWidth", { value: 400 });
    resize([], {} as ResizeObserver);
    tick(0);
    const [, camera] = vi.mocked(active.render).mock.calls[0] as [Scene, PerspectiveCamera];
    expect(camera.aspect).toBe(0.5);
    expect(active.setSize).toHaveBeenLastCalledWith(400, 800);
    const horizontalHalfFov = Math.atan(Math.tan(camera.fov * Math.PI / 360) * camera.aspect);
    expect(camera.position.length() * Math.sin(horizontalHalfFov)).toBeGreaterThan(16.9);
    expect(callbacks.invokeMethodAsync).not.toHaveBeenCalled();
  });

  it("stops hidden rendering and excludes the hidden interval from the first resumed delta", () => {
    init(container, universe, callbacks);
    const active = renderer();
    tick(1000);
    tick(2000);
    const scene = vi.mocked(active.render).mock.calls[0][0] as Scene;
    const planet = scene.children.find((child) => child.userData.bodyId === "planet")!;
    const position = planet.position.clone();
    vi.spyOn(document, "hidden", "get").mockReturnValue(true);
    document.dispatchEvent(new Event("visibilitychange"));
    expect(frames.size).toBe(0);
    vi.spyOn(document, "hidden", "get").mockReturnValue(false);
    document.dispatchEvent(new Event("visibilitychange"));
    tick(100000);
    expect(planet.position.equals(position)).toBe(true);
    tick(101000);
    expect(planet.position.equals(position)).toBe(false);
  });

  it("releases the previous scene on reinitialization and removes all lifecycle hooks on disposal", () => {
    init(container, universe, callbacks);
    const previous = renderer();
    init(container, universe, callbacks);
    expect(previous.dispose).toHaveBeenCalledOnce();
    expect(previous.forceContextLoss).toHaveBeenCalledOnce();
    expect(container.children).toHaveLength(1);
    const active = renderer();
    dispose();
    expect(disconnect).toHaveBeenCalledTimes(2);
    expect(active.dispose).toHaveBeenCalledOnce();
    expect(active.forceContextLoss).toHaveBeenCalledOnce();
    expect(container.children).toHaveLength(0);
    expect(frames.size).toBe(0);
    document.dispatchEvent(new Event("visibilitychange"));
    expect(frames.size).toBe(0);
  });
});

function startScene() {
  init(container, universe, callbacks);
  const active = renderer();
  vi.spyOn(active.domElement, "getBoundingClientRect").mockReturnValue({ left: 20, top: 30, width: 1200, height: 800 } as DOMRect);
  tick(0);
  const [scene, camera] = vi.mocked(active.render).mock.calls[0] as [Scene, PerspectiveCamera];
  const body = scene.children.find((child) => child.userData.bodyId === "planet") as Mesh<SphereGeometry>;
  return { active, camera, body };
}

function bodyScreenPosition(camera: PerspectiveCamera, body: Mesh) {
  camera.updateMatrixWorld();
  const projected = body.position.clone().project(camera);
  return { clientX: 20 + (projected.x + 1) * 600, clientY: 30 + (1 - projected.y) * 400 };
}

describe("scene focus and input", () => {
  it("applies immediate focus, reframes on resize, and tweens back to overview in 0.8 seconds", () => {
    const { camera, body } = startScene();
    const position = body.position.clone();
    focus("planet", true);
    expect(camera.position.toArray()).toEqual(Object.values(focusPose(position, body.geometry.parameters.radius, 1.5).position));
    tick(1000);
    expect(body.position.equals(position)).toBe(true);
    Object.defineProperty(container, "clientWidth", { value: 400 });
    resize([], {} as ResizeObserver);
    expect(camera.position.toArray()).toEqual(Object.values(focusPose(position, body.geometry.parameters.radius, 0.5).position));
    clearFocus();
    tick(1400);
    const overview = overviewPose(16.9, 0.5);
    expect(camera.position.toArray()).not.toEqual(Object.values(overview.position));
    tick(1800);
    expect(camera.position.toArray()).toEqual(Object.values(overview.position));
    expect(body.position.equals(position)).toBe(false);
  });

  it("animates focus from the current pose and ignores unknown focus IDs", () => {
    const { camera, body } = startScene();
    const initial = camera.position.clone();
    focus("planet", false);
    expect(camera.position.equals(initial)).toBe(true);
    tick(400);
    expect(camera.position.equals(initial)).toBe(false);
    const midpoint = camera.position.clone();
    focus("unknown", true);
    expect(camera.position.equals(midpoint)).toBe(true);
    tick(800);
    expect(camera.position.toArray()).toEqual(Object.values(focusPose(body.position, body.geometry.parameters.radius, 1.5).position));
  });

  it("pauses on keyboard highlight, treats unknown IDs as null, and resumes on clear", () => {
    const { body } = startScene();
    const initial = body.position.clone();
    highlight("planet");
    tick(1000);
    expect(body.position.equals(initial)).toBe(true);
    highlight("unknown");
    tick(2000);
    expect(body.position.equals(initial)).toBe(false);
    highlight("planet");
    const paused = body.position.clone();
    tick(3000);
    expect(body.position.equals(paused)).toBe(true);
    highlight(null);
    tick(4000);
    expect(body.position.equals(paused)).toBe(false);
    expect(callbacks.invokeMethodAsync).not.toHaveBeenCalled();
  });

  it.each(["mouse", "pen"])("pauses on %s hover, sets the cursor and resumes on pointerleave", (pointerType) => {
    const { active, camera, body } = startScene();
    const initial = body.position.clone();
    active.domElement.dispatchEvent(new PointerEvent("pointermove", { ...bodyScreenPosition(camera, body), pointerType }));
    expect(active.domElement.style.cursor).toBe("pointer");
    tick(1000);
    expect(body.position.equals(initial)).toBe(true);
    expect(body.rotation.y).toBeGreaterThan(0);
    expect(callbacks.invokeMethodAsync).not.toHaveBeenCalled();
    active.domElement.dispatchEvent(new PointerEvent("pointerleave"));
    expect(active.domElement.style.cursor).toBe("");
    tick(2000);
    expect(body.position.equals(initial)).toBe(false);
  });

  it("clears hover over empty space and ignores touch hover", () => {
    const { active, camera, body } = startScene();
    const position = bodyScreenPosition(camera, body);
    active.domElement.dispatchEvent(new PointerEvent("pointermove", { ...position, pointerType: "mouse" }));
    active.domElement.dispatchEvent(new PointerEvent("pointermove", { clientX: 20, clientY: 30, pointerType: "mouse" }));
    expect(active.domElement.style.cursor).toBe("");
    active.domElement.dispatchEvent(new PointerEvent("pointermove", { ...position, pointerType: "touch" }));
    expect(active.domElement.style.cursor).toBe("");
    const initial = body.position.clone();
    tick(1000);
    expect(body.position.equals(initial)).toBe(false);
  });

  it("reports body and empty-space clicks without focusing itself, and handles rejected callbacks", async () => {
    const { active, camera, body } = startScene();
    const overview = camera.position.clone();
    const initial = body.position.clone();
    active.domElement.dispatchEvent(new MouseEvent("click", bodyScreenPosition(camera, body)));
    expect(callbacks.invokeMethodAsync).toHaveBeenLastCalledWith("OnBodyClicked", "planet");
    expect(camera.position.equals(overview)).toBe(true);
    tick(1000);
    expect(body.position.equals(initial)).toBe(false);
    vi.mocked(callbacks.invokeMethodAsync).mockRejectedValueOnce(new Error("Disposed .NET reference"));
    active.domElement.dispatchEvent(new MouseEvent("click", { clientX: 20, clientY: 30 }));
    expect(callbacks.invokeMethodAsync).toHaveBeenLastCalledWith("OnBodyClicked", null);
    await Promise.resolve();
  });

  it("removes all canvas listeners on disposal", () => {
    const { active, camera, body } = startScene();
    const position = bodyScreenPosition(camera, body);
    active.domElement.dispatchEvent(new PointerEvent("pointermove", { ...position, pointerType: "mouse" }));
    expect(active.domElement.style.cursor).toBe("pointer");
    dispose();
    expect(active.domElement.style.cursor).toBe("");
    active.domElement.dispatchEvent(new MouseEvent("click", position));
    active.domElement.dispatchEvent(new PointerEvent("pointermove", { ...position, pointerType: "mouse" }));
    active.domElement.dispatchEvent(new PointerEvent("pointerleave"));
    expect(callbacks.invokeMethodAsync).not.toHaveBeenCalled();
    expect(active.domElement.style.cursor).toBe("");
  });
});
