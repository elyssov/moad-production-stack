import JSZip from 'jszip'
import type { LevelDocument } from './model'
import { parseLevelJson, runtimeContract } from './serialization'

function downloadBlob(fileName: string, blob: Blob): void {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}

function dataUrlBytes(dataUrl: string): Uint8Array {
  const binary = atob(dataUrl.split(',')[1] ?? '')
  return Uint8Array.from(binary, (character) => character.charCodeAt(0))
}

export async function buildMoadMap(level: LevelDocument): Promise<Blob> {
  const zip = new JSZip()
  const source = structuredClone(level)
  if (source.background) source.background.dataUrl = ''
  zip.file('manifest.json', JSON.stringify({ format: 'moadmap', version: 1, source: 'level.source.json', runtime: 'level.runtime.json' }, null, 2))
  zip.file('level.source.json', JSON.stringify(source, null, 2))
  zip.file('level.runtime.json', JSON.stringify(runtimeContract(level), null, 2))
  if (level.background?.dataUrl) zip.file(`background/${level.background.fileName}`, dataUrlBytes(level.background.dataUrl))
  if (level.background) {
    for (const occluder of level.occluders) {
      const canvas = document.createElement('canvas')
      canvas.width = level.background.width
      canvas.height = level.background.height
      const context = canvas.getContext('2d')
      if (!context || occluder.points.length < 3) continue
      context.beginPath()
      context.moveTo(occluder.points[0].x, occluder.points[0].y)
      for (const point of occluder.points.slice(1)) context.lineTo(point.x, point.y)
      context.closePath()
      context.fillStyle = `rgba(255,255,255,${occluder.opacity})`
      context.fill()
      const blob = await new Promise<Blob>((resolve, reject) => canvas.toBlob((value) => value ? resolve(value) : reject(new Error('Could not encode occlusion mask')), 'image/png'))
      zip.file(`masks/${occluder.id}.png`, new Uint8Array(await blob.arrayBuffer()))
    }
  }
  return zip.generateAsync({ type: 'blob', compression: 'DEFLATE', compressionOptions: { level: 6 } })
}

export async function saveMoadMap(level: LevelDocument): Promise<void> {
  downloadBlob(`${level.id}.moadmap`, await buildMoadMap(level))
}

export async function openMoadMap(file: File): Promise<LevelDocument> {
  const zip = await JSZip.loadAsync(file)
  const sourceEntry = zip.file('level.source.json')
  if (!sourceEntry) throw new Error('Archive has no level.source.json')
  const level = parseLevelJson(await sourceEntry.async('text'))
  if (level.background) {
    const backgroundEntry = zip.file(`background/${level.background.fileName}`)
    if (backgroundEntry) {
      const bytes = await backgroundEntry.async('base64')
      const extension = /\.jpe?g$/i.test(level.background.fileName) ? 'jpeg' : 'png'
      level.background.dataUrl = `data:image/${extension};base64,${bytes}`
    }
  }
  return level
}
