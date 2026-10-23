# lIl Browser — service icon exploration

Six original directions for the lIl Browser service mark.  
Reference direction: the softened, gradient-led geometry of the 2025–2026 Gemini / Workspace refresh — without reusing Google's G, Gemini's four-point sparkle, or any existing product silhouette.

## Deliverables

- **Final SVG:** [`final/lil-service-icon.svg`](final/lil-service-icon.svg)
- **Final transparent PNG:** [`final/lil-service-icon-1024.png`](final/lil-service-icon-1024.png) (1024 × 1024)
- **Size comparison:** [`comparison-light.png`](comparison-light.png) / [`comparison-dark.png`](comparison-dark.png)
- **All concepts:** [`concepts/`](concepts/)
- **Reproducible render:** [`generate_icons.py`](generate_icons.py) (Edge headless renders the same SVG geometry at 16 / 32 / 128 / 1024)

## Concepts

| # | Name | Idea | 16 px | 32 px | 128 px |
|---|------|------|:---:|:---:|:---:|
| 01 | **Orbit / portal** | A browser portal ring with one detached orbiting node. The negative gap separates the node instead of adding detail. | ● | ●● | ●●● |
| 02 | **Folded L** | A folded ribbon forms an abstract lowercase L: a tab edge and a page fold in one silhouette. | ● | ●● | ●●● |
| 03 | **Duo link** | Two rounded capsules cross like linked tabs. A calm aperture sits at the intersection. | ○ | ● | ●●● |
| 04 | **Prism aperture** | A rounded triangular window with a circular aperture; speed and focus without a literal play button. | ● | ●● | ●●● |
| 05 | **Soft bloom** | Four overlapping lobes with a warm centre. The most approachable direction, but the least browser-specific. | ● | ●● | ●●● |
| 06 | **lIl pillars** | An optically spaced lIl monogram. Strong brand recall, but the three thin strokes lose clarity at 16 px. | ○ | ● | ●●● |

Legend: ○ weak, ● usable, ●● clear, ●●● strong.

## Final choice: 01 Orbit / portal

The ring reads as a browser window or portal; the detached node reads as a tab, a person, or a connected service. The break in the ring is functional, not decorative: it keeps the node visually separate at 16 px and prevents the mark from becoming a generic magnifier or a letter Q.

- **Silhouette:** one ring + one node; no more than two masses.
- **Optical balance:** the node is slightly outside the ring's lower-right quadrant so the ring does not feel like a closed badge.
- **Gradient:** blue → teal → lime, with a soft radial highlight on the node.
- **Originality:** no G-bar, no four-point Gemini sparkle, no Google product colour sequence, no existing service silhouette.
- **Transparency:** all edges are vector, with no background tile, so the mark works on light, dark, and photographic surfaces.

At 16 px the ring remains a clean circle and the detached node remains a separate dot. More detailed concepts (03 and 06) were rejected as the primary mark because their internal gaps collapse at that size.

## Production notes

- SVG viewBox and canvas: `1024 × 1024`.
- Final PNG: transparent RGBA, 1024 × 1024.
- Gradients are defined in SVG; no raster effects or filters are required.
- Suggested minimum clear space: one node diameter on all sides.
- The final mark is also suitable for a future app icon by placing it on the existing ink/gold tile, but the standalone asset intentionally has no tile.
