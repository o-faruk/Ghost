from __future__ import annotations
import base64
import io
import os
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
        lw = len(label) * 8 + 6
        draw.rectangle([x1, y1 - 16, x1 + lw, y1], fill=(255, 30, 90))
        draw.text((x1 + 3, y1 - 14), label, fill="white")

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
                            "text": (
                                f'The screenshot shows a UI with {len(elements)} numbered '
                                f'elements (red boxes, 0-indexed).\n'
                                f'User query: "{request.query}"\n\n'
                                f'Call point_to_element with the best matching element.'
                            ),
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
