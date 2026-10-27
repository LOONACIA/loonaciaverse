import { PerspectiveCamera, Raycaster, Vector2, WebGLRenderer } from "three";
import type { Quaternion, Vector3 } from "three";
import { EffectComposer } from "three/addons/postprocessing/EffectComposer.js";
import { OutputPass } from "three/addons/postprocessing/OutputPass.js";
import { RenderPass } from "three/addons/postprocessing/RenderPass.js";
import { UnrealBloomPass } from "three/addons/postprocessing/UnrealBloomPass.js";
import { CSS2DRenderer } from "three/addons/renderers/CSS2DRenderer.js";
import { CameraTween, focusPose, overviewPose } from "./camera";
import type { Pose } from "./camera";
import { createBodyLabel } from "./labels";
import { shouldPauseOrbits } from "./orbit";
import { getQuality } from "./quality";
import type { RenderQuality } from "./quality";
import { createFocusRing } from "./ring";
import { createSystem } from "./system";
import type { DotNetCallbacks, UniverseDto } from "./types";
import { WARP_DURATION_MS, warpTimeline } from "./warp";

interface WarpState {
  startedAt: number;
  startPosition: Vector3;
  direction: Vector3;
  distance: number;
  startRotation: Quaternion;
  endRotation: Quaternion;
  overlay: HTMLDivElement;
  promise: Promise<void>;
  resolve: () => void;
  timerId: ReturnType<typeof setTimeout>;
}

