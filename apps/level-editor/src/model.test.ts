import { readFileSync } from 'node:fs'
import { describe, expect, test } from 'vitest'
import { createLevel, segmentAngleDegrees, validateLevel } from './model'
import { parseLevelJson, runtimeContract } from './serialization'
import { buildMoadMap } from './package'
import JSZip from 'jszip'

describe('MoAD level authoring contract', () => {
  test('imports the current Papyrus Chamber without losing the three depths', () => {
    const source = readFileSync('src/fixtures/papyrus_chamber_runtime_v08.json', 'utf8')
    const level = parseLevelJson(source)
    expect(level.depths.map((depth) => depth.id)).toEqual(['near', 'middle', 'far'])
    expect(level.supports.length).toBeGreaterThanOrEqual(8)
    expect(level.transitions.length).toBeGreaterThanOrEqual(6)
    expect(level.enemies).toHaveLength(2)
  })

  test('ships a complete competition tomb proof', () => {
    const source = readFileSync('public/demo/level.source.json', 'utf8')
    const level = parseLevelJson(source)
    expect(level.supports.length).toBeGreaterThanOrEqual(9)
    expect(level.transitions.length).toBeGreaterThanOrEqual(6)
    expect(new Set(level.supports.map((support) => support.depth))).toEqual(new Set(['near', 'middle', 'far']))
    expect(level.enemies).toHaveLength(3)
    expect(validateLevel(level).filter((issue) => issue.severity === 'error')).toHaveLength(0)
  })

  test('stores screen support and numeric physical height independently', () => {
    const level = createLevel()
    level.background = { fileName: 'proof.png', dataUrl: '', width: 1920, height: 720 }
    level.spawns.push({ id: 'player_spawn', kind: 'player', depth: 'middle', heightMeters: 0, x: 100, y: 600 })
    level.supports.push({
      id: 'slope', depth: 'middle', traversal: 'stable', speedMultiplier: 1, placeholder: false,
      points: [{ x: 100, y: 610, heightMeters: 0 }, { x: 500, y: 370, heightMeters: 2.5 }],
    })
    const runtime = runtimeContract(level)
    const surfaces = runtime.collision_surfaces as Array<Record<string, unknown>>
    expect(surfaces[0].points).toEqual([[100, 610], [500, 370]])
    expect(surfaces[0].height_points_m).toEqual([0, 2.5])
    expect(validateLevel(level).filter((issue) => issue.severity === 'error')).toHaveLength(0)
  })

  test('rejects authored slopes above 55 degrees', () => {
    const angle = segmentAngleDegrees(
      { x: 0, y: 600, heightMeters: 0 },
      { x: 80, y: 300, heightMeters: 2 },
      80,
    )
    expect(angle).toBeCloseTo(63.435, 2)
    const level = createLevel()
    level.background = { fileName: 'proof.png', dataUrl: '', width: 800, height: 600 }
    level.spawns.push({ id: 'player_spawn', kind: 'player', depth: 'near', heightMeters: 0, x: 10, y: 500 })
    level.supports.push({ id: 'illegal', depth: 'near', traversal: 'stable', speedMultiplier: 1, placeholder: false, points: [{ x: 0, y: 500, heightMeters: 0 }, { x: 80, y: 300, heightMeters: 2 }] })
    expect(validateLevel(level).some((issue) => issue.message.includes('63.4 degrees'))).toBe(true)
  })

  test('exports event volumes with both depth and height interval', () => {
    const level = createLevel()
    level.events.push({ id: 'trigger_01', eventId: 'PAPYRUS_014', depth: 'far', heightMin: 2, heightMax: 4, points: [{ x: 1, y: 1 }, { x: 5, y: 1 }, { x: 5, y: 5 }], activation: 'enter', mode: 'once', enabled: true })
    const runtime = runtimeContract(level)
    expect(runtime.event_triggers).toEqual([expect.objectContaining({ event_id: 'PAPYRUS_014', lane: 'far_gallery', height_min_m: 2, height_max_m: 4 })])
  })

  test('exports bound step transitions and swaps anchors for the reverse path', () => {
    const level = createLevel()
    level.transitions.push({
      id: 'steps_01', fromSupportId: 'lower_track', toSupportId: 'upper_track',
      fromDepth: 'middle', toDepth: 'middle', fromHeight: 0, toHeight: 3, type: 'steps',
      bidirectional: true, duration: 1.5, triggerRadius: 65, placeholder: false,
      path: [{ x: 100, y: 500 }, { x: 180, y: 420 }, { x: 260, y: 300 }],
    })
    const transitions = runtimeContract(level).transitions as Array<Record<string, unknown>>
    expect(transitions[0]).toEqual(expect.objectContaining({ transition_type: 'stair', from_support_id: 'lower_track', to_support_id: 'upper_track' }))
    expect(transitions[1]).toEqual(expect.objectContaining({ from_support_id: 'upper_track', to_support_id: 'lower_track' }))
  })

  test('builds a reopenable moadmap archive with source and runtime contracts', async () => {
    const level = createLevel()
    level.id = 'archive_proof'
    const blob = await buildMoadMap(level)
    const zip = await JSZip.loadAsync(await blob.arrayBuffer())
    expect(Object.keys(zip.files)).toEqual(expect.arrayContaining(['manifest.json', 'level.source.json', 'level.runtime.json']))
    expect(JSON.parse(await zip.file('level.source.json')!.async('text')).id).toBe('archive_proof')
  })
})
