export type DepthId = 'near' | 'middle' | 'far'
export type ToolId = 'select' | 'pan' | 'support' | 'transition' | 'event' | 'hazard' | 'obstacle' | 'occlusion' | 'enemy' | 'spawn'
export type TransitionType = 'steps' | 'ladder' | 'cliff' | 'depth_walk' | 'rope' | 'lift' | 'stairs'

export interface WorldPoint {
  x: number
  y: number
}

export interface SupportPoint extends WorldPoint {
  heightMeters: number
}

export interface DepthTrack {
  id: DepthId
  runtimeId: string
  label: string
  color: string
  scale: number
  zIndex: number
}

export interface SupportTrack {
  id: string
  depth: DepthId
  traversal: 'stable' | 'unstable_bridge' | 'slippery' | 'crawl'
  speedMultiplier: number
  points: SupportPoint[]
  placeholder: boolean
}

export interface TransitionPath {
  id: string
  fromSupportId?: string
  toSupportId?: string
  fromDepth: DepthId
  toDepth: DepthId
  fromHeight: number
  toHeight: number
  type: TransitionType
  bidirectional: boolean
  duration: number
  path: WorldPoint[]
  triggerRadius: number
  placeholder: boolean
}

export interface AreaShape {
  id: string
  depth: DepthId
  heightMin: number
  heightMax: number
  points: WorldPoint[]
}

export interface EventArea extends AreaShape {
  eventId: string
  activation: 'enter' | 'exit' | 'inside' | 'interact'
  mode: 'once' | 'repeat'
  enabled: boolean
}

export interface HazardArea extends AreaShape {
  hazardType: 'pit' | 'spikes' | 'fire' | 'crush' | 'scripted'
  damage: number
  respawnId: string
}

export interface ObstacleArea extends AreaShape {
  material: 'stone' | 'wood' | 'metal' | 'glass' | 'organic'
  blocksMovement: boolean
  blocksBallistics: boolean
  blocksMagic: boolean
  blocksThrown: boolean
  coverLevel: 0 | 1 | 2 | 3
}

export interface OcclusionArea extends AreaShape {
  opacity: number
}

export interface EnemyMarker extends WorldPoint {
  id: string
  archetype: string
  depth: DepthId
  heightMeters: number
  patrolLeft: number
  patrolRight: number
  placeholder: boolean
}

export interface SpawnMarker extends WorldPoint {
  id: string
  depth: DepthId
  heightMeters: number
  kind: 'player' | 'checkpoint' | 'objective' | 'camera'
}

export interface BackgroundAsset {
  fileName: string
  dataUrl: string
  width: number
  height: number
}

export interface LevelDocument {
  format: 'moad-level-source'
  schemaVersion: 1
  id: string
  title: string
  pixelsPerMeter: number
  background: BackgroundAsset | null
  depths: DepthTrack[]
  supports: SupportTrack[]
  transitions: TransitionPath[]
  events: EventArea[]
  hazards: HazardArea[]
  obstacles: ObstacleArea[]
  occluders: OcclusionArea[]
  enemies: EnemyMarker[]
  spawns: SpawnMarker[]
  runtimeBase?: Record<string, unknown>
}

export interface ValidationIssue {
  severity: 'error' | 'warning'
  objectId: string
  message: string
}

export const DEPTHS: DepthTrack[] = [
  { id: 'near', runtimeId: 'near_front', label: 'Near', color: '#f2c94c', scale: 1, zIndex: 2 },
  { id: 'middle', runtimeId: 'middle_main', label: 'Middle', color: '#42d17b', scale: 0.88, zIndex: 0 },
  { id: 'far', runtimeId: 'far_gallery', label: 'Far', color: '#48a7ff', scale: 0.76, zIndex: -2 },
]

export const COLORS = {
  event: '#ff3fb4',
  hazard: '#ef4444',
  obstacle: '#ff8a35',
  occlusion: '#b9a7d8',
  enemy: '#f8fafc',
  spawn: '#62e6ff',
}

export function createLevel(): LevelDocument {
  return {
    format: 'moad-level-source',
    schemaVersion: 1,
    id: 'new_level',
    title: 'Untitled chamber',
    pixelsPerMeter: 80,
    background: null,
    depths: structuredClone(DEPTHS),
    supports: [],
    transitions: [],
    events: [],
    hazards: [],
    obstacles: [],
    occluders: [],
    enemies: [],
    spawns: [],
  }
}