interface SceneState {
  container: HTMLElement;
  renderer: WebGLRenderer;
  composer: EffectComposer | null;
  quality: RenderQuality;
  labelRenderer: CSS2DRenderer;
  labels: ReturnType<typeof createBodyLabel>[];
  ring: ReturnType<typeof createFocusRing>;
  originalContainerPosition: string | null;
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
  warp: WarpState | null;
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

function updateDecorations(current: SceneState, deltaSeconds = 0): void {
  current.camera.updateMatrixWorld();
  current.labels.forEach((label, index) => {
    const body = current.system.bodies[index];
    const emphasizedId = body.userData.bodyId === current.hoveredId ? current.hoveredId : current.highlightedId;
    label.update(body, current.camera, current.focusedId, emphasizedId, current.warp !== null);
  });
  current.ring.update(deltaSeconds, current.warp !== null);
  current.labelRenderer.render(current.system.scene, current.camera);
}

export function focus(bodyId: string, immediate: boolean): void {
  if (!state || state.warp) return;
  const pose = bodyPose(state, bodyId);
  if (!pose) return;
  state.focusedId = bodyId;
  state.ring.focus(state.system.bodies.find((mesh) => mesh.userData.bodyId === bodyId)!, immediate);
  state.tween.retarget(pose, immediate ? 0 : 0.8);
  applyPose(state.camera, state.tween.current);
  updateDecorations(state);
}

export function clearFocus(): void {
  if (!state || state.warp) return;
  state.focusedId = null;
  state.ring.focus(null);
  state.tween.retarget(overviewPose(state.system.radius, state.camera.aspect), 0.8);
  updateDecorations(state);
}

export function highlight(bodyId: string | null): void {
  if (!state || state.warp) return;
  state.highlightedId = state.system.bodies.some((mesh) => mesh.userData.bodyId === bodyId) ? bodyId : null;
  updateDecorations(state);
}

function updateWarp(current: SceneState, elapsedMs: number): void {
  const active = current.warp!;
  const timeline = warpTimeline(elapsedMs);
  current.camera.position.copy(active.startPosition).addScaledVector(active.direction, active.distance * 0.98 * timeline.dash);
  current.camera.quaternion.slerpQuaternions(active.startRotation, active.endRotation, Math.min(1, timeline.dash * 4));
  current.system.setWarpStreak(active.direction, timeline.streak);
  active.overlay.style.opacity = String(timeline.fade);
}

export function warp(bodyId: string): Promise<void> {
  const current = state;
  if (!current) return Promise.resolve();
  const body = current.system.bodies.find((mesh) => mesh.userData.bodyId === bodyId);
  if (!body) return Promise.resolve();
  if (current.warp) return current.warp.promise;

  applyPose(current.camera, current.tween.current);
  const startPosition = current.camera.position.clone();
  const direction = body.position.clone().sub(startPosition);
  const distance = direction.length();
  direction.normalize();
  const startRotation = current.camera.quaternion.clone();
  current.camera.lookAt(body.position);
  const endRotation = current.camera.quaternion.clone();
  current.camera.quaternion.copy(startRotation);

  const overlay = document.createElement("div");
  overlay.setAttribute("aria-hidden", "true");
  Object.assign(overlay.style, { position: "absolute", inset: "0", backgroundColor: "#05070d", opacity: "0", pointerEvents: "none" });
  current.container.append(overlay);
  current.hoveredId = null;
  current.highlightedId = null;
  current.renderer.domElement.style.cursor = "";

  let resolve!: () => void;
  const promise = new Promise<void>((complete) => { resolve = complete; });
  current.warp = {
    startedAt: performance.now(), startPosition, direction, distance, startRotation, endRotation,
    overlay, promise, resolve,
    // Completion must not depend on rAF: hidden tabs suspend the render loop.
    timerId: setTimeout(() => {
      if (state === current) updateWarp(current, WARP_DURATION_MS);
      resolve();
    }, WARP_DURATION_MS),
  };
  updateDecorations(current);
  return promise;
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

  const quality = getQuality({ devicePixelRatio: window.devicePixelRatio, coarsePointer: window.matchMedia("(pointer: coarse)").matches, viewportWidth: container.clientWidth });
  const system = createSystem(universe, quality.starCount, quality.pixelRatio);
  const labelRenderer = new CSS2DRenderer();
  labelRenderer.domElement.className = "scene-label-layer";
  Object.assign(labelRenderer.domElement.style, { position: "absolute", inset: "0", pointerEvents: "none", zIndex: "0" });
  const originalContainerPosition = getComputedStyle(container).position === "static" ? container.style.position : null;
  if (originalContainerPosition !== null) container.style.position = "relative";
  const hudColor = getComputedStyle(document.documentElement).getPropertyValue("--color-hud").trim() || "#5fd4ff";
  const ring = createFocusRing(hudColor);
  system.scene.add(ring.mesh, ring.orbit);
  const camera = new PerspectiveCamera(45, 1, 0.1, 1000);
  renderer.setPixelRatio(quality.pixelRatio);
  renderer.domElement.style.display = "block";
  const composer = quality.bloom ? new EffectComposer(renderer) : null;
  if (composer) {
    composer.addPass(new RenderPass(system.scene, camera));
    // Include the faint atmosphere rims while keeping the dark nebula below threshold.
    composer.addPass(new UnrealBloomPass(new Vector2(1, 1), 0.25, 0.35, 0.1));
    composer.addPass(new OutputPass());
  }

  const resize = () => {
    const width = Math.max(container.clientWidth, 1);
    const height = Math.max(container.clientHeight, 1);
    renderer.setSize(width, height);
    composer?.setSize(width, height);
    labelRenderer.setSize(width, height);
    camera.aspect = width / height;
    const overview = overviewPose(system.radius, camera.aspect);
    if (!current.warp) {
      const pose = current.focusedId === null ? overview : bodyPose(current, current.focusedId)!;
      current.tween.retarget(pose, 0);
      applyPose(camera, pose);
    }
    camera.far = Math.max(1000, Math.hypot(overview.position.x, overview.position.y, overview.position.z) + system.radius * 16);
    camera.updateProjectionMatrix();
    updateDecorations(current);
  };

  const frame = (timestamp: number) => {
    if (state !== current) {
      return;
    }
    const delta = current.lastTimestamp === null ? 0 : (timestamp - current.lastTimestamp) / 1000;
    current.lastTimestamp = timestamp;
    system.update(delta, current.warp !== null || shouldPauseOrbits(current.focusedId !== null, current.hoveredId !== null, current.highlightedId !== null));
    if (current.warp) updateWarp(current, performance.now() - current.warp.startedAt);
    else applyPose(camera, current.tween.step(delta));
    updateDecorations(current, delta);
    if (composer) composer.render(delta);
    else renderer.render(system.scene, camera);
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
    if (current.warp) return;
    void current.callbacks.invokeMethodAsync("OnBodyClicked", pick(event)).catch(() => {});
  };
  const onPointerLeave = () => {
    if (current.warp) return;
    current.hoveredId = null;
    renderer.domElement.style.cursor = "";
    updateDecorations(current);
  };
  const onPointerMove = (event: PointerEvent) => {
    if (current.warp) return;
    if (event.pointerType !== "mouse" && event.pointerType !== "pen") {
      onPointerLeave();
      return;
    }
    current.hoveredId = pick(event);
    renderer.domElement.style.cursor = current.hoveredId === null ? "" : "pointer";
    updateDecorations(current);
  };
  const labels = universe.bodies.map((body) => {
    const label = createBodyLabel(body, (id) => {
      if (current.warp) return;
      void callbacks.invokeMethodAsync("OnBodyClicked", id).catch(() => {});
    }, (id) => {
      if (current.warp) return;
      current.hoveredId = id;
      updateDecorations(current);
    });
    system.scene.add(label.object);
    return label;
  });
  const removePointerListeners = () => {
    renderer.domElement.removeEventListener("click", onClick);
    renderer.domElement.removeEventListener("pointermove", onPointerMove);
    renderer.domElement.removeEventListener("pointerleave", onPointerLeave);
    onPointerLeave();
  };
  const observer = new ResizeObserver(resize);
  const initialPose = overviewPose(system.radius, 1);
  const current: SceneState = {
    container, renderer, composer, quality,
    labelRenderer, labels, ring, originalContainerPosition, system, callbacks, camera, tween: new CameraTween(initialPose, initialPose, 0),
    focusedId: null, hoveredId: null, highlightedId: null, removePointerListeners,
    observer, onVisibilityChange, frameId: null, lastTimestamp: null, warp: null,
  };
  state = current;
  container.append(renderer.domElement, labelRenderer.domElement);
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
  if (current.warp) {
    clearTimeout(current.warp.timerId);
    current.warp.resolve();
    current.warp.overlay.remove();
  }
  current.labels.forEach((label) => label.dispose());
  current.labelRenderer.domElement.remove();
  current.ring.dispose();
  current.composer?.passes.forEach((pass) => pass.dispose());
  current.composer?.dispose();
  current.system.dispose();
  current.renderer.dispose();
  current.renderer.forceContextLoss();
  current.renderer.domElement.remove();
  if (current.originalContainerPosition !== null) current.container.style.position = current.originalContainerPosition;
}
