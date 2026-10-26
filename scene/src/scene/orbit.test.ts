import { describe, expect, it } from "vitest";
import { OrbitClock, orbitPosition } from "./orbit";

describe("orbitPosition", () => {
  const orbit = { radius: 8, periodSeconds: 80, phase: 0 };

  it("starts on the positive X axis at phase zero", () => {
    expect(orbitPosition(orbit, 0)).toEqual({ x: 8, z: 0 });
  });

  it("reaches the positive Z axis after a quarter period", () => {
    const position = orbitPosition(orbit, 20);
    expect(position.x).toBeCloseTo(0);
    expect(position.z).toBeCloseTo(8);
  });

  it("adds the configured phase to the elapsed orbit angle", () => {
    const position = orbitPosition({ ...orbit, phase: Math.PI / 2 }, 20);
    expect(position.x).toBeCloseTo(-8);
    expect(position.z).toBeCloseTo(0);
  });

  it("keeps the star at the origin at any time", () => {
    expect(orbitPosition(null, 123)).toEqual({ x: 0, z: 0 });
  });
});

describe("OrbitClock", () => {
  it("excludes paused time and resumes without jumping ahead", () => {
    const clock = new OrbitClock();
    clock.advance(2, false);
    clock.advance(100, true);
    expect(clock.seconds).toBe(2);
    clock.advance(3, false);
    expect(clock.seconds).toBe(5);
  });
});
