"""
Thin wrapper around the OmniParser v2 YOLO detection model.
We intentionally skip Florence-2 captioning — on CPU (AMD on Windows) it adds
20+ seconds per screenshot. Claude handles element identification from the
annotated screenshot instead.
"""
from __future__ import annotations
from pathlib import Path
from typing import TypedDict

from PIL import Image

MODEL_PATH = Path(__file__).parent.parent / "models" / "icon_detect" / "model.pt"

# Lazy-loaded singleton so the model only loads on the first /analyze call
_model = None


class DetectedElement(TypedDict):
    bbox: list[int]        # [x1, y1, x2, y2] in image pixel coords
    confidence: float


def _load_model():
    global _model
    if _model is not None:
        return _model

    if not MODEL_PATH.exists():
        raise RuntimeError(
            f"OmniParser detection model not found at:\n  {MODEL_PATH}\n"
            "Run `python setup_models.py` in the python-service directory first."
        )

    # Import here so the service starts even if ultralytics isn't installed yet
    # (gives a cleaner error message from the /analyze endpoint)
    try:
        from ultralytics import YOLO
    except ImportError:
        raise RuntimeError(
            "ultralytics not installed. Run:\n"
            "  pip install -r requirements-torch.txt"
        )

    import os
    os.environ.setdefault("YOLO_VERBOSE", "False")  # suppress YOLO per-frame logs

    _model = YOLO(str(MODEL_PATH))
    return _model


def detect_elements(
    image: Image.Image,
    conf_threshold: float = 0.05,
    max_elements: int = 100,
) -> list[DetectedElement]:
    """
    Run YOLO detection on a PIL image.
    Returns up to max_elements bounding boxes sorted by confidence (descending).
    Coordinates are in the original image's pixel space.
    """
    model = _load_model()

    results = model(image, conf=conf_threshold, device="cpu", verbose=False)

    elements: list[DetectedElement] = []
    for result in results:
        for box in result.boxes:
            x1, y1, x2, y2 = (int(v) for v in box.xyxy[0].tolist())
            conf = float(box.conf[0])
            # Skip degenerate boxes
            if x2 <= x1 or y2 <= y1:
                continue
            elements.append({"bbox": [x1, y1, x2, y2], "confidence": round(conf, 3)})

    elements.sort(key=lambda e: e["confidence"], reverse=True)
    return elements[:max_elements]
