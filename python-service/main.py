from __future__ import annotations
import base64
import io
import os
from pathlib import Path
import time

from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from PIL import Image, ImageDraw
from pydantic import BaseModel
import anthropic

from utils.omniparser import detect_elements

load_dotenv()

app = FastAPI(title="Ghost AI Service", version="0.2.0")

# Anthropic client — will raise at import time if the key is missing
_anthropic_client: anthropic.Anthropic | None = None


def get_client() -> anthropic.Anthropic:
    global _anthropic_client
    if _anthropic_client is None:
        api_key = os.environ.get("ANTHROPIC_API_KEY", "")
        if not api_key:
            raise RuntimeError(
                "ANTHROPIC_API_KEY not set. "
                "Copy .env.example to .env and add your key."
            )
        _anthropic_client = anthropic.Anthropic(api_key=api_key)
    return _anthropic_client


# ─── Request / response models ────────────────────────────────────────────────

class AnalyzeRequest(BaseModel):
    screenshot_b64: str   # raw base64 PNG — no data-URL prefix
    query: str


class BoundingBox(BaseModel):
    x: int
    y: int
    width: int
    height: int


class AnalyzeResponse(BaseModel):
    target_x: int         # center of target element, physical px (capture-space)
    target_y: int
    label: str
    tooltip: str
    confidence: float
    bbox: BoundingBox


# ─── Helpers ──────────────────────────────────────────────────────────────────

def _annotate(image: Image.Image, elements: list[dict]) -> str:
    """
    Draw numbered red bounding boxes on a copy of the image, resize to ≤1280 px
    wide (keeps API payload small), return as base64 PNG.
    """
    annotated = image.copy().convert("RGB")
    draw = ImageDraw.Draw(annotated)

    for i, elem in enumerate(elements):
        x1, y1, x2, y2 = elem["bbox"]
        draw.rectangle([x1, y1, x2, y2], outline=(255, 30, 90), width=2)
        label = str(i)
        # Larger label box so numbers are legible after downscaling
        lw = len(label) * 11 + 8
        lh = 20
        tag_y = max(0, y1 - lh)
        draw.rectangle([x1, tag_y, x1 + lw, tag_y + lh], fill=(255, 30, 90))
        draw.text((x1 + 4, tag_y + 3), label, fill="white")

    # Downscale for Claude to reduce token cost and latency
    max_w = 1280
    if annotated.width > max_w:
        ratio = max_w / annotated.width
        annotated = annotated.resize(
            (max_w, int(annotated.height * ratio)), Image.LANCZOS
        )

    buf = io.BytesIO()
    annotated.save(buf, format="PNG", optimize=True)
    return base64.b64encode(buf.getvalue()).decode()


def _build_prompt(query: str, image: Image.Image, elements: list[dict]) -> str:
    """
    Build the Claude prompt with normalized (0.0–1.0) coordinates grouped by
    screen region so Claude can reason about position without knowing resolution.
    """
    w, h = image.width, image.height

    # Group elements into screen regions using normalized y
    title_bar, browser_chrome, toolbar, taskbar, content = [], [], [], [], []
    for i, elem in enumerate(elements):
        x1, y1, x2, y2 = elem["bbox"]
        cx = (x1 + x2) / 2
        cy = (y1 + y2) / 2
        ew = x2 - x1
        xn = round(cx / w, 3)
        yn = round(cy / h, 3)
        wn = round(ew / w, 3)
        entry = f"{i:>3} x={xn:.3f} y={yn:.3f} w={wn:.3f}"
        if yn < 0.025:          # top 2.5% — OS title bar + window controls
            title_bar.append(entry)
        elif yn < 0.08:         # 2.5-8% — browser chrome (tabs + address bar + nav buttons)
            browser_chrome.append(entry)
        elif yn < 0.13:         # 8-13% — secondary toolbars (bookmarks, app toolbars)
            toolbar.append(entry)
        elif yn > 0.93:         # bottom 7% — taskbar
            taskbar.append(entry)
        else:
            content.append(entry)

    def fmt_group(name: str, items: list[str]) -> str:
        if not items:
            return f"{name}: (none detected)"
        return f"{name}:\n" + "\n".join(f"  {e}" for e in items)

    lines = [
        f"Desktop screenshot ({w}x{h}px). Red numbered boxes in the image mark detected "
        f"UI elements. Coordinates below are normalized 0.0–1.0 (x: left→right, y: top→bottom).",
        "",
        fmt_group("OS title bar / window controls (y<0.025)", title_bar),
        fmt_group("Browser chrome — tabs, address bar, nav buttons (y 0.025–0.08)  ← address bar is the WIDEST element here (w>0.3)", browser_chrome),
        fmt_group("Secondary toolbar — bookmarks bar, app menus (y 0.08–0.13)", toolbar),
        fmt_group("Page content (y 0.13–0.93)", content),
        fmt_group("Taskbar (y>0.93)", taskbar),
        "",
        f'User query: "{query}"',
        "",
        "How to pick the right element:",
        "• Window / tab controls (position-based):",
        "  - close window/X → largest x in 'OS title bar'",
        "  - minimize → second-largest x in 'OS title bar'",
        "  - maximize/restore → third-largest x in 'OS title bar'",
        "  - close tab → largest x in 'Browser chrome'",
        "  - new tab (+) → second-largest x in 'Browser chrome'",
        "  - address bar / URL bar / omnibox → WIDEST element (largest w) in 'Browser chrome'",
        "• Everything else — visually identify each numbered element in the image "
        "(read labels, icons, text), pick the best match, use region as a sanity check.",
        "",
        "Call point_to_element with the index of the best matching element.",
    ]
    return "\n".join(lines)


