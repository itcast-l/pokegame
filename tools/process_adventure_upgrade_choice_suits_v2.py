from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = ROOT / "generated-images" / "adventure_upgrade_choice_suits_v2"
OUTPUT_ROOT = ROOT / "art" / "ui" / "adventure_upgrade_cards"
SUITS = ("hearts", "diamonds", "clubs", "spades")
TIER_FILES = {
    "common": "common",
    "rare": "uncommon",
    "legendary": "rare",
}
BACKGROUND_LUMA_THRESHOLD = 115.0
SCAN_LEFT = 80
SCAN_RIGHT = 944
MIN_CARD_ROW_PIXELS = 20
EDGE_FEATHER_PIXELS = 3


def build_card_alpha(rgb: np.ndarray) -> np.ndarray:
    luma = rgb.astype(np.float32).mean(axis=2)
    dark_pixels = luma < BACKGROUND_LUMA_THRESHOLD
    height, width = dark_pixels.shape
    spans: dict[int, tuple[int, int]] = {}

    for y in range(height):
        indices = np.flatnonzero(dark_pixels[y, SCAN_LEFT:SCAN_RIGHT]) + SCAN_LEFT
        if indices.size >= MIN_CARD_ROW_PIXELS:
            spans[y] = int(indices.min()), int(indices.max())

    if not spans:
        raise RuntimeError("Could not locate the card silhouette")

    first_y = min(spans)
    last_y = max(spans)
    for y in range(first_y, last_y + 1):
        if y in spans:
            continue
        previous_y = max(candidate for candidate in spans if candidate < y)
        next_y = min(candidate for candidate in spans if candidate > y)
        blend = (y - previous_y) / (next_y - previous_y)
        previous = spans[previous_y]
        following = spans[next_y]
        spans[y] = (
            round(previous[0] + (following[0] - previous[0]) * blend),
            round(previous[1] + (following[1] - previous[1]) * blend),
        )

    alpha = np.zeros((height, width), dtype=np.uint8)
    for y in range(first_y, last_y + 1):
        nearby = [spans[probe_y] for probe_y in range(max(first_y, y - 2), min(last_y, y + 2) + 1)]
        left = max(0, min(span[0] for span in nearby) - EDGE_FEATHER_PIXELS)
        right = min(width - 1, max(span[1] for span in nearby) + EDGE_FEATHER_PIXELS)
        alpha[y, left : right + 1] = 255
        for feather in range(EDGE_FEATHER_PIXELS):
            value = round(255 * (feather + 1) / (EDGE_FEATHER_PIXELS + 1))
            if left - EDGE_FEATHER_PIXELS + feather >= 0:
                alpha[y, left - EDGE_FEATHER_PIXELS + feather] = value
            if right + EDGE_FEATHER_PIXELS - feather < width:
                alpha[y, right + EDGE_FEATHER_PIXELS - feather] = value

    for feather in range(EDGE_FEATHER_PIXELS):
        value = round(255 * (feather + 1) / (EDGE_FEATHER_PIXELS + 1))
        alpha[first_y - EDGE_FEATHER_PIXELS + feather, alpha[first_y] > 0] = value
        alpha[last_y + EDGE_FEATHER_PIXELS - feather, alpha[last_y] > 0] = value

    return alpha


def process_asset(source: Path, output: Path) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = np.array(image)
    pixels[:, :, 3] = build_card_alpha(pixels[:, :, :3])
    output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(pixels, mode="RGBA").save(output, optimize=True)
    print(f"processed {source.relative_to(ROOT)} -> {output.relative_to(ROOT)}")


def main() -> None:
    for suit in SUITS:
        for source_tier, output_tier in TIER_FILES.items():
            process_asset(
                SOURCE_ROOT / suit / f"{source_tier}.png",
                OUTPUT_ROOT / suit / "processed" / f"{suit}-{output_tier}.png",
            )


if __name__ == "__main__":
    main()