export function makeId(prefix: string, items: { id: string }[]): string {
  let index = items.length + 1
  let candidate = `${prefix}_${String(index).padStart(2, '0')}`
  while (items.some((item) => item.id === candidate)) {
    index++
    candidate = `${prefix}_${String(index).padStart(2, '0')}`
  }
  return candidate
}

export function segmentAngleDegrees(a: SupportPoint, b: SupportPoint, pixelsPerMeter: number): number {
  const horizontalMeters = Math.abs(b.x - a.x) / pixelsPerMeter
  const verticalMeters = Math.abs(b.heightMeters - a.heightMeters)
  if (horizontalMeters < 0.001) return verticalMeters > 0.001 ? 90 : 0
  return Math.atan2(verticalMeters, horizontalMeters) * 180 / Math.PI
}

export function validateLevel(level: LevelDocument): ValidationIssue[] {
  const issues: ValidationIssue[] = []
  if (!level.background) issues.push({ severity: 'error', objectId: 'level', message: 'Background is not loaded.' })
  if (level.supports.length === 0) issues.push({ severity: 'error', objectId: 'level', message: 'No support tracks authored.' })
  if (!level.spawns.some((spawn) => spawn.kind === 'player')) {
    issues.push({ severity: 'error', objectId: 'level', message: 'Player spawn is missing.' })
  }
  for (const support of level.supports) {
    if (support.points.length < 2) {
      issues.push({ severity: 'error', objectId: support.id, message: 'Support needs at least two points.' })
      continue
    }
    for (let index = 1; index < support.points.length; index++) {
      const angle = segmentAngleDegrees(support.points[index - 1], support.points[index], level.pixelsPerMeter)
      if (angle > 55.001) {
        issues.push({ severity: 'error', objectId: support.id, message: `Segment ${index} is ${angle.toFixed(1)} degrees. Use a 90 degree ladder or cliff transition.` })
      }
    }
    if (support.placeholder) issues.push({ severity: 'warning', objectId: support.id, message: 'Support is marked PLACEHOLDER.' })
  }
  for (const transition of level.transitions) {
    if (transition.path.length < 2) issues.push({ severity: 'error', objectId: transition.id, message: 'Transition needs at least two path points.' })
    const changesDepth = transition.fromDepth !== transition.toDepth
    const changesHeight = Math.abs(transition.toHeight - transition.fromHeight) > 0.01
    if (!changesDepth && !changesHeight) issues.push({ severity: 'error', objectId: transition.id, message: 'Transition changes neither depth nor height.' })
    if (!changesDepth && changesHeight && !['steps', 'stairs', 'ladder', 'cliff', 'rope', 'lift'].includes(transition.type)) {
      issues.push({ severity: 'error', objectId: transition.id, message: 'Vertical transition must use steps, a rung ladder, cliff, rope, or lift.' })
    }
    if (!transition.fromSupportId || !transition.toSupportId) issues.push({ severity: 'warning', objectId: transition.id, message: 'Transition endpoints are not bound to support tracks.' })
  }
  for (const area of [...level.events, ...level.hazards, ...level.obstacles, ...level.occluders]) {
    if (area.points.length < 3) issues.push({ severity: 'error', objectId: area.id, message: 'Area needs at least three points.' })
    if (area.heightMin > area.heightMax) issues.push({ severity: 'error', objectId: area.id, message: 'Minimum height exceeds maximum height.' })
  }
  for (const event of level.events) {
    if (!event.eventId.trim()) issues.push({ severity: 'error', objectId: event.id, message: 'Event number is empty.' })
  }
  for (const enemy of level.enemies) {
    if (enemy.patrolLeft > enemy.x || enemy.patrolRight < enemy.x) {
      issues.push({ severity: 'error', objectId: enemy.id, message: 'Enemy spawn must remain inside its patrol interval.' })
    }
    if (enemy.placeholder) issues.push({ severity: 'warning', objectId: enemy.id, message: 'Enemy is marked PLACEHOLDER.' })
  }
  return issues
}

export function depthByRuntimeId(level: LevelDocument, runtimeId: string): DepthId {
  return level.depths.find((depth) => depth.runtimeId === runtimeId)?.id ?? 'middle'
}
