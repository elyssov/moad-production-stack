# MoAD Level Editor

Authoring tool for the custom Mystery of Ancient Darkness 2.5D runtime.

## Run

```powershell
.\start-editor.ps1
```

The editor opens at `http://127.0.0.1:4173/`.

Use **Demo tomb** to open the included competition proof without a file picker.

## Authoring law

- Colour is discrete depth: yellow near, green middle, blue far.
- Every support vertex carries numeric physical height in metres.
- A continuous support has one depth but may change height.
- Walkable slope is limited to 55 degrees in physical coordinates.
- A steeper point is authored as a 90 degree ladder or cliff transition.
- Screen `x/y` anchors Alice's feet to painted art; numeric height drives world topology.
- Event, hazard, and obstacle areas always carry depth and a height interval.
- Occlusion polygons are rasterized into per-area PNG masks inside the map archive.

## Files

`Save map` produces a `.moadmap` ZIP archive containing:

- `manifest.json`
- `level.source.json`
- `level.runtime.json`
- `background/<image>`

`Runtime JSON` exports the engine-facing contract directly. Existing Papyrus Chamber runtime JSON can be imported and migrated into the editor.

## Verification

```powershell
npm test
npm run lint
npm run build
```
