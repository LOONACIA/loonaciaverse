import { describe, expect, it } from "vitest";
import { AdditiveBlending, BackSide, Color, ShaderMaterial } from "three";
import {
  createAtmosphereMaterial, createCloudMaterial, createCoronaMaterial, createNebulaMaterial, createPlanetMaterial,
  createPlanetRingMaterial, createStarFieldMaterial, createStarMaterial,
} from "./materials";
import type { BodyDto } from "./types";

const visuals: BodyDto["visual"][] = [
  { baseColor: "#236b8e", atmosphereColor: "#5fd4ff", noiseScale: 4, glowIntensity: 0.4, surface: "terran" },
  { baseColor: "#6956a8", atmosphereColor: "#bda5ff", noiseScale: 5, glowIntensity: 1.8, surface: "gas" },
  { baseColor: "#785339", atmosphereColor: "#ffb347", noiseScale: 6, glowIntensity: 0, surface: "desert" },
];

describe("procedural materials", () => {
  it.each(visuals)("maps visual parameters to surface and glow uniforms: %j", (visual) => {
    const planet = createPlanetMaterial(visual);
    const star = createStarMaterial(visual);
    const atmosphere = createAtmosphereMaterial(visual);
    const corona = createCoronaMaterial(visual);
    const ring = createPlanetRingMaterial(visual);
    try {
      expect(planet.uniforms).toEqual({
        uBaseColor: { value: new Color(visual.baseColor) },
        uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
        uNoiseScale: { value: visual.noiseScale },
        uTime: { value: 0 },
      });
      expect(star.uniforms).toEqual({ ...planet.uniforms, uGlowIntensity: { value: visual.glowIntensity } });
      const { uTime: _, ...ringUniforms } = planet.uniforms;
      expect(ring.uniforms).toEqual(ringUniforms);
      const glow = { uAtmosphereColor: { value: new Color(visual.atmosphereColor) }, uGlowIntensity: { value: visual.glowIntensity } };
      expect(atmosphere.uniforms).toEqual(glow);
      expect(corona.uniforms).toEqual({ ...glow, uTime: { value: 0 } });
      for (const material of [atmosphere, corona]) {
        expect(material.blending).toBe(AdditiveBlending);
        expect(material.transparent).toBe(true);
        expect(material.depthWrite).toBe(false);
      }
      expect(atmosphere.side).toBe(BackSide);
      expect(ring.transparent).toBe(true);
      expect(ring.depthWrite).toBe(false);
    } finally {
      [planet, star, atmosphere, corona, ring].forEach((material) => material.dispose());
    }
  });

  it("gives each planet surface type its own surface shader", () => {
    const materials = visuals.map(createPlanetMaterial);
    try {
      const shaders = materials.map((material) => material.fragmentShader);
      expect(new Set(shaders).size).toBe(visuals.length);
      shaders.forEach((shader) => expect(shader).toMatch(/vec3 surface\(vec3 direction, float light, inout vec3 normal, out float gloss, out vec3 emission\)/));
      expect(new Set(materials.map((material) => material.vertexShader)).size).toBe(1);
    } finally {
      materials.forEach((material) => material.dispose());
    }
  });

  it("scatters sunlight only in terran air", () => {
    for (const visual of visuals) {
      const materials = [createPlanetMaterial(visual), createAtmosphereMaterial(visual)];
      try {
        materials.forEach((material) => expect(material.defines).toEqual(visual.surface === "terran" ? { SCATTERING: "" } : {}));
      } finally {
        materials.forEach((material) => material.dispose());
      }
    }
  });

  it("draws terran clouds as a translucent layer whose cover the ground shares for its shadows", () => {
    const clouds = createCloudMaterial(visuals[0]);
    const planet = createPlanetMaterial(visuals[0]);
    try {
      expect(clouds.name).toBe("clouds");
      expect(clouds.uniforms).toEqual({
        uAtmosphereColor: { value: new Color(visuals[0].atmosphereColor) },
        uNoiseScale: { value: visuals[0].noiseScale },
        uTime: { value: 0 },
      });
      expect(clouds.transparent).toBe(true);
      expect(clouds.depthWrite).toBe(false);
      expect(clouds.vertexShader).toBe(planet.vertexShader);
      const cover = clouds.fragmentShader.match(/float cloudCover\(vec3 direction\) \{[\s\S]*?return [^;]+;\s*\}/)![0];
      expect(planet.fragmentShader).toContain(cover);
    } finally {
      [clouds, planet].forEach((material) => material.dispose());
    }
  });

  const factories: [string, () => ShaderMaterial][] = [
    ["planet", () => createPlanetMaterial(visuals[0])],
    ["atmosphere", () => createAtmosphereMaterial(visuals[0])],
    ["clouds", () => createCloudMaterial(visuals[0])],
    ["planet ring", () => createPlanetRingMaterial(visuals[0])],
    ["star", () => createStarMaterial(visuals[0])],
    ["corona", () => createCoronaMaterial(visuals[0])],
    ["star field", () => createStarFieldMaterial(1.5)],
    ["nebula", () => createNebulaMaterial()],
  ];
  it.each(factories)("declares every supplied uniform with the matching GLSL type: %s", (_, createMaterial) => {
    const material = createMaterial();
    try {
      expect(material).toBeInstanceOf(ShaderMaterial);
      const shaders = material.vertexShader + "\n" + material.fragmentShader;
      for (const [name, uniform] of Object.entries(material.uniforms)) {
        const type = uniform.value instanceof Color ? "vec3" : "float";
        expect(shaders).toMatch(new RegExp(`uniform\\s+${type}\\s+${name}\\s*;`));
      }
      // Check the reverse direction too, so a declaration typo cannot silently get a default value.
      const declaredUniforms = [...shaders.matchAll(/uniform\s+(?:vec3|float)\s+(u\w+)\s*;/g)].map((match) => match[1]);
      expect(declaredUniforms.sort()).toEqual(Object.keys(material.uniforms).sort());
      expect(material.fragmentShader).toContain("#include <tonemapping_fragment>");
      expect(material.fragmentShader).toContain("#include <colorspace_fragment>");
    } finally {
      material.dispose();
    }
  });

  it("draws the star field as additive screen-sized points that start fully visible", () => {
    const material = createStarFieldMaterial(1.5);
    try {
      expect(material.name).toBe("star-field");
      expect(material.uniforms).toEqual({ uTime: { value: 0 }, uOpacity: { value: 1 }, uPixelRatio: { value: 1.5 } });
      expect(material.vertexShader).toMatch(/gl_PointSize\s*=\s*aSize \* uPixelRatio;/);
      for (const attribute of ["float aSize", "vec3 aColor", "vec3 aTwinkle"]) {
        expect(material.vertexShader).toContain(`attribute ${attribute};`);
      }
      expect(material.blending).toBe(AdditiveBlending);
      expect(material.depthWrite).toBe(false);
    } finally {
      material.dispose();
    }
  });

  it("renders the nebula from inside without writing depth", () => {
    const material = createNebulaMaterial();
    try {
      expect(material.name).toBe("nebula");
      expect(material.side).toBe(BackSide);
      expect(material.depthWrite).toBe(false);
      expect(material.uniforms.uTime.value).toBe(0);
    } finally {
      material.dispose();
    }
  });
});
