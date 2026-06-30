from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

app = FastAPI(title="Ghost AI Service", version="0.1.0")


class AnalyzeRequest(BaseModel):
    screenshot_b64: str  # base64-encoded PNG captured at native resolution
    query: str           # user's natural-language question


class BoundingBox(BaseModel):
    x: int
    y: int
    width: int
    height: int


class AnalyzeResponse(BaseModel):
    target_x: int        # center of target element in physical pixels (capture-space)
    target_y: int
    label: str           # element label from OmniParser
    tooltip: str         # instruction text from Claude
    confidence: float
    bbox: BoundingBox


@app.get("/health")
async def health():
    return {"status": "ok", "version": "0.1.0"}


@app.post("/analyze", response_model=AnalyzeResponse)
async def analyze(request: AnalyzeRequest):
    # Milestone 2 will implement:
    #   1. decode screenshot_b64 → PIL image
    #   2. run OmniParser v2 → List[{bbox, label, confidence}]
    #   3. send elements + query to Claude (claude-sonnet-4-6, structured JSON output)
    #   4. return the selected element + generated tooltip
    raise HTTPException(status_code=501, detail="OmniParser + Claude pipeline not yet implemented")
