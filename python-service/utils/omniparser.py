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

    try:
        from ultralytics import YOLO
    except ImportError:
        raise RuntimeError(
            "ultralytics not installed. Run:\n"
            "  pip install -r requirements-torch.txt"
        )

    import os
    os.environ.setdefault("YOLO_VERBOSE", "False")

    _model = YOLO(str(MODEL_PATH))
    return _model


def _iou(a: list[int], b: list[int]) -> float:
    """Intersection-over-union of two [x1,y1,x2,y2] boxes."""
    ix1, iy1 = max(a[0], b[0]), max(a[1], b[1])
    ix2, iy2 = min(a[2], b[2]), min(a[3], b[3])
    inter = max(0, ix2 - ix1) * max(0, iy2 - iy1)
    if inter == 0:
        return 0.0
    area_a = (a[2] - a[0]) * (a[3] - a[1])
    area_b = (b[2] - b[0]) * (b[3] - b[1])
    return inter / (area_a + area_b - inter)


def _nms(elements: list[DetectedElement], iou_threshold: float = 0.5) -> list[DetectedElement]:
    """Greedy NMS — keeps highest-confidence box when two overlap significantly."""
    kept: list[DetectedElement] = []
    for elem in elements:  # already sorted by confidence descending
        if all(_iou(elem["bbox"], k["bbox"]) < iou_threshold for k in kept):
            kept.append(elem)
    return kept


def detect_elements(
    image: Image.Image,
    conf_threshold: float = 0.15,
    max_elements: int = 60,
) -> list[DetectedElement]:
    """
    Run YOLO detection on a PIL image.
    Returns up to max_elements bounding boxes sorted by confidence (descending),
    with NMS applied to remove heavily overlapping duplicates.
    """
    model = _load_model()

    results = model(image, conf=conf_threshold, device="cpu", verbose=False)

    elements: list[DetectedElement] = []
    for result in results:
        for box in result.boxes:
            x1, y1, x2, y2 = (int(v) for v in box.xyxy[0].tolist())
            conf = float(box.conf[0])
            if x2 <= x1 or y2 <= y1:
                continue
            elements.append({"bbox": [x1, y1, x2, y2], "confidence": round(conf, 3)})

    elements.sort(key=lambda e: e["confidence"], reverse=True)
    elements = _nms(elements)
    return elements[:max_elements]
