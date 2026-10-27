import { AdditiveBlending, Color, DoubleSide, Mesh, RingGeometry, ShaderMaterial } from "three";
import type { SphereGeometry } from "three";

const ORBIT_REVEAL_SECONDS = 1;

export function ringOpacityTarget(focusedId: string | null, warping: boolean): number {
  return focusedId !== null && !warping ? 1 : 0;
}

export function fadeRingOpacity(opacity: number, target: number, deltaSeconds: number): number {
  const step = Math.max(0, deltaSeconds) / 0.3;
  const bounded = Math.max(0, Math.min(1, opacity));
  return target > bounded ? Math.min(target, bounded + step) : Math.max(target, bounded - step);
}

export function revealOrbit(reveal: number, deltaSeconds: number): number {
  return Math.min(1, Math.max(0, reveal) + Math.max(0, deltaSeconds) / ORBIT_REVEAL_SECONDS);
}

// The selected planet's orbit, drawn outward from the planet in both directions.
function createOrbitTrail(color: string) {
  const material = new ShaderMaterial({
    name: "focus-orbit",
    uniforms: {
      uColor: { value: new Color(color) }, uOpacity: { value: 0 }, uRadius: { value: 1 }, uPlanetAngle: { value: 0 }, uReveal: { value: 0 },
    },
    vertexShader: /* glsl */ `
      varying vec2 vPosition;
      void main() {
        vPosition = position.xy;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: /* glsl */ `
      uniform vec3 uColor;
      uniform float uOpacity;
      uniform float uRadius;
      uniform float uPlanetAngle;
      uniform float uReveal;
      varying vec2 vPosition;
      const float PI = 3.14159265;
      void main() {
        float fromPlanet = abs(mod(atan(vPosition.y, vPosition.x) - uPlanetAngle + PI, 2.0 * PI) - PI);
        float drawn = uReveal * PI;
        float visible = 1.0 - smoothstep(drawn - 0.02, drawn, fromPlanet);
        // A bright leading edge while drawing, then the trail dims away from the planet.
        float head = exp(-(drawn - fromPlanet) * 12.0) * (1.0 - uReveal);
        float trail = mix(1.0, 0.2, fromPlanet / PI) + head;
        float radius = length(vPosition);
        float distance = abs(radius - 1.0) * uRadius;
        float line = (1.0 - smoothstep(0.004, 0.014, distance)) * 0.8 + exp(-distance * 50.0) * 0.3;

        // Grid on the orbital plane inside the orbit, brighter toward the orbit line.
        float inside = 1.0 - smoothstep(0.985, 0.995, radius);
        float toOrbit = smoothstep(0.15, 1.0, radius);
        float fill = toOrbit * toOrbit * toOrbit * 0.04;
        // Widths are in world units, so the grid lines stay as thin as the orbit line.
        float ringDistance = abs(fract(radius * 6.0 + 0.5) - 0.5) / 6.0 * uRadius;
        float rings = (1.0 - smoothstep(0.004, 0.012, ringDistance)) * 0.22;
        float angle = atan(vPosition.y, vPosition.x);
        float spokeDistance = abs(fract(angle * 12.0 / (2.0 * PI) + 0.5) - 0.5) * 2.0 * PI / 12.0 * radius * uRadius;
        float spokes = (1.0 - smoothstep(0.004, 0.012, spokeDistance)) * 0.14;
        // Ticks just inside the orbit line, spaced and sized the same on every orbit.
        float tickCount = floor(2.0 * PI * uRadius / 0.3);
        float tickDistance = abs(fract(angle * tickCount / (2.0 * PI) + 0.5) - 0.5) * 2.0 * PI / tickCount * radius * uRadius;
        float tickBand = step((1.0 - radius) * uRadius, 0.22);
        float ticks = (1.0 - smoothstep(0.005, 0.012, tickDistance)) * tickBand * 0.4;
        float grid = (max(max(rings, spokes), ticks) * toOrbit + fill) * inside;

        gl_FragColor = vec4(uColor, (line + grid) * trail * visible * uOpacity);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }
    `,
    transparent: true,
    depthWrite: false,
    side: DoubleSide,
    blending: AdditiveBlending,
  });
  const mesh = new Mesh(new RingGeometry(0.01, 1.02, 512), material);
  mesh.rotation.x = -Math.PI / 2;
  mesh.visible = false;
  return mesh;
}

export function createFocusRing(color: string) {
  const material = new ShaderMaterial({
    name: "focus-ring",
    uniforms: { uColor: { value: new Color(color) }, uOpacity: { value: 0 }, uSpin: { value: 0 } },
    vertexShader: /* glsl */ `
      varying vec2 vPosition;
      void main() {
        vPosition = position.xy;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: /* glsl */ `
      uniform vec3 uColor;
      uniform float uOpacity;
      uniform float uSpin;
      varying vec2 vPosition;
      const float TAU = 6.2831853;

      float rail(float radius, float at, float width) {
        return 1.0 - smoothstep(width, width + 0.008, abs(radius - at));
      }

      float band(float radius, float inner, float outer) {
        return smoothstep(inner - 0.004, inner + 0.004, radius) * (1.0 - smoothstep(outer - 0.004, outer + 0.004, radius));
      }

      // 1 where the turn fraction (0..1 around the ring) falls inside an arc of the given count and duty.
      float arcs(float turn, float count, float duty) {
        return step(fract(turn * count), duty);
      }

      void main() {
        float radius = length(vPosition);
        float turn = atan(vPosition.y, vPosition.x) / TAU + 0.5;
        // The mesh spins one way; the inner track counter-rotates at the same speed.
        float innerTurn = turn + 2.0 * uSpin / TAU;

        float innerDashes = rail(radius, 1.16, 0.004) * arcs(innerTurn, 90.0, 0.55) * 0.45;
        // Main band: bright edges and a faint fill, split into three arcs.
        float mainArcs = arcs(turn + 0.02, 3.0, 0.94);
        float mainBand = (max(rail(radius, 1.27, 0.006), rail(radius, 1.39, 0.004)) * 0.9 + band(radius, 1.27, 1.39) * 0.14) * mainArcs;
        // Tick marks on two opposite arcs, between the band and the outer rail.
        float ticks = band(radius, 1.43, 1.50) * arcs(turn, 120.0, 0.2) * arcs(turn + 0.18, 2.0, 0.32) * 0.8;
        float outerRail = rail(radius, 1.58, 0.003) * 0.35;
        // Four short brackets along the outer rail.
        float brackets = rail(radius, 1.58, 0.012) * arcs(turn + 0.06, 4.0, 0.05) * 0.9;
        float halo = exp(-abs(radius - 1.33) * 9.0) * 0.07;

        float alpha = max(max(max(innerDashes, mainBand), max(ticks, outerRail)), brackets) + halo;
        gl_FragColor = vec4(uColor, alpha * uOpacity);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }
    `,
    transparent: true,
    depthWrite: false,
    side: DoubleSide,
    blending: AdditiveBlending,
  });
  const mesh = new Mesh(new RingGeometry(1.1, 1.65, 256), material);
  mesh.visible = false;
  // Lies in the orbital plane, so the oblique camera sees it as an ellipse.
  mesh.rotation.x = -Math.PI / 2;
  const orbit = createOrbitTrail(color);
  let focusedBody: Mesh<SphereGeometry> | null = null;
  let angle = 0;
  const orbitUniforms = orbit.material.uniforms;
  return {
    mesh,
    orbit,
    focus(body: Mesh<SphereGeometry> | null, immediate = false): void {
      if (body && body !== focusedBody) {
        material.uniforms.uOpacity.value = 0;
        orbitUniforms.uReveal.value = 0;
      }
      focusedBody = body;
      if (body) {
        mesh.position.copy(body.position);
        mesh.scale.setScalar(body.geometry.parameters.radius);
        // The star sits at the origin and has no orbit.
        orbitUniforms.uRadius.value = Math.hypot(body.position.x, body.position.z);
        orbit.scale.setScalar(Math.max(orbitUniforms.uRadius.value, 1e-3));
        if (immediate) {
          material.uniforms.uOpacity.value = 1;
          orbitUniforms.uReveal.value = 1;
        }
      }
    },
    update(deltaSeconds: number, warping: boolean): void {
      const target = ringOpacityTarget(focusedBody?.userData.bodyId ?? null, warping);
      material.uniforms.uOpacity.value = warping ? 0 : fadeRingOpacity(material.uniforms.uOpacity.value, target, deltaSeconds);
      mesh.visible = material.uniforms.uOpacity.value > 0;
      if (focusedBody) {
        mesh.position.copy(focusedBody.position);
        // Ring-local Y is world -Z after lying the ring flat.
        orbitUniforms.uPlanetAngle.value = Math.atan2(-focusedBody.position.z, focusedBody.position.x);
        orbitUniforms.uReveal.value = revealOrbit(orbitUniforms.uReveal.value, deltaSeconds);
      }
      orbitUniforms.uOpacity.value = material.uniforms.uOpacity.value;
      orbit.visible = mesh.visible && orbitUniforms.uRadius.value > 0;
      angle += Math.max(0, deltaSeconds) * 0.12;
      mesh.rotation.z = angle;
      material.uniforms.uSpin.value = angle;
    },
    dispose(): void {
      mesh.removeFromParent();
      mesh.geometry.dispose();
      material.dispose();
      orbit.removeFromParent();
      orbit.geometry.dispose();
      orbit.material.dispose();
    },
  };
}
