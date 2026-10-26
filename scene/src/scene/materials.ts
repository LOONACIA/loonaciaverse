import { AdditiveBlending, BackSide, Color, ShaderMaterial } from "three";
import type { BodyDto } from "./types";

type Visual = BodyDto["visual"];

/*
 * The hash3 function below is adapted from "Hash without Sine" by Dave Hoskins:
 * https://www.shadertoy.com/view/4djSRW
 * MIT License, Copyright (c) 2014 David Hoskins.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
const noise = /* glsl */ `
  // hash3 adapted from "Hash without Sine" by Dave Hoskins (https://www.shadertoy.com/view/4djSRW), MIT License, Copyright (c) 2014 David Hoskins.
  float hash3(vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
  }

  // Locally written trilinear value noise and four-octave fbm interpolation.
  float valueNoise(vec3 p) {
    vec3 cell = floor(p);
    vec3 f = fract(p);
    vec3 blend = f * f * (3.0 - 2.0 * f);
    float lower = mix(
      mix(hash3(cell), hash3(cell + vec3(1.0, 0.0, 0.0)), blend.x),
      mix(hash3(cell + vec3(0.0, 1.0, 0.0)), hash3(cell + vec3(1.0, 1.0, 0.0)), blend.x), blend.y);
    float upper = mix(
      mix(hash3(cell + vec3(0.0, 0.0, 1.0)), hash3(cell + vec3(1.0, 0.0, 1.0)), blend.x),
      mix(hash3(cell + vec3(0.0, 1.0, 1.0)), hash3(cell + vec3(1.0, 1.0, 1.0)), blend.x), blend.y);
    return mix(lower, upper, blend.z);
  }

  float fbm(vec3 p) {
    float result = 0.0;
    float weight = 0.5;
    for (int octave = 0; octave < 4; octave++) {
      result += weight * valueNoise(p);
      p = p * 2.03 + vec3(7.1, 3.7, 1.9);
      weight *= 0.5;
    }
    return result;
  }
`;

// Surface coordinates stay local; lighting and Fresnel vectors share view space.
const bodyVertex = /* glsl */ `
  varying vec3 vPosition;
  varying vec3 vViewPosition;
  varying vec3 vNormal;
  varying vec3 vStarDirection;
  void main() {
    vec4 viewPosition = modelViewMatrix * vec4(position, 1.0);
    vPosition = position;
    vViewPosition = viewPosition.xyz;
    vNormal = normalMatrix * normal;
    vStarDirection = (viewMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz - viewPosition.xyz;
    gl_Position = projectionMatrix * viewPosition;
  }
`;

const outputColor = /* glsl */ `
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
`;

export function createPlanetMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "planet",
    uniforms: {
      uBaseColor: { value: new Color(visual.baseColor) },
      uNoiseScale: { value: visual.noiseScale },
      uTime: { value: 0 },
    },
    vertexShader: bodyVertex,
    // Slowly flowing fbm modulates the surface; Lambert light separates day and night.
    fragmentShader: /* glsl */ `
      uniform vec3 uBaseColor;
      uniform float uNoiseScale;
      uniform float uTime;
      varying vec3 vPosition;
      varying vec3 vNormal;
      varying vec3 vStarDirection;
      ${noise}
      void main() {
        float pattern = fbm(normalize(vPosition) * uNoiseScale + vec3(uTime * 0.008));
        float day = max(dot(normalize(vNormal), normalize(vStarDirection)), 0.0);
        vec3 surface = uBaseColor * mix(0.45, 1.35, pattern);
        gl_FragColor = vec4(surface * (0.045 + 0.955 * day), 1.0);
        ${outputColor}
      }
    `,
  });
}

export function createAtmosphereMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "atmosphere",
    uniforms: {
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uGlowIntensity: { value: visual.glowIntensity },
    },
    vertexShader: bodyVertex,
    // A grazing view angle produces a Fresnel rim on the slightly enlarged sphere.
    fragmentShader: /* glsl */ `
      uniform vec3 uAtmosphereColor;
      uniform float uGlowIntensity;
      varying vec3 vNormal;
      varying vec3 vViewPosition;
      void main() {
        float facing = max(dot(normalize(vNormal), normalize(-vViewPosition)), 0.0);
        float rim = pow(1.0 - facing, 3.0);
        gl_FragColor = vec4(uAtmosphereColor, rim * uGlowIntensity);
        ${outputColor}
      }
    `,
    blending: AdditiveBlending,
    transparent: true,
    depthWrite: false,
  });
}

export function createStarMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "star",
    uniforms: {
      uBaseColor: { value: new Color(visual.baseColor) },
      uNoiseScale: { value: visual.noiseScale },
      uGlowIntensity: { value: visual.glowIntensity },
      uTime: { value: 0 },
    },
    vertexShader: bodyVertex,
    // The star emits its own light, with gentle noise-driven brightness variation.
    fragmentShader: /* glsl */ `
      uniform vec3 uBaseColor;
      uniform float uNoiseScale;
      uniform float uGlowIntensity;
      uniform float uTime;
      varying vec3 vPosition;
      ${noise}
      void main() {
        float pattern = fbm(normalize(vPosition) * uNoiseScale + vec3(uTime * 0.025));
        float brightness = mix(0.65, 1.5, pattern) + uGlowIntensity * 0.2;
        gl_FragColor = vec4(uBaseColor * brightness, 1.0);
        ${outputColor}
      }
    `,
  });
}

export function createCoronaMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "corona",
    uniforms: {
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uGlowIntensity: { value: visual.glowIntensity },
    },
    // Offset the star center in view space so its child plane always faces the camera.
    vertexShader: /* glsl */ `
      varying vec2 vUv;
      void main() {
        vUv = uv;
        vec4 center = modelViewMatrix * vec4(0.0, 0.0, 0.0, 1.0);
        center.xy += position.xy;
        gl_Position = projectionMatrix * center;
      }
    `,
    // A radial falloff reaches zero before the edges of the billboard.
    fragmentShader: /* glsl */ `
      uniform vec3 uAtmosphereColor;
      uniform float uGlowIntensity;
      varying vec2 vUv;
      void main() {
        float radius = length(vUv - vec2(0.5)) * 2.0;
        float glow = pow(max(1.0 - radius, 0.0), 3.0);
        gl_FragColor = vec4(uAtmosphereColor, glow * uGlowIntensity * 0.55);
        ${outputColor}
      }
    `,
    blending: AdditiveBlending,
    transparent: true,
    depthWrite: false,
  });
}

export function createNebulaMaterial(): ShaderMaterial {
  return new ShaderMaterial({
    name: "nebula",
    uniforms: { uTime: { value: 0 } },
    vertexShader: bodyVertex,
    // Low-frequency clouds barely lift the dark background, keeping HUD text legible.
    fragmentShader: /* glsl */ `
      uniform float uTime;
      varying vec3 vPosition;
      ${noise}
      void main() {
        vec3 direction = normalize(vPosition);
        float cloud = smoothstep(0.25, 0.8, fbm(direction * 2.0 + vec3(uTime * 0.001)));
        vec3 tint = mix(vec3(0.003, 0.008, 0.012), vec3(0.012, 0.004, 0.018), direction.y * 0.5 + 0.5);
        gl_FragColor = vec4(vec3(0.0015, 0.0021, 0.0040) + tint * cloud * 0.32, 1.0);
        ${outputColor}
      }
    `,
    side: BackSide,
    depthWrite: false,
  });
}
