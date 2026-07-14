import { readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import JSZip from 'jszip'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const demoRoot = path.join(root, 'public', 'demo')
const source = JSON.parse(await readFile(path.join(demoRoot, 'level.source.json'), 'utf8'))
const background = await readFile(path.join(demoRoot, source.background.fileName))
const laneByDepth = Object.fromEntries(source.depths.map((depth) => [depth.id, depth.runtimeId]))

const transitionType = {
  steps: 'stair',
  ladder: 'ladder',
  cliff: 'cliff',
  depth_walk: 'depth_walk',
  rope: 'rope',
  lift: 'lift',
}

const runtime = {
  id: source.id,
  version: 1,
  status: 'EDITOR_AUTHORED',
  background_file: `background/${source.background.fileName}`,
  runtime_size: [source.background.width, source.background.height],
  world_metrics: {
    pixels_per_meter: source.pixelsPerMeter,
    route_to_art_offset: [0, 0],
    reference: 'Editor-authored screen support and independent physical height in metres',
  },
  lanes: source.depths.map((depth, index) => ({ id: depth.runtimeId, lane: index, scale: depth.scale, z_index: depth.zIndex, collision_layer: 2 ** (index + 1) })),
  spawn: {
    lane: laneByDepth[source.spawns[0].depth],
    position: [source.spawns[0].x, source.spawns[0].y],
    height_m: source.spawns[0].heightMeters,
  },
  collision_surfaces: source.supports.map((support) => ({
    id: support.id,
    lane: laneByDepth[support.depth],
    height_points_m: support.points.map((point) => point.heightMeters),
    traversal_mode: support.traversal,
    speed_multiplier: support.speedMultiplier,
    points: support.points.map((point) => [point.x, point.y]),
    placeholder: support.placeholder,
  })),
  transitions: source.transitions.map((transition) => ({
    id: transition.id,
    from_support_id: transition.fromSupportId,
    to_support_id: transition.toSupportId,
    from_lane: laneByDepth[transition.fromDepth],
    to_lane: laneByDepth[transition.toDepth],
    from_height_m: transition.fromHeight,
    to_height_m: transition.toHeight,
    transition_type: transitionType[transition.type],
    duration: transition.duration,
    path_points: transition.path.map((point) => [point.x, point.y]),
    placeholder: transition.placeholder,
  })),
  enemy_spawns: source.enemies.map((enemy) => ({ id: enemy.id, archetype: enemy.archetype, lane: laneByDepth[enemy.depth], height_m: enemy.heightMeters, position: [enemy.x, enemy.y], patrol: [enemy.patrolLeft, enemy.patrolRight] })),
  event_triggers: source.events.map((event) => ({ id: event.id, event_id: event.eventId, lane: laneByDepth[event.depth], height_min_m: event.heightMin, height_max_m: event.heightMax, polygon: event.points.map((point) => [point.x, point.y]), activation: event.activation, mode: event.mode, enabled: event.enabled })),
  hazards: source.hazards,
  editor_obstacles: source.obstacles,
  occlusion_masks: source.occluders.map((occluder) => ({ id: occluder.id, lane: laneByDepth[occluder.depth], height_min_m: occluder.heightMin, height_max_m: occluder.heightMax, polygon: occluder.points.map((point) => [point.x, point.y]) })),
  editor: { name: 'MoAD Visual Level Editor', schema_version: 1, height_model: 'numeric_metres', depth_model: 'fixed_per_support' },
}

const zip = new JSZip()
zip.file('manifest.json', JSON.stringify({ format: 'moadmap', version: 1, source: 'level.source.json', runtime: 'level.runtime.json' }, null, 2))
zip.file('level.source.json', JSON.stringify(source, null, 2))
zip.file('level.runtime.json', JSON.stringify(runtime, null, 2))
zip.file(`background/${source.background.fileName}`, background)
const archive = await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE', compressionOptions: { level: 6 } })
await writeFile(path.join(root, 'public', 'semi-abandoned-tomb.moadmap'), archive)
console.log(`Built semi-abandoned-tomb.moadmap (${archive.length} bytes)`)
