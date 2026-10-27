import { describe, expect, it, vi } from "vitest";
import { Color, Mesh, MeshBasicMaterial, SphereGeometry } from "three";
import { createFocusRing, fadeRingOpacity, revealOrbit, ringOpacityTarget } from "./ring";

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

  it("reveals the orbit in one second without overshooting or reversing for negative time", () => {
    expect(revealOrbit(0, 0.5)).toBeCloseTo(0.5);
    expect(revealOrbit(0.5, 0.5)).toBe(1);
    expect(revealOrbit(0.5, 10)).toBe(1);
    expect(revealOrbit(0.5, -1)).toBe(0.5);
    expect(revealOrbit(-1, 0)).toBe(0);
  });
});

describe("focus ring", () => {
  it("follows the selected body's size and position, lies in the orbital plane, rotates, fades and hides immediately in warp", () => {
    const ring = createFocusRing("#ffb347");
    const body = new Mesh(new SphereGeometry(2), new MeshBasicMaterial());
    body.userData.bodyId = "star";
    body.position.set(3, 0, 2);
    const { mesh } = ring;
    expect(mesh.visible).toBe(false);
    expect(mesh.material.uniforms.uColor.value).toEqual(new Color("#ffb347"));
    expect(mesh.material.depthWrite).toBe(false);
    ring.focus(body);
    ring.update(0.15, false);
    expect(mesh.visible).toBe(true);
    expect(mesh.material.uniforms.uOpacity.value).toBeCloseTo(0.5);
    expect(mesh.position.equals(body.position)).toBe(true);
    expect(mesh.scale.toArray()).toEqual([2, 2, 2]);
    expect(mesh.rotation.x).toBeCloseTo(-Math.PI / 2);
    const rotation = mesh.quaternion.clone();
    ring.update(0.15, false);
    expect(mesh.quaternion.equals(rotation)).toBe(false);
    ring.focus(null);
    ring.update(0.15, false);
    expect(mesh.material.uniforms.uOpacity.value).toBeCloseTo(0.5);
    expect(mesh.position.equals(body.position)).toBe(true);
    ring.update(0.15, false);
    expect(mesh.visible).toBe(false);
    ring.focus(body, true);
    ring.update(0, false);
    expect(mesh.visible).toBe(true);
    ring.update(0, true);
    expect(mesh.visible).toBe(false);
    expect(mesh.material.uniforms.uOpacity.value).toBe(0);
    const geometryDispose = vi.spyOn(mesh.geometry, "dispose");
    const materialDispose = vi.spyOn(mesh.material, "dispose");
    const orbitGeometryDispose = vi.spyOn(ring.orbit.geometry, "dispose");
    const orbitMaterialDispose = vi.spyOn(ring.orbit.material, "dispose");
    ring.dispose();
    expect(geometryDispose).toHaveBeenCalledOnce();
    expect(materialDispose).toHaveBeenCalledOnce();
    expect(orbitGeometryDispose).toHaveBeenCalledOnce();
    expect(orbitMaterialDispose).toHaveBeenCalledOnce();
    body.geometry.dispose();
    body.material.dispose();
  });
});

describe("focus orbit", () => {
  it("draws the planet's orbit outward from the planet in the orbital plane and hides it for the star", () => {
    const ring = createFocusRing("#5fd4ff");
    const { orbit } = ring;
    const uniforms = orbit.material.uniforms;
    const planet = new Mesh(new SphereGeometry(0.9), new MeshBasicMaterial());
    planet.userData.bodyId = "projects";
    planet.position.set(0, 0, 8);
    expect(orbit.visible).toBe(false);
    expect(orbit.rotation.x).toBeCloseTo(-Math.PI / 2);
    ring.focus(planet);
    ring.update(0.15, false);
    expect(orbit.visible).toBe(true);
    expect(orbit.scale.x).toBe(8);
    expect(uniforms.uRadius.value).toBe(8);
    expect(uniforms.uPlanetAngle.value).toBeCloseTo(-Math.PI / 2);
    expect(uniforms.uReveal.value).toBeCloseTo(0.15);
    expect(uniforms.uOpacity.value).toBe(ring.mesh.material.uniforms.uOpacity.value);
    ring.update(10, false);
    expect(uniforms.uReveal.value).toBe(1);

    const star = new Mesh(new SphereGeometry(2), new MeshBasicMaterial());
    star.userData.bodyId = "about";
    ring.focus(star);
    expect(uniforms.uReveal.value).toBe(0);
    ring.update(0.15, false);
    expect(ring.mesh.visible).toBe(true);
    expect(orbit.visible).toBe(false);

    ring.focus(planet, true);
    expect(uniforms.uReveal.value).toBe(1);
    ring.update(0, true);
    expect(orbit.visible).toBe(false);
    ring.dispose();
    [planet, star].forEach((body) => { body.geometry.dispose(); body.material.dispose(); });
  });
});
