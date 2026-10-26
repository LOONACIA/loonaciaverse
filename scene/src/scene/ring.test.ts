import { describe, expect, it, vi } from "vitest";
import { Color, Mesh, MeshBasicMaterial, PerspectiveCamera, SphereGeometry } from "three";
import { createFocusRing, fadeRingOpacity, ringOpacityTarget } from "./ring";

describe("ring opacity", () => {
  it.each([
    [null, false, 0], [null, true, 0], ["blog", false, 1], ["blog", true, 0],
  ] as const)("focus %s, warp %s target %s", (focusedId, warping, expected) => {
    expect(ringOpacityTarget(focusedId, warping)).toBe(expected);
  });

  it("fades in and out in 0.3 seconds without overshooting or reversing for negative time", () => {
    expect(fadeRingOpacity(0, 1, 0)).toBe(0);
    expect(fadeRingOpacity(0, 1, 0.15)).toBeCloseTo(0.5);
    expect(fadeRingOpacity(0, 1, 0.3)).toBe(1);
    expect(fadeRingOpacity(0, 1, 10)).toBe(1);
    expect(fadeRingOpacity(1, 0, 0.15)).toBeCloseTo(0.5);
    expect(fadeRingOpacity(1, 0, 0.3)).toBe(0);
    expect(fadeRingOpacity(1, 0, 10)).toBe(0);
    expect(fadeRingOpacity(0.5, 1, -1)).toBe(0.5);
    expect(fadeRingOpacity(0.5, 0, -1)).toBe(0.5);
    expect(fadeRingOpacity(-1, 0, 0)).toBe(0);
    expect(fadeRingOpacity(2, 1, 0)).toBe(1);
  });
});

describe("focus ring", () => {
  it("follows the selected body's size and position, rotates, fades and hides immediately in warp", () => {
    const ring = createFocusRing("#ffb347");
    const body = new Mesh(new SphereGeometry(2), new MeshBasicMaterial());
    body.userData.bodyId = "star";
    body.position.set(3, 0, 2);
    const camera = new PerspectiveCamera();
    camera.rotation.x = -0.5;
    const { mesh } = ring;
    expect(mesh.visible).toBe(false);
    expect(mesh.material.uniforms.uColor.value).toEqual(new Color("#ffb347"));
    expect(mesh.material.depthWrite).toBe(false);
    ring.focus(body);
    ring.update(0.15, camera, false);
    expect(mesh.visible).toBe(true);
    expect(mesh.material.uniforms.uOpacity.value).toBeCloseTo(0.5);
    expect(mesh.position.equals(body.position)).toBe(true);
    expect(mesh.scale.toArray()).toEqual([2, 2, 2]);
    const rotation = mesh.quaternion.clone();
    ring.update(0.15, camera, false);
    expect(mesh.quaternion.equals(rotation)).toBe(false);
    ring.focus(null);
    ring.update(0.15, camera, false);
    expect(mesh.material.uniforms.uOpacity.value).toBeCloseTo(0.5);
    expect(mesh.position.equals(body.position)).toBe(true);
    ring.update(0.15, camera, false);
    expect(mesh.visible).toBe(false);
    ring.focus(body, true);
    ring.update(0, camera, false);
    expect(mesh.visible).toBe(true);
    ring.update(0, camera, true);
    expect(mesh.visible).toBe(false);
    expect(mesh.material.uniforms.uOpacity.value).toBe(0);
    const geometryDispose = vi.spyOn(mesh.geometry, "dispose");
    const materialDispose = vi.spyOn(mesh.material, "dispose");
    ring.dispose();
    expect(geometryDispose).toHaveBeenCalledOnce();
    expect(materialDispose).toHaveBeenCalledOnce();
    body.geometry.dispose();
    body.material.dispose();
  });
});
