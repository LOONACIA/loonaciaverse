import { describe, expect, it } from "vitest";
import { getQuality, parallaxOffset } from "./quality";

describe("render quality", () => {
  const desktop = { devicePixelRatio: 1, coarsePointer: false, viewportWidth: 1200 };

  it("enables desktop effects and retains native resolution below the cap", () => {
    expect(getQuality(desktop)).toEqual({ pixelRatio: 1, starCount: 3000, bloom: true, parallax: true });
    expect(getQuality({ ...desktop, devicePixelRatio: 1.75 }).pixelRatio).toBe(1.75);
    expect(getQuality({ ...desktop, devicePixelRatio: 3 }).pixelRatio).toBe(2);
  });

  it.each([
    { coarsePointer: true, viewportWidth: 1200 },
    { coarsePointer: false, viewportWidth: 767 },
    { coarsePointer: true, viewportWidth: 400 },
  ])("disables effects and halves the stars on mobile: %o", (mobile) => {
    const quality = getQuality({ ...desktop, ...mobile, devicePixelRatio: 3 });
    expect(quality).toEqual({ pixelRatio: 1.5, starCount: 1500, bloom: false, parallax: false });
    expect(quality.starCount).toBeLessThanOrEqual(getQuality(desktop).starCount / 2);
    expect(getQuality({ ...desktop, ...mobile, devicePixelRatio: 1.25 }).pixelRatio).toBe(1.25);
  });

  it("starts the desktop tier at 768 pixels", () => {
    expect(getQuality({ ...desktop, viewportWidth: 768 }).bloom).toBe(true);
  });
});

describe("parallax offsets", () => {
  const radians = 2 * Math.PI / 180;

  it("has no offset at the center", () => {
    expect(parallaxOffset({ x: 0, y: 0 }, 2)).toEqual({ x: 0, y: 0 });
  });

  it.each([
    { x: 1, y: 0 }, { x: -1, y: 0 }, { x: 0, y: 1 }, { x: 0, y: -1 },
    { x: 1, y: 1 }, { x: -1, y: -1 }, { x: 2, y: -3 },
  ])("keeps the combined angle within the limit even at corners: %o", (pointer) => {
    const offset = parallaxOffset(pointer, 2);
    expect(Math.hypot(offset.x, offset.y)).toBeLessThanOrEqual(radians + 1e-12);
    expect(Math.hypot(offset.x, offset.y)).toBeCloseTo(radians);
  });

  it("looks toward the pointer in camera-local pitch and yaw", () => {
    expect(parallaxOffset({ x: 0.5, y: 0.25 }, 2)).toEqual({ x: radians * 0.25, y: -radians * 0.5 });
    expect(parallaxOffset({ x: -0.5, y: -0.25 }, 2)).toEqual({ x: -radians * 0.25, y: radians * 0.5 });
  });

  it("can disable the angle entirely", () => {
    const offset = parallaxOffset({ x: 1, y: 1 }, 0);
    expect(Math.hypot(offset.x, offset.y)).toBe(0);
  });
});
