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
FRAME_COUNT = 4
RANKS = ("J", "Q", "K")
FIT_SCALE = {"J": "0.88", "Q": "0.88", "K": "0.96"}

RANK_SPEC = {
    "J": {
        "rank_name": "Jack",
        "role": "Tide Herald Sea Drake",
        "creature": (
            "a mature tide-herald sea drake creature, front-facing, non-humanoid, lean angular eel-dragon body, "
            "sharp shell crest, swept fins, coral barbels, weathered teal scales, natural conch horn ridge, "
            "watchful serious sea-creature expression, compact herald silhouette"
        ),
    },
    "Q": {
        "rank_name": "Queen",
        "role": "Abyssal Pearl Ray Queen",
        "creature": (
            "a majestic abyssal pearl ray queen creature, front-facing, non-humanoid, broad angular manta-ray body, "
            "long trailing fins, spined shell-crown crest grown from the skull, coral cheek fins, pearl armor-like "
            "scale plates, narrow animal eyes, calm severe ocean-creature expression, ancient deep-sea silhouette"
        ),
    },
    "K": {
        "rank_name": "King",
        "role": "Coral-Crowned Dragon King",
        "creature": (
            "an ancient coral-crowned sea dragon king creature, front-facing, non-humanoid, powerful compact "
            "leviathan head and curled serpentine body, coral antler crest shaped like a natural trident, "
            "deep sapphire scales, pearl chin fins, stern noble ocean-creature expression"
        ),
    },
}


def prompt_for(rank: str) -> str:
    spec = RANK_SPEC[rank]
    return f"""Generate a PNG image now using the image_generation tool.

Input image 1 is only the accepted ocean card face style reference. Use its pearly white card face, coral-and-shell border, teal wave ornaments, bubble accents, sea-glass highlights, compact square playing-card proportions, and clean HD Godot asset readability. Do not copy any rank, suit, pip, or symbol from other cards.

Create exactly one 2x2 animation sprite sheet for a suitless ocean-themed {spec['rank_name']} card. This is a no-suit sea-creature court card: there must be no hearts, diamonds, clubs, spades, pips, suit icons, suit-colored corner marks, or suit labels anywhere. The only text allowed is the rank letter "{rank}" in the top-left corner and the same "{rank}" in the bottom-right corner. Keep those rank letters readable at 128x128.

The four equal cells are four frames of one subtle idle loop, not four different cards. Every frame must show the same centered upright suitless {spec['rank_name']} card, same scale, same card silhouette, same rank letters, and same central creature composition. The only frame-to-frame changes should be subtle underwater dynamics inside the card art: small bubbles rising, sea-glass glints sliding along the border, faint wave shimmer, tiny pearl highlights, and very slight fin or whisker motion.

Central creature direction: one single front-facing non-human ocean creature of {spec['creature']}. The creature must face directly toward the viewer with a symmetrical readable animal pose. It must clearly be a living sea creature, not a pure object emblem. The visual tone must be mature, regal, slightly ominous, and suitable for a royal court card. Avoid cute mascot styling, baby proportions, chibi proportions, oversized round eyes, tiny smiling mouth, plush-toy softness, cartoon innocence, or overly friendly expressions. Do not use any person, human, humanoid, mermaid, siren, dragon-person, human torso, human shoulders, human arms, hands, legs, feet, clothing, robe, armor worn as human clothing, crown worn like human clothing, weapon held in hands, human hairstyle, human-like face, human eyes, nose, lips, beard, or human portrait. Animal eyes or glowing creature eyes are allowed only if they do not read as human facial features. Do not make a reversible poker court card. Do not add an upside-down duplicate creature. Do not show a side profile, three-quarter profile, turned-away head, split mirrored figure, or two-headed court layout. The creature should be centered inside the card, still reading as a clean playing-card court illustration rather than a scene.

Production constraints: output exactly a 2 rows by 2 columns sprite sheet, four equal cells, no visible grid lines, no guide marks, no labels outside the cards. Each cell must have one card only. The full card rectangle in every cell must occupy only about 35 to 45 percent of that cell's height, centered with at least 25 percent flat magenta padding above, below, left, and right. Keep the creature compact inside the card's inner oval; no card border, crest, fin, whisker, horn, trident, bubbles, sparkle, or ornament may approach or cross a cell edge. Background across the entire sheet must be 100% solid flat magenta #FF00FF, including the space between cells. No gradients or texture in the magenta background. No extra cards, no hand, no table, no underwater scene outside the card, no UI."""


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


