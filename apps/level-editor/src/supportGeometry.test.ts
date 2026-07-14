import { describe, expect, it } from 'vitest'
import { nearestSupportAnchor } from './supportGeometry'
import type { SupportTrack } from './model'

const support: SupportTrack = {
  id: 'middle_ramp', depth: 'middle', traversal: 'stable', speedMultiplier: 1, placeholder: false,
  points: [{ x: 0, y: 100, heightMeters: 0 }, { x: 200, y: 50, heightMeters: 4 }],
}

describe('nearestSupportAnchor', () => {
  it('projects onto a segment and interpolates physical height', () => {
    const anchor = nearestSupportAnchor(support, { x: 100, y: 80 })
    expect(anchor.supportId).toBe('middle_ramp')
    expect(anchor.depth).toBe('middle')
    expect(anchor.point.x).toBeCloseTo(98.82, 1)
    expect(anchor.point.y).toBeCloseTo(75.29, 1)
    expect(anchor.heightMeters).toBeCloseTo(1.98, 1)
  })

  it('clamps anchors to a support endpoint', () => {
    const anchor = nearestSupportAnchor(support, { x: 400, y: 20 })
    expect(anchor.point).toEqual({ x: 200, y: 50 })
    expect(anchor.heightMeters).toBe(4)
  })
})
