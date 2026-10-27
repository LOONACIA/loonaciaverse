import type { BodyDto } from "./types";

// Pointing at a body holds it still for clicking. The camera follows the focused body, so it never needs to be held.
export function shouldPauseOrbits(focusedId: string | null, hoveredId: string | null, highlightedId: string | null): boolean {
  return [hoveredId, highlightedId].some((id) => id !== null && id !== focusedId);
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
