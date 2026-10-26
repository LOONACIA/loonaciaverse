export interface UniverseDto {
  bodies: BodyDto[];
}

export type PlanetSurface = "terran" | "gas" | "desert";

export interface BodyDto {
  id: string;
  name: string;
  subtitle: string;
  locked: boolean;
  visual: {
    baseColor: string;
    atmosphereColor: string;
    noiseScale: number;
    glowIntensity: number;
    // Null only for the star; the content builder requires it for every planet.
    surface: PlanetSurface | null;
  };
  orbit: {
    radius: number;
    periodSeconds: number;
    phase: number;
  } | null;
}

export interface DotNetCallbacks {
  invokeMethodAsync(methodName: string, ...args: unknown[]): Promise<unknown>;
}
