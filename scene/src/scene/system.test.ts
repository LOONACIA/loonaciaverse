import { afterEach, describe, expect, it, vi } from "vitest";
import { Mesh, MeshBasicMaterial, MeshStandardMaterial, PointLight, Points, SphereGeometry } from "three";
import { createSystem } from "./system";
import type { UniverseDto } from "./types";

// Deliberately use arbitrary IDs and put the star after a planet.
const universe: UniverseDto = {
  bodies: [
    {
      id: "planet-a", name: "A", subtitle: "PLANET", locked: false,
      visual: { baseColor: "#236b8e", atmosphereColor: "#5fd4ff", noiseScale: 4, glowIntensity: 0.4 },
      orbit: { radius: 8, periodSeconds: 80, phase: 0.5 },
    },
    {
      id: "central", name: "STAR", subtitle: "STAR", locked: false,
      visual: { baseColor: "#fff2cc", atmosphereColor: "#ffd780", noiseScale: 3.5, glowIntensity: 1.8 },
      orbit: null,
    },
    {
      id: "planet-b", name: "B", subtitle: "PLANET", locked: true,
      visual: { baseColor: "#785339", atmosphereColor: "#ffb347", noiseScale: 6, glowIntensity: 0.2 },
      orbit: { radius: 16, periodSeconds: 120, phase: 4.5 },
    },
  ],
};

const systems: ReturnType<typeof createSystem>[] = [];
function makeSystem() {
  const system = createSystem(universe);
  systems.push(system);
  return system;
}

afterEach(() => {
  systems.forEach((system) => system.dispose());
  systems.length = 0;
  vi.restoreAllMocks();
});

describe("createSystem", () => {
  it("creates one sphere mesh per body with its data ID", () => {
    const { scene, bodies } = makeSystem();
    expect(scene.children.filter((child) => child instanceof Mesh)).toHaveLength(universe.bodies.length);
    expect(bodies.map((mesh) => mesh.userData.bodyId)).toEqual(universe.bodies.map((body) => body.id));
    expect(bodies.every((mesh) => mesh.geometry instanceof SphereGeometry)).toBe(true);
  });

  it("uses orbit null to place a larger self-lit star at the origin", () => {
    const { scene, bodies, update } = makeSystem();
    update(30);
    const star = bodies[1];
    expect(star.position.toArray()).toEqual([0, 0, 0]);
    expect(star.material).toBeInstanceOf(MeshBasicMaterial);
    expect(bodies[0].material).toBeInstanceOf(MeshStandardMaterial);
    expect(star.material.color.getHexString()).toBe("fff2cc");
    for (const planet of [bodies[0], bodies[2]]) {
      expect(star.geometry.parameters.radius).toBeGreaterThan(planet.geometry.parameters.radius);
    }
    const light = scene.children.find((child) => child instanceof PointLight)!;
    expect(light.position.toArray()).toEqual(star.position.toArray());
  });

  it("moves planets along their XZ orbits while every body spins", () => {
    const { bodies, update } = makeSystem();
    const initialPosition = bodies[0].position.clone();
    update(20);
    expect(bodies[0].position.equals(initialPosition)).toBe(false);
    bodies.forEach((mesh, index) => {
      expect(mesh.rotation.y).toBeGreaterThan(0);
      expect(mesh.position.y).toBe(0);
      expect(mesh.position.length()).toBeCloseTo(universe.bodies[index].orbit?.radius ?? 0);
    });
    expect(bodies[0].position.x).toBeCloseTo(8 * Math.cos(0.5 + Math.PI / 2));
    expect(bodies[0].position.z).toBeCloseTo(8 * Math.sin(0.5 + Math.PI / 2));
  });

  it("provides one background point cloud and releases all GPU resources", () => {
    const { scene, bodies, dispose } = makeSystem();
    const points = scene.children.filter((child) => child instanceof Points);
    expect(points).toHaveLength(1);
    expect(points[0].geometry.getAttribute("position").count).toBe(3000);
    const resources = [...bodies.flatMap((mesh) => [mesh.geometry, mesh.material]), points[0].geometry, points[0].material];
    const spies = resources.map((resource) => vi.spyOn(resource, "dispose"));
    dispose();
    spies.forEach((spy) => expect(spy).toHaveBeenCalledOnce());
    expect(scene.children).toHaveLength(0);
  });
});
