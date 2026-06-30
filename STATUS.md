# Ghost — Status

## What It Is
An Electron overlay app (Windows) that puts an animated ghost cursor + tooltip on top of any app, pointing at a UI element based on a plain-English query.

---

## Flow

```
User presses Alt+Shift+G
        ↓
Overlay appears (floating input panel, draggable)
        ↓
User types a query (e.g. "close the window")
        ↓
Electron hides overlay → takes a screenshot (desktopCapturer)
        ↓
POST /analyze → Python FastAPI service (127.0.0.1:8000)
        ↓
OmniParser YOLO detects up to 50 UI elements (bounding boxes)
conf=0.05, NMS iou=0.3 to remove duplicates
        ↓
Screenshot annotated with numbered red boxes → downscaled to 1280px wide
        ↓
Claude (claude-sonnet-4-6) receives:
  - annotated image (visual)
  - coordinate table: each element's index, x, y, width (all normalized 0–1)
  - elements grouped by screen region (title bar / browser chrome / toolbar / content / taskbar)
  - positional rules for window controls + visual rules for everything else
        ↓
Claude calls point_to_element(element_index, tooltip)
        ↓
Python returns target_x, target_y (center of element, physical px)
        ↓
Electron converts to logical px (÷ scaleFactor)
Overlay appears with ghost cursor at (x, y) + tooltip
        ↓
User clicks or presses Escape to dismiss
```

---

## What's Working

| Query type | Accuracy | Notes |
|---|---|---|
| Close window / X button | ✅ Reliable | Positional rule: rightmost element in title bar (y<2.5%) |
| Minimize / maximize | ✅ Reliable | Second/third-rightmost in title bar |
| Close tab | ✅ Reliable | Rightmost in browser chrome group |
| Undo button | ✅ Good | Visual ID from image |
| General toolbar icons | ⚠️ Variable | Depends on how clearly the icon is numbered in annotated image |
| Address bar / URL bar | ❌ Broken | See below |
| Menu items (File, Edit…) | ⚠️ Variable | OmniParser detects text-adjacent icons but not text labels |

---

## Known Issues

### 1. Address bar not detected
**Symptom:** Ghost lands on a tiny icon (23x26px) within the address bar row instead of the input field center.

**Root cause:** OmniParser's YOLO was trained on icon-sized UI elements. It reliably detects buttons and icons (20–100px) but does NOT detect large flat rectangles like an address bar input (~1500x30px). So the "widest element in browser chrome" fallback picks the widest *icon* in that row, not the bar itself.

**Fix options (not yet implemented):**
- Hardcode address bar position heuristic: y ≈ 3–6% of screen height, x center ≈ 40–45% of width
- Crop the browser chrome strip and run a different detection pass on it
- Add a post-processing step that synthesizes a "virtual" address bar element based on the gap between detected icons in the nav row

---

### 2. General visual element ID is hit-or-miss
**Symptom:** Queries like "show me the File menu" or "where is the search box" may land on wrong elements.

**Root cause:** Claude is visually identifying elements from a 1280px-wide screenshot where each element is a small numbered red box. For elements without obvious iconography (menus, text links, unstyled buttons) this is essentially a guessing game.

**Fix options:**
- Run OCR (pytesseract) on each detected bounding box and send the text to Claude ("element 12 contains text: 'File'")
- Re-enable Florence-2 captioning from OmniParser — slow (~20s on CPU) but gives text descriptions per element
- Add Tesseract as optional post-processor for text elements only

---

### 3. First query is slow (~2s for detection)
**Symptom:** First `/analyze` takes 2+ seconds; subsequent ones take ~0.4s.

**Cause:** YOLO model lazy-loads on first call. Expected behavior — model stays in memory after that.

**Fix:** Pre-warm the model at service startup (call `_load_model()` on startup event).

---

## Architecture

```
Ghost/
├── electron/
│   ├── main.ts          # App lifecycle, IPC, screenshot, state machine
│   ├── preload.ts       # contextBridge → ghostAPI
│   └── platform/        # OS-specific hotkeys, screen info
├── overlay-renderer/
│   ├── src/App.tsx      # State machine UI: idle | input | thinking | result | error
│   ├── src/components/
│   │   └── QueryPanel.tsx   # Draggable input panel, two-div CSS pattern
│   └── src/index.css    # CSS keyframes: ghost-appear, ghost-bob, ghost-pulse
└── python-service/
    ├── main.py          # FastAPI, annotate(), _build_prompt(), /analyze endpoint
    ├── utils/
    │   └── omniparser.py    # YOLO detection, NMS, lazy singleton
    └── setup_models.py  # Downloads OmniParser weights from HuggingFace
```

**Key constants:**
- Model: `claude-sonnet-4-6` (~$0.006/query)
- Detection: OmniParser v2 `icon_detect` YOLO, conf=0.05, NMS iou=0.3, max 50 elements
- Overlay: `frame:false, transparent:true, alwaysOnTop, setIgnoreMouseEvents(true, {forward:true})`
- Hotkey: `Alt+Shift+G`

---

## Next Priorities

1. **Address bar fix** — synthesize a virtual element or use positional heuristic for URL bar queries
2. **Model warm-up** — call `_load_model()` on startup to eliminate the 2s first-query delay
3. **OCR on bounding boxes** — add pytesseract to label text-containing elements so Claude can match "File menu" → element 4 ("File")
4. **Error UX** — current error card is plain; could show a retry button
5. **Packaging** — `electron-builder` setup for Windows `.exe` installer
