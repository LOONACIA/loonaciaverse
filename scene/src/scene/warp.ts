// Leave a little headroom within the PRD's one-second travel transition.
export const WARP_DURATION_MS = 900;

export function warpTimeline(elapsedMs: number): { dash: number; streak: number; fade: number } {
  const progress = Math.max(0, Math.min(1, elapsedMs / WARP_DURATION_MS));
  const ramp = (start: number, end: number) => Math.max(0, Math.min(1, (progress - start) / (end - start)));
  return {
    dash: ramp(0, 0.8) ** 2,
    streak: ramp(0.15, 0.85) ** 2,
    fade: ramp(0.6, 1),
  };
}
