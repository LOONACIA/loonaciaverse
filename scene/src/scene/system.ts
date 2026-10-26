import { AdditiveBlending, AmbientLight, BufferGeometry, Color, Float32BufferAttribute, LineBasicMaterial, LineSegments, Mesh, MeshBasicMaterial, MeshStandardMaterial, PointLight, Points, PointsMaterial, Scene, SphereGeometry } from "three";
import type { Vector3 } from "three";
import { OrbitClock, orbitPosition } from "./orbit";
import type { UniverseDto } from "./types";

const STAR_COUNT = 3000;
const STAR_RADIUS = 2;
const PLANET_RADIUS = 0.9;
const SPIN_RADIANS_PER_SECOND = 0.12;

export function createSystem(universe: UniverseDto) {
  const scene = new Scene();
  scene.background = new Color("#05070d");
  const clock = new OrbitClock();
  const bodies = universe.bodies.map((body) => {
    const isStar = body.orbit === null;
    const geometry = new SphereGeometry(isStar ? STAR_RADIUS : PLANET_RADIUS, 48, 32);
    const material = isStar
      ? new MeshBasicMaterial({ color: body.visual.baseColor })
      : new MeshStandardMaterial({ color: body.visual.baseColor, roughness: 0.85 });
    const mesh = new Mesh(geometry, material);
    mesh.userData.bodyId = body.id;
    const position = orbitPosition(body.orbit, 0);
    mesh.position.set(position.x, 0, position.z);
    scene.add(mesh);

    if (isStar) {
      const light = new PointLight(body.visual.baseColor, 600);
      light.position.copy(mesh.position);
      scene.add(light);
    }

    return mesh;
  });
  scene.add(new AmbientLight("#9aacc8", 0.15));

  // Include each body's size when framing the entire orbit, even at its nearest edge.
  const radius = Math.max(STAR_RADIUS, ...universe.bodies.map((body) => body.orbit === null ? STAR_RADIUS : body.orbit.radius + PLANET_RADIUS));
  const positions = new Float32Array(STAR_COUNT * 3);
  for (let index = 0; index < STAR_COUNT; index++) {
    const azimuth = Math.random() * Math.PI * 2;
    const y = Math.random() * 2 - 1;
    const horizontal = Math.sqrt(1 - y * y);
    const distance = radius * (8 + Math.random() * 4);
    positions.set([Math.cos(azimuth) * horizontal * distance, y * distance, Math.sin(azimuth) * horizontal * distance], index * 3);
  }
  const starGeometry = new BufferGeometry();
  starGeometry.setAttribute("position", new Float32BufferAttribute(positions, 3));
  const starMaterial = new PointsMaterial({ color: "#d5e5ff", size: 0.12, sizeAttenuation: true });
  scene.add(new Points(starGeometry, starMaterial));

  // Each streak starts at an existing star and extends along the travel direction.
  const streakPositions = new Float32BufferAttribute(new Float32Array(STAR_COUNT * 6), 3);
  const streakGeometry = new BufferGeometry();
  streakGeometry.setAttribute("position", streakPositions);
  const streakMaterial = new LineBasicMaterial({ color: "#d5e5ff", transparent: true, opacity: 0, blending: AdditiveBlending, depthWrite: false });
  const streaks = new LineSegments(streakGeometry, streakMaterial);
  streaks.visible = false;
  streaks.frustumCulled = false;
  scene.add(streaks);

  return {
    scene,
    bodies,
    radius,
    setWarpStreak(direction: Vector3, amount: number): void {
      streaks.visible = amount > 0;
      streakMaterial.opacity = amount;
      starMaterial.opacity = 1 - amount;
      starMaterial.transparent = true;
      const length = radius * 6 * amount;
      for (let index = 0; index < STAR_COUNT; index++) {
        const offset = index * 3;
        const x = positions[offset], y = positions[offset + 1], z = positions[offset + 2];
        streakPositions.setXYZ(index * 2, x, y, z);
        streakPositions.setXYZ(index * 2 + 1, x + direction.x * length, y + direction.y * length, z + direction.z * length);
      }
      streakPositions.needsUpdate = true;
    },
    update(deltaSeconds: number, orbitsPaused = false): void {
      clock.advance(deltaSeconds, orbitsPaused);
      bodies.forEach((mesh, index) => {
        const position = orbitPosition(universe.bodies[index].orbit, clock.seconds);
        mesh.position.set(position.x, 0, position.z);
        mesh.rotation.y += deltaSeconds * SPIN_RADIANS_PER_SECOND;
      });
    },
    dispose(): void {
      bodies.forEach((mesh) => {
        mesh.geometry.dispose();
        mesh.material.dispose();
      });
      starGeometry.dispose();
      starMaterial.dispose();
      streakGeometry.dispose();
      streakMaterial.dispose();
      scene.clear();
    },
  };
}
