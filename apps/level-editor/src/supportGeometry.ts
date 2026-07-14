import type { SupportTrack, WorldPoint } from './model'

export interface SupportAnchor {
  supportId: string
  depth: SupportTrack['depth']
  point: WorldPoint
  heightMeters: number
}

export function nearestSupportAnchor(support: SupportTrack, pointer: WorldPoint): SupportAnchor {
  let best = {
    distanceSquared: Number.POSITIVE_INFINITY,
    point: { x: support.points[0]?.x ?? 0, y: support.points[0]?.y ?? 0 },
    heightMeters: support.points[0]?.heightMeters ?? 0,
  }

  for (let index = 1; index < support.points.length; index++) {
    const from = support.points[index - 1]
    const to = support.points[index]
    const dx = to.x - from.x; const dy = to.y - from.y
    const lengthSquared = dx * dx + dy * dy
    const rawT = lengthSquared ? ((pointer.x - from.x) * dx + (pointer.y - from.y) * dy) / lengthSquared : 0
    const t = Math.max(0, Math.min(1, rawT))
    const point = { x: from.x + dx * t, y: from.y + dy * t }
    const distanceSquared = (pointer.x - point.x) ** 2 + (pointer.y - point.y) ** 2
    if (distanceSquared < best.distanceSquared) {
      best = { distanceSquared, point, heightMeters: from.heightMeters + (to.heightMeters - from.heightMeters) * t }
    }
  }

  return { supportId: support.id, depth: support.depth, point: best.point, heightMeters: best.heightMeters }
}