# ─── Endpoints ────────────────────────────────────────────────────────────────

@app.get("/health")
async def health():
    return {"status": "ok", "version": "0.2.0"}


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze(request: AnalyzeRequest):
    t0 = time.perf_counter()

    # 1. Decode screenshot
    try:
        img_bytes = base64.b64decode(request.screenshot_b64)
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid screenshot: {e}")

    # 2. OmniParser YOLO detection
    try:
        elements = detect_elements(image)
    except RuntimeError as e:
        raise HTTPException(status_code=503, detail=str(e))

    if not elements:
        raise HTTPException(
            status_code=422,
            detail="No UI elements detected in screenshot. Try a different screen.",
        )

    t_detect = time.perf_counter()
    print(f"[ghost] detected {len(elements)} elements in {t_detect - t0:.2f}s")

    # 3. Annotate screenshot with numbered boxes for Claude
    annotated_b64 = _annotate(image, elements)

    # Save annotated image for debugging — open debug_annotated.png to see what Claude sees
    try:
        debug_img_bytes = base64.b64decode(annotated_b64)
        debug_path = Path(__file__).parent / "debug_annotated.png"
        debug_path.write_bytes(debug_img_bytes)
        print(f"[ghost] debug image saved → {debug_path}")
    except Exception:
        pass

    # 4. Claude selects the target element via tool use (guaranteed structured output)
    try:
        client = get_client()
        response = client.messages.create(
            model="claude-sonnet-4-6",
            max_tokens=128,
            tools=[
                {
                    "name": "point_to_element",
                    "description": (
                        "Identify which numbered UI element in the screenshot "
                        "best matches the user's query, then write a short tooltip."
                    ),
                    "input_schema": {
                        "type": "object",
                        "properties": {
                            "element_index": {
                                "type": "integer",
                                "description": "0-based index of the target element",
                            },
                            "tooltip": {
                                "type": "string",
                                "description": (
                                    "One concise instruction for the user, "
                                    "max 8 words, e.g. 'Click the mute button'"
                                ),
                            },
                        },
                        "required": ["element_index", "tooltip"],
                    },
                }
            ],
            tool_choice={"type": "tool", "name": "point_to_element"},
            messages=[
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "image",
                            "source": {
                                "type": "base64",
                                "media_type": "image/png",
                                "data": annotated_b64,
                            },
                        },
                        {
                            "type": "text",
                            "text": _build_prompt(request.query, image, elements),
                        },
                    ],
                }
            ],
        )
    except anthropic.AuthenticationError:
        raise HTTPException(status_code=401, detail="Invalid Anthropic API key in .env")
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"Claude API error: {e}")

    t_claude = time.perf_counter()
    print(f"[ghost] Claude responded in {t_claude - t_detect:.2f}s")

    # 5. Extract tool call result
    tool_block = next(
        (b for b in response.content if b.type == "tool_use"), None
    )
    if tool_block is None:
        raise HTTPException(status_code=502, detail="Claude did not call the tool")

    idx = int(tool_block.input["element_index"])
    tooltip = str(tool_block.input["tooltip"])

    if idx < 0 or idx >= len(elements):
        raise HTTPException(
            status_code=422,
            detail=f"Claude returned out-of-range index {idx} (max {len(elements) - 1})",
        )

    # 6. Build response — coordinates are in the original image's pixel space
    elem = elements[idx]
    x1, y1, x2, y2 = elem["bbox"]
    cx, cy = (x1 + x2) // 2, (y1 + y2) // 2

    print(
        f"[ghost] → element {idx} at ({cx},{cy}), "
        f"conf={elem['confidence']:.2f}, tooltip='{tooltip}' "
        f"| total {time.perf_counter() - t0:.2f}s"
    )

    return AnalyzeResponse(
        target_x=cx,
        target_y=cy,
        label=f"element_{idx}",
        tooltip=tooltip,
        confidence=elem["confidence"],
        bbox=BoundingBox(x=x1, y=y1, width=x2 - x1, height=y2 - y1),
    )
