import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Circle, Group, Image as KonvaImage, Label, Layer, Line, Rect, Stage, Tag, Text } from 'react-konva'
import type Konva from 'konva'
import type { KonvaEventObject } from 'konva/lib/Node'
import { AlertTriangle, Box, ChevronDown, Crosshair, Download, Eye, Footprints, FolderOpen, Hand, Image as ImageIcon, MapPin, MousePointer2, Redo2, Save, Skull, Sparkles, Undo2, Upload, Waypoints } from 'lucide-react'
import './App.css'
import { COLORS, createLevel, DEPTHS, makeId, segmentAngleDegrees, validateLevel, type AreaShape, type DepthId, type EnemyMarker, type LevelDocument, type SupportPoint, type SupportTrack, type ToolId, type TransitionType, type WorldPoint } from './model'
import { downloadJson, parseLevelJson, runtimeContract } from './serialization'
import { openMoadMap, saveMoadMap } from './package'
import { isImageFile } from './fileTypes'
import { panWithArrow } from './viewport'
import { nearestSupportAnchor, type SupportAnchor } from './supportGeometry'

const TOOLBAR: { id: ToolId, label: string, icon: typeof MousePointer2 }[] = [
  { id: 'select', label: 'Select', icon: MousePointer2 }, { id: 'pan', label: 'Pan', icon: Hand },
  { id: 'support', label: 'Support track', icon: Footprints }, { id: 'transition', label: 'Transition', icon: Waypoints },
  { id: 'event', label: 'Event trigger', icon: Sparkles }, { id: 'hazard', label: 'Hazard', icon: Skull },
  { id: 'obstacle', label: 'Obstacle', icon: Box }, { id: 'enemy', label: 'Enemy', icon: Crosshair },
  { id: 'occlusion', label: 'Occlusion mask', icon: Eye },
  { id: 'spawn', label: 'Spawn / marker', icon: MapPin },
]

type DrawingTool = 'support' | 'transition' | 'event' | 'hazard' | 'obstacle' | 'occlusion'
type Draft = { tool: DrawingTool, points: WorldPoint[], heights: number[], startAnchor?: SupportAnchor } | null

function useLoadedImage(source: string | undefined): HTMLImageElement | undefined {
  const [image, setImage] = useState<HTMLImageElement>()
  useEffect(() => {
    if (!source) { setImage(undefined); return }
    const next = new window.Image(); next.onload = () => setImage(next); next.src = source
  }, [source])
  return image
}

const pointList = (points: WorldPoint[]) => points.flatMap((point) => [point.x, point.y])
const polygonCenter = (points: WorldPoint[]) => points.length ? ({ x: points.reduce((sum, point) => sum + point.x, 0) / points.length, y: points.reduce((sum, point) => sum + point.y, 0) / points.length }) : ({ x: 0, y: 0 })

