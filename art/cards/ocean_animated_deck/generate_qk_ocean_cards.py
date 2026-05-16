#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image


BASE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = BASE_DIR.parents[2]
TEST_IMAGE = Path("/Users/cakeju/.cc-switch/skills/test-image/scripts/generate_image.py")
SPRITE_PROCESSOR = Path("/Users/cakeju/.cc-switch/skills/generate2dsprite/scripts/generate2dsprite.py")

CARD_SIZE = 128
FRAME_COUNT = 4
SUITS = ("spades", "hearts", "diamonds", "clubs")
RANKS = ("Q", "K")

SUIT_SPEC = {
    "spades": {
        "suit_name": "Spades",
        "ink": "deep navy spade suit marks",
        "accent": "seafoam teal glints and dark blue tide lines",
    },
    "hearts": {
        "suit_name": "Hearts",
        "ink": "warm coral heart suit marks",
        "accent": "coral glints with seafoam teal highlights",
    },
    "diamonds": {
        "suit_name": "Diamonds",
        "ink": "warm coral diamond suit marks",
        "accent": "coral gem glints with pale aqua highlights",
    },
    "clubs": {
        "suit_name": "Clubs",
        "ink": "deep teal club suit marks",
        "accent": "dark kelp-green glints with small coral notes",
    },
}

RANK_SPEC = {
    "Q": {
        "rank_name": "Queen",
        "letter": "Q",
        "role": "Dragon Mother",
        "portrait": (
            "an elegant ocean Dragon Mother court portrait, regal sea-dragon queen, "
            "shell crown, pearl ornaments, flowing kelp hair, calm matriarch expression"
        ),
    },
    "K": {
        "rank_name": "King",
        "letter": "K",
        "role": "Dragon King",
        "portrait": (
            "a powerful ocean Dragon King court portrait, regal sea-dragon ruler, "
            "coral crown, pearl armor, trident-like silhouette detail, stern royal expression"
        ),
    },
}


def prompt_for(suit: str, rank: str) -> str:
    suit_spec = SUIT_SPEC[suit]
    rank_spec = RANK_SPEC[rank]
    card_name = f"{rank_spec['rank_name']} of {suit_spec['suit_name']}"

    return f"""Generate a PNG image now using the image_generation tool.

Input image 1 is the exact identity and layout reference for the playing card: {card_name}. Preserve the same card identity, same rank letter "{rank}", same suit family, same upright court-card structure, same readable corner rank labels, and the same simple in-game card proportions. Input image 2 is the accepted ocean card face style reference. Input image 3 is the accepted animated ocean deck preview style reference. Use them only for card face style, border language, palette discipline, and Godot-ready readability.

Create exactly one 2x2 animation sprite sheet for an ocean-themed {card_name} playing card in a clean HD 2D Godot asset style. The four equal cells are four frames of one subtle idle loop, not four different cards. Every frame must show the same centered upright {card_name}, same scale, same silhouette, same corner labels, same suit marks, and same court portrait composition. The only frame-to-frame changes should be subtle underwater dynamics: small bubbles rising inside the card art, sea-glass glints sliding along the border, faint wave shimmer on the court portrait, and tiny pearl highlights. Keep the animation loop calm and readable.

Court portrait direction: replace the traditional court figure with {rank_spec['portrait']}. The portrait must still read as a playing-card court figure, centered inside the card, symmetrical enough for classic poker readability, not a full scene. Keep the required rank letter "{rank}" visible in the top-left and bottom-right corners only, and keep {suit_spec['ink']} visible in the corners and near the portrait. Use {suit_spec['accent']}. Preserve the existing ocean deck surface: pearly white card face, thin coral-and-shell border, subtle teal wave pattern, small bubbles, faint sea-glass highlights, soft aqua shadows, crisp readable edges.

Production constraints: output exactly a 2 rows by 2 columns sprite sheet, four equal cells, no visible grid lines, no guide marks, no labels outside the cards. Each cell must have one card only. Important containment rule: the card in every cell must occupy only about 45 to 55 percent of that cell's height, centered with at least 15 percent flat magenta padding above, below, left, and right; no card border, crown, weapon, hair, bubbles, sparkle, or ornament may approach or cross a cell edge. Background across the entire sheet must be 100% solid flat magenta #FF00FF, including the space between cells. No gradients or texture in the magenta background. No extra cards, no hand, no table, no underwater scene outside the card, no UI. No text beyond the required corner rank letter "{rank}" and suit symbols. Do not add wrong rank letters, no A, J, or numbers. The card must remain readable when each frame is downscaled to 128x128."""


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


def organize_processed_outputs(out_dir: Path, prompt: str, suit: str, rank: str) -> None:
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
            "style": "ocean_qk_dragon_court",
            "suit": suit,
            "rank": rank,
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


def generate_card(suit: str, rank: str, force: bool = False) -> None:
    prompt = prompt_for(suit, rank)
    card_dir = BASE_DIR / suit / rank
    out_dir = card_dir / "processed"
    out_dir.mkdir(parents=True, exist_ok=True)
    prompt_path = out_dir / "prompt-used.txt"
    source_sheet = out_dir / "source-sheet.png"
    identity_ref = PROJECT_DIR / "art" / "cards" / "standard_deck" / suit / f"{rank}.png"
    style_ref = BASE_DIR / "source_assets" / "card_template_clean.png"
    deck_ref = BASE_DIR / "deck-preview-frame-001.png"

    prompt_path.write_text(prompt, encoding="utf-8")
    if force or not source_sheet.exists():
        run_command(
            [
                sys.executable,
                str(TEST_IMAGE),
                "--prompt",
                prompt,
                "--input-image",
                str(identity_ref),
                "--input-image",
                str(style_ref),
                "--input-image",
                str(deck_ref),
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
            "0.96",
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
    organize_processed_outputs(out_dir, prompt, suit, rank)


def build_previews() -> None:
    for frame_index in range(1, FRAME_COUNT + 1):
        preview = Image.new("RGBA", (len(RANKS) * CARD_SIZE, len(SUITS) * CARD_SIZE), (0, 0, 0, 0))
        for row, suit in enumerate(SUITS):
            for col, rank in enumerate(RANKS):
                frame = Image.open(
                    BASE_DIR / suit / rank / "processed" / "frames" / f"frame-{frame_index:03d}.png"
                ).convert("RGBA")
                preview.alpha_composite(frame, (col * CARD_SIZE, row * CARD_SIZE))
        preview.save(BASE_DIR / f"qk-preview-frame-{frame_index:03d}.png")

    frames = [
        Image.open(BASE_DIR / f"qk-preview-frame-{index:03d}.png").convert("RGBA")
        for index in range(1, FRAME_COUNT + 1)
    ]
    frames[0].save(
        BASE_DIR / "qk-preview-animation.gif",
        save_all=True,
        append_images=frames[1:],
        duration=160,
        loop=0,
        disposal=2,
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate ocean-themed Q/K animated card assets.")
    parser.add_argument("--force", action="store_true", help="Regenerate source sheets even if they already exist.")
    parser.add_argument("--suit", choices=SUITS, action="append", help="Limit generation to one or more suits.")
    parser.add_argument("--rank", choices=RANKS, action="append", help="Limit generation to Q and/or K.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    suits = tuple(args.suit) if args.suit else SUITS
    ranks = tuple(args.rank) if args.rank else RANKS
    for suit in suits:
        for rank in ranks:
            print(f"Generating {suit} {rank}", flush=True)
            generate_card(suit, rank, force=args.force)
    if suits == SUITS and ranks == RANKS:
        build_previews()


if __name__ == "__main__":
    main()
