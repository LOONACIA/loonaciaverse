import { afterEach, describe, expect, it, vi } from "vitest";
import { Vector3 } from "three";
import { BACK, UP } from "./camera";
import { createMeteor, METEOR_FLIGHT_SECONDS, METEOR_MAX_WAIT_SECONDS, METEOR_MIN_WAIT_SECONDS } from "./meteor";

const back = new Vector3(BACK.x, BACK.y, BACK.z);
const up = new Vector3(UP.x, UP.y, UP.z);

afterEach(() => {
  vi.restoreAllMocks();
});

describe("meteor", () => {
  it.each([
    [0, METEOR_MIN_WAIT_SECONDS],
    [0.999, METEOR_MIN_WAIT_SECONDS + 0.999 * (METEOR_MAX_WAIT_SECONDS - METEOR_MIN_WAIT_SECONDS)],
  ])("stays hidden for its random wait (random %d)", (random, wait) => {
    vi.spyOn(Math, "random").mockReturnValue(random);
    const meteor = createMeteor(100);
    meteor.update(wait - 0.01);
    expect(meteor.mesh.visible).toBe(false);
    meteor.update(0.02);
    expect(meteor.mesh.visible).toBe(true);
  });

  it("falls across the plane facing the camera, fades in and out, then waits again", () => {
    vi.spyOn(Math, "random").mockReturnValue(0);
    const meteor = createMeteor(100);
    const opacity = () => meteor.mesh.material.uniforms.uOpacity.value as number;
    meteor.update(METEOR_MIN_WAIT_SECONDS);
    const start = meteor.mesh.position.clone();
    expect(start.dot(back)).toBeCloseTo(-100);
    expect(new Vector3(0, 0, 1).applyQuaternion(meteor.mesh.quaternion).distanceTo(back)).toBeLessThan(1e-9);
    expect(opacity()).toBe(0);

    meteor.update(METEOR_FLIGHT_SECONDS / 2);
    const travel = meteor.mesh.position.clone().sub(start);
    expect(meteor.mesh.position.dot(back)).toBeCloseTo(-100);
    // The head (+X) leads, and the path runs down the screen.
    expect(travel.clone().normalize().distanceTo(new Vector3(1, 0, 0).applyQuaternion(meteor.mesh.quaternion))).toBeLessThan(1e-9);
    expect(travel.dot(up)).toBeLessThan(0);
    expect(opacity()).toBeCloseTo(1);

    meteor.update(METEOR_FLIGHT_SECONDS / 2);
    expect(meteor.mesh.visible).toBe(false);
    expect(opacity()).toBe(0);
    meteor.update(METEOR_MIN_WAIT_SECONDS - 0.01);
    expect(meteor.mesh.visible).toBe(false);
  });

  it("ignores negative frame deltas", () => {
    vi.spyOn(Math, "random").mockReturnValue(0);
    const meteor = createMeteor(100);
    meteor.update(-1000);
    meteor.update(METEOR_MIN_WAIT_SECONDS - 0.01);
    expect(meteor.mesh.visible).toBe(false);
  });
});
