# Visual Level Editor

The browser build is the primary OpenAI Build Week developer-tool artifact.

## Acceptance path

1. Open the hosted editor.
2. Load `semi-abandoned-tomb.moadmap` from this folder's `public` assets.
3. Select **Support**, choose a depth colour and numeric height, and draw a
   walkable line beneath the painted floor.
4. Press Space to finish the line; click it to resume editing.
5. Select **Transition**, click a support, draw the route, and finish on another
   support.
6. Export runtime JSON or save the complete `.moadmap` archive.

The map archive is a ZIP containing the background image, editable source data,
engine-facing data, and generated occlusion masks.
