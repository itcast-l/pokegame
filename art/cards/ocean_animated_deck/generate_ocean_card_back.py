#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image


BASE_DIR = Path(__file__).resolve().parent
TEST_IMAGE = Path("/Users/cakeju/.cc-switch/skills/test-image/scripts/generate_image.py")
SPRITE_PROCESSOR = Path("/Users/cakeju/.cc-switch/skills/generate2dsprite/scripts/generate2dsprite.py")

CARD_SIZE = 128
KEY_FRAME_COUNT = 4
FRAME_COUNT = 8
FIT_SCALE = "0.96"


def prompt_for_back() -> str:
    return """Generate a PNG image now using the image_generation tool.

Input image 1 is only the accepted ocean card face style reference. Use its compact square playing-card proportions, rounded card silhouette, coral-and-shell border language, teal wave ornaments, pearly trim, sea-glass highlights, and clean HD Godot asset readability. Create a card BACK, not a face card.

Create exactly one 2x2 animation sprite sheet for an ocean-themed playing card back. The four equal cells are four frames of one subtle idle loop, not four different designs. Every frame must show the same centered upright card back, same scale, same card silhouette, and same symmetrical back pattern. The only frame-to-frame changes should be subtle underwater dynamics inside the card art: small bubbles rising, sea-glass glints sliding along the border, faint wave shimmer, pearl highlights, and a gentle glow shift in the central sigil.

Card back art direction: a premium deep-ocean card back with a dark teal-to-abyss-blue enamel center panel, pearly white rim, coral-and-shell corner ornaments, a central shell compass medallion wrapped by a subtle tidal vortex, small pearl nodes, wave filigree, and faint sea-dragon scale patterning. It should feel royal, mysterious, and consistent with the ocean court cards. The design must be symmetrical, upright, and clearly read as the back side of a playing card rather than a face card.

Strict content rules: no ranks, no letters, no numbers, no words, no hearts, diamonds, clubs, spades, no pips, no suit icons, no face-card portraits, no people, no hands, no table, no UI, no scene outside the card. Do not copy rank letters or suit symbols from the reference image. Do not include any readable text anywhere.

Production constraints: output exactly a 2 rows by 2 columns sprite sheet, four equal cells, no visible grid lines, no guide marks, no labels outside the cards. Each cell must have one card back only. The full card rectangle in every cell must occupy only about 45 to 55 percent of that cell's height, centered with at least 18 percent flat magenta padding above, below, left, and right. No card border, shell point, coral corner, bubble, sparkle, ornament, or glow may approach or cross a cell edge. Background across the entire sheet must be 100% solid flat magenta #FF00FF, including the space between cells. No gradients or texture in the magenta background."""


def run_command(command: list[str]) -> None:
    print("+", " ".join(command), flush=True)
    subprocess.run(command, check=True)


def compose_sheet(frames: list[Image.Image], cols: int, rows: int) -> Image.Image:
    sheet = Image.new("RGBA", (cols * CARD_SIZE, rows * CARD_SIZE), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        x = (index % cols) * CARD_SIZE
        y = (index // cols) * CARD_SIZE
        sheet.alpha_composite(frame, (x, y))
    return sheet


def insert_midpoint_frames(key_frames: list[Image.Image]) -> list[Image.Image]:
    frames = []
    for index, frame in enumerate(key_frames):
        next_frame = key_frames[(index + 1) % len(key_frames)]
        frames.append(frame)
        frames.append(Image.blend(frame, next_frame, 0.5))
    return frames


def organize_processed_outputs(out_dir: Path, prompt: str) -> None:
    frame_sources = [out_dir / f"idle-{index}.png" for index in range(1, KEY_FRAME_COUNT + 1)]
    missing = [path for path in frame_sources if not path.exists()]
    if missing:
        raise FileNotFoundError(f"Missing processed frames: {missing}")

    frames_dir = out_dir / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)
    key_frames = []
    for index, source in enumerate(frame_sources, start=1):
        frame = Image.open(source).convert("RGBA")
        key_frames.append(frame)

    frames = insert_midpoint_frames(key_frames)
    for index, frame in enumerate(frames, start=1):
        frame.save(frames_dir / f"frame-{index:03d}.png")

    frames[0].save(out_dir / "clean.png")
    frames[0].save(out_dir / "raw.png")
    compose_sheet(frames, 4, 2).save(out_dir / "sheet-transparent.png")
    compose_sheet(frames, 8, 1).save(out_dir / "strip-transparent.png")
    frames[0].save(
        out_dir / "animation.gif",
        save_all=True,
        append_images=frames[1:],
        duration=80,
        loop=0,
        disposal=2,
    )

    meta_path = out_dir / "pipeline-meta.json"
    meta = json.loads(meta_path.read_text(encoding="utf-8")) if meta_path.exists() else {}
    meta.update(
        {
            "style": "ocean_card_back",
            "rank": "back",
            "suit": "none",
            "role": "Ocean Card Back",
            "key_frame_count": KEY_FRAME_COUNT,
            "frame_count": FRAME_COUNT,
            "single_size": CARD_SIZE,
            "raw_source": "source-sheet.png",
            "outputs": [
                "clean.png",
                "raw.png",
                "frames/frame-001.png",
                "frames/frame-002.png",
                "frames/frame-003.png",
                "frames/frame-004.png",
                "frames/frame-005.png",
                "frames/frame-006.png",
                "frames/frame-007.png",
                "frames/frame-008.png",
                "sheet-transparent.png",
                "strip-transparent.png",
                "animation.gif",
            ],
        }
    )
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")
    (out_dir / "prompt-used.txt").write_text(prompt, encoding="utf-8")


def generate_back(force: bool = False) -> None:
    prompt = prompt_for_back()
    out_dir = BASE_DIR / "back" / "processed"
    out_dir.mkdir(parents=True, exist_ok=True)
    prompt_path = out_dir / "prompt-used.txt"
    source_sheet = out_dir / "source-sheet.png"
    style_ref = BASE_DIR / "source_assets" / "card_template_clean.png"

    prompt_path.write_text(prompt, encoding="utf-8")
    if force or not source_sheet.exists():
        run_command(
            [
                sys.executable,
                str(TEST_IMAGE),
                "--prompt",
                prompt,
                "--input-image",
                str(style_ref),
                "--size",
                "1024x1024",
                "--quality",
                "high",
                "--output-format",
                "png",
                "--output",
                str(source_sheet),
            ]
        )

    run_command(
        [
            sys.executable,
            str(SPRITE_PROCESSOR),
            "process",
            "--input",
            str(source_sheet),
            "--target",
            "asset",
            "--mode",
            "idle",
            "--output-dir",
            str(out_dir),
            "--prompt-file",
            str(prompt_path),
            "--rows",
            "2",
            "--cols",
            "2",
            "--cell-size",
            str(CARD_SIZE),
            "--fit-scale",
            FIT_SCALE,
            "--trim-border",
            "2",
            "--edge-clean-depth",
            "2",
            "--align",
            "center",
            "--shared-scale",
            "--component-mode",
            "largest",
            "--component-padding",
            "8",
            "--min-component-area",
            "3000",
            "--edge-touch-margin",
            "6",
            "--reject-edge-touch",
            "--threshold",
            "100",
            "--edge-threshold",
            "150",
            "--duration",
            "160",
        ]
    )
    organize_processed_outputs(out_dir, prompt)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate an animated ocean-themed card back.")
    parser.add_argument("--force", action="store_true", help="Regenerate source sheet even if it already exists.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    generate_back(force=args.force)


if __name__ == "__main__":
    main()
