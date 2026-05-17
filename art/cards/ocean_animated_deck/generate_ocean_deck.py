#!/usr/bin/env python3
from __future__ import annotations

import json
import math
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont


BASE_DIR = Path(__file__).resolve().parent
TEMPLATE_RAW = BASE_DIR / "source_assets" / "card_template_raw.png"
FONT_PATH = Path("/System/Library/Fonts/Supplemental/Arial Bold.ttf")
CANVAS_SIZE = 512
OUTPUT_SIZE = 128
FRAME_COUNT = 4

RANKS = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10"]
SUITS = {
    "spades": {
        "short": "spades",
        "ink": (10, 37, 72, 255),
        "accent": (74, 185, 196, 210),
    },
    "hearts": {
        "short": "hearts",
        "ink": (218, 86, 69, 255),
        "accent": (83, 193, 202, 210),
    },
    "diamonds": {
        "short": "diamonds",
        "ink": (224, 98, 74, 255),
        "accent": (78, 184, 198, 210),
    },
    "clubs": {
        "short": "clubs",
        "ink": (12, 63, 74, 255),
        "accent": (236, 122, 94, 210),
    },
}


def is_magenta(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, _a = pixel
    return r > 180 and b > 170 and g < 90


def prepare_template() -> tuple[Image.Image, tuple[int, int, int, int]]:
    source = Image.open(TEMPLATE_RAW).convert("RGBA")
    pixels = source.load()
    for y in range(source.height):
        for x in range(source.width):
            if is_magenta(pixels[x, y]):
                pixels[x, y] = (0, 0, 0, 0)

    bbox = source.getbbox()
    if bbox is None:
        raise RuntimeError("Template has no visible card pixels")

    card = source.crop(bbox)
    target_h = 468
    target_w = round(card.width * target_h / card.height)
    card = card.resize((target_w, target_h), Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    origin = ((CANVAS_SIZE - target_w) // 2, 22)
    canvas.alpha_composite(card, origin)
    card_box = canvas.getbbox()
    if card_box is None:
        raise RuntimeError("Prepared template has no visible pixels")
    return canvas, card_box


def load_font(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_PATH), size=size)


def draw_centered_text(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, font: ImageFont.FreeTypeFont, fill: tuple[int, int, int, int]) -> None:
    bbox = draw.textbbox((0, 0), text, font=font)
    width = bbox[2] - bbox[0]
    height = bbox[3] - bbox[1]
    draw.text((xy[0] - width // 2, xy[1] - height // 2 - bbox[1]), text, font=font, fill=fill)


def draw_heart(draw: ImageDraw.ImageDraw, cx: int, cy: int, size: int, fill: tuple[int, int, int, int]) -> None:
    s = size
    r = s * 0.23
    draw.ellipse((cx - s * 0.42, cy - s * 0.42, cx - s * 0.42 + r * 2, cy - s * 0.42 + r * 2), fill=fill)
    draw.ellipse((cx + s * 0.42 - r * 2, cy - s * 0.42, cx + s * 0.42, cy - s * 0.42 + r * 2), fill=fill)
    draw.polygon(
        [
            (cx - s * 0.46, cy - s * 0.16),
            (cx + s * 0.46, cy - s * 0.16),
            (cx, cy + s * 0.52),
        ],
        fill=fill,
    )


def draw_diamond(draw: ImageDraw.ImageDraw, cx: int, cy: int, size: int, fill: tuple[int, int, int, int]) -> None:
    s = size
    draw.polygon(
        [
            (cx, cy - s * 0.56),
            (cx + s * 0.42, cy),
            (cx, cy + s * 0.56),
            (cx - s * 0.42, cy),
        ],
        fill=fill,
    )


def draw_spade(draw: ImageDraw.ImageDraw, cx: int, cy: int, size: int, fill: tuple[int, int, int, int]) -> None:
    s = size
    draw.polygon(
        [
            (cx, cy - s * 0.58),
            (cx + s * 0.40, cy - s * 0.12),
            (cx + s * 0.28, cy + s * 0.22),
            (cx + s * 0.08, cy + s * 0.10),
            (cx + s * 0.08, cy + s * 0.42),
            (cx + s * 0.25, cy + s * 0.42),
            (cx + s * 0.25, cy + s * 0.54),
            (cx - s * 0.25, cy + s * 0.54),
            (cx - s * 0.25, cy + s * 0.42),
            (cx - s * 0.08, cy + s * 0.42),
            (cx - s * 0.08, cy + s * 0.10),
            (cx - s * 0.28, cy + s * 0.22),
            (cx - s * 0.40, cy - s * 0.12),
        ],
        fill=fill,
    )
    draw.ellipse((cx - s * 0.43, cy - s * 0.05, cx - s * 0.03, cy + s * 0.32), fill=fill)
    draw.ellipse((cx + s * 0.03, cy - s * 0.05, cx + s * 0.43, cy + s * 0.32), fill=fill)


def draw_club(draw: ImageDraw.ImageDraw, cx: int, cy: int, size: int, fill: tuple[int, int, int, int]) -> None:
    s = size
    r = s * 0.22
    draw.ellipse((cx - r, cy - s * 0.55, cx + r, cy - s * 0.55 + r * 2), fill=fill)
    draw.ellipse((cx - s * 0.40, cy - s * 0.18, cx - s * 0.40 + r * 2, cy - s * 0.18 + r * 2), fill=fill)
    draw.ellipse((cx + s * 0.40 - r * 2, cy - s * 0.18, cx + s * 0.40, cy - s * 0.18 + r * 2), fill=fill)
    draw.polygon(
        [
            (cx - s * 0.08, cy + s * 0.02),
            (cx + s * 0.08, cy + s * 0.02),
            (cx + s * 0.22, cy + s * 0.52),
            (cx - s * 0.22, cy + s * 0.52),
        ],
        fill=fill,
    )


def draw_suit(draw: ImageDraw.ImageDraw, suit: str, cx: int, cy: int, size: int, fill: tuple[int, int, int, int]) -> None:
    if suit == "spades":
        draw_spade(draw, cx, cy, size, fill)
    elif suit == "hearts":
        draw_heart(draw, cx, cy, size, fill)
    elif suit == "diamonds":
        draw_diamond(draw, cx, cy, size, fill)
    elif suit == "clubs":
        draw_club(draw, cx, cy, size, fill)


def suit_image(suit: str, size: int, fill: tuple[int, int, int, int], rotate: bool = False) -> Image.Image:
    pad = max(12, size // 4)
    image = Image.new("RGBA", (size + pad * 2, size + pad * 2), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw_suit(draw, suit, image.width // 2, image.height // 2, size, fill)
    if rotate:
        image = image.rotate(180)
    return image


def draw_rotated_suit(canvas: Image.Image, suit: str, cx: int, cy: int, size: int, fill: tuple[int, int, int, int], rotate: bool) -> None:
    icon = suit_image(suit, size, fill, rotate=rotate)
    canvas.alpha_composite(icon, (round(cx - icon.width / 2), round(cy - icon.height / 2)))


def draw_corner(canvas: Image.Image, rank: str, suit: str, x: int, y: int, fill: tuple[int, int, int, int], rotate: bool = False) -> None:
    group = Image.new("RGBA", (96, 132), (0, 0, 0, 0))
    draw = ImageDraw.Draw(group)
    font_size = 54 if rank == "10" else 68
    rank_font = load_font(font_size)
    draw_centered_text(draw, (48, 34), rank, rank_font, fill)
    draw_suit(draw, suit, 48, 88, 34, fill)
    if rotate:
        group = group.rotate(180)
    canvas.alpha_composite(group, (x, y))


def pip_positions(rank: str, box: tuple[int, int, int, int]) -> list[tuple[int, int, bool]]:
    x0, y0, x1, y1 = box
    cx = (x0 + x1) // 2
    left = cx - 58
    right = cx + 58
    top = y0 + 148
    upper = y0 + 206
    mid = (y0 + y1) // 2
    lower = y1 - 206
    bottom = y1 - 148

    if rank == "A":
        return [(cx, mid, False)]
    if rank == "2":
        return [(cx, top, False), (cx, bottom, True)]
    if rank == "3":
        return [(cx, top, False), (cx, mid, False), (cx, bottom, True)]
    if rank == "4":
        return [(left, top, False), (right, top, False), (left, bottom, True), (right, bottom, True)]
    if rank == "5":
        return [(left, top, False), (right, top, False), (cx, mid, False), (left, bottom, True), (right, bottom, True)]
    if rank == "6":
        return [(left, top, False), (right, top, False), (left, mid, False), (right, mid, False), (left, bottom, True), (right, bottom, True)]
    if rank == "7":
        return [(left, top, False), (right, top, False), (cx, upper, False), (left, mid, False), (right, mid, False), (left, bottom, True), (right, bottom, True)]
    if rank == "8":
        return [
            (left, y0 + 142, False),
            (right, y0 + 142, False),
            (left, y0 + 200, False),
            (right, y0 + 200, False),
            (left, y1 - 200, True),
            (right, y1 - 200, True),
            (left, y1 - 142, True),
            (right, y1 - 142, True),
        ]
    if rank == "9":
        return [
            (left, top, False),
            (right, top, False),
            (left, upper, False),
            (right, upper, False),
            (cx, mid, False),
            (left, lower, True),
            (right, lower, True),
            (left, bottom, True),
            (right, bottom, True),
        ]
    if rank == "10":
        return [
            (left, top, False),
            (right, top, False),
            (left, upper, False),
            (right, upper, False),
            (left, lower, True),
            (right, lower, True),
            (left, bottom, True),
            (right, bottom, True),
            (cx, y0 + 178, False),
            (cx, y1 - 178, True),
        ]
    raise ValueError(f"Unsupported rank: {rank}")


def compose_card(template: Image.Image, card_box: tuple[int, int, int, int], suit: str, rank: str) -> Image.Image:
    spec = SUITS[suit]
    canvas = template.copy()
    fill = spec["ink"]
    accent = spec["accent"]
    x0, y0, x1, y1 = card_box

    draw_corner(canvas, rank, suit, x0 + 18, y0 + 14, fill)
    draw_corner(canvas, rank, suit, x1 - 114, y1 - 146, fill, rotate=True)

    pip_size = 168 if rank == "A" else 44 if rank in {"8", "9", "10"} else 50
    for cx, cy, rotate in pip_positions(rank, card_box):
        draw_rotated_suit(canvas, suit, cx, cy, pip_size, fill, rotate)

    line = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    line_draw = ImageDraw.Draw(line)
    for cx, cy, rotate in pip_positions(rank, card_box):
        if rank == "A":
            continue
        line_draw.arc((cx - 20, cy - 18, cx + 20, cy + 18), 205, 335, fill=accent, width=3)
    canvas = Image.alpha_composite(canvas, line)
    return canvas


def card_alpha_mask(card: Image.Image) -> Image.Image:
    return card.getchannel("A")


def effect_frame(card: Image.Image, frame_index: int, seed: int) -> Image.Image:
    frame = card.copy()
    mask = card_alpha_mask(card)
    overlay = Image.new("RGBA", card.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    offset = -170 + frame_index * 150 + (seed % 45)
    draw.polygon(
        [
            (offset, -40),
            (offset + 56, -40),
            (offset + 360, CANVAS_SIZE + 40),
            (offset + 304, CANVAS_SIZE + 40),
        ],
        fill=(145, 245, 255, 35),
    )
    draw.line((offset + 50, 0, offset + 330, CANVAS_SIZE), fill=(255, 255, 255, 32), width=5)

    rng = random.Random(seed)
    for bubble_index in range(7):
        bx = rng.randint(140, 372)
        base_y = rng.randint(82, 430)
        by = 82 + ((base_y - 82 - frame_index * (10 + bubble_index % 3 * 3)) % 348)
        radius = rng.randint(5, 11)
        alpha = 44 + rng.randint(0, 28)
        draw.ellipse((bx - radius, by - radius, bx + radius, by + radius), outline=(82, 194, 207, alpha), width=2)
        draw.ellipse((bx - radius // 2, by - radius // 2, bx, by), fill=(255, 255, 255, alpha // 3))

    overlay.putalpha(ImageChops.multiply(overlay.getchannel("A"), mask))
    return Image.alpha_composite(frame, overlay)


def downsample(image: Image.Image) -> Image.Image:
    return image.resize((OUTPUT_SIZE, OUTPUT_SIZE), Image.Resampling.LANCZOS)


def save_card_assets(template: Image.Image, card_box: tuple[int, int, int, int], suit: str, rank: str) -> None:
    card = compose_card(template, card_box, suit, rank)
    out_dir = BASE_DIR / suit / rank / "processed"
    frames_dir = out_dir / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)

    clean = downsample(card)
    clean.save(out_dir / "clean.png")
    clean.save(out_dir / "raw.png")

    frames = []
    seed = sum(ord(c) for c in f"{suit}-{rank}")
    for index in range(FRAME_COUNT):
        frame = downsample(effect_frame(card, index, seed))
        frame_path = frames_dir / f"frame-{index + 1:03d}.png"
        frame.save(frame_path)
        frames.append(frame)

    sheet = Image.new("RGBA", (OUTPUT_SIZE * 2, OUTPUT_SIZE * 2), (0, 0, 0, 0))
    strip = Image.new("RGBA", (OUTPUT_SIZE * FRAME_COUNT, OUTPUT_SIZE), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, ((index % 2) * OUTPUT_SIZE, (index // 2) * OUTPUT_SIZE))
        strip.alpha_composite(frame, (index * OUTPUT_SIZE, 0))
    sheet.save(out_dir / "sheet-transparent.png")
    strip.save(out_dir / "strip-transparent.png")

    gif_frames = [frame.convert("RGBA") for frame in frames]
    gif_frames[0].save(
        out_dir / "animation.gif",
        save_all=True,
        append_images=gif_frames[1:],
        duration=160,
        loop=0,
        disposal=2,
    )

    meta = {
        "target": "asset",
        "mode": "single",
        "style": "ocean_animated_deck",
        "source_template": str(TEMPLATE_RAW.relative_to(BASE_DIR)),
        "suit": suit,
        "rank": rank,
        "frames": FRAME_COUNT,
        "single_size": OUTPUT_SIZE,
    }
    (out_dir / "pipeline-meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")


def build_previews() -> None:
    cell = OUTPUT_SIZE
    ranks = RANKS
    suits = list(SUITS.keys())
    for frame_index in range(FRAME_COUNT):
        preview = Image.new("RGBA", (cell * len(ranks), cell * len(suits)), (0, 0, 0, 0))
        for row, suit in enumerate(suits):
            for col, rank in enumerate(ranks):
                frame = Image.open(BASE_DIR / suit / rank / "processed" / "frames" / f"frame-{frame_index + 1:03d}.png").convert("RGBA")
                preview.alpha_composite(frame, (col * cell, row * cell))
        preview.save(BASE_DIR / f"deck-preview-frame-{frame_index + 1:03d}.png")

    gif_frames = [
        Image.open(BASE_DIR / f"deck-preview-frame-{index + 1:03d}.png").convert("RGBA")
        for index in range(FRAME_COUNT)
    ]
    gif_frames[0].save(
        BASE_DIR / "deck-preview-animation.gif",
        save_all=True,
        append_images=gif_frames[1:],
        duration=160,
        loop=0,
        disposal=2,
    )


def main() -> None:
    template, card_box = prepare_template()
    template.save(BASE_DIR / "source_assets" / "card_template_clean.png")
    for suit in SUITS:
        for rank in RANKS:
            save_card_assets(template, card_box, suit, rank)
    build_previews()


if __name__ == "__main__":
    main()
