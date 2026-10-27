import { AdditiveBlending, BackSide, Color, DoubleSide, ShaderMaterial } from "three";
import type { BodyDto, PlanetSurface } from "./types";

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

  // Locally written cellular noise: distances to the nearest and second-nearest jittered feature points.
  vec2 cellular(vec3 p) {
    vec3 cell = floor(p);
    vec3 f = fract(p);
    vec2 nearest = vec2(8.0);
    for (int z = -1; z <= 1; z++) {
      for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
          vec3 offset = vec3(float(x), float(y), float(z));
          vec3 id = cell + offset;
          vec3 feature = offset + vec3(hash3(id), hash3(id + 19.7), hash3(id + 41.3)) - f;
          float squared = dot(feature, feature);
          nearest = squared < nearest.x ? vec2(squared, nearest.x) : vec2(nearest.x, min(nearest.y, squared));
        }
      }
    }
    return sqrt(nearest);
  }

  vec3 rotateY(vec3 p, float angle) {
    float c = cos(angle);
    float s = sin(angle);
    return vec3(c * p.x + s * p.z, p.y, c * p.z - s * p.x);
  }
`;

// Surface coordinates stay local; lighting and Fresnel vectors share view space.
const bodyVertex = /* glsl */ `
  varying vec3 vPosition;
  varying vec3 vViewPosition;
  varying vec3 vNormal;
  varying vec3 vStarDirection;
  varying vec3 vLocalStarDirection;
  void main() {
    vec4 viewPosition = modelViewMatrix * vec4(position, 1.0);
    vPosition = position;
    vViewPosition = viewPosition.xyz;
    vNormal = normalMatrix * normal;
    vStarDirection = (viewMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz - viewPosition.xyz;
    // The same direction in the body's own frame; bodies are never scaled, so the transpose inverts the rotation.
    vLocalStarDirection = -modelMatrix[3].xyz * mat3(modelMatrix) - position;
    gl_Position = projectionMatrix * viewPosition;
  }
`;

// Screen-space derivative bump mapping after Mikkelsen, "Bump Mapping Unparametrized
// Surfaces on the GPU" (2010), in view space.
const bump = /* glsl */ `
  vec3 bumpNormal(vec3 normal, float height) {
    vec3 dx = dFdx(vViewPosition);
    vec3 dy = dFdy(vViewPosition);
    vec3 r1 = cross(dy, normal);
    vec3 r2 = cross(normal, dx);
    float det = dot(dx, r1);
    return normalize(abs(det) * normal - sign(det) * (dFdx(height) * r1 + dFdy(height) * r2));
  }
`;

// Sunlight reddens through the long air path near the terminator.
const reddening = /* glsl */ `
  vec3 sunlight(float light) {
    return mix(vec3(1.0, 0.42, 0.2), vec3(1.0), smoothstep(0.0, 0.4, light));
  }
`;

// Terran weather, in the planet's own frame so the ground can sample the same clouds for their shadows.
const clouds = /* glsl */ `
  float cloudCover(vec3 direction) {
    // Westerlies carry the weather east, visibly faster than the ground turns.
    vec3 p = rotateY(direction, -uTime * 0.06) * uNoiseScale * 1.1;
    // The field shifts through the sphere and its warp changes, so clouds billow, form and dissolve.
    vec2 warp = vec2(fbm(p * 0.6 + vec3(0.0, uTime * 0.05, 3.1)), fbm(p * 0.6 + vec3(5.2, 1.3, uTime * 0.05))) - 0.5;
    float density = fbm(p + vec3(warp * 2.0, uTime * 0.03));
    // A wet equator and storm tracks between dry subtropical belts.
    density += 0.05 * cos(asin(direction.y) * 6.0);
    return smoothstep(0.47, 0.72, density);
  }
`;

const outputColor = /* glsl */ `
  #include <tonemapping_fragment>
  #include <colorspace_fragment>
`;

// Each surface returns albedo for a local unit direction, may add self-lit emission and may tilt
// the view-space shading normal. `light` is the unclamped cosine toward the star from the
// untilted normal, so night-side effects can fade in.
const surfaces: Record<PlanetSurface, string> = {
  // Warped continents with shallow coasts, polar ice, cloud shadows and night-side city lights.
  // The clouds themselves are a separate layer above the ground.
  terran: /* glsl */ `
    ${clouds}
    vec3 surface(vec3 direction, float light, inout vec3 normal, out float gloss, out vec3 emission) {
      vec3 p = direction * uNoiseScale * 0.55;
      vec2 warp = vec2(fbm(p + 3.1), fbm(p + 7.7)) - 0.5;
      float height = fbm(p * 1.3 + vec3(warp, warp.x - warp.y) * 1.6);
      float land = smoothstep(0.49, 0.505, height);
      vec3 ocean = mix(uBaseColor * 0.3, mix(uBaseColor, uAtmosphereColor, 0.5), smoothstep(0.36, 0.49, height));
      vec3 lowland = mix(vec3(0.13, 0.30, 0.22), uBaseColor, 0.2);
      vec3 ground = mix(lowland, vec3(0.50, 0.45, 0.34), smoothstep(0.53, 0.66, height));
      float ice = smoothstep(0.80, 0.86, abs(direction.y) + warp.x * 0.3);
      vec3 color = mix(mix(ocean, ground, land), vec3(0.86, 0.93, 1.0), ice);
      // Shadows fall away from the star, farther when it is low; the offset is exaggerated so they read at this size.
      vec3 toStar = normalize(vLocalStarDirection);
      float sunHeight = dot(toStar, direction);
      float shadow = cloudCover(normalize(direction + (toStar - direction * sunHeight) * 0.03 / max(sunHeight, 0.2)));
      color *= 1.0 - shadow * 0.6;
      gloss = (1.0 - land) * (1.0 - ice) * (1.0 - shadow);
      float settled = smoothstep(0.5, 0.7, valueNoise(direction * uNoiseScale * 3.0)) * land * (1.0 - ice);
      float cities = smoothstep(0.8, 0.92, valueNoise(direction * uNoiseScale * 24.0)) * settled;
      emission = vec3(1.0, 0.72, 0.4) * cities * 0.6 * (1.0 - smoothstep(-0.2, 0.05, light));
      return color;
    }
  `,
  // Latitude bands that drift as zonal jets, fine turbulence and a long-lived oval storm.
  gas: /* glsl */ `
    vec3 surface(vec3 direction, float light, inout vec3 normal, out float gloss, out vec3 emission) {
      gloss = 0.0;
      emission = vec3(0.0);
      vec3 flowing = rotateY(direction, sin(direction.y * 7.0) * uTime * 0.03);
      float turbulence = fbm(flowing * vec3(1.0, 4.0, 1.0) * uNoiseScale * 0.6) - 0.5;
      float streaks = fbm(flowing * vec3(2.0, 14.0, 2.0) * uNoiseScale * 0.35);
      float band = direction.y * uNoiseScale * 1.4 + turbulence * 1.6;
      vec3 color = mix(uBaseColor * 0.35, mix(uBaseColor, uAtmosphereColor, 0.75), 0.5 + 0.5 * sin(band * 3.1416));
      color = mix(color, mix(uAtmosphereColor, vec3(1.0, 0.9, 0.95), 0.45), smoothstep(0.6, 0.95, sin(band * 7.3 + 1.3)) * 0.6);
      color *= 0.75 + 0.5 * streaks;
      vec3 storm = normalize(vec3(0.55, -0.34, 0.76));
      vec3 east = normalize(cross(vec3(0.0, 1.0, 0.0), storm));
      vec2 offset = vec2(dot(direction, east) * 0.55, dot(direction, cross(storm, east)));
      float radius = length(offset);
      float twist = (1.0 - smoothstep(0.0, 0.14, radius)) * 5.0;
      vec2 swirl = mat2(cos(twist), sin(twist), -sin(twist), cos(twist)) * offset;
      float eye = (1.0 - smoothstep(0.07, 0.13, radius)) * step(0.0, dot(direction, storm));
      vec3 stormColor = mix(uAtmosphereColor, vec3(1.0, 0.78, 0.88), 0.45) * (0.7 + 0.6 * fbm(vec3(swirl * 22.0, 3.0)));
      return mix(color, stormColor, eye * 0.85);
    }
  `,
  // A rusty desert world: dark basalt regions over dusty plains, polar caps, one long equatorial
  // rift and craters. Relief tilts the normal so rims and canyon walls catch the light.
  desert: /* glsl */ `
    // Locally written bowl-and-rim craters; only some cellular cells hold one.
    float craters(vec3 p) {
      vec3 cell = floor(p);
      vec3 f = fract(p);
      float height = 0.0;
      for (int z = -1; z <= 1; z++) {
        for (int y = -1; y <= 1; y++) {
          for (int x = -1; x <= 1; x++) {
            vec3 id = cell + vec3(float(x), float(y), float(z));
            float radius = 0.15 + 0.3 * hash3(id + 3.7);
            float d = length(id - cell + vec3(hash3(id), hash3(id + 19.7), hash3(id + 41.3)) - f) / radius;
            float bowl = min(d * d - 1.0, 0.0);
            float rim = exp(-(d - 1.0) * (d - 1.0) * 16.0) * 0.35;
            height += step(0.45, hash3(id + 11.1)) * (bowl + rim) * radius;
          }
        }
      }
      return height;
    }

    ${bump}
    vec3 surface(vec3 direction, float light, inout vec3 normal, out float gloss, out vec3 emission) {
      gloss = 0.0;
      emission = vec3(0.0);
      vec3 p = direction * uNoiseScale * 0.5;
      vec2 warp = vec2(fbm(p + 2.3), fbm(p + 8.1)) - 0.5;
      float terrain = fbm(p * 0.9 + vec3(warp, warp.x) * 1.5);
      vec3 color = mix(uBaseColor * 0.45, mix(uBaseColor, uAtmosphereColor, 0.35), smoothstep(0.38, 0.6, terrain));
      color *= 0.85 + 0.3 * fbm(direction * uNoiseScale * 4.0);
      float rift = abs(direction.y - 0.08 + (fbm(p * 3.0) - 0.5) * 0.05);
      float canyon = (1.0 - smoothstep(0.01, 0.035, rift)) * (1.0 - smoothstep(0.5, 0.9, abs(atan(direction.z, direction.x) - 0.8)));
      float crater = craters(direction * uNoiseScale * 0.8) + craters(direction * uNoiseScale * 2.5 + 5.0) * 0.4;
      color *= (1.0 - 0.5 * canyon) * (1.0 + 0.5 * crater);
      float ice = smoothstep(0.9, 0.94, abs(direction.y) + (warp.x + fbm(p * 4.0) - 0.5) * 0.12);
      color = mix(color, vec3(0.88, 0.86, 0.83), ice);
      normal = bumpNormal(normal, (crater - canyon * 0.6 + terrain * 0.3) * (1.0 - ice) * 0.025);
      return color;
    }
  `,
};

// Only terran air is dense enough to redden sunlight at the terminator and scatter it by day.
function airDefines(visual: Visual): Record<string, string> {
  return visual.surface === "terran" ? { SCATTERING: "" } : {};
}

export function createPlanetMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "planet",
    defines: airDefines(visual),
    uniforms: {
      uBaseColor: { value: new Color(visual.baseColor) },
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uNoiseScale: { value: visual.noiseScale },
      uTime: { value: 0 },
    },
    vertexShader: bodyVertex,
    // The surface type supplies albedo; shared lighting adds glints, a hazy lit limb and a faint night fill.
    fragmentShader: /* glsl */ `
      uniform vec3 uBaseColor;
      uniform vec3 uAtmosphereColor;
      uniform float uNoiseScale;
      uniform float uTime;
      varying vec3 vPosition;
      varying vec3 vViewPosition;
      varying vec3 vNormal;
      varying vec3 vStarDirection;
      varying vec3 vLocalStarDirection;
      ${noise}
      ${reddening}
      ${surfaces[visual.surface!]}
      void main() {
        vec3 sphereNormal = normalize(vNormal);
        vec3 toStar = normalize(vStarDirection);
        vec3 toView = normalize(-vViewPosition);
        float light = dot(sphereNormal, toStar);
        vec3 normal = sphereNormal;
        float gloss;
        vec3 emission;
        vec3 albedo = surface(normalize(vPosition), light, normal, gloss, emission);
        // A tilted normal must not light the far side of the terminator.
        float day = max(dot(normal, toStar), 0.0) * step(0.0, light);
        float glint = pow(max(dot(normal, normalize(toStar + toView)), 0.0), 48.0) * gloss * day;
        float limb = pow(1.0 - max(dot(sphereNormal, toView), 0.0), 2.5) * smoothstep(-0.2, 0.4, light);
        #ifdef SCATTERING
          vec3 sun = sunlight(light);
          vec3 haze = mix(vec3(1.0, 0.5, 0.35), uAtmosphereColor, smoothstep(0.0, 0.35, light));
          // The air's own glow: a faint blue veil by day and a warm band just past the terminator.
          vec3 inscatter = uAtmosphereColor * day * 0.05 + vec3(1.0, 0.45, 0.3) * exp(-pow((light + 0.03) / 0.1, 2.0)) * 0.05;
        #else
          vec3 sun = vec3(1.0);
          vec3 haze = uAtmosphereColor;
          vec3 inscatter = vec3(0.0);
        #endif
        vec3 color = albedo * (day * sun + 0.015) + uAtmosphereColor * 0.008 + vec3(1.0, 0.95, 0.85) * sun * glint * 0.6 + inscatter;
        color = mix(color, haze * (day + 0.1), limb * 0.6);
        gl_FragColor = vec4(color + emission, 1.0);
        ${outputColor}
      }
    `,
  });
}

export function createCloudMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "clouds",
    uniforms: {
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uNoiseScale: { value: visual.noiseScale },
      uTime: { value: 0 },
    },
    vertexShader: bodyVertex,
    fragmentShader: /* glsl */ `
      uniform vec3 uAtmosphereColor;
      uniform float uNoiseScale;
      uniform float uTime;
      varying vec3 vPosition;
      varying vec3 vViewPosition;
      varying vec3 vNormal;
      varying vec3 vStarDirection;
      ${noise}
      ${bump}
      ${reddening}
      ${clouds}
      void main() {
        float cover = cloudCover(normalize(vPosition));
        vec3 sphereNormal = normalize(vNormal);
        vec3 toStar = normalize(vStarDirection);
        vec3 toView = normalize(-vViewPosition);
        float light = dot(sphereNormal, toStar);
        // Thick cores bulge, so their sunward sides catch the light.
        vec3 normal = bumpNormal(sphereNormal, cover * 0.015);
        // Wrapped light keeps cloud tops lit a little past the terminator, in reddened sunset light.
        float day = clamp((dot(normal, toStar) + 0.1) / 1.1, 0.0, 1.0);
        float limb = pow(1.0 - max(dot(sphereNormal, toView), 0.0), 2.5);
        vec3 color = vec3(0.95, 0.97, 1.0) * (day * sunlight(light) + 0.01);
        color = mix(color, uAtmosphereColor * (day + 0.05), limb * 0.5);
        gl_FragColor = vec4(color, cover * 0.9);
        ${outputColor}
      }
    `,
    transparent: true,
    depthWrite: false,
  });
}

export function createAtmosphereMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "atmosphere",
    defines: airDefines(visual),
    uniforms: {
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uGlowIntensity: { value: visual.glowIntensity },
    },
    vertexShader: bodyVertex,
    // Only back faces show around the planet: the halo hugs the limb (|cos| near 0.38 for a
    // 1.08x shell), fades out at the shell silhouette and is brightest where the star lights it.
    fragmentShader: /* glsl */ `
      uniform vec3 uAtmosphereColor;
      uniform float uGlowIntensity;
      varying vec3 vNormal;
      varying vec3 vViewPosition;
      varying vec3 vStarDirection;
      void main() {
        vec3 normal = normalize(vNormal);
        float halo = pow(clamp(abs(dot(normal, normalize(-vViewPosition))) * 2.6, 0.0, 1.0), 3.0);
        float facing = dot(normal, normalize(vStarDirection));
        float lit = smoothstep(-0.4, 0.5, facing);
        vec3 color = uAtmosphereColor;
        #ifdef SCATTERING
          // The halo turns warm where it crosses the terminator.
          color = mix(color, vec3(1.0, 0.45, 0.3), exp(-pow((facing + 0.05) / 0.2, 2.0)) * 0.8);
        #endif
        gl_FragColor = vec4(color, halo * uGlowIntensity * (0.3 + 1.7 * lit));
        ${outputColor}
      }
    `,
    side: BackSide,
    blending: AdditiveBlending,
    transparent: true,
    depthWrite: false,
  });
}

// Ring geometry is built in planet radii, so its model scale is the planet radius.
export const PLANET_RING_INNER = 1.45;
export const PLANET_RING_OUTER = 2.05;

export function createPlanetRingMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "planet-ring",
    uniforms: {
      uBaseColor: { value: new Color(visual.baseColor) },
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uNoiseScale: { value: visual.noiseScale },
    },
    vertexShader: /* glsl */ `
      varying float vRadius;
      varying vec3 vWorldPosition;
      varying vec3 vPlanetCenter;
      varying float vPlanetRadius;
      void main() {
        vec4 world = modelMatrix * vec4(position, 1.0);
        vRadius = length(position.xy);
        vWorldPosition = world.xyz;
        vPlanetCenter = (modelMatrix * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
        vPlanetRadius = length(modelMatrix[0].xyz);
        gl_Position = projectionMatrix * viewMatrix * world;
      }
    `,
    // Radial noise forms ringlets around one dark division; the planet shadows the far side from the star.
    fragmentShader: /* glsl */ `
      uniform vec3 uBaseColor;
      uniform vec3 uAtmosphereColor;
      uniform float uNoiseScale;
      varying float vRadius;
      varying vec3 vWorldPosition;
      varying vec3 vPlanetCenter;
      varying float vPlanetRadius;
      ${noise}
      void main() {
        float t = (vRadius - ${PLANET_RING_INNER.toFixed(2)}) / ${(PLANET_RING_OUTER - PLANET_RING_INNER).toFixed(2)};
        float ringlets = valueNoise(vec3(t * uNoiseScale * 6.0, 0.5, 0.5)) * 0.6 + valueNoise(vec3(t * uNoiseScale * 22.0, 1.5, 0.5)) * 0.4;
        float density = mix(0.2, 1.0, ringlets) * smoothstep(0.02, 0.045, abs(t - 0.62));
        density *= smoothstep(0.0, 0.08, t) * (1.0 - smoothstep(0.88, 1.0, t));
        vec3 toStar = normalize(-vWorldPosition);
        vec3 toCenter = vPlanetCenter - vWorldPosition;
        float along = dot(toCenter, toStar);
        float miss = length(toCenter - toStar * along);
        float shadow = along > 0.0 ? smoothstep(vPlanetRadius * 0.9, vPlanetRadius * 1.05, miss) : 1.0;
        vec3 color = mix(uBaseColor * 0.7, uAtmosphereColor, ringlets * 0.7) * (0.2 + 0.8 * shadow);
        gl_FragColor = vec4(color, density * 0.5);
        ${outputColor}
      }
    `,
    side: DoubleSide,
    transparent: true,
    depthWrite: false,
  });
}

export function createStarMaterial(visual: Visual): ShaderMaterial {
  return new ShaderMaterial({
    name: "star",
    uniforms: {
      uBaseColor: { value: new Color(visual.baseColor) },
      uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
      uNoiseScale: { value: visual.noiseScale },
      uGlowIntensity: { value: visual.glowIntensity },
      uTime: { value: 0 },
    },
    vertexShader: bodyVertex,
    // Warped plasma swirls over fine soft granules and slow sunspots. Cooler regions and the
    // darkened limb lean toward the corona color.
    fragmentShader: /* glsl */ `
      uniform vec3 uBaseColor;
      uniform vec3 uAtmosphereColor;
      uniform float uNoiseScale;
      uniform float uGlowIntensity;
      uniform float uTime;
      varying vec3 vPosition;
      varying vec3 vViewPosition;
      varying vec3 vNormal;
      ${noise}
      void main() {
        vec3 direction = normalize(vPosition);
        float facing = max(dot(normalize(vNormal), normalize(-vViewPosition)), 0.0);
        vec3 flow = direction * uNoiseScale + vec3(uTime * 0.02);
        vec2 warp = vec2(fbm(flow), fbm(flow + 5.3)) - 0.5;
        float plasma = fbm(direction * uNoiseScale * 1.6 + vec3(warp * 2.0, uTime * -0.015));
        float granules = 1.0 - smoothstep(0.0, 0.8, cellular(direction * uNoiseScale * 6.0 + vec3(warp, 0.0)).x);
        float spots = smoothstep(0.68, 0.75, fbm(direction * uNoiseScale * 0.5 + vec3(11.0, uTime * 0.004, 0.0)));
        float heat = clamp(0.2 + 0.75 * plasma + 0.15 * granules, 0.0, 1.0) * (1.0 - 0.7 * spots);
        vec3 color = mix(uAtmosphereColor * 0.75, uBaseColor, heat) * heat * mix(0.35, 1.0, pow(facing, 0.6));
        gl_FragColor = vec4(color * (1.0 + uGlowIntensity * 0.05), 1.0);
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
      uTime: { value: 0 },
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
    // Slowly shifting streamers modulate a radial falloff that reaches zero before the billboard edges;
    // the outer fringe leans pink.
    fragmentShader: /* glsl */ `
      uniform vec3 uAtmosphereColor;
      uniform float uGlowIntensity;
      uniform float uTime;
      varying vec2 vUv;
      ${noise}
      void main() {
        vec2 offset = vUv - vec2(0.5);
        float radius = length(offset) * 2.0;
        float angle = atan(offset.y, offset.x);
        float streamers = fbm(vec3(cos(angle) * 2.5, sin(angle) * 2.5, uTime * 0.05 - radius * 0.8));
        float falloff = max(1.0 - radius, 0.0);
        float glow = pow(falloff, 3.0) * (0.7 + 0.7 * streamers) + pow(falloff, 1.6) * streamers * 0.12;
        vec3 color = mix(uAtmosphereColor, vec3(1.0, 0.6, 0.8), smoothstep(0.25, 0.8, radius) * 0.45);
        gl_FragColor = vec4(color, glow * uGlowIntensity * 0.55);
        ${outputColor}
      }
    `,
    blending: AdditiveBlending,
    transparent: true,
    depthWrite: false,
  });
}

