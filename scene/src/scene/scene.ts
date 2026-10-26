import { PerspectiveCamera, Raycaster, Vector2, WebGLRenderer } from "three";
import { CameraTween, focusPose, overviewPose } from "./camera";
import type { Pose } from "./camera";
import { shouldPauseOrbits } from "./orbit";
import { createSystem } from "./system";
import type { DotNetCallbacks, UniverseDto } from "./types";

interface SceneState {
  renderer: WebGLRenderer;
  system: ReturnType<typeof createSystem>;
  callbacks: DotNetCallbacks;
  camera: PerspectiveCamera;
  tween: CameraTween;
  focusedId: string | null;
  hoveredId: string | null;
  highlightedId: string | null;
  removePointerListeners: () => void;
  observer: ResizeObserver;
  onVisibilityChange: () => void;
  frameId: number | null;
  lastTimestamp: number | null;
}

let state: SceneState | null = null;

function applyPose(camera: PerspectiveCamera, pose: Pose): void {
  camera.position.set(pose.position.x, pose.position.y, pose.position.z);
  camera.lookAt(pose.target.x, pose.target.y, pose.target.z);
}

function bodyPose(current: SceneState, bodyId: string): Pose | null {
  const body = current.system.bodies.find((mesh) => mesh.userData.bodyId === bodyId);
  return body ? focusPose(body.position, body.geometry.parameters.radius, current.camera.aspect) : null;
}

export function focus(bodyId: string, immediate: boolean): void {
  if (!state) return;
  const pose = bodyPose(state, bodyId);
  if (!pose) return;
  state.focusedId = bodyId;
  state.tween.retarget(pose, immediate ? 0 : 0.8);
  applyPose(state.camera, state.tween.current);
}

export function clearFocus(): void {
  if (!state) return;
  state.focusedId = null;
  state.tween.retarget(overviewPose(state.system.radius, state.camera.aspect), 0.8);
}

export function highlight(bodyId: string | null): void {
  if (!state) return;
  state.highlightedId = state.system.bodies.some((mesh) => mesh.userData.bodyId === bodyId) ? bodyId : null;
}

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
    const overview = overviewPose(system.radius, camera.aspect);
    const pose = current.focusedId === null ? overview : bodyPose(current, current.focusedId)!;
    current.tween.retarget(pose, 0);
    applyPose(camera, pose);
    camera.far = Math.max(1000, Math.hypot(overview.position.x, overview.position.y, overview.position.z) + system.radius * 16);
    camera.updateProjectionMatrix();
  };

  const frame = (timestamp: number) => {
    if (state !== current) {
      return;
    }
    const delta = current.lastTimestamp === null ? 0 : (timestamp - current.lastTimestamp) / 1000;
    current.lastTimestamp = timestamp;
    system.update(delta, shouldPauseOrbits(current.focusedId !== null, current.hoveredId !== null, current.highlightedId !== null));
    applyPose(camera, current.tween.step(delta));
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

  const raycaster = new Raycaster();
  const pointer = new Vector2();
  const pick = (event: MouseEvent): string | null => {
    const bounds = renderer.domElement.getBoundingClientRect();
    if (bounds.width <= 0 || bounds.height <= 0) return null;
    pointer.set((event.clientX - bounds.left) / bounds.width * 2 - 1, -(event.clientY - bounds.top) / bounds.height * 2 + 1);
    camera.updateMatrixWorld();
    system.scene.updateMatrixWorld(true);
    raycaster.setFromCamera(pointer, camera);
    return raycaster.intersectObjects(system.bodies, false)[0]?.object.userData.bodyId ?? null;
  };
  const onClick = (event: MouseEvent) => {
    void current.callbacks.invokeMethodAsync("OnBodyClicked", pick(event)).catch(() => {});
  };
  const onPointerLeave = () => {
    current.hoveredId = null;
    renderer.domElement.style.cursor = "";
  };
  const onPointerMove = (event: PointerEvent) => {
    if (event.pointerType !== "mouse" && event.pointerType !== "pen") {
      onPointerLeave();
      return;
    }
    current.hoveredId = pick(event);
    renderer.domElement.style.cursor = current.hoveredId === null ? "" : "pointer";
  };
  const removePointerListeners = () => {
    renderer.domElement.removeEventListener("click", onClick);
    renderer.domElement.removeEventListener("pointermove", onPointerMove);
    renderer.domElement.removeEventListener("pointerleave", onPointerLeave);
    onPointerLeave();
  };
  const observer = new ResizeObserver(resize);
  const initialPose = overviewPose(system.radius, 1);
  const current: SceneState = {
    renderer, system, callbacks, camera, tween: new CameraTween(initialPose, initialPose, 0),
    focusedId: null, hoveredId: null, highlightedId: null, removePointerListeners,
    observer, onVisibilityChange, frameId: null, lastTimestamp: null,
  };
  state = current;
  container.append(renderer.domElement);
  resize();
  observer.observe(container);
  document.addEventListener("visibilitychange", onVisibilityChange);
  renderer.domElement.addEventListener("click", onClick);
  renderer.domElement.addEventListener("pointermove", onPointerMove);
  renderer.domElement.addEventListener("pointerleave", onPointerLeave);
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
  current.removePointerListeners();
  current.observer.disconnect();
  current.system.dispose();
  current.renderer.dispose();
  current.renderer.forceContextLoss();
  current.renderer.domElement.remove();
}
