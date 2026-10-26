export interface UniverseDto {
  bodies: BodyDto[];
}

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
