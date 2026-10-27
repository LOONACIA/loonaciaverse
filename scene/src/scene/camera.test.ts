import { describe, expect, it } from "vitest";
import { CameraTween, easeInOutCubic, focusPose, interpolatePose, overviewPose } from "./camera";
import type { Pose, Vector3 } from "./camera";

const from: Pose = { position: { x: 0, y: 10, z: 20 }, target: { x: 0, y: 0, z: 0 } };
const to: Pose = { position: { x: 8, y: 4, z: 6 }, target: { x: 2, y: 1, z: 3 } };
const subtract = (a: Vector3, b: Vector3): Vector3 => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
const dot = (a: Vector3, b: Vector3) => a.x * b.x + a.y * b.y + a.z * b.z;
const cross = (a: Vector3, b: Vector3): Vector3 => ({ x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x });

describe("camera poses", () => {
  it.each([0, 0.5, 1])("preserves easing boundary %s", (t) => {
    expect(easeInOutCubic(t)).toBe(t);
  });

  it("eases in and out symmetrically and clamps progress", () => {
    expect(easeInOutCubic(0.25)).toBe(0.0625);
    expect(easeInOutCubic(0.75)).toBe(0.9375);
    expect(easeInOutCubic(-1)).toBe(0);
    expect(easeInOutCubic(2)).toBe(1);
  });

  it("interpolates both position and target", () => {
    expect(interpolatePose(from, to, 0)).toEqual(from);
    expect(interpolatePose(from, to, 1)).toEqual(to);
    expect(interpolatePose(from, to, 0.5)).toEqual({ position: { x: 4, y: 7, z: 13 }, target: { x: 1, y: 0.5, z: 1.5 } });
  });

  it.each([0.5, 1, 1.5, 2])("fits the oblique orbital disc snugly inside the frame at aspect %s", (aspect) => {
    const pose = overviewPose(10, aspect);
    const forward = subtract(pose.target, pose.position);
    const right = cross(forward, { x: 0, y: 1, z: 0 });
    const up = cross(right, forward);
    const length = (vector: Vector3) => Math.hypot(vector.x, vector.y, vector.z);
    const tangent = Math.tan(Math.PI / 8);
    const extent = Math.max(...Array.from({ length: 360 }, (_, degree) => {
      const angle = degree * Math.PI / 180;
      const offset = subtract({ x: 10 * Math.cos(angle), y: 0, z: 10 * Math.sin(angle) }, pose.position);
      const depth = dot(offset, forward) / length(forward);
      return Math.max(
        Math.abs(dot(offset, up) / length(up)) / (depth * tangent),
        Math.abs(dot(offset, right) / length(right)) / (depth * tangent * aspect),
      );
    }));
    expect(extent).toBeLessThan(1);
    expect(extent).toBeGreaterThan(0.8);
  });

  it.each([0.5, 1, 1.5, 2])("approaches the body while preserving the overview direction at aspect %s", (aspect) => {
    const body = { x: 8, y: 0, z: -4 };
    const overview = overviewPose(16.9, aspect);
    const focused = focusPose(body, 0.9, aspect);
    const offset = subtract(focused.position, body);
    expect(Math.hypot(offset.x, offset.y, offset.z)).toBeLessThan(Math.hypot(overview.position.x, overview.position.y, overview.position.z));
    const direction = subtract(focused.position, focused.target);
    const overviewDirection = subtract(overview.position, overview.target);
    const perpendicular = cross(direction, overviewDirection);
    expect(Math.hypot(perpendicular.x, perpendicular.y, perpendicular.z)).toBeCloseTo(0);
  });

  it.each([1.2, 1.5, 2])("centers the body at the HUD's on-screen radius at aspect %s", (aspect) => {
    const body = { x: -3, y: 0, z: 8 };
    const pose = focusPose(body, 0.9, aspect);
    expect(pose.target).toEqual(body);
    // A sphere's silhouette subtends asin(radius / distance); NDC spans the full height over 2 units.
    const offset = subtract(pose.position, body);
    const silhouette = Math.tan(Math.asin(0.9 / Math.hypot(offset.x, offset.y, offset.z))) / Math.tan(Math.PI / 8) / 2;
    // HudPanel.razor.css draws its callouts around min(20dvh, 11.5vw).
    expect(silhouette).toBeCloseTo(Math.min(0.2, 0.115 * aspect));
  });

  it.each([0.5, 1])("leaves HUD space below at aspect %s", (aspect) => {
    const body = { x: -3, y: 0, z: 8 };
    const pose = focusPose(body, 0.9, aspect);
    const forward = subtract(pose.target, pose.position);
    const right = cross(forward, { x: 0, y: 1, z: 0 });
    const up = cross(right, forward);
    const targetToBody = subtract(body, pose.target);
    expect(dot(up, targetToBody)).toBeGreaterThan(0);
    expect(dot(right, targetToBody)).toBeCloseTo(0);
  });

  it("scales the approach distance with the body's radius", () => {
    const small = focusPose({ x: 0, y: 0, z: 0 }, 1, 1.5);
    const large = focusPose({ x: 0, y: 0, z: 0 }, 2, 1.5);
    expect(large.position.x).toBeCloseTo(small.position.x * 2);
    expect(large.position.y).toBeCloseTo(small.position.y * 2);
    expect(large.position.z).toBeCloseTo(small.position.z * 2);
  });
});

describe("CameraTween", () => {
  it("arrives at the exact target in 0.8 seconds and remains there", () => {
    const tween = new CameraTween(from, to, 0.8);
    expect(tween.current).toEqual(from);
    expect(tween.step(0.4)).toEqual(interpolatePose(from, to, 0.5));
    expect(tween.step(0.4)).toEqual(to);
    expect(tween.step(10)).toEqual(to);
  });

  it("restarts at its current pose when retargeted during a transition", () => {
    const tween = new CameraTween(from, to, 0.8);
    const current = tween.step(0.2);
    tween.retarget(from);
    expect(tween.step(0)).toEqual(current);
    expect(tween.step(0.4)).toEqual(interpolatePose(current, from, 0.5));
    expect(tween.step(0.4)).toEqual(from);
  });

  it("applies immediate poses and can animate again afterwards", () => {
    const tween = new CameraTween(from, from, 0);
    tween.retarget(to, 0);
    expect(tween.current).toEqual(to);
    expect(tween.step(0)).toEqual(to);
    tween.retarget(from, 0.8);
    expect(tween.step(0.8)).toEqual(from);
  });
});
