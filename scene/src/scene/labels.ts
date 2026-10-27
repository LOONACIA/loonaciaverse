import { Vector3 } from "three";
import type { Mesh, PerspectiveCamera, SphereGeometry } from "three";
import { CSS2DObject } from "three/addons/renderers/CSS2DRenderer.js";
import type { BodyDto } from "./types";

// Labels appear only for the hovered or keyboard-highlighted body; the focused body's name is in the HUD.
export function isLabelVisible(bodyId: string, focusedId: string | null, emphasizedId: string | null): boolean {
  return bodyId !== focusedId && bodyId === emphasizedId;
}

export function createBodyLabel(body: Pick<BodyDto, "id" | "name" | "subtitle">, onClick: (id: string) => void, onHover: (id: string | null) => void) {
  const element = document.createElement("div");
  element.className = "scene-label";
  // Keyboard navigation and accessible names belong to the Blazor body buttons.
  element.setAttribute("aria-hidden", "true");
  element.style.pointerEvents = "auto";
  const name = document.createElement("span");
  name.className = "scene-label-name";
  name.textContent = body.name;
  const subtitle = document.createElement("span");
  subtitle.className = "scene-label-subtitle";
  subtitle.textContent = body.subtitle;
  element.append(name, subtitle);

  const click = (event: MouseEvent) => { event.stopPropagation(); onClick(body.id); };
  const enter = (event: PointerEvent) => {
    if (event.pointerType === "mouse" || event.pointerType === "pen") onHover(body.id);
  };
  const leave = () => onHover(null);
  element.addEventListener("click", click);
  element.addEventListener("pointerenter", enter);
  element.addEventListener("pointerleave", leave);
  const object = new CSS2DObject(element);
  const offset = new Vector3();
  const projected = new Vector3();

  return {
    object,
    update(mesh: Mesh<SphereGeometry>, camera: PerspectiveCamera, focusedId: string | null, emphasizedId: string | null, warping: boolean): void {
      object.visible = !warping && isLabelVisible(body.id, focusedId, emphasizedId);
      // Hide immediately too, including while the render loop is suspended.
      if (!object.visible) element.style.display = "none";
      // Offset along camera-right, independently of the body's axial rotation.
      // Right-hand bodies put their label on the left to stay inside the viewport.
      const side = projected.copy(mesh.position).project(camera).x > 0.45 ? -1 : 1;
      object.center.set(side === 1 ? 0 : 1, 0.5);
      offset.set(side * mesh.geometry.parameters.radius * 1.45, 0, 0).applyQuaternion(camera.quaternion);
      object.position.copy(mesh.position).add(offset);
    },
    dispose(): void {
      element.removeEventListener("click", click);
      element.removeEventListener("pointerenter", enter);
      element.removeEventListener("pointerleave", leave);
      object.removeFromParent();
      element.remove();
    },
  };
}
