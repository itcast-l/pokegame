#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image


BASE_DIR = Path(__file__).resolve().parent
PROJECT_DIR = BASE_DIR.parents[2]
TEST_IMAGE = Path("/Users/cakeju/.cc-switch/skills/test-image/scripts/generate_image.py")
SPRITE_PROCESSOR = Path("/Users/cakeju/.cc-switch/skills/generate2dsprite/scripts/generate2dsprite.py")

OUTPUT_ROOT = BASE_DIR / "suited_jqk_backup"
CARD_SIZE = 128
FRAME_COUNT = 4
SUITS = ("spades", "hearts", "diamonds", "clubs")
RANKS = ("J", "Q", "K")

SUIT_SPEC = {
    "spades": {
        "name": "Spades",
        "ink": "deep navy spade symbols",
        "accent": "seafoam glints and dark tide-blue accents",
    },
    "hearts": {
        "name": "Hearts",
        "ink": "warm coral heart symbols",
        "accent": "coral glints and pale aqua highlights",
    },
    "diamonds": {
        "name": "Diamonds",
        "ink": "warm coral diamond symbols",
        "accent": "coral gem glints and pale aqua highlights",
    },
    "clubs": {
        "name": "Clubs",
        "ink": "deep teal club symbols",
        "accent": "kelp-green glints with small coral notes",
    },
}

RANK_SPEC = {
    "J": {
        "name": "Jack",
        "role": "Tide Herald Sea Drake",
        "portrait": (
            "a mature tide-herald sea drake court portrait, non-human ocean creature, "
            "lean angular eel-dragon body, shell crest, swept fins, coral barbels, "
            "watchful serious expression, compact herald silhouette"
        ),
    },
    "Q": {
        "name": "Queen",
        "role": "Abyssal Pearl Ray Queen",
        "portrait": (
            "a regal abyssal pearl ray queen court portrait, non-human ocean creature, "
            "broad manta-ray body, long trailing fins, shell-crown crest grown from the head, "
            "pearl scale plates, calm severe expression"
        ),
    },
    "K": {
        "name": "King",
        "role": "Coral-Crowned Dragon King",
        "portrait": (
            "an ancient coral-crowned sea dragon king court portrait, non-human ocean creature, "
            "powerful compact leviathan head and curled serpentine body, coral antler crest, "
            "deep sapphire scales, stern royal expression"
        ),
    },
}


def prompt_for(suit: str, rank: str) -> str:
    suit_spec = SUIT_SPEC[suit]
    rank_spec = RANK_SPEC[rank]
    card_name = f"{rank_spec['name']} of {suit_spec['name']}"

    return f"""Use the input images as visual references.

Input image 1 is the exact rank and suit identity reference for {card_name}. Preserve the readable corner rank "{rank}", {suit_spec['ink']}, and classic playing-card identity. Input image 2 is the accepted ocean card face style reference: pearly white card face, coral-and-shell border, teal wave ornaments, bubble accents, sea-glass highlights, compact square playing-card proportions. Input image 3 is the current accepted suitless ocean {rank_spec['name']} creature card reference. Preserve that ocean court-card creature family and overall Godot 2D asset readability, but add the correct {suit_spec['name']} suit identity.

Create exactly one clean HD 2D game asset sprite sheet: a 2 rows by 2 columns idle animation sheet for an ocean-themed {card_name}. The four equal cells are four frames of one subtle idle loop, not four different cards. Every cell must show the same centered upright {card_name}, same scale, same card silhouette, same corner rank letters, same {suit_spec['name']} suit marks, and same central court portrait composition. The only frame-to-frame changes should be tiny underwater idle details: small bubbles rising inside the card art, faint sea-glass glints on the border, very subtle fin or crest motion, and tiny pearl highlights.

Central portrait direction: {rank_spec['portrait']}. It must read as a playing-card court portrait, centered inside the card, symmetrical and clean, not a full scene. Keep the required rank letter "{rank}" visible in the top-left and bottom-right corners only. Keep {suit_spec['ink']} visible in the corners and as one small central suit emblem near the portrait. Use {suit_spec['accent']}. Do not add wrong rank letters, no A, Q, K, J mismatch, no numbers. Do not add any words besides the required corner rank letter "{rank}".

Production constraints: output exactly a 2x2 sprite sheet, four equal cells, no visible grid lines, no borders between cells, no guide marks, no labels outside the cards. Each cell has one card only. The full card must fit fully inside each cell, centered, with large solid magenta padding on all four sides. In each 512x512 cell, the card must occupy only about 68 to 76 percent of the cell height, never taller. The top edge of every card in every cell must sit 60 to 85 pixels below that cell's top edge, and the bottom edge must leave at least 60 pixels of flat magenta padding. All four cards must share the same vertical placement; do not shift the bottom row upward. Leave at least 60 pixels of flat magenta padding left and right. Do not let the card approach a cell boundary. No card border, crown, fin, horn, tail, bubble, sparkle, or ornament may cross or touch a cell edge. Use the same silhouette scale in every frame. Background across the entire sheet must be 100% solid flat magenta #FF00FF, including the space between cells. No gradients or texture in the magenta background. No extra cards, no hand, no table, no underwater scene outside the card, no UI."""


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


