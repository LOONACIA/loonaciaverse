import { AdditiveBlending, BufferGeometry, Color, Float32BufferAttribute, LineBasicMaterial, LineSegments, Mesh, PlaneGeometry, Points, RingGeometry, Scene, SphereGeometry } from "three";
import type { ShaderMaterial, Vector3 } from "three";
import {
  createAtmosphereMaterial, createCloudMaterial, createCoronaMaterial, createNebulaMaterial, createPlanetMaterial,
  createPlanetRingMaterial, createStarFieldMaterial, createStarMaterial, PLANET_RING_INNER, PLANET_RING_OUTER,
} from "./materials";
import { createMeteor } from "./meteor";
import { OrbitClock, orbitPosition } from "./orbit";
import { DESKTOP_STAR_COUNT } from "./quality";
import type { UniverseDto } from "./types";

const STAR_RADIUS = 3;
const PLANET_RADIUS = 1.35;
const SPIN_RADIANS_PER_SECOND = 0.12;
// Gas giants lean, so the fixed camera sees their equatorial ring as a tilted ellipse.
const RINGED_AXIAL_TILT = 0.4;
// Terran clouds float this far above the ground, in planet radii, below the 1.08x atmosphere shell.
const CLOUD_HEIGHT = 1.02;
// Mostly cool white stars, with a few warm, pink and teal ones for the dreamy palette.
const STAR_TINTS = ["#e4ecff", "#e4ecff", "#e4ecff", "#b4cdff", "#b4cdff", "#ffe4c4", "#ffc8ea", "#bff7f0"].map((tint) => new Color(tint));