def organize_processed_outputs(out_dir: Path, prompt: str, rank: str) -> None:
    frame_sources = [out_dir / f"idle-{index}.png" for index in range(1, FRAME_COUNT + 1)]
    missing = [path for path in frame_sources if not path.exists()]
    if missing:
        raise FileNotFoundError(f"Missing processed frames: {missing}")

    frames_dir = out_dir / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)
    frames = []
    for index, source in enumerate(frame_sources, start=1):
        frame = Image.open(source).convert("RGBA")
        frame_path = frames_dir / f"frame-{index:03d}.png"
        frame.save(frame_path)
        frames.append(frame)

    frames[0].save(out_dir / "clean.png")
    frames[0].save(out_dir / "raw.png")
    compose_sheet(frames, 4, 1).save(out_dir / "strip-transparent.png")
    compose_sheet(frames, 2, 2).save(out_dir / "sheet-transparent.png")

    meta_path = out_dir / "pipeline-meta.json"
    meta = json.loads(meta_path.read_text(encoding="utf-8")) if meta_path.exists() else {}
    meta.update(
        {
            "style": "ocean_suitless_court",
            "rank": rank,
            "suit": "none",
            "role": RANK_SPEC[rank]["role"],
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
                "sheet-transparent.png",
                "strip-transparent.png",
                "animation.gif",
            ],
        }
    )
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")
    (out_dir / "prompt-used.txt").write_text(prompt, encoding="utf-8")


def generate_card(rank: str, force: bool = False) -> None:
    prompt = prompt_for(rank)
    out_dir = BASE_DIR / "court" / rank / "processed"
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
            FIT_SCALE[rank],
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
    organize_processed_outputs(out_dir, prompt, rank)


def build_previews() -> None:
    for frame_index in range(1, FRAME_COUNT + 1):
        preview = Image.new("RGBA", (len(RANKS) * CARD_SIZE, CARD_SIZE), (0, 0, 0, 0))
        for col, rank in enumerate(RANKS):
            frame = Image.open(
                BASE_DIR / "court" / rank / "processed" / "frames" / f"frame-{frame_index:03d}.png"
            ).convert("RGBA")
            preview.alpha_composite(frame, (col * CARD_SIZE, 0))
        preview.save(BASE_DIR / f"suitless-jqk-preview-frame-{frame_index:03d}.png")

    frames = [
        Image.open(BASE_DIR / f"suitless-jqk-preview-frame-{index:03d}.png").convert("RGBA")
        for index in range(1, FRAME_COUNT + 1)
    ]
    frames[0].save(
        BASE_DIR / "suitless-jqk-preview-animation.gif",
        save_all=True,
        append_images=frames[1:],
        duration=160,
        loop=0,
        disposal=2,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate suitless ocean-themed J/Q/K animated card assets.")
    parser.add_argument("--force", action="store_true", help="Regenerate source sheets even if they already exist.")
    parser.add_argument("--rank", choices=RANKS, action="append", help="Limit generation to J, Q, and/or K.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    ranks = tuple(args.rank) if args.rank else RANKS
    for rank in ranks:
        print(f"Generating suitless {rank}", flush=True)
        generate_card(rank, force=args.force)
    if ranks == RANKS:
        build_previews()


if __name__ == "__main__":
    main()
