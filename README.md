# Ghost

An Electron overlay app that puts an animated ghost cursor on top of any window, pointing at a UI element based on a plain-English query.

Ask it "close the window" or "show me the undo button" — it takes a screenshot, runs AI vision, and drops a cursor exactly where you need to click.

---

## How It Works

```
Alt+Shift+G  →  type a query  →  Ghost points to the right element
```

Under the hood:

1. **Screenshot** — Electron captures the screen behind the overlay
2. **Detection** — OmniParser v2 YOLO finds up to 50 UI element bounding boxes
3. **Vision** — Claude (`claude-sonnet-4-6`) receives the annotated screenshot + coordinate table, picks the best matching element
4. **Result** — Ghost cursor animates to the element with a tooltip

Each query costs ~$0.006 in API credits (~300 queries per $2).

---

## Requirements

- **Dev machine:** Node.js 18+, Python 3.11–3.13
- **Windows PC (runtime):** Python 3.11–3.13, an Anthropic API key
- AMD GPU on Windows → CPU-only PyTorch (ROCm support is limited)

---

## Setup

### 1. Clone and install JS deps

```bash
git clone https://github.com/o-faruk/Ghost.git
cd Ghost
npm install
```

### 2. Set up the Python service

```bat
cd python-service
python -m venv .venv
.venv\Scripts\activate

pip install -r requirements.txt
pip install -r requirements-torch.txt
```

### 3. Add your API key

```bat
copy .env.example .env
notepad .env
```

Paste your `ANTHROPIC_API_KEY` in `.env`.

### 4. Download OmniParser weights (~30 MB)

```bat
python setup_models.py
```

### 5. Start the Python service

```bat
uvicorn main:app --host 127.0.0.1 --port 8000 --reload
```

### 6. Start the Electron app (separate terminal)

```bash
cd ..
npm run dev
```

Press **Alt+Shift+G** to open the query panel.

---

## Project Structure

```
Ghost/
├── electron/
│   ├── main.ts            # App lifecycle, IPC, screenshot, state machine
│   ├── preload.ts         # contextBridge → window.ghostAPI
│   └── platform/          # OS hotkeys, screen info
├── overlay-renderer/
│   ├── src/
│   │   ├── App.tsx        # State machine: idle | input | thinking | result | error
│   │   ├── components/
│   │   │   └── QueryPanel.tsx   # Draggable floating input panel
│   │   └── index.css      # CSS keyframes: ghost-appear, ghost-bob, ghost-pulse
│   └── index.html
└── python-service/
    ├── main.py            # FastAPI service, /analyze endpoint
    ├── utils/
    │   └── omniparser.py  # YOLO detection wrapper, NMS
    ├── setup_models.py    # Downloads OmniParser weights
    ├── requirements.txt
    ├── requirements-torch.txt   # CPU-only PyTorch
    └── .env.example
```

---

## Current Accuracy

| Query | Status |
|---|---|
| Close / minimize / maximize window | Reliable |
| Close tab | Reliable |
| Undo / redo buttons | Good |
| General toolbar icons | Variable |
| Address bar | In progress |
| Menu items (File, Edit…) | Variable |

See [`STATUS.md`](STATUS.md) for full details on what works, what doesn't, and what's next.

---

## Tech Stack

- **Electron** — transparent always-on-top overlay, click-through when idle
- **React + TypeScript + Tailwind** — overlay UI, pure CSS animations (no Framer Motion)
- **esbuild** — bundles the Electron main process
- **Vite** — dev server for the overlay renderer
- **Python FastAPI** — AI pipeline microservice
- **OmniParser v2** — YOLO-based UI element detection
- **Claude claude-sonnet-4-6** — vision model for element identification