export function createStarFieldMaterial(pixelRatio: number): ShaderMaterial {
  return new ShaderMaterial({
    name: "star-field",
    uniforms: {
      uTime: { value: 0 },
      uOpacity: { value: 1 },
      uPixelRatio: { value: pixelRatio },
    },
    // Each star twinkles with its own phase, speed and depth (aTwinkle). Distant stars are point
    // sources, so their size stays in screen pixels.
    vertexShader: /* glsl */ `
      uniform float uTime;
      uniform float uPixelRatio;
      attribute float aSize;
      attribute vec3 aColor;
      attribute vec3 aTwinkle;
      varying vec3 vColor;
      void main() {
        vColor = aColor * (1.0 - aTwinkle.z * (0.5 + 0.5 * sin(uTime * aTwinkle.y + aTwinkle.x)));
        gl_PointSize = aSize * uPixelRatio;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    // A sharp core with a soft halo that fades out before the sprite edge.
    fragmentShader: /* glsl */ `
      uniform float uOpacity;
      varying vec3 vColor;
      void main() {
        float radius = length(gl_PointCoord - vec2(0.5)) * 2.0;
        float shape = exp(-radius * radius * 9.0) + exp(-radius * 4.0) * 0.2;
        gl_FragColor = vec4(vColor, shape * (1.0 - smoothstep(0.8, 1.0, radius)) * uOpacity);
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
    // The low-frequency domain warp and its slow drift run per vertex on a dense sphere;
    // only cloud density and wisps are evaluated per pixel.
    vertexShader: /* glsl */ `
      uniform float uTime;
      varying vec3 vCloudPosition;
      varying vec2 vWarp;
      ${noise}
      void main() {
        vec3 direction = normalize(position);
        vec3 drift = vec3(0.0035, -0.0015, 0.0025) * uTime;
        vec3 p = direction * 1.4;
        vWarp = vec2(fbm(p + drift), fbm(p + vec3(5.2, 1.3, 2.8) - drift)) - 0.5;
        vCloudPosition = direction * 2.2 + vec3(vWarp, vWarp.x - vWarp.y) * 1.8 + drift * 2.0;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    // Teal, violet and pink clouds stay well below the bloom threshold, keeping HUD text legible.
    fragmentShader: /* glsl */ `
      varying vec3 vCloudPosition;
      varying vec2 vWarp;
      ${noise}
      void main() {
        float clouds = fbm(vCloudPosition);
        float wisps = fbm(vCloudPosition * 3.1 + 4.0);
        float density = smoothstep(0.42, 0.82, clouds) * (0.3 + 1.2 * wisps * wisps);
        float hue = 0.5 + (vWarp.x - vWarp.y) * 2.4 + (clouds - 0.5);
        vec3 tint = mix(vec3(0.010, 0.052, 0.064), vec3(0.033, 0.014, 0.066), smoothstep(0.3, 0.55, hue));
        tint = mix(tint, vec3(0.056, 0.012, 0.040), smoothstep(0.65, 0.95, hue));
        gl_FragColor = vec4(vec3(0.0015, 0.0021, 0.0040) + tint * density, 1.0);
        ${outputColor}
      }
    `,
    side: BackSide,
    depthWrite: false,
  });
}
