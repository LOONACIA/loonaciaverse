import type { BodyDto } from "./types";

export function shouldPauseOrbits(focused: boolean, hovered: boolean, highlighted: boolean): boolean {
  return focused || hovered || highlighted;
}

export function orbitPosition(orbit: BodyDto["orbit"], orbitSeconds: number): { x: number; z: number } {
  if (orbit === null) {
    return { x: 0, z: 0 };
  }

  const angle = orbit.phase + (2 * Math.PI * orbitSeconds) / orbit.periodSeconds;
  return { x: orbit.radius * Math.cos(angle), z: orbit.radius * Math.sin(angle) };
}

export class OrbitClock {
  private elapsedSeconds = 0;

  get seconds(): number {
    return this.elapsedSeconds;
  }

  advance(deltaSeconds: number, paused: boolean): void {
    if (!paused) {
      this.elapsedSeconds += deltaSeconds;
    }
  }
}
