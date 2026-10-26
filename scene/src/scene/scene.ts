import { MathUtils, PerspectiveCamera, WebGLRenderer } from "three";
import { createSystem } from "./system";
import type { DotNetCallbacks, UniverseDto } from "./types";

interface SceneState {
  renderer: WebGLRenderer;
  system: ReturnType<typeof createSystem>;
  callbacks: DotNetCallbacks;
  observer: ResizeObserver;
  onVisibilityChange: () => void;
  frameId: number | null;
  lastTimestamp: number | null;
}

let state: SceneState | null = null;

function supportsWebGL2(): boolean {
  const canvas = document.createElement("canvas");
  try {
    const context = canvas.getContext("webgl2");
    if (!context) {
      return false;
    }
    context.getExtension("WEBGL_lose_context")?.loseContext();
    return true;
  } catch {
    return false;
  }
}

export function init(container: HTMLElement, universe: UniverseDto, callbacks: DotNetCallbacks): boolean {
  dispose();
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches || !supportsWebGL2()) {
    return false;
  }

  let renderer: WebGLRenderer;
  try {
    renderer = new WebGLRenderer({ antialias: true });
  } catch {
    return false;
  }

  const system = createSystem(universe);
  const camera = new PerspectiveCamera(45, 1, 0.1, 1000);
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.domElement.style.display = "block";

  const resize = () => {
    const width = Math.max(container.clientWidth, 1);
    const height = Math.max(container.clientHeight, 1);
    renderer.setSize(width, height);
    camera.aspect = width / height;
    const verticalHalfFov = MathUtils.degToRad(camera.fov / 2);
    const horizontalHalfFov = Math.atan(Math.tan(verticalHalfFov) * camera.aspect);
    const distance = system.radius * 1.1 / Math.sin(Math.min(verticalHalfFov, horizontalHalfFov));
    camera.position.set(0, 0.75, 1).normalize().multiplyScalar(distance);
    camera.lookAt(0, 0, 0);
    camera.far = Math.max(1000, distance + system.radius * 16);
    camera.updateProjectionMatrix();
  };

  const frame = (timestamp: number) => {
    if (state !== current) {
      return;
    }
    const delta = current.lastTimestamp === null ? 0 : (timestamp - current.lastTimestamp) / 1000;
    current.lastTimestamp = timestamp;
    system.update(delta);
    renderer.render(system.scene, camera);
    current.frameId = requestAnimationFrame(frame);
  };

  const onVisibilityChange = () => {
    if (current.frameId !== null) {
      cancelAnimationFrame(current.frameId);
      current.frameId = null;
    }
    current.lastTimestamp = null;
    if (!document.hidden) {
      current.frameId = requestAnimationFrame(frame);
    }
  };

  const observer = new ResizeObserver(resize);
  const current: SceneState = { renderer, system, callbacks, observer, onVisibilityChange, frameId: null, lastTimestamp: null };
  state = current;
  container.append(renderer.domElement);
  resize();
  observer.observe(container);
  document.addEventListener("visibilitychange", onVisibilityChange);
  onVisibilityChange();
  return true;
}

export function dispose(): void {
  if (state === null) {
    return;
  }

  const current = state;
  state = null;
  if (current.frameId !== null) {
    cancelAnimationFrame(current.frameId);
  }
  document.removeEventListener("visibilitychange", current.onVisibilityChange);
  current.observer.disconnect();
  current.system.dispose();
  current.renderer.dispose();
  current.renderer.forceContextLoss();
  current.renderer.domElement.remove();
}
