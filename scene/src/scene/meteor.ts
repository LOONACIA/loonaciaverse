import { AdditiveBlending, Matrix4, Mesh, PlaneGeometry, ShaderMaterial, Vector3 } from "three";
import { BACK, UP } from "./camera";

export const METEOR_FLIGHT_SECONDS = 0.9;
export const METEOR_MIN_WAIT_SECONDS = 5;
export const METEOR_MAX_WAIT_SECONDS = 14;

function nextWait(): number {
  return METEOR_MIN_WAIT_SECONDS + Math.random() * (METEOR_MAX_WAIT_SECONDS - METEOR_MIN_WAIT_SECONDS);
}

// An occasional shooting star crosses the sky behind the system. The camera keeps one
// orientation, so the meteor lies in a plane facing BACK at `distance` from the origin.
export function createMeteor(distance: number) {
  const material = new ShaderMaterial({
    name: "meteor",
    uniforms: { uOpacity: { value: 0 } },
    vertexShader: /* glsl */ `
      varying vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    // The bright head sits at +X; the tail narrows and fades toward -X.
    fragmentShader: /* glsl */ `
      uniform float uOpacity;
      varying vec2 vUv;
      void main() {
        float across = abs(vUv.y - 0.5) * 2.0;
        float trail = (1.0 - smoothstep(0.0, mix(0.15, 1.0, vUv.x), across)) * pow(vUv.x, 2.5);
        gl_FragColor = vec4(vec3(0.85, 0.93, 1.0), trail * uOpacity);
        #include <tonemapping_fragment>
        #include <colorspace_fragment>
      }
    `,
    transparent: true,
    depthWrite: false,
    blending: AdditiveBlending,
  });
  const mesh = new Mesh(new PlaneGeometry(1, 1), material);
  mesh.visible = false;
  const right = new Vector3(1, 0, 0);
  const up = new Vector3(UP.x, UP.y, UP.z);
  const back = new Vector3(BACK.x, BACK.y, BACK.z);
  const center = back.clone().multiplyScalar(-distance);
  const start = new Vector3();
  const velocity = new Vector3();
  const across = new Vector3();
  const basis = new Matrix4();
  let wait = nextWait();
  let elapsed = 0;

  const launch = () => {
    // Start in the upper part of the view and fall diagonally to either side.
    const angle = -Math.PI / 2 + (Math.random() < 0.5 ? -1 : 1) * (0.35 + Math.random() * 0.6);
    start.copy(center)
      .addScaledVector(right, (Math.random() * 2 - 1) * distance * 0.4)
      .addScaledVector(up, (0.05 + Math.random() * 0.25) * distance);
    velocity.copy(right).multiplyScalar(Math.cos(angle)).addScaledVector(up, Math.sin(angle));
    across.crossVectors(back, velocity);
    mesh.quaternion.setFromRotationMatrix(basis.makeBasis(velocity, across, back));
    mesh.scale.set(distance * 0.1, distance * 0.003, 1);
    velocity.multiplyScalar(distance * 0.3);
    elapsed = 0;
    mesh.visible = true;
  };

  return {
    mesh,
    update(deltaSeconds: number): void {
      const delta = Math.max(0, deltaSeconds);
      if (!mesh.visible) {
        wait -= delta;
        if (wait > 0) return;
        launch();
      } else elapsed += delta;
      const progress = elapsed / METEOR_FLIGHT_SECONDS;
      if (progress >= 1) {
        mesh.visible = false;
        material.uniforms.uOpacity.value = 0;
        wait = nextWait();
        return;
      }
      mesh.position.copy(start).addScaledVector(velocity, elapsed);
      material.uniforms.uOpacity.value = Math.sin(Math.PI * progress);
    },
  };
}
