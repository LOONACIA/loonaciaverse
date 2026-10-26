import { describe, expect, it } from "vitest";
import { getQuality } from "./quality";

describe("render quality", () => {
  const desktop = { devicePixelRatio: 1, coarsePointer: false, viewportWidth: 1200 };

  it("enables desktop effects and retains native resolution below the cap", () => {
    expect(getQuality(desktop)).toEqual({ pixelRatio: 1, starCount: 8000, bloom: true });
    expect(getQuality({ ...desktop, devicePixelRatio: 1.75 }).pixelRatio).toBe(1.75);
    expect(getQuality({ ...desktop, devicePixelRatio: 3 }).pixelRatio).toBe(2);
  });

  it.each([
    { coarsePointer: true, viewportWidth: 1200 },
    { coarsePointer: false, viewportWidth: 767 },
    { coarsePointer: true, viewportWidth: 400 },
  ])("disables effects and halves the stars on mobile: %o", (mobile) => {
    const quality = getQuality({ ...desktop, ...mobile, devicePixelRatio: 3 });
    expect(quality).toEqual({ pixelRatio: 1.5, starCount: 4000, bloom: false });
    expect(quality.starCount).toBeLessThanOrEqual(getQuality(desktop).starCount / 2);
    expect(getQuality({ ...desktop, ...mobile, devicePixelRatio: 1.25 }).pixelRatio).toBe(1.25);
  });

  it("starts the desktop tier at 768 pixels", () => {
    expect(getQuality({ ...desktop, viewportWidth: 768 }).bloom).toBe(true);
  });
});