def normalize_processed_outputs(out_dir: Path, suit: str, rank: str) -> None:
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
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    meta.update(
        {
            "style": "ocean_suited_jqk_backup",
            "backup_only": True,
            "not_connected_to_game": True,
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

    if meta.get("edge_touch_frames"):
        raise ValueError(f"{suit} {rank} has edge-touch frames: {meta['edge_touch_frames']}")


def generate_card(suit: str, rank: str, force: bool = False) -> None:
    out_dir = OUTPUT_ROOT / suit / rank / "processed"
    out_dir.mkdir(parents=True, exist_ok=True)

    prompt = prompt_for(suit, rank)
    prompt_path = out_dir / "prompt-used.txt"
    source_sheet = out_dir / "source-sheet.png"
    prompt_path.write_text(prompt, encoding="utf-8")

    identity_ref = PROJECT_DIR / "art" / "cards" / "standard_deck" / suit / f"{rank}.png"
    style_ref = BASE_DIR / "source_assets" / "card_template_clean.png"
    court_ref = BASE_DIR / "court" / rank / "processed" / "frames" / "frame-001.png"

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
                str(court_ref),
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
            "--role",
            RANK_SPEC[rank]["role"],
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
    normalize_processed_outputs(out_dir, suit, rank)


def build_previews() -> None:
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    for frame_index in range(1, FRAME_COUNT + 1):
        preview = Image.new("RGBA", (len(RANKS) * CARD_SIZE, len(SUITS) * CARD_SIZE), (0, 0, 0, 0))
        for row, suit in enumerate(SUITS):
            for col, rank in enumerate(RANKS):
                frame = Image.open(
                    OUTPUT_ROOT / suit / rank / "processed" / "frames" / f"frame-{frame_index:03d}.png"
                ).convert("RGBA")
                preview.alpha_composite(frame, (col * CARD_SIZE, row * CARD_SIZE))
        preview.save(OUTPUT_ROOT / f"suited-jqk-backup-preview-frame-{frame_index:03d}.png")

    frames = [
        Image.open(OUTPUT_ROOT / f"suited-jqk-backup-preview-frame-{index:03d}.png").convert("RGBA")
        for index in range(1, FRAME_COUNT + 1)
    ]
    frames[0].save(
        OUTPUT_ROOT / "suited-jqk-backup-preview-animation.gif",
        save_all=True,
        append_images=frames[1:],
        duration=160,
        loop=0,
        disposal=2,
    )

    manifest = {
        "name": "suited_jqk_backup",
        "backup_only": True,
        "not_connected_to_game": True,
        "note": "备用带花色 J/Q/K 资源。当前游戏仍使用 art/cards/ocean_animated_deck/court/J|Q|K。",
        "suits": list(SUITS),
        "ranks": list(RANKS),
        "frame_count": FRAME_COUNT,
        "single_size": CARD_SIZE,
        "cards": [
            {
                "suit": suit,
                "rank": rank,
                "frame_001": f"res://art/cards/ocean_animated_deck/suited_jqk_backup/{suit}/{rank}/processed/frames/frame-001.png",
                "sheet": f"res://art/cards/ocean_animated_deck/suited_jqk_backup/{suit}/{rank}/processed/sheet-transparent.png",
            }
            for suit in SUITS
            for rank in RANKS
        ],
    }
    (OUTPUT_ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate backup suited ocean J/Q/K card assets.")
    parser.add_argument("--force", action="store_true", help="Regenerate source sheets even if they already exist.")
    parser.add_argument("--suit", choices=SUITS, action="append", help="Limit generation to one or more suits.")
    parser.add_argument("--rank", choices=RANKS, action="append", help="Limit generation to one or more ranks.")
    parser.add_argument("--skip-preview", action="store_true", help="Do not rebuild preview montage and manifest.")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    suits = tuple(args.suit) if args.suit else SUITS
    ranks = tuple(args.rank) if args.rank else RANKS
    for suit in suits:
        for rank in ranks:
            print(f"Generating backup {suit} {rank}", flush=True)
            generate_card(suit, rank, force=args.force)
    if not args.skip_preview and suits == SUITS and ranks == RANKS:
        build_previews()


if __name__ == "__main__":
    main()
