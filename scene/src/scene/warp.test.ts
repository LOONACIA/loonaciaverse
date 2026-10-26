import { describe, expect, it } from "vitest";
import { WARP_DURATION_MS, warpTimeline } from "./warp";

describe("warp timeline", () => {
  it("finishes within one second", () => {
    expect(WARP_DURATION_MS).toBeGreaterThan(0);
    expect(WARP_DURATION_MS).toBeLessThanOrEqual(1000);
  });

  it("clamps the start and end boundaries", () => {
    for (const elapsed of [-100, 0]) {
      expect(warpTimeline(elapsed)).toEqual({ dash: 0, streak: 0, fade: 0 });
    }
    for (const elapsed of [WARP_DURATION_MS, WARP_DURATION_MS + 100, Infinity]) {
      expect(warpTimeline(elapsed)).toEqual({ dash: 1, streak: 1, fade: 1 });
    }
  });

  it("keeps every effect bounded and monotonically increasing", () => {
    let previous = warpTimeline(0);
    for (let elapsed = 1; elapsed <= WARP_DURATION_MS; elapsed++) {
      const current = warpTimeline(elapsed);
      for (const key of ["dash", "streak", "fade"] as const) {
        expect(current[key]).toBeGreaterThanOrEqual(previous[key]);
        expect(current[key]).toBeGreaterThanOrEqual(0);
        expect(current[key]).toBeLessThanOrEqual(1);
      }
      previous = current;
    }
  });

  it("starts the dash before streaks and finishes the fade last", () => {
    const dashStarted = warpTimeline(WARP_DURATION_MS * 0.1);
    expect(dashStarted.dash).toBeGreaterThan(0);
    expect(dashStarted.streak).toBe(0);
    expect(dashStarted.fade).toBe(0);
    const streakStarted = warpTimeline(WARP_DURATION_MS * 0.4);
    expect(streakStarted.streak).toBeGreaterThan(0);
    expect(streakStarted.fade).toBe(0);
    const fading = warpTimeline(WARP_DURATION_MS * 0.9);
    expect(fading.dash).toBe(1);
    expect(fading.streak).toBe(1);
    expect(fading.fade).toBeGreaterThan(0);
    expect(fading.fade).toBeLessThan(1);
  });
});
