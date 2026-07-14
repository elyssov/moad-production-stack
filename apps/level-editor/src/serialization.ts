import { createLevel, depthByRuntimeId, type DepthId, type LevelDocument, type SupportPoint, type TransitionType, type WorldPoint } from './model'

type JsonObject = Record<string, unknown>

const points = (value: unknown): WorldPoint[] => Array.isArray(value)
  ? value.filter(Array.isArray).map((point) => ({ x: Number(point[0]), y: Number(point[1]) }))
  : []

const runtimeDepth = (level: LevelDocument, depth: DepthId) => level.depths.find((item) => item.id === depth)?.runtimeId ?? 'middle_main'

export function parseLevelJson(text: string): LevelDocument {
  const value = JSON.parse(text) as JsonObject
  if (value.format === 'moad-level-source') {
    const source = value as unknown as LevelDocument
    source.occluders ??= []
    return source
  }

  const level = createLevel()
  level.runtimeBase = value
  level.id = String(value.id ?? 'imported_level')
  level.title = String(value.title ?? level.id)
  const runtimeSize = Array.isArray(value.runtime_size) ? value.runtime_size : [3840, 720]
  const metrics = (value.world_metrics ?? {}) as JsonObject
  level.pixelsPerMeter = Number(metrics.pixels_per_meter ?? 80)
  const backgroundFile = String(value.background_file ?? '')
  level.background = backgroundFile ? { fileName: backgroundFile, dataUrl: '', width: Number(runtimeSize[0]), height: Number(runtimeSize[1]) } : null

  if (Array.isArray(value.lanes)) {
    for (const laneValue of value.lanes as JsonObject[]) {
      const laneNumber = Number(laneValue.lane)
      const depth = level.depths.find((item) => item.id === (laneNumber === 0 ? 'near' : laneNumber === 2 ? 'far' : 'middle'))
      if (!depth) continue
      depth.runtimeId = String(laneValue.id ?? depth.runtimeId)
      depth.scale = Number(laneValue.scale ?? depth.scale)
      depth.zIndex = Number(laneValue.z_index ?? depth.zIndex)
    }
  }

  if (Array.isArray(value.collision_surfaces)) {
    level.supports = (value.collision_surfaces as JsonObject[]).map((surface) => {
      const route = points(surface.points)
      const heights = Array.isArray(surface.height_points_m) ? surface.height_points_m.map(Number) : []
      const fallbackHeight = Number(surface.height_m ?? Number(surface.elevation_rank ?? 0) * 2.5)
      return {
        id: String(surface.id),
        depth: depthByRuntimeId(level, String(surface.lane)),
        traversal: String(surface.traversal_mode ?? 'stable') as 'stable',
        speedMultiplier: Number(surface.speed_multiplier ?? 1),
        points: route.map((point, index): SupportPoint => ({ ...point, heightMeters: heights[index] ?? fallbackHeight })),
        placeholder: Boolean(surface.placeholder),
      }
    })
  }

  if (Array.isArray(value.transitions)) {
    level.transitions = (value.transitions as JsonObject[]).map((transition) => {
      const rawType = String(transition.transition_type ?? 'depth_walk')
      return {
        id: String(transition.id),
        fromSupportId: transition.from_support_id ? String(transition.from_support_id) : undefined,
        toSupportId: transition.to_support_id ? String(transition.to_support_id) : undefined,
        fromDepth: depthByRuntimeId(level, String(transition.from_lane)),
        toDepth: depthByRuntimeId(level, String(transition.to_lane)),
        fromHeight: Number(transition.from_height_m ?? 0),
        toHeight: Number(transition.to_height_m ?? 0),
        type: (rawType === 'stair' || rawType === 'stairs' ? 'steps' : rawType) as TransitionType,
        bidirectional: Boolean(transition.bidirectional ?? false),
        duration: Number(transition.duration ?? 1.25),
        path: points(transition.path_points),
        triggerRadius: Number(transition.trigger_radius ?? 70),
        placeholder: Boolean(transition.placeholder),
      }
    })
  }

  if (Array.isArray(value.event_triggers)) {
    level.events = (value.event_triggers as JsonObject[]).map((event) => ({
      id: String(event.id), eventId: String(event.event_id ?? event.id),
      depth: depthByRuntimeId(level, String(event.lane)),
      heightMin: Number(event.height_min_m ?? 0), heightMax: Number(event.height_max_m ?? 99),
      points: points(event.polygon), activation: String(event.activation ?? 'enter') as 'enter',
      mode: String(event.mode ?? 'once') as 'once', enabled: Boolean(event.enabled ?? true),
    }))
  }

  if (Array.isArray(value.hazards)) {
    level.hazards = (value.hazards as JsonObject[]).map((hazard) => ({
      id: String(hazard.id), depth: depthByRuntimeId(level, String(hazard.lane)),
      heightMin: Number(hazard.height_min_m ?? 0), heightMax: Number(hazard.height_max_m ?? 99),
      points: points(hazard.polygon), hazardType: String(hazard.hazard_type ?? 'pit') as 'pit',
      damage: Number(hazard.damage ?? 999), respawnId: String(hazard.respawn_id ?? 'player_spawn'),
    }))
  }

  if (Array.isArray(value.editor_obstacles)) {
    level.obstacles = (value.editor_obstacles as JsonObject[]).map((obstacle) => ({
      id: String(obstacle.id), depth: depthByRuntimeId(level, String(obstacle.lane)),
      heightMin: Number(obstacle.height_min_m ?? 0), heightMax: Number(obstacle.height_max_m ?? 99),
      points: points(obstacle.polygon), material: String(obstacle.material ?? 'stone') as 'stone',
      blocksMovement: Boolean(obstacle.blocks_movement ?? true), blocksBallistics: Boolean(obstacle.blocks_ballistics ?? true),
      blocksMagic: Boolean(obstacle.blocks_magic ?? true), blocksThrown: Boolean(obstacle.blocks_thrown ?? true),
      coverLevel: Number(obstacle.cover_level ?? 0) as 0,
    }))
  }

  if (Array.isArray(value.editor_occluders)) {
    level.occluders = (value.editor_occluders as JsonObject[]).map((occluder) => ({
      id: String(occluder.id), depth: depthByRuntimeId(level, String(occluder.lane)),
      heightMin: Number(occluder.height_min_m ?? 0), heightMax: Number(occluder.height_max_m ?? 99),
      points: points(occluder.polygon), opacity: Number(occluder.opacity ?? 1),
    }))
  }

  if (Array.isArray(value.enemy_spawns)) {
    level.enemies = (value.enemy_spawns as JsonObject[]).map((enemy) => {
      const position = points([enemy.position])[0] ?? { x: 0, y: 0 }
      return {
        id: String(enemy.node), archetype: String(enemy.archetype ?? enemy.display_name ?? 'placeholder_enemy'),
        depth: depthByRuntimeId(level, String(enemy.lane)), heightMeters: Number(enemy.height_m ?? 0), ...position,
        patrolLeft: Number(enemy.patrol_left ?? position.x - 100), patrolRight: Number(enemy.patrol_right ?? position.x + 100),
        placeholder: Boolean(enemy.placeholder),
      }
    })
  }

  if (value.spawn && typeof value.spawn === 'object') {
    const spawn = value.spawn as JsonObject
    const position = points([spawn.position])[0] ?? { x: 100, y: 600 }
    level.spawns.push({ id: 'player_spawn', kind: 'player', depth: depthByRuntimeId(level, String(spawn.lane)), heightMeters: Number(spawn.height_m ?? 0), ...position })
  }
  return level
}

