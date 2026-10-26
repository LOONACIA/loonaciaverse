import { describe, expect, it } from "vitest";
import { AdditiveBlending, BackSide, Color, ShaderMaterial } from "three";
import { createAtmosphereMaterial, createCoronaMaterial, createNebulaMaterial, createPlanetMaterial, createStarMaterial } from "./materials";
import type { BodyDto } from "./types";

const visuals: BodyDto["visual"][] = [
  { baseColor: "#236b8e", atmosphereColor: "#5fd4ff", noiseScale: 4, glowIntensity: 0.4 },
  { baseColor: "#fff2cc", atmosphereColor: "#ffd780", noiseScale: 3.5, glowIntensity: 1.8 },
  { baseColor: "#785339", atmosphereColor: "#ffb347", noiseScale: 6, glowIntensity: 0 },
];

describe("procedural materials", () => {
  it.each(visuals)("maps visual parameters to surface and glow uniforms: %j", (visual) => {
    const planet = createPlanetMaterial(visual);
    const star = createStarMaterial(visual);
    const atmosphere = createAtmosphereMaterial(visual);
    const corona = createCoronaMaterial(visual);
    try {
      expect(planet.uniforms).toEqual({
        uBaseColor: { value: new Color(visual.baseColor) },
        uNoiseScale: { value: visual.noiseScale },
        uTime: { value: 0 },
      });
      expect(star.uniforms).toEqual({ ...planet.uniforms, uGlowIntensity: { value: visual.glowIntensity } });
      for (const material of [atmosphere, corona]) {
        expect(material.uniforms).toEqual({
          uAtmosphereColor: { value: new Color(visual.atmosphereColor) },
          uGlowIntensity: { value: visual.glowIntensity },
        });
        expect(material.blending).toBe(AdditiveBlending);
        expect(material.transparent).toBe(true);
        expect(material.depthWrite).toBe(false);
      }
    } finally {
      [planet, star, atmosphere, corona].forEach((material) => material.dispose());
    }
  });

  const factories = [createPlanetMaterial, createAtmosphereMaterial, createStarMaterial, createCoronaMaterial, createNebulaMaterial];
  it.each(factories)("declares every supplied uniform with the matching GLSL type: %s", (createMaterial) => {
    const material = createMaterial(visuals[0]);
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