function App() {
  const [level, setLevel] = useState<LevelDocument>(() => createLevel())
  const [tool, setTool] = useState<ToolId>('select')
  const [depth, setDepth] = useState<DepthId>('middle')
  const [height, setHeight] = useState(0)
  const [targetHeight, setTargetHeight] = useState(2.5)
  const [transitionType, setTransitionType] = useState<TransitionType>('steps')
  const [eventId, setEventId] = useState('EVENT_001')
  const [archetype, setArchetype] = useState('tomb_mummy_placeholder')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedPoint, setSelectedPoint] = useState<{ supportId: string, index: number } | null>(null)
  const [draft, setDraft] = useState<Draft>(null)
  const [editingSupportId, setEditingSupportId] = useState<string | null>(null)
  const [pointer, setPointer] = useState<WorldPoint | null>(null)
  const [view, setView] = useState({ x: 40, y: 40, scale: 0.33 })
  const [message, setMessage] = useState('Load a background or a .moadmap to begin.')
  const [showValidation, setShowValidation] = useState(false)
  const [history, setHistory] = useState<LevelDocument[]>([])
  const [future, setFuture] = useState<LevelDocument[]>([])
  const stageRef = useRef<Konva.Stage>(null)
  const backgroundInput = useRef<HTMLInputElement>(null)
  const mapInput = useRef<HTMLInputElement>(null)
  const jsonInput = useRef<HTMLInputElement>(null)
  const paintRef = useRef<{ tool: 'event' | 'hazard' | 'obstacle' | 'occlusion', points: WorldPoint[] } | null>(null)
  const suppressClick = useRef(false)
  const backgroundImage = useLoadedImage(level.background?.dataUrl)
  const issues = useMemo(() => validateLevel(level), [level])

  const commit = useCallback((next: LevelDocument, note?: string) => {
    setHistory((items) => [...items.slice(-49), level]); setFuture([]); setLevel(next); if (note) setMessage(note)
  }, [level])
  const mutate = useCallback((change: (next: LevelDocument) => void, note?: string) => {
    const next = structuredClone(level); change(next); commit(next, note)
  }, [commit, level])
  const undo = useCallback(() => {
    const previous = history.at(-1); if (!previous) return
    setHistory((items) => items.slice(0, -1)); setFuture((items) => [level, ...items].slice(0, 50)); setLevel(previous); setDraft(null)
  }, [history, level])
  const redo = useCallback(() => {
    const next = future[0]; if (!next) return
    setFuture((items) => items.slice(1)); setHistory((items) => [...items, level].slice(-50)); setLevel(next); setDraft(null)
  }, [future, level])

  const deleteSelected = useCallback(() => {
    if (!selectedId) return
    mutate((next) => {
      next.supports = next.supports.filter((item) => item.id !== selectedId); next.transitions = next.transitions.filter((item) => item.id !== selectedId)
      next.events = next.events.filter((item) => item.id !== selectedId); next.hazards = next.hazards.filter((item) => item.id !== selectedId)
      next.obstacles = next.obstacles.filter((item) => item.id !== selectedId); next.enemies = next.enemies.filter((item) => item.id !== selectedId)
      next.occluders = next.occluders.filter((item) => item.id !== selectedId)
      next.spawns = next.spawns.filter((item) => item.id !== selectedId)
    }, `${selectedId} deleted.`); setSelectedId(null); setSelectedPoint(null); setEditingSupportId(null); setDraft(null)
  }, [mutate, selectedId])

  const finishDraft = useCallback(() => {
    if (!draft) return
    if (draft.tool === 'transition') { setMessage('Finish the transition by clicking a different support track.'); return }
    const minimum = ['event', 'hazard', 'obstacle', 'occlusion'].includes(draft.tool) ? 3 : 2
    if (draft.points.length < minimum) { setMessage(`Need at least ${minimum} points.`); return }
    mutate((next) => {
      if (draft.tool === 'support') {
        const points = draft.points.map((point, index) => ({ ...point, heightMeters: draft.heights[index] }))
        const existing = editingSupportId ? next.supports.find((support) => support.id === editingSupportId) : undefined
        if (existing) Object.assign(existing, { depth, points })
        else next.supports.push({ id: makeId('support', next.supports), depth, traversal: 'stable', speedMultiplier: 1, points, placeholder: false })
      }
      if (draft.tool === 'event') next.events.push({ id: makeId('event_area', next.events), eventId, depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: draft.points, activation: 'enter', mode: 'once', enabled: true })
      if (draft.tool === 'hazard') next.hazards.push({ id: makeId('hazard', next.hazards), depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: draft.points, hazardType: 'pit', damage: 999, respawnId: 'player_spawn' })
      if (draft.tool === 'obstacle') next.obstacles.push({ id: makeId('obstacle', next.obstacles), depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: draft.points, material: 'stone', blocksMovement: true, blocksBallistics: true, blocksMagic: true, blocksThrown: true, coverLevel: 0 })
      if (draft.tool === 'occlusion') next.occluders.push({ id: makeId('occluder', next.occluders), depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: draft.points, opacity: 1 })
    }, editingSupportId ? `${editingSupportId} updated.` : `${draft.tool} authored.`); setDraft(null); setEditingSupportId(null)
  }, [depth, draft, editingSupportId, eventId, height, mutate, targetHeight])

  useEffect(() => {
    const keyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      const isEditing = target?.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target?.tagName ?? '')
      if (isEditing) return
      if (event.code === 'Space' && draft) { event.preventDefault(); finishDraft(); return }
      if (event.key.startsWith('Arrow')) {
        event.preventDefault()
        setView((current) => panWithArrow(current, event.key, event.shiftKey))
        return
      }
      if (event.key === 'Escape') { setDraft(null); setEditingSupportId(null); setMessage('Draft cancelled.'); return }
      if (event.key === 'Enter' && draft) { event.preventDefault(); finishDraft(); return }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') { event.preventDefault(); if (event.shiftKey) redo(); else undo(); return }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') { event.preventDefault(); redo(); return }
      if (event.key === 'Delete' && selectedId) deleteSelected()
    }
    window.addEventListener('keydown', keyDown); return () => window.removeEventListener('keydown', keyDown)
  }, [deleteSelected, draft, finishDraft, redo, selectedId, undo])

  const loadBackground = async (file: File) => {
    try {
      if (!isImageFile(file)) throw new Error('Choose a PNG or JPEG image.')
      setMessage(`Opening background ${file.name}...`)
      const dataUrl = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader()
        reader.onerror = () => reject(reader.error ?? new Error('The image file could not be read.'))
        reader.onload = () => resolve(String(reader.result))
        reader.readAsDataURL(file)
      })
      const image = await new Promise<HTMLImageElement>((resolve, reject) => {
        const candidate = new window.Image()
        candidate.onerror = () => reject(new Error('The browser could not decode this image.'))
        candidate.onload = () => resolve(candidate)
        candidate.src = dataUrl
      })
      if (!image.naturalWidth || !image.naturalHeight) throw new Error('The image has invalid dimensions.')
      mutate((next) => { next.background = { fileName: file.name, dataUrl, width: image.naturalWidth, height: image.naturalHeight } }, `Background loaded: ${image.naturalWidth} x ${image.naturalHeight}.`)
      setView({ x: 24, y: 24, scale: Math.min(1, Math.max(0.08, (window.innerWidth - 410) / image.naturalWidth)) })
    } catch (error) {
      setMessage(`IMAGE OPEN FAILED: ${error instanceof Error ? error.message : String(error)}`)
    }
  }
  const loadMap = async (file: File) => {
    try {
      const loaded = file.name.endsWith('.moadmap') ? await openMoadMap(file) : parseLevelJson(await file.text())
      setHistory([]); setFuture([]); setLevel(loaded); setDraft(null); setEditingSupportId(null); setSelectedId(null); setSelectedPoint(null)
      setMessage(`Opened ${file.name}. ${validateLevel(loaded).length} validation notices.`)
      setView({ x: 24, y: 24, scale: Math.min(1, Math.max(0.08, (window.innerWidth - 410) / (loaded.background?.width ?? 3840))) })
    } catch (error) { setMessage(`OPEN FAILED: ${error instanceof Error ? error.message : String(error)}`) }
  }
  const loadDemoMap = async () => {
    try {
      setMessage('Opening the semi-abandoned tomb proof map...')
      const response = await fetch('semi-abandoned-tomb.moadmap')
      if (!response.ok) throw new Error(`Demo map request failed (${response.status}).`)
      const file = new File([await response.blob()], 'semi-abandoned-tomb.moadmap', { type: 'application/zip' })
      await loadMap(file)
    } catch (error) {
      setMessage(`DEMO OPEN FAILED: ${error instanceof Error ? error.message : String(error)}`)
    }
  }
  const openFile = async (file: File) => {
    if (isImageFile(file)) await loadBackground(file)
    else await loadMap(file)
  }
  const worldPointer = (): WorldPoint | null => {
    const raw = stageRef.current?.getPointerPosition(); if (!raw) return null
    return { x: (raw.x - view.x) / view.scale, y: (raw.y - view.y) / view.scale }
  }
  const handleStageClick = (event: KonvaEventObject<MouseEvent>) => {
    if (suppressClick.current) { suppressClick.current = false; return }
    if (tool === 'select' || tool === 'pan' || event.evt.button !== 0) return
    const point = worldPointer(); if (!point) return
    if (tool === 'enemy') { mutate((next) => next.enemies.push({ id: makeId('enemy', next.enemies), archetype, depth, heightMeters: height, x: point.x, y: point.y, patrolLeft: point.x - 120, patrolRight: point.x + 120, placeholder: true }), 'Enemy placed. Drag the patrol handles.'); return }
    if (tool === 'spawn') {
      mutate((next) => { const existing = next.spawns.find((spawn) => spawn.kind === 'player'); if (existing) Object.assign(existing, { ...point, depth, heightMeters: height }); else next.spawns.push({ id: 'player_spawn', kind: 'player', depth, heightMeters: height, ...point }) }, 'Player spawn placed.'); return
    }
    if (tool === 'transition') {
      if (!draft || draft.tool !== 'transition') { setMessage('A transition must start on a support track.'); return }
      setDraft({ ...draft, points: [...draft.points, point], heights: [...draft.heights, draft.heights.at(-1) ?? 0] })
      return
    }
    if (!['support', 'transition', 'event', 'hazard', 'obstacle', 'occlusion'].includes(tool)) return
    const drawingTool = tool as DrawingTool
    if (!draft || draft.tool !== drawingTool) { setDraft({ tool: drawingTool, points: [point], heights: [height] }); return }
    if (drawingTool === 'support' && draft.points.length) {
      const previous = { ...draft.points.at(-1)!, heightMeters: draft.heights.at(-1)! }; const candidate: SupportPoint = { ...point, heightMeters: height }
      const angle = segmentAngleDegrees(previous, candidate, level.pixelsPerMeter)
      if (angle > 55) {
        setDraft(null)
        mutate((next) => {
          if (draft.points.length > 1) {
            const points = draft.points.map((item, index) => ({ ...item, heightMeters: draft.heights[index] }))
            const existing = editingSupportId ? next.supports.find((support) => support.id === editingSupportId) : undefined
            if (existing) Object.assign(existing, { depth, points })
            else next.supports.push({ id: makeId('support', next.supports), depth, traversal: 'stable', speedMultiplier: 1, points, placeholder: false })
          }
          next.transitions.push({ id: makeId('vertical', next.transitions), fromDepth: depth, toDepth: depth, fromHeight: previous.heightMeters, toHeight: height, type: transitionType === 'ladder' ? 'ladder' : 'cliff', bidirectional: true, duration: 1.4, path: [{ x: previous.x, y: previous.y }, { x: previous.x, y: point.y }], triggerRadius: 60, placeholder: false })
        }, `Slope ${angle.toFixed(1)} degrees snapped to a 90 degree ${transitionType === 'ladder' ? 'ladder' : 'cliff'} transition.`); setEditingSupportId(null); return
      }
    }
    setDraft({ ...draft, points: [...draft.points, point], heights: [...draft.heights, drawingTool === 'transition' ? targetHeight : height] })
  }
  const handlePointerMove = (event: KonvaEventObject<MouseEvent>) => {
    const point = worldPointer(); setPointer(point)
    const painting = paintRef.current
    if (!point || !painting || event.evt.buttons !== 1) return
    const previous = painting.points.at(-1)!
    if (Math.hypot(point.x - previous.x, point.y - previous.y) < 12 / view.scale) return
    painting.points.push(point)
    setDraft({ tool: painting.tool, points: [...painting.points], heights: painting.points.map(() => height) })
  }
  const handlePaintStart = (event: KonvaEventObject<MouseEvent>) => {
    if (event.evt.button !== 0 || draft || !['event', 'hazard', 'obstacle', 'occlusion'].includes(tool)) return
    const point = worldPointer(); if (!point) return
    paintRef.current = { tool: tool as 'event' | 'hazard' | 'obstacle' | 'occlusion', points: [point] }
    setDraft({ tool: tool as DrawingTool, points: [point], heights: [height] })
  }
  const handlePaintEnd = () => {
    const painting = paintRef.current; if (!painting) return
    paintRef.current = null; suppressClick.current = true
    if (painting.points.length < 3) return
    mutate((next) => {
      if (painting.tool === 'event') next.events.push({ id: makeId('event_area', next.events), eventId, depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: painting.points, activation: 'enter', mode: 'once', enabled: true })
      if (painting.tool === 'hazard') next.hazards.push({ id: makeId('hazard', next.hazards), depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: painting.points, hazardType: 'pit', damage: 999, respawnId: 'player_spawn' })
      if (painting.tool === 'obstacle') next.obstacles.push({ id: makeId('obstacle', next.obstacles), depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: painting.points, material: 'stone', blocksMovement: true, blocksBallistics: true, blocksMagic: true, blocksThrown: true, coverLevel: 0 })
      if (painting.tool === 'occlusion') next.occluders.push({ id: makeId('occluder', next.occluders), depth, heightMin: Math.min(height, targetHeight), heightMax: Math.max(height, targetHeight), points: painting.points, opacity: 1 })
    }, `${painting.tool} painted.`)
    setDraft(null)
  }
  const updateEnemy = (id: string, change: Partial<EnemyMarker>) => mutate((next) => Object.assign(next.enemies.find((enemy) => enemy.id === id)!, change))
  const updateSupportPoint = (supportId: string, index: number, change: Partial<SupportPoint>) => mutate((next) => Object.assign(next.supports.find((support) => support.id === supportId)!.points[index], change))
  const resumeSupport = (support: SupportTrack) => {
    const pointer = worldPointer()
    const first = support.points[0]; const last = support.points.at(-1)
    const reverse = pointer && first && last && Math.hypot(pointer.x - first.x, pointer.y - first.y) < Math.hypot(pointer.x - last.x, pointer.y - last.y)
    const points = (reverse ? [...support.points].reverse() : support.points).map(({ x, y }) => ({ x, y }))
    const heights = (reverse ? [...support.points].reverse() : support.points).map((point) => point.heightMeters)
    setDepth(support.depth); setHeight(heights.at(-1) ?? 0); setSelectedId(support.id); setSelectedPoint(null)
    setEditingSupportId(support.id); setDraft({ tool: 'support', points, heights }); setMessage(`${support.id} reopened. Space saves; Esc keeps the previous version.`)
  }
  const anchorTransitionToSupport = (support: SupportTrack) => {
    const pointer = worldPointer(); if (!pointer) return
    const anchor = nearestSupportAnchor(support, pointer)
    if (!draft || draft.tool !== 'transition' || !draft.startAnchor) {
      setDepth(anchor.depth); setHeight(anchor.heightMeters); setSelectedId(support.id); setSelectedPoint(null)
      setDraft({ tool: 'transition', points: [anchor.point], heights: [anchor.heightMeters], startAnchor: anchor })
      setMessage(`Transition starts on ${support.id}. Add bends, then click a different support track.`)
      return
    }
    if (draft.startAnchor.supportId === support.id) { setMessage('A transition must end on a different support track.'); return }
    const last = draft.points.at(-1)
    const path = last && Math.hypot(last.x - anchor.point.x, last.y - anchor.point.y) < 0.5 ? draft.points : [...draft.points, anchor.point]
    const start = draft.startAnchor
    mutate((next) => next.transitions.push({
      id: makeId('transition', next.transitions), fromSupportId: start.supportId, toSupportId: anchor.supportId,
      fromDepth: start.depth, toDepth: anchor.depth, fromHeight: start.heightMeters, toHeight: anchor.heightMeters,
      type: transitionType, bidirectional: true, duration: 1.5, path, triggerRadius: 65, placeholder: false,
    }), `Transition bound: ${start.supportId} -> ${anchor.supportId}.`)
    setDraft(null); setSelectedId(null)
  }
  const handleWheel = (event: KonvaEventObject<WheelEvent>) => {
    event.evt.preventDefault(); const mouse = stageRef.current?.getPointerPosition(); if (!mouse) return
    const oldScale = view.scale; const scale = Math.max(0.08, Math.min(3, oldScale * (event.evt.deltaY > 0 ? 0.88 : 1.12)))
    const world = { x: (mouse.x - view.x) / oldScale, y: (mouse.y - view.y) / oldScale }
    setView({ scale, x: mouse.x - world.x * scale, y: mouse.y - world.y * scale })
  }

  const selectedObject = [...level.supports, ...level.transitions, ...level.events, ...level.hazards, ...level.obstacles, ...level.occluders, ...level.enemies, ...level.spawns].find((item) => item.id === selectedId)
  const selectedArea = selectedObject && 'points' in selectedObject && 'depth' in selectedObject && 'heightMin' in selectedObject && 'heightMax' in selectedObject
    ? selectedObject as AreaShape
    : undefined
  const selectedSupportPoint = selectedPoint ? level.supports.find((support) => support.id === selectedPoint.supportId)?.points[selectedPoint.index] : undefined
  const currentDepth = level.depths.find((item) => item.id === depth) ?? DEPTHS[1]
  const previewPoints = draft && pointer ? [...draft.points, pointer] : draft?.points ?? []
  const canvasWidth = Math.max(600, window.innerWidth - 378); const canvasHeight = Math.max(500, window.innerHeight - 58)

  return <div className="app-shell">
    <header className="topbar"><div className="brand"><span className="brand-mark">M</span><div><strong>MoAD Level Editor</strong><small>{level.title}</small></div></div><div className="file-actions">
      <button className="command" onClick={() => mapInput.current?.click()}><FolderOpen size={17}/> Open</button><button className="command" onClick={() => void loadDemoMap()}><Sparkles size={17}/> Demo tomb</button><button className="icon-button" onClick={() => jsonInput.current?.click()} title="Import runtime JSON"><Upload size={18}/></button><button className="command" onClick={() => backgroundInput.current?.click()}><ImageIcon size={17}/> Set image</button><span className="divider"/>
      <button className="icon-button" disabled={!history.length} onClick={undo} title="Undo"><Undo2 size={18}/></button><button className="icon-button" disabled={!future.length} onClick={redo} title="Redo"><Redo2 size={18}/></button><span className="divider"/>
      <button className="command primary" onClick={() => void saveMoadMap(level)}><Save size={17}/> Save map</button><button className="command" onClick={() => downloadJson(`${level.id}.runtime.json`, runtimeContract(level))}><Download size={17}/> Runtime JSON</button>
    </div><input ref={backgroundInput} hidden type="file" accept=".png,.jpg,.jpeg,image/png,image/jpeg" onChange={(event) => { const file = event.target.files?.[0]; event.target.value = ''; if (file) void loadBackground(file) }}/><input ref={mapInput} hidden type="file" accept=".moadmap,.json,.png,.jpg,.jpeg,image/png,image/jpeg" onChange={(event) => { const file = event.target.files?.[0]; event.target.value = ''; if (file) void openFile(file) }}/><input ref={jsonInput} hidden type="file" accept=".json" onChange={(event) => { const file = event.target.files?.[0]; event.target.value = ''; if (file) void loadMap(file) }}/></header>
    <main className="workspace"><aside className="toolrail">{TOOLBAR.map(({ id, label, icon: Icon }) => <button key={id} className={`tool ${tool === id ? 'active' : ''}`} onClick={() => { setTool(id); setDraft(null); setEditingSupportId(null) }} title={label}><Icon size={20}/><span>{label}</span></button>)}</aside>
      <section className="canvas-wrap"><div className="canvas-status"><span>{tool.toUpperCase()}</span><span>{currentDepth.label} / H {height.toFixed(2)} m</span><span>{Math.round(view.scale * 100)}%</span></div>
        <Stage ref={stageRef} width={canvasWidth} height={canvasHeight} onWheel={handleWheel} onClick={handleStageClick} onDblClick={finishDraft} onMouseDown={handlePaintStart} onMouseMove={handlePointerMove} onMouseUp={handlePaintEnd}>
          <Layer draggable={tool === 'pan'} x={view.x} y={view.y} scaleX={view.scale} scaleY={view.scale} onDragEnd={(event) => setView((current) => ({ ...current, x: event.target.x(), y: event.target.y() }))}>
            {level.background && (backgroundImage
              ? <KonvaImage image={backgroundImage} width={level.background.width} height={level.background.height}/>
              : <Rect width={level.background.width} height={level.background.height} fill="#171715"/>)}
            {level.supports.map((support) => <Group key={support.id} onClick={(event) => { event.cancelBubble = true; if (tool === 'transition') anchorTransitionToSupport(support); else if (tool === 'support' && !draft) resumeSupport(support); else setSelectedId(support.id) }}><Line points={pointList(support.points)} stroke={level.depths.find((item) => item.id === support.depth)?.color} strokeWidth={selectedId === support.id ? 10 : 6} hitStrokeWidth={24} lineCap="round" lineJoin="round" shadowColor="#000" shadowBlur={3}/>{support.points.map((point, index) => <Group key={index} x={point.x} y={point.y} draggable={tool !== 'support' && tool !== 'transition'} onClick={(event) => { event.cancelBubble = true; if (tool === 'transition') anchorTransitionToSupport(support); else if (tool === 'support' && !draft) resumeSupport(support); else { setSelectedId(support.id); setSelectedPoint({ supportId: support.id, index }) } }} onDragEnd={(event) => updateSupportPoint(support.id, index, { x: event.target.x(), y: event.target.y() })}><Circle radius={selectedPoint?.supportId === support.id && selectedPoint.index === index ? 10 : selectedId === support.id ? 8 : 5} fill="#101315" stroke="#fff" strokeWidth={2}/><Label x={8} y={-27}><Tag fill="#101315dd" cornerRadius={3}/><Text text={`H ${point.heightMeters.toFixed(2)}`} fill="#fff" fontSize={14} padding={5}/></Label></Group>)}</Group>)}
            {level.transitions.map((transition) => <Group key={transition.id} onClick={(event) => { event.cancelBubble = true; setSelectedId(transition.id) }}><Line points={pointList(transition.path)} stroke="#f8fafc" strokeWidth={selectedId === transition.id ? 9 : 5} dash={[18, 10]} lineCap="round" hitStrokeWidth={24}/><Label x={transition.path[0]?.x ?? 0} y={(transition.path[0]?.y ?? 0) - 32}><Tag fill="#111827e8" stroke="#f8fafc" cornerRadius={3}/><Text text={`${transition.type}  ${transition.fromDepth}:${transition.fromHeight} -> ${transition.toDepth}:${transition.toHeight}`} fill="#fff" fontSize={13} padding={6}/></Label></Group>)}
            <AreaLayer items={level.events} color={COLORS.event} selectedId={selectedId} onSelect={setSelectedId} label={(item) => item.eventId}/><AreaLayer items={level.hazards} color={COLORS.hazard} selectedId={selectedId} onSelect={setSelectedId} label={(item) => item.hazardType}/><AreaLayer items={level.obstacles} color={COLORS.obstacle} selectedId={selectedId} onSelect={setSelectedId} label={(item) => `${item.material} / cover ${item.coverLevel}`}/>
            <AreaLayer items={level.occluders} color={COLORS.occlusion} selectedId={selectedId} onSelect={setSelectedId} label={() => 'foreground occlusion'}/>
            {level.enemies.map((enemy) => <Group key={enemy.id} onClick={(event) => { event.cancelBubble = true; setSelectedId(enemy.id) }}><Line points={[enemy.patrolLeft, enemy.y, enemy.patrolRight, enemy.y]} stroke="#f8fafc" strokeWidth={3} dash={[12, 8]}/><Circle x={enemy.patrolLeft} y={enemy.y} radius={10} fill="#111" stroke="#f8fafc" strokeWidth={3} draggable onDragEnd={(event) => updateEnemy(enemy.id, { patrolLeft: Math.min(event.target.x(), enemy.x) })}/><Circle x={enemy.patrolRight} y={enemy.y} radius={10} fill="#111" stroke="#f8fafc" strokeWidth={3} draggable onDragEnd={(event) => updateEnemy(enemy.id, { patrolRight: Math.max(event.target.x(), enemy.x) })}/><Circle x={enemy.x} y={enemy.y} radius={selectedId === enemy.id ? 17 : 14} fill="#9f1239" stroke="#fff" strokeWidth={3} draggable onDragEnd={(event) => updateEnemy(enemy.id, { x: event.target.x(), y: event.target.y() })}/><Label x={enemy.x + 18} y={enemy.y - 18}><Tag fill="#111827e8"/><Text text={`${enemy.archetype}\n${enemy.depth} H ${enemy.heightMeters}`} fill="#fff" fontSize={13} padding={6}/></Label></Group>)}
            {level.spawns.map((spawn) => <Group key={spawn.id} onClick={(event) => { event.cancelBubble = true; setSelectedId(spawn.id) }}><Circle x={spawn.x} y={spawn.y} radius={selectedId === spawn.id ? 19 : 15} fill={COLORS.spawn} stroke="#082f49" strokeWidth={4}/><Text x={spawn.x + 20} y={spawn.y - 9} text={`${spawn.kind} / ${spawn.depth} / H ${spawn.heightMeters}`} fill="#fff" fontSize={14}/></Group>)}
            {draft && <><Line points={pointList(previewPoints)} stroke={draft.tool === 'support' ? currentDepth.color : draft.tool === 'event' ? COLORS.event : draft.tool === 'hazard' ? COLORS.hazard : draft.tool === 'obstacle' ? COLORS.obstacle : draft.tool === 'occlusion' ? COLORS.occlusion : '#fff'} strokeWidth={6} dash={[12, 8]} closed={['event', 'hazard', 'obstacle', 'occlusion'].includes(draft.tool)} fill={['event', 'hazard', 'obstacle', 'occlusion'].includes(draft.tool) ? '#ffffff15' : undefined}/>{draft.points.map((point, index) => <Circle key={index} x={point.x} y={point.y} radius={6} fill="#fff" stroke="#111" strokeWidth={2}/>)}</>}
          </Layer></Stage><div className="message-bar">{message}<span>Arrows pan. Space saves a line. Click a support to resume. Esc cancels.</span></div></section>
      <aside className="inspector"><section><h2>Level</h2><label>Level ID<input value={level.id} onChange={(event) => mutate((next) => { next.id = event.target.value })}/></label><label>Title<input value={level.title} onChange={(event) => mutate((next) => { next.title = event.target.value })}/></label><label>Pixels per metre<input type="number" min="1" value={level.pixelsPerMeter} onChange={(event) => mutate((next) => { next.pixelsPerMeter = Number(event.target.value) })}/></label></section>
        <section><h2>Depth</h2><div className="segments">{level.depths.map((item) => <button key={item.id} className={depth === item.id ? 'selected' : ''} style={{ '--swatch': item.color } as React.CSSProperties} onClick={() => setDepth(item.id)}><i/>{item.label}</button>)}</div><p className="hint">Colour is depth. A continuous support never changes it.</p></section>
        <section><h2>Physical height</h2><div className="number-row"><label>From / point<input type="number" step="0.1" value={height} onChange={(event) => setHeight(Number(event.target.value))}/></label><label>To / maximum<input type="number" step="0.1" value={targetHeight} onChange={(event) => setTargetHeight(Number(event.target.value))}/></label></div><p className="hint">Slope limit: 55 degrees. Steeper support snaps to a 90 degree transition.</p></section>
        {(tool === 'transition' || tool === 'support') && <section><h2>Movement</h2><label>Transition type<select value={transitionType} onChange={(event) => setTransitionType(event.target.value as TransitionType)}><option value="steps">Stone steps / staircase</option><option value="ladder">Rung ladder</option><option value="cliff">Cliff / pull-up</option><option value="depth_walk">Depth walk</option><option value="rope">Rope / unstable crossing</option><option value="lift">Lift</option></select><ChevronDown size={15}/></label>{tool === 'transition' && <p className="hint">Start and finish on support tracks. Their depth and physical height are inferred from the anchors.</p>}</section>}
        {tool === 'event' && <section><h2>Event beacon</h2><label>Event number<input value={eventId} onChange={(event) => setEventId(event.target.value)}/></label><p className="hint">Hold and drag to paint a magenta volume, or click vertices for an exact polygon. It activates only on its assigned depth and height interval.</p></section>}{tool === 'enemy' && <section><h2>Enemy</h2><label>Archetype<input value={archetype} onChange={(event) => setArchetype(event.target.value)}/></label><p className="hint">New enemies remain explicit placeholders until registered.</p></section>}
        {selectedObject && <section className="selection"><h2>Selection</h2><code>{selectedObject.id}</code>{selectedArea && <><label>Installed depth<select value={selectedArea.depth} onChange={(event) => mutate((next) => { const area = [...next.events, ...next.hazards, ...next.obstacles, ...next.occluders].find((item) => item.id === selectedArea.id); if (area) area.depth = event.target.value as DepthId }, `${selectedArea.id} moved to ${event.target.value} depth.`)}>{level.depths.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select><ChevronDown size={15}/></label><div className="number-row"><label>Height min<input type="number" step="0.1" value={selectedArea.heightMin} onChange={(event) => mutate((next) => { const area = [...next.events, ...next.hazards, ...next.obstacles, ...next.occluders].find((item) => item.id === selectedArea.id); if (area) area.heightMin = Number(event.target.value) })}/></label><label>Height max<input type="number" step="0.1" value={selectedArea.heightMax} onChange={(event) => mutate((next) => { const area = [...next.events, ...next.hazards, ...next.obstacles, ...next.occluders].find((item) => item.id === selectedArea.id); if (area) area.heightMax = Number(event.target.value) })}/></label></div></>}{selectedSupportPoint && selectedPoint?.supportId === selectedId && <><div className="number-row"><label>Point X<input type="number" step="1" value={selectedSupportPoint.x} onChange={(event) => updateSupportPoint(selectedPoint.supportId, selectedPoint.index, { x: Number(event.target.value) })}/></label><label>Point Y<input type="number" step="1" value={selectedSupportPoint.y} onChange={(event) => updateSupportPoint(selectedPoint.supportId, selectedPoint.index, { y: Number(event.target.value) })}/></label></div><label>Point height, metres<input type="number" step="0.1" value={selectedSupportPoint.heightMeters} onChange={(event) => updateSupportPoint(selectedPoint.supportId, selectedPoint.index, { heightMeters: Number(event.target.value) })}/></label></>}<button className="danger" onClick={deleteSelected}>Delete selected</button></section>}
        <section className="validation"><button className={`validation-head ${issues.some((issue) => issue.severity === 'error') ? 'has-errors' : ''}`} onClick={() => setShowValidation((value) => !value)}><AlertTriangle size={18}/><span>{issues.filter((issue) => issue.severity === 'error').length} errors / {issues.filter((issue) => issue.severity === 'warning').length} debt</span></button>{showValidation && <div className="issue-list">{issues.length ? issues.map((issue, index) => <button key={index} onClick={() => setSelectedId(issue.objectId)}><b>{issue.severity}</b><span>{issue.objectId}: {issue.message}</span></button>) : <p>No validation issues.</p>}</div>}</section>
        <section className="legend"><h2>Map census</h2><dl><div><dt>Supports</dt><dd>{level.supports.length}</dd></div><div><dt>Transitions</dt><dd>{level.transitions.length}</dd></div><div><dt>Events</dt><dd>{level.events.length}</dd></div><div><dt>Hazards</dt><dd>{level.hazards.length}</dd></div><div><dt>Occluders</dt><dd>{level.occluders.length}</dd></div><div><dt>Enemies</dt><dd>{level.enemies.length}</dd></div></dl></section>
      </aside></main>
  </div>
}

function AreaLayer<T extends AreaShape>({ items, color, selectedId, onSelect, label }: { items: T[], color: string, selectedId: string | null, onSelect: (id: string) => void, label: (item: T) => string }) {
  return <>{items.map((item) => { const center = polygonCenter(item.points); return <Group key={item.id} onClick={(event) => { event.cancelBubble = true; onSelect(item.id) }}><Line points={pointList(item.points)} closed fill={`${color}35`} stroke={color} strokeWidth={selectedId === item.id ? 9 : 5} hitStrokeWidth={20}/><Label x={center.x} y={center.y}><Tag fill="#101315e8" stroke={color}/><Text text={`${label(item)}\n${item.depth} / H ${item.heightMin}-${item.heightMax}`} fill="#fff" align="center" fontSize={13} padding={6}/></Label></Group> })}</>
}

export default App
