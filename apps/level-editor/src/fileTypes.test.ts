import { describe, expect, it } from 'vitest'
import { isImageFile } from './fileTypes'

describe('isImageFile', () => {
  it('accepts PNG files even when Windows supplies no MIME type', () => {
    expect(isImageFile({ name: 'tomb.PNG', type: '' })).toBe(true)
  })

  it('accepts image MIME types and rejects map packages', () => {
    expect(isImageFile({ name: 'background', type: 'image/png' })).toBe(true)
    expect(isImageFile({ name: 'level.moadmap', type: 'application/zip' })).toBe(false)
  })
})
