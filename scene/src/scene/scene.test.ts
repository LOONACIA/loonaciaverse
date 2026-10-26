import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PerspectiveCamera, Scene, WebGLRenderer } from "three";
import { dispose, init } from "./scene";
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
