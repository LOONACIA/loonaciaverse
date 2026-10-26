import { AdditiveBlending, Color, DoubleSide, Mesh, RingGeometry, ShaderMaterial } from "three";
import type { PerspectiveCamera, SphereGeometry } from "three";

export function ringOpacityTarget(focusedId: string | null, warping: boolean): number {
  return focusedId !== null && !warping ? 1 : 0;
}

export function fadeRingOpacity(opacity: number, target: number, deltaSeconds: number): number {
  const step = Math.max(0, deltaSeconds) / 0.3;
  const bounded = Math.max(0, Math.min(1, opacity));
  return target > bounded ? Math.min(target, bounded + step) : Math.max(target, bounded - step);
}

export function createFocusRing(color: string) {
  const material = new ShaderMaterial({
    name: "focus-ring",
    uniforms: { uColor: { value: new Color(color) }, uOpacity: { value: 0 } },
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
      varying vec2 vPosition;
      void main() {
        float radius = length(vPosition);
        float angle = atan(vPosition.y, vPosition.x);
        float rail = 1.0 - smoothstep(0.006, 0.016, abs(radius - 1.30));
        float ticks = step(0.85, fract(angle * 48.0 / 6.2831853));
        ticks *= smoothstep(1.35, 1.37, radius) * (1.0 - smoothstep(1.46, 1.48, radius));
        gl_FragColor = vec4(uColor, max(rail * 0.65, ticks) * uOpacity);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }
    `,
    transparent: true,
    depthWrite: false,
    side: DoubleSide,
    blending: AdditiveBlending,
  });
  const mesh = new Mesh(new RingGeometry(1.25, 1.5, 192), material);
  mesh.visible = false;
  let focusedBody: Mesh<SphereGeometry> | null = null;
  let angle = 0;
  return {
    mesh,
    focus(body: Mesh<SphereGeometry> | null, immediate = false): void {
      if (body && body !== focusedBody) material.uniforms.uOpacity.value = 0;
      focusedBody = body;
      if (body) {
        mesh.position.copy(body.position);
        mesh.scale.setScalar(body.geometry.parameters.radius);
        if (immediate) material.uniforms.uOpacity.value = 1;
      }
    },
    update(deltaSeconds: number, camera: PerspectiveCamera, warping: boolean): void {
      const target = ringOpacityTarget(focusedBody?.userData.bodyId ?? null, warping);
      material.uniforms.uOpacity.value = warping ? 0 : fadeRingOpacity(material.uniforms.uOpacity.value, target, deltaSeconds);
      mesh.visible = material.uniforms.uOpacity.value > 0;
      if (focusedBody) mesh.position.copy(focusedBody.position);
      angle += Math.max(0, deltaSeconds) * 0.12;
      mesh.quaternion.copy(camera.quaternion);
      mesh.rotateZ(angle);
    },
    dispose(): void {
      mesh.removeFromParent();
      mesh.geometry.dispose();
      material.dispose();
    },
  };
}
