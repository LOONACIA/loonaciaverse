export interface RenderQuality {
  pixelRatio: number;
  starCount: number;
  bloom: boolean;
}

export const DESKTOP_STAR_COUNT = 8000;

export function getQuality({ devicePixelRatio, coarsePointer, viewportWidth }: { devicePixelRatio: number; coarsePointer: boolean; viewportWidth: number }): RenderQuality {
  const mobile = coarsePointer || viewportWidth < 768;
  return {
    pixelRatio: Math.min(devicePixelRatio, mobile ? 1.5 : 2),
    starCount: mobile ? DESKTOP_STAR_COUNT / 2 : DESKTOP_STAR_COUNT,
    bloom: !mobile,
  };
}

