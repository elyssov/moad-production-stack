import { describe, expect, it } from 'vitest'
import { panWithArrow } from './viewport'

describe('panWithArrow', () => {
  const view = { x: 40, y: 20, scale: 2 }

  it('pans the camera without changing zoom', () => {
    expect(panWithArrow(view, 'ArrowRight', false)).toEqual({ x: -80, y: 20, scale: 2 })
    expect(panWithArrow(view, 'ArrowUp', false)).toEqual({ x: 40, y: 140, scale: 2 })
  })

  it('uses a larger step while Shift is held', () => {
    expect(panWithArrow(view, 'ArrowLeft', true)).toEqual({ x: 360, y: 20, scale: 2 })
  })
})
