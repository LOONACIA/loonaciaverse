export interface RenderQuality {
  pixelRatio: number;
  starCount: number;
  bloom: boolean;
  parallax: boolean;
}

export const DESKTOP_STAR_COUNT = 3000;

export function getQuality({ devicePixelRatio, coarsePointer, viewportWidth }: { devicePixelRatio: number; coarsePointer: boolean; viewportWidth: number }): RenderQuality {
  const mobile = coarsePointer || viewportWidth < 768;
  return {
    pixelRatio: Math.min(devicePixelRatio, mobile ? 1.5 : 2),
    starCount: mobile ? DESKTOP_STAR_COUNT / 2 : DESKTOP_STAR_COUNT,
    bloom: !mobile,
    parallax: !mobile,
  };
}

// Radians in camera-local axes: look toward the pointer, with a circular angle limit.
export function parallaxOffset(pointerNdc: { x: number; y: number }, maxDegrees: number): { x: number; y: number } {
  const scale = Math.max(0, maxDegrees) * Math.PI / 180 / Math.max(1, Math.hypot(pointerNdc.x, pointerNdc.y));
  return { x: pointerNdc.y * scale || 0, y: -pointerNdc.x * scale || 0 };
}