export function createSystem(universe: UniverseDto, starCount = DESKTOP_STAR_COUNT, pixelRatio = 1) {
  const scene = new Scene();
  scene.background = new Color("#05070d");
  const clock = new OrbitClock();
  const animatedMaterials: ShaderMaterial[] = [];
  let elapsedSeconds = 0;
  const bodies = universe.bodies.map((body) => {
    const isStar = body.orbit === null;
    const geometry = new SphereGeometry(isStar ? STAR_RADIUS : PLANET_RADIUS, 48, 32);
    const material = isStar
      ? createStarMaterial(body.visual)
      : createPlanetMaterial(body.visual);
    animatedMaterials.push(material);
    const mesh = new Mesh(geometry, material);
    mesh.userData.bodyId = body.id;
    const position = orbitPosition(body.orbit, 0);
    mesh.position.set(position.x, 0, position.z);
    scene.add(mesh);

    if (isStar) {
      const corona = createCoronaMaterial(body.visual);
      animatedMaterials.push(corona);
      // The glow fades out at twice the star's radius.
      mesh.add(new Mesh(new PlaneGeometry(STAR_RADIUS * 4, STAR_RADIUS * 4), corona));
    } else {
      mesh.add(new Mesh(new SphereGeometry(PLANET_RADIUS * 1.08, 48, 32), createAtmosphereMaterial(body.visual)));
    }
    if (body.visual.surface === "terran") {
      // Shares the planet's frame and time, so the ground draws shadows of the same clouds.
      const clouds = createCloudMaterial(body.visual);
      animatedMaterials.push(clouds);
      mesh.add(new Mesh(new SphereGeometry(PLANET_RADIUS * CLOUD_HEIGHT, 48, 32), clouds));
    }
    if (body.visual.surface === "gas") {
      // Spin (Y) runs inside the tilt (Z), so the ring stays in the equatorial plane.
      mesh.rotation.order = "ZYX";
      mesh.rotation.z = RINGED_AXIAL_TILT;
      const ring = new Mesh(new RingGeometry(PLANET_RING_INNER, PLANET_RING_OUTER, 128), createPlanetRingMaterial(body.visual));
      ring.rotation.x = -Math.PI / 2;
      ring.scale.setScalar(PLANET_RADIUS);
      mesh.add(ring);
    }

    return mesh;
  });

  // Include each body's size when framing the entire orbit, even at its nearest edge.
  const radius = Math.max(STAR_RADIUS, ...universe.bodies.map((body) => body.orbit === null ? STAR_RADIUS : body.orbit.radius + PLANET_RADIUS));
  const nebulaMaterial = createNebulaMaterial();
  // Dense enough for the per-vertex warp to interpolate smoothly.
  const nebula = new Mesh(new SphereGeometry(radius * 14, 128, 64), nebulaMaterial);
  nebula.renderOrder = -1;
  scene.add(nebula);
  animatedMaterials.push(nebulaMaterial);
  const positions = new Float32Array(starCount * 3);
  const sizes = new Float32Array(starCount);
  const colors = new Float32Array(starCount * 3);
  const twinkles = new Float32Array(starCount * 3);
  const color = new Color();
  for (let index = 0; index < starCount; index++) {
    const azimuth = Math.random() * Math.PI * 2;
    const y = Math.random() * 2 - 1;
    const horizontal = Math.sqrt(1 - y * y);
    const distance = radius * (8 + Math.random() * 4);
    positions.set([Math.cos(azimuth) * horizontal * distance, y * distance, Math.sin(azimuth) * horizontal * distance], index * 3);
    // Cubing skews the field toward faint stars, leaving a few large bright ones.
    const magnitude = Math.random() ** 3;
    sizes[index] = 1.6 + magnitude * 5;
    color.copy(STAR_TINTS[Math.floor(Math.random() * STAR_TINTS.length)]).multiplyScalar(0.35 + 0.65 * magnitude);
    colors.set([color.r, color.g, color.b], index * 3);
    twinkles.set([Math.random() * Math.PI * 2, 0.5 + Math.random() * 2, 0.15 + Math.random() * 0.45], index * 3);
  }
  const starGeometry = new BufferGeometry();
  starGeometry.setAttribute("position", new Float32BufferAttribute(positions, 3));
  starGeometry.setAttribute("aSize", new Float32BufferAttribute(sizes, 1));
  starGeometry.setAttribute("aColor", new Float32BufferAttribute(colors, 3));
  starGeometry.setAttribute("aTwinkle", new Float32BufferAttribute(twinkles, 3));
  const starMaterial = createStarFieldMaterial(pixelRatio);
  animatedMaterials.push(starMaterial);
  scene.add(new Points(starGeometry, starMaterial));
  // Between the inner and outer star shells.
  const meteor = createMeteor(radius * 9);
  scene.add(meteor.mesh);

  // Each streak starts at an existing star and extends along the travel direction.
  const streakPositions = new Float32BufferAttribute(new Float32Array(starCount * 6), 3);
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
      starMaterial.uniforms.uOpacity.value = 1 - amount;
      const length = radius * 6 * amount;
      for (let index = 0; index < starCount; index++) {
        const offset = index * 3;
        const x = positions[offset], y = positions[offset + 1], z = positions[offset + 2];
        streakPositions.setXYZ(index * 2, x, y, z);
        streakPositions.setXYZ(index * 2 + 1, x + direction.x * length, y + direction.y * length, z + direction.z * length);
      }
      streakPositions.needsUpdate = true;
    },
    update(deltaSeconds: number, orbitsPaused = false): void {
      clock.advance(deltaSeconds, orbitsPaused);
      elapsedSeconds += deltaSeconds;
      animatedMaterials.forEach((material) => { material.uniforms.uTime.value = elapsedSeconds; });
      meteor.update(deltaSeconds);
      bodies.forEach((mesh, index) => {
        const position = orbitPosition(universe.bodies[index].orbit, clock.seconds);
        mesh.position.set(position.x, 0, position.z);
        mesh.rotation.y += deltaSeconds * SPIN_RADIANS_PER_SECOND;
      });
    },
    dispose(): void {
      // Traverse children too: atmosphere shells and the corona own GPU resources.
      scene.traverse((object) => {
        if (object instanceof Mesh || object instanceof Points || object instanceof LineSegments) {
          object.geometry.dispose();
          const materials = Array.isArray(object.material) ? object.material : [object.material];
          materials.forEach((material) => material.dispose());
        }
      });
      scene.clear();
    },
  };
}
