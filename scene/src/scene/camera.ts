export interface Vector3 {
  x: number;
  y: number;
  z: number;
}

export interface Pose {
  position: Vector3;
  target: Vector3;
}

// Fixed oblique view, with camera-right +X and camera-up (0, 0.8, -0.6).
const BACK = { x: 0, y: 0.6, z: 0.8 };
const UP = { x: 0, y: 0.8, z: -0.6 };
const HALF_FOV = Math.PI / 8;

function positionFrom(target: Vector3, distance: number): Vector3 {
  return { x: target.x + BACK.x * distance, y: target.y + BACK.y * distance, z: target.z + BACK.z * distance };
}

export function overviewPose(systemRadius: number, aspect: number): Pose {
  const horizontalHalfFov = Math.atan(Math.tan(HALF_FOV) * aspect);
  const distance = systemRadius * 1.1 / Math.sin(Math.min(HALF_FOV, horizontalHalfFov));
  const target = { x: 0, y: 0, z: 0 };
  return { position: positionFrom(target, distance), target };
}

export function focusPose(bodyPosition: Vector3, bodyRadius: number, aspect: number): Pose {
  const distance = bodyRadius * 6 * Math.max(1, 1 / aspect);
  const halfHeight = distance * Math.tan(HALF_FOV);
  const target = aspect > 1
    ? { x: bodyPosition.x - halfHeight * aspect * 0.3, y: bodyPosition.y, z: bodyPosition.z }
    : { x: bodyPosition.x, y: bodyPosition.y - UP.y * halfHeight * 0.4, z: bodyPosition.z - UP.z * halfHeight * 0.4 };
  return { position: positionFrom(target, distance), target };
}

export function easeInOutCubic(t: number): number {
  const progress = Math.max(0, Math.min(1, t));
  return progress < 0.5 ? 4 * progress ** 3 : 1 - (-2 * progress + 2) ** 3 / 2;
}

export function interpolatePose(from: Pose, to: Pose, t: number): Pose {
  const progress = Math.max(0, Math.min(1, t));
  const interpolate = (a: Vector3, b: Vector3): Vector3 => {
    if (progress === 0) return { ...a };
    if (progress === 1) return { ...b };
    return { x: a.x + (b.x - a.x) * progress, y: a.y + (b.y - a.y) * progress, z: a.z + (b.z - a.z) * progress };
  };
  return { position: interpolate(from.position, to.position), target: interpolate(from.target, to.target) };
}

export class CameraTween {
  private from: Pose;
  private to: Pose;
  private pose: Pose;
  private elapsedSeconds = 0;

  constructor(from: Pose, to: Pose, private durationSeconds: number) {
    this.from = interpolatePose(from, from, 0);
    this.to = interpolatePose(to, to, 0);
    this.pose = durationSeconds <= 0 ? this.to : this.from;
  }

  get current(): Pose {
    return interpolatePose(this.pose, this.pose, 0);
  }

  retarget(to: Pose, durationSeconds = this.durationSeconds): void {
    this.from = this.current;
    this.to = interpolatePose(to, to, 0);
    this.durationSeconds = durationSeconds;
    this.elapsedSeconds = 0;
    if (durationSeconds <= 0) this.pose = this.to;
  }

  step(deltaSeconds: number): Pose {
    this.elapsedSeconds += Math.max(0, deltaSeconds);
    const progress = this.durationSeconds <= 0 ? 1 : this.elapsedSeconds / this.durationSeconds;
    this.pose = interpolatePose(this.from, this.to, easeInOutCubic(progress));
    return this.current;
  }
}
