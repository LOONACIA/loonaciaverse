import { describe, expect, it, vi } from "vitest";
import { Mesh, MeshBasicMaterial, PerspectiveCamera, SphereGeometry } from "three";
import { createBodyLabel, labelState } from "./labels";

const body = { id: "blog", name: "ARCHIVA", subtitle: "BLOG" };

describe("label state", () => {
  it.each([
    [null, null, "normal"],
    [null, "blog", "emphasized"],
    [null, "other", "normal"],
    ["blog", null, "hidden"],
    ["blog", "blog", "hidden"],
    ["blog", "other", "hidden"],
    ["other", null, "normal"],
    ["other", "blog", "emphasized"],
    ["other", "other", "normal"],
  ] as const)("focus %s and emphasis %s yield %s", (focusedId, emphasizedId, expected) => {
    expect(labelState("blog", focusedId, emphasizedId)).toBe(expected);
  });
});

describe("body labels", () => {
  it("creates the name and subtitle spans using text content", () => {
    const label = createBodyLabel(body, vi.fn(), vi.fn());
    expect(label.object.element.outerHTML).toContain('class="scene-label"');
    expect([...label.object.element.children].map((child) => [child.tagName, child.className, child.textContent])).toEqual([
      ["SPAN", "scene-label-name", "ARCHIVA"], ["SPAN", "scene-label-subtitle", "BLOG"],
    ]);
    expect(label.object.element.getAttribute("aria-hidden")).toBe("true");
    expect(label.object.element.style.pointerEvents).toBe("auto");
    label.dispose();
    const unsafe = createBodyLabel({ ...body, name: "<img src=x>", subtitle: "<script>" }, vi.fn(), vi.fn());
    expect(unsafe.object.element.querySelector("img, script")).toBeNull();
    expect(unsafe.object.element.firstElementChild?.textContent).toBe("<img src=x>");
    unsafe.dispose();
  });

  it("reports clicks and mouse/pen hover with its body ID and removes listeners on disposal", () => {
    const click = vi.fn(), hover = vi.fn(), parentClick = vi.fn();
    const label = createBodyLabel(body, click, hover);
    const element = label.object.element;
    const parent = document.createElement("div");
    parent.addEventListener("click", parentClick);
    parent.append(element);
    element.firstElementChild!.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    expect(click).toHaveBeenCalledExactlyOnceWith("blog");
    expect(parentClick).not.toHaveBeenCalled();
    element.dispatchEvent(new PointerEvent("pointerenter", { pointerType: "touch" }));
    expect(hover).not.toHaveBeenCalled();
    for (const pointerType of ["mouse", "pen"]) {
      element.dispatchEvent(new PointerEvent("pointerenter", { pointerType }));
      expect(hover).toHaveBeenLastCalledWith("blog");
      element.dispatchEvent(new PointerEvent("pointerleave"));
      expect(hover).toHaveBeenLastCalledWith(null);
    }
    label.dispose();
    click.mockClear();
    hover.mockClear();
    element.click();
    element.dispatchEvent(new PointerEvent("pointerenter", { pointerType: "mouse" }));
    element.dispatchEvent(new PointerEvent("pointerleave"));
    expect(click).not.toHaveBeenCalled();
    expect(hover).not.toHaveBeenCalled();
    expect(parent.children).toHaveLength(0);
  });

  it("offsets beside a spinning body and applies focus, emphasis and warp visibility", () => {
    const label = createBodyLabel(body, vi.fn(), vi.fn());
    const mesh = new Mesh(new SphereGeometry(2), new MeshBasicMaterial());
    const camera = new PerspectiveCamera(45, 1.5, 0.1, 100);
    camera.position.z = 20;
    camera.updateMatrixWorld();
    label.update(mesh, camera, null, "blog", false);
    const initial = label.object.position.clone();
    expect(initial.x).toBeGreaterThan(2);
    expect(label.object.element.classList.contains("is-emphasized")).toBe(true);
    mesh.rotation.y = Math.PI;
    label.update(mesh, camera, null, null, false);
    expect(label.object.position.equals(initial)).toBe(true);
    expect(label.object.element.classList.contains("is-emphasized")).toBe(false);
    label.update(mesh, camera, "blog", "blog", false);
    expect(label.object.visible).toBe(false);
    label.update(mesh, camera, null, null, true);
    expect(label.object.visible).toBe(false);
    expect(label.object.element.style.display).toBe("none");
    mesh.position.x = 8;
    label.update(mesh, camera, null, null, false);
    expect(label.object.visible).toBe(true);
    expect(label.object.position.x).toBeLessThan(mesh.position.x - 2);
    expect(label.object.center.x).toBe(1);
    label.dispose();
    mesh.geometry.dispose();
    mesh.material.dispose();
  });
});
