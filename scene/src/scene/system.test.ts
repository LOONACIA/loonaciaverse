import { afterEach, describe, expect, it, vi } from "vitest";
import { Color, Light, LineSegments, Mesh, PlaneGeometry, Points, RingGeometry, ShaderMaterial, SphereGeometry, Vector3 } from "three";
import type { BufferGeometry, Material } from "three";
import { createSystem } from "./system";
import type { UniverseDto } from "./types";

// Deliberately use arbitrary IDs and put the star after a planet.
const universe: UniverseDto = {
  bodies: [
    {
      id: "planet-a", name: "A", subtitle: "PLANET", locked: false,
      visual: { baseColor: "#6956a8", atmosphereColor: "#bda5ff", noiseScale: 4, glowIntensity: 0.4, surface: "gas" },
      orbit: { radius: 8, periodSeconds: 80, phase: 0.5 },
    },
    {
      id: "central", name: "STAR", subtitle: "STAR", locked: false,
      visual: { baseColor: "#fff2cc", atmosphereColor: "#ffd780", noiseScale: 3.5, glowIntensity: 1.8, surface: null },
      orbit: null,
    },
    {
      id: "planet-b", name: "B", subtitle: "PLANET", locked: true,
      visual: { baseColor: "#785339", atmosphereColor: "#ffb347", noiseScale: 6, glowIntensity: 0.2, surface: "desert" },
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
    expect(scene.children.filter((child) => child.userData.bodyId)).toHaveLength(universe.bodies.length);
    expect(bodies.map((mesh) => mesh.userData.bodyId)).toEqual(universe.bodies.map((body) => body.id));
    expect(bodies.every((mesh) => mesh.geometry instanceof SphereGeometry)).toBe(true);
  });

  it("uses orbit null to place a larger self-lit star at the origin", () => {
    const { scene, bodies, update } = makeSystem();
    update(30);
    const star = bodies[1];
    expect(star.position.toArray()).toEqual([0, 0, 0]);
    expect(star.material).toBeInstanceOf(ShaderMaterial);
    expect(star.material.name).toBe("star");
    expect(star.material.uniforms.uBaseColor.value).toEqual(new Color("#fff2cc"));
    for (const planet of [bodies[0], bodies[2]]) {
      expect(planet.material).toBeInstanceOf(ShaderMaterial);
      expect(planet.material.name).toBe("planet");
      expect(star.geometry.parameters.radius).toBeGreaterThan(planet.geometry.parameters.radius);
    }
    expect(scene.children.some((child) => child instanceof Light)).toBe(false);
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

  it("provides one twinkling background point cloud and releases all GPU resources", () => {
    const { scene, dispose } = makeSystem();
    const points = scene.children.filter((child) => child instanceof Points);
    expect(points).toHaveLength(1);
    for (const [name, size] of [["position", 3], ["aSize", 1], ["aColor", 3], ["aTwinkle", 3]] as const) {
      expect(points[0].geometry.getAttribute(name).count).toBe(8000);
      expect(points[0].geometry.getAttribute(name).itemSize).toBe(size);
    }
    const resources: (BufferGeometry | Material)[] = [];
    scene.traverse((object) => {
      if (object instanceof Mesh || object instanceof Points || object instanceof LineSegments) {
        resources.push(object.geometry, ...(Array.isArray(object.material) ? object.material : [object.material]));
      }
    });
    // Three surfaces, three glow children, one planet ring, nebula, stars, streaks and the meteor
    // each own a geometry and material.
    expect(resources).toHaveLength(22);
    const spies = resources.map((resource) => vi.spyOn(resource, "dispose"));
    dispose();
    spies.forEach((spy) => expect(spy).toHaveBeenCalledOnce());
    expect(scene.children).toHaveLength(0);
    dispose();
    spies.forEach((spy) => expect(spy).toHaveBeenCalledOnce());
  });

  it("adds atmosphere shells and a camera-facing corona as children outside the body pick list", () => {
    const { bodies } = makeSystem();
    bodies.forEach((body, index) => {
      expect(body.children).toHaveLength(universe.bodies[index].visual.surface === "gas" ? 2 : 1);
      const glow = body.children[0] as Mesh<SphereGeometry | PlaneGeometry, ShaderMaterial>;
      expect(glow.parent).toBe(body);
      expect(bodies).not.toContain(glow);
      expect(glow.material.uniforms.uAtmosphereColor.value).toEqual(new Color(universe.bodies[index].visual.atmosphereColor));
      expect(glow.material.uniforms.uGlowIntensity.value).toBe(universe.bodies[index].visual.glowIntensity);
      if (universe.bodies[index].orbit === null) {
        expect(glow.material.name).toBe("corona");
        expect(glow.geometry).toBeInstanceOf(PlaneGeometry);
      } else {
        expect(glow.material.name).toBe("atmosphere");
        expect(glow.geometry).toBeInstanceOf(SphereGeometry);
        expect((glow.geometry as SphereGeometry).parameters.radius).toBeCloseTo(body.geometry.parameters.radius * 1.08);
      }
    });
  });

  it("rings only gas planets, in the equatorial plane of a tilted spin axis", () => {
    const { bodies, update } = makeSystem();
    const [gas, star, desert] = bodies;
    const ring = gas.children[1] as Mesh<RingGeometry, ShaderMaterial>;
    expect(ring.geometry).toBeInstanceOf(RingGeometry);
    expect(ring.material.name).toBe("planet-ring");
    expect(bodies).not.toContain(ring);
    expect(ring.rotation.x).toBeCloseTo(-Math.PI / 2);
    expect(ring.scale.x).toBe(gas.geometry.parameters.radius);
    expect(gas.rotation.order).toBe("ZYX");
    expect(gas.rotation.z).toBeGreaterThan(0);
    for (const body of [star, desert]) {
      expect(body.rotation.z).toBe(0);
      expect(body.children.some((child) => child instanceof Mesh && child.geometry instanceof RingGeometry)).toBe(false);
    }
    // Spinning inside the tilt keeps the ring's world normal fixed.
    gas.updateMatrixWorld(true);
    const normal = new Vector3(0, 0, 1).transformDirection(ring.matrixWorld);
    update(7);
    gas.updateMatrixWorld(true);
    expect(new Vector3(0, 0, 1).transformDirection(ring.matrixWorld).distanceTo(normal)).toBeLessThan(1e-9);
  });

  it("fades the star field out as warp streaks grow", () => {
    const { scene, setWarpStreak } = makeSystem();
    const stars = scene.children.find((child) => child instanceof Points) as Points<BufferGeometry, ShaderMaterial>;
    setWarpStreak(new Vector3(0, 0, -1), 0.25);
    expect(stars.material.uniforms.uOpacity.value).toBe(0.75);
    setWarpStreak(new Vector3(0, 0, -1), 0);
    expect(stars.material.uniforms.uOpacity.value).toBe(1);
  });

  it("places the dim nebula outside the entire star field and draws it first", () => {
    const { scene, radius } = makeSystem();
    const nebula = scene.children.find((child) => child instanceof Mesh && child.material.name === "nebula") as Mesh<SphereGeometry, ShaderMaterial>;
    const stars = scene.children.find((child) => child instanceof Points) as Points;
    const positions = stars.geometry.getAttribute("position");
    expect(nebula.geometry.parameters.radius).toBe(radius * 14);
    expect(nebula.renderOrder).toBeLessThan(stars.renderOrder);
    for (let index = 0; index < positions.count; index++) {
      expect(new Vector3().fromBufferAttribute(positions, index).length()).toBeLessThan(nebula.geometry.parameters.radius);
    }
  });

  it("keeps surface, corona, nebula and star field time flowing while orbits are paused", () => {
    const { scene, bodies, update } = makeSystem();
    const initial = bodies.map((mesh) => mesh.position.clone());
    update(2, false);
    update(3, true);
    const materials: ShaderMaterial[] = [];
    scene.traverse((object) => {
      if ((object instanceof Mesh || object instanceof Points) && object.material instanceof ShaderMaterial && object.material.uniforms.uTime) {
        materials.push(object.material);
      }
    });
    expect(materials).toHaveLength(universe.bodies.length + 3);
    materials.forEach((material) => expect(material.uniforms.uTime.value).toBe(5));
    expect(bodies[0].position.equals(initial[0])).toBe(false);
    const paused = bodies.map((mesh) => mesh.position.clone());
    update(4, true);
    bodies.forEach((body, index) => expect(body.position.equals(paused[index])).toBe(true));
    materials.forEach((material) => expect(material.uniforms.uTime.value).toBe(9));
  });

  it("derives appearance from visual and orbit type regardless of ID, name or lock", () => {
    const original = makeSystem();
    const renamed = createSystem({ bodies: universe.bodies.map((body, index) => ({
      ...body, id: `changed-${index}`, name: "RENAMED", subtitle: "CHANGED", locked: !body.locked,
    })) });
    systems.push(renamed);
    original.bodies.forEach((body, index) => {
      const other = renamed.bodies[index];
      expect(other.geometry.parameters).toEqual(body.geometry.parameters);
      for (const [first, second] of [[body.material, other.material], [
        (body.children[0] as Mesh<BufferGeometry, ShaderMaterial>).material,
        (other.children[0] as Mesh<BufferGeometry, ShaderMaterial>).material,
      ]]) {
        expect(second.name).toBe(first.name);
        expect(second.uniforms).toEqual(first.uniforms);
        expect(second.vertexShader).toBe(first.vertexShader);
        expect(second.fragmentShader).toBe(first.fragmentShader);
      }
    });
  });

  it("pauses all orbits without stopping spin or jumping ahead on resume", () => {
    const { bodies, update } = makeSystem();
    const initial = bodies.map((mesh) => mesh.position.clone());
    update(100, true);
    bodies.forEach((mesh, index) => {
      expect(mesh.position.equals(initial[index])).toBe(true);
      expect(mesh.rotation.y).toBeGreaterThan(0);
    });
    update(20, false);
    expect(bodies[0].position.x).toBeCloseTo(8 * Math.cos(0.5 + Math.PI / 2));
    expect(bodies[0].position.z).toBeCloseTo(8 * Math.sin(0.5 + Math.PI / 2));
  });
});