export function runtimeContract(level: LevelDocument): JsonObject {
  const background = level.background
  const playerSpawn = level.spawns.find((spawn) => spawn.kind === 'player') ?? { x: 100, y: (background?.height ?? 720) - 100, depth: 'near' as DepthId, heightMeters: 0 }
  const objective = level.spawns.find((spawn) => spawn.kind === 'objective')
  const base = level.runtimeBase ?? {}
  return {
    ...base,
    id: level.id,
    version: Number(base.version ?? 1),
    status: 'EDITOR_AUTHORED',
    background_file: background?.fileName ?? 'background.png',
    runtime_size: [background?.width ?? 3840, background?.height ?? 720],
    world_metrics: {
      pixels_per_meter: level.pixelsPerMeter,
      route_to_art_offset: [0, 0],
      reference: 'Editor-authored screen support and independent physical height in metres',
    },
    lanes: level.depths.map((depth, index) => ({ id: depth.runtimeId, lane: index, scale: depth.scale, z_index: depth.zIndex, collision_layer: 2 ** (index + 1) })),
    spawn: { lane: runtimeDepth(level, playerSpawn.depth), position: [playerSpawn.x, playerSpawn.y], height_m: playerSpawn.heightMeters },
    objective: objective
      ? { id: objective.id, lane: runtimeDepth(level, objective.depth), position: [objective.x, objective.y], pickup_half_extents: [65, 65] }
      : (base.objective ?? { id: 'objective', lane: runtimeDepth(level, 'far'), position: [background?.width ?? 3600, 180], pickup_half_extents: [65, 65] }),
    collision_surfaces: level.supports.map((support) => ({
      id: support.id, lane: runtimeDepth(level, support.depth),
      elevation_rank: Math.round(Math.min(...support.points.map((point) => point.heightMeters))),
      height_points_m: support.points.map((point) => point.heightMeters),
      traversal_mode: support.traversal, speed_multiplier: support.speedMultiplier,
      points: support.points.map((point) => [point.x, point.y]), placeholder: support.placeholder,
      visible_art_contract: 'Authored directly over the background in MoAD Level Editor',
    })),
    transitions: level.transitions.flatMap((transition) => transition.bidirectional ? [transition, {
      ...transition, id: `${transition.id}_reverse`, fromDepth: transition.toDepth, toDepth: transition.fromDepth,
      fromSupportId: transition.toSupportId, toSupportId: transition.fromSupportId,
      fromHeight: transition.toHeight, toHeight: transition.fromHeight, path: [...transition.path].reverse(), bidirectional: false,
    }] : [transition]).map((transition) => ({
      id: transition.id, from_lane: runtimeDepth(level, transition.fromDepth), to_lane: runtimeDepth(level, transition.toDepth),
      from_support_id: transition.fromSupportId, to_support_id: transition.toSupportId,
      from_height_m: transition.fromHeight, to_height_m: transition.toHeight,
      transition_type: transition.type === 'stairs' || transition.type === 'steps' ? 'stair' : transition.type,
      transition_family: transition.fromDepth === transition.toDepth ? 'vertical' : 'depth',
      transition_direction: transition.fromDepth === transition.toDepth ? 'vertical' : 'in',
      height_direction: transition.toHeight > transition.fromHeight ? 'up' : transition.toHeight < transition.fromHeight ? 'down' : 'level',
      animation_clip: `${transition.type === 'steps' ? 'stairs' : transition.type}_candidate`, duration: transition.duration, lane_handoff_t: 0.5,
      trigger_polygon: triggerPolygon(transition.path[0], transition.triggerRadius),
      path_points: transition.path.map((point) => [point.x, point.y]), placeholder: transition.placeholder,
      visible_art_contract: 'Authored directly over the background in MoAD Level Editor',
    })),
    enemy_spawns: level.enemies.map((enemy) => ({
      node: enemy.id, display_name: enemy.archetype, archetype: enemy.archetype,
      lane: runtimeDepth(level, enemy.depth), position: [enemy.x, enemy.y], height_m: enemy.heightMeters,
      patrol_left: enemy.patrolLeft, patrol_right: enemy.patrolRight, cover_level: 0,
      motion_class: enemy.archetype.includes('mummy') ? 'hobble' : 'walk', patrol_speed: 35,
      max_hp: 10, armour_class: 0, visual_height_meters: 1.75, placeholder: enemy.placeholder,
    })),
    event_triggers: level.events.map((event) => ({
      id: event.id, event_id: event.eventId, lane: runtimeDepth(level, event.depth),
      height_min_m: event.heightMin, height_max_m: event.heightMax,
      polygon: event.points.map((point) => [point.x, point.y]), activation: event.activation, mode: event.mode, enabled: event.enabled,
    })),
    hazards: level.hazards.map((hazard) => ({
      id: hazard.id, lane: runtimeDepth(level, hazard.depth), height_min_m: hazard.heightMin, height_max_m: hazard.heightMax,
      polygon: hazard.points.map((point) => [point.x, point.y]), hazard_type: hazard.hazardType, damage: hazard.damage, respawn_id: hazard.respawnId,
    })),
    editor_obstacles: level.obstacles.map((obstacle) => ({
      id: obstacle.id, lane: runtimeDepth(level, obstacle.depth), height_min_m: obstacle.heightMin, height_max_m: obstacle.heightMax,
      polygon: obstacle.points.map((point) => [point.x, point.y]), material: obstacle.material,
      blocks_movement: obstacle.blocksMovement, blocks_ballistics: obstacle.blocksBallistics,
      blocks_magic: obstacle.blocksMagic, blocks_thrown: obstacle.blocksThrown, cover_level: obstacle.coverLevel,
    })),
    editor_occluders: level.occluders.map((occluder) => ({
      id: occluder.id, lane: runtimeDepth(level, occluder.depth), height_min_m: occluder.heightMin, height_max_m: occluder.heightMax,
      polygon: occluder.points.map((point) => [point.x, point.y]), opacity: occluder.opacity,
    })),
    occlusion_masks: level.occluders.map((occluder) => ({ id: occluder.id, lane: runtimeDepth(level, occluder.depth), file: `masks/${occluder.id}.png`, opacity: occluder.opacity })),
    editor: { name: 'MoAD Level Editor', schema_version: level.schemaVersion, height_model: 'numeric_metres', depth_model: 'fixed_per_support' },
  }
}

function triggerPolygon(point: WorldPoint | undefined, radius: number): number[][] {
  const center = point ?? { x: 0, y: 0 }
  return [[center.x - radius, center.y - radius], [center.x + radius, center.y - radius], [center.x + radius, center.y + radius], [center.x - radius, center.y + radius]]
}

export function downloadJson(fileName: string, value: unknown): void {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(url)
}
