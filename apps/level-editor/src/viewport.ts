export interface ViewTransform {
  x: number
  y: number
  scale: number
}

export function panWithArrow(view: ViewTransform, key: string, fast: boolean): ViewTransform {
  const step = fast ? 320 : 120
  if (key === 'ArrowLeft') return { ...view, x: view.x + step }
  if (key === 'ArrowRight') return { ...view, x: view.x - step }
  if (key === 'ArrowUp') return { ...view, y: view.y + step }
  if (key === 'ArrowDown') return { ...view, y: view.y - step }
  return view
}
