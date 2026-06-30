"""
Downloads the OmniParser v2 icon detection model weights from HuggingFace.
Only downloads the YOLO detection model (~30 MB) — skips the large Florence-2
caption model since we use Claude for element selection instead.

Run once before starting the service:
    python setup_models.py
"""
from pathlib import Path

try:
    from huggingface_hub import snapshot_download
except ImportError:
    print("huggingface_hub not found. Run: pip install huggingface_hub")
    raise SystemExit(1)

REPO_ID = "microsoft/OmniParser-v2.0"
LOCAL_DIR = Path("models")

print(f"Downloading detection weights from {REPO_ID} ...")
print("(Only icon_detect/ — skipping the ~1 GB Florence-2 caption model)\n")

try:
    snapshot_download(
        repo_id=REPO_ID,
        allow_patterns=["icon_detect/*"],
        local_dir=str(LOCAL_DIR),
        local_dir_use_symlinks=False,
    )
    print(f"\n✓ Weights saved to {LOCAL_DIR.resolve() / 'icon_detect'}/")
    print("  Expected files: model.pt, model.yaml")
    print("\nIf those files are missing, check the actual repo structure at:")
    print(f"  https://huggingface.co/{REPO_ID}/tree/main")
except Exception as e:
    print(f"\n✗ Download failed: {e}")
    print("\nIf you get a 401/403, the model may require HuggingFace login:")
    print("  pip install huggingface_hub[cli]")
    print("  huggingface-cli login")
    print("  python setup_models.py")
    raise SystemExit(1)
