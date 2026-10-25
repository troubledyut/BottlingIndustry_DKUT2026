# Safety Sign Sprite Sheet

Generated from `preprocessed/` (pictograms only — text labels and banners removed).

## Atlas
- **File:** `spritesheet.png`
- **Size:** 4096 × 4096 px (4K)
- **Grid:** 16 × 16 cells
- **Cell:** 256 × 256 px
- **Slots used:** 248 / 256 (last 8 cells empty)
- **Background:** transparent (RGBA)
- **Content fit:** each pictogram is trimmed to its tight bounds, scaled (aspect-preserved) to fit a 240×240 box, and centred in its cell, leaving a 8px transparent gutter on every side to prevent texture/mip bleeding.

## Slot layout
Slots are filled **row-major** (left→right, top→bottom), grouped by category in this order:

| category | count | slot index range |
|---|---|---|
| `safe_condition_signs` | 47 | 0–46 |
| `hazard_warning_signs` | 80 | 47–126 |
| `prohibition_signs` | 73 | 127–199 |
| `mandatory_signs` | 48 | 200–247 |

`index = row * 16 + col`  ·  `slot_x = col * 256`  ·  `slot_y = row * 256`

## Mapping file — `spritesheet.tsv`
Tab-separated, one row per sign, with a header row. Columns:

| column | meaning |
|---|---|
| `index` | slot index, 0-based, row-major |
| `category` | source category folder |
| `name` | sign name (matches the `.svg` filename, no extension) |
| `col`, `row` | grid cell coordinates (0–15) |
| `slot_x`, `slot_y` | top-left pixel of the **cell** in the atlas |
| `slot_w`, `slot_h` | cell size (always 256) |
| `content_x`, `content_y` | top-left pixel of the **visible pictogram** (cell + centring offset) |
| `content_w`, `content_h` | actual pixel size of the visible pictogram |

Use **slot_** columns for fixed-grid UV math; use **content_** columns when you need the tight pixel rect of the artwork itself.

### UV example (OpenGL/Unity, origin bottom-left)
```
u0 = slot_x / 4096
v0 = 1 - (slot_y + slot_h) / 4096
u1 = (slot_x + slot_w) / 4096
v1 = 1 - slot_y / 4096
```
(For a top-left origin, e.g. CSS/canvas, use `slot_x/y` directly.)
