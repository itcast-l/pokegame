from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame")
OUT = ROOT / "art/characters/player_fish_avatar/run-003/finisher_12f_preview"
SRC = ROOT / "art/characters/player_fish_avatar/run-001"
BG = ROOT / "art/maps/underwater_card_table_no_deck_pads_v1_1920x1080.png"
ORB = ROOT / "art/fx/score_fill_orbs/run-001/processed"
RING = ROOT / "art/fx/attack_bubble_burst/run-001/processed"
BEAM = ROOT / "art/fx/fish_attack_burst/run-001/processed"

SIZE = (1536, 864)


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def fit_cover(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    src = img.convert("RGBA")
    scale = max(size[0] / src.width, size[1] / src.height)
    resized = src.resize((int(src.width * scale), int(src.height * scale)), Image.Resampling.LANCZOS)
    left = max(0, (resized.width - size[0]) // 2)
    top = max(0, (resized.height - size[1]) // 2)
    return resized.crop((left, top, left + size[0], top + size[1]))


def alpha_glow(img: Image.Image, color: tuple[int, int, int, int], blur: int) -> Image.Image:
    glow = Image.new("RGBA", img.size, color)
    glow.putalpha(img.getchannel("A").filter(ImageFilter.GaussianBlur(blur)))
    return glow


def place(base: Image.Image, layer: Image.Image, center: tuple[int, int], scale: float = 1.0, rotation: float = 0.0, mirror: bool = False) -> Image.Image:
    img = layer
    if mirror:
        img = ImageOps.mirror(img)
    if scale != 1.0:
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.Resampling.LANCZOS)
    if rotation:
        img = img.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
    x = int(center[0] - img.width / 2)
    y = int(center[1] - img.height / 2)
    base.alpha_composite(img, (x, y))
    return base


def layer_screen(base: Image.Image, layer: Image.Image, center: tuple[int, int], scale: float = 1.0, rotation: float = 0.0, mirror: bool = False) -> Image.Image:
    img = layer
    if mirror:
        img = ImageOps.mirror(img)
    if scale != 1.0:
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.Resampling.LANCZOS)
    if rotation:
        img = img.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
    x = int(center[0] - img.width / 2)
    y = int(center[1] - img.height / 2)
    layer_img = Image.new("RGBA", base.size, (0, 0, 0, 0))
    layer_img.alpha_composite(img, (x, y))
    screened = ImageChops.screen(base.convert("RGB"), layer_img.convert("RGB")).convert("RGBA")
    screened.putalpha(ImageChops.lighter(base.getchannel("A"), layer_img.getchannel("A")))
    return screened


def vignette(base: Image.Image, strength: float) -> Image.Image:
    mask = Image.new("L", base.size, 255)
    px = mask.load()
    cx = base.width / 2
    cy = base.height / 2
    for y in range(base.height):
        for x in range(base.width):
            dx = abs(x - cx) / cx
            dy = abs(y - cy) / cy
            d = (dx * dx + dy * dy) ** 0.5
            px[x, y] = max(0, min(255, int(255 * (1.0 - strength * max(0.0, d - 0.1)))))
    base.putalpha(mask)
    return base


def build_background(frame_index: int) -> Image.Image:
    bg = fit_cover(load(BG), SIZE)
    zoom = 1.0 + frame_index * 0.006
    if zoom != 1.0:
        scaled = bg.resize((int(bg.width * zoom), int(bg.height * zoom)), Image.Resampling.LANCZOS)
        left = (scaled.width - SIZE[0]) // 2
        top = (scaled.height - SIZE[1]) // 2
        bg = scaled.crop((left, top, left + SIZE[0], top + SIZE[1]))

    tint = Image.new("RGBA", SIZE, (5, 25, 45, 0))
    tint_alpha = [110, 108, 106, 103, 98, 92, 86, 80, 74, 68, 62, 56][frame_index]
    tint.putalpha(tint_alpha)
    bg.alpha_composite(tint)
    return vignette(bg, 0.42 + frame_index * 0.01)


def avatar(action: str, index: int) -> Image.Image:
    return load(SRC / f"{action}/processed/{action}-{index}.png")


def fx(path_root: Path, index: int) -> Image.Image:
    return load(path_root / f"fx-{index}.png")


def make_frame(frame_index: int) -> Image.Image:
    bg = build_background(frame_index)
    draw = ImageDraw.Draw(bg)

    fish_right = (1090, 520)
    impact_left = (525, 560)
    orb_center = (1040, 520)

    if frame_index <= 2:
        orb = fx(ORB, frame_index + 1)
        ring = fx(RING, frame_index + 1)
        bg = layer_screen(bg, orb, orb_center, scale=1.55 + frame_index * 0.08)
        bg = layer_screen(bg, ring, orb_center, scale=1.55 + frame_index * 0.08)
        fish = avatar("idle", 1 if frame_index == 0 else 1)
        glow = alpha_glow(fish.resize((260, 260), Image.Resampling.LANCZOS), (140, 230, 255, 120), 22)
        bg.alpha_composite(glow, (fish_right[0] - glow.width // 2, fish_right[1] - glow.height // 2))
        bg = place(bg, fish, fish_right, scale=2.02 + frame_index * 0.03)
        if frame_index == 2:
            bg = place(bg, avatar("attack", 1), fish_right, scale=1.45)
    elif frame_index <= 5:
        beam_idx = frame_index - 2
        bg = layer_screen(bg, fx(BEAM, beam_idx), impact_left, scale=1.60 + frame_index * 0.06)
        bg = layer_screen(bg, fx(RING, min(4, beam_idx + 1)), impact_left, scale=1.25 + frame_index * 0.03)
        bg = place(bg, avatar("attack", beam_idx), fish_right, scale=1.58 + beam_idx * 0.05, rotation=-3 + beam_idx * 1.3)
        bg = layer_screen(bg, fx(ORB, min(4, beam_idx + 1)), fish_right, scale=1.0 + beam_idx * 0.06)
    elif frame_index <= 8:
        beam_idx = 4
        impact_idx = frame_index - 5
        bg = layer_screen(bg, fx(BEAM, beam_idx), impact_left, scale=1.76 + impact_idx * 0.05)
        bg = layer_screen(bg, fx(RING, impact_idx), impact_left, scale=1.34 + impact_idx * 0.05)
        bg = layer_screen(bg, fx(ORB, 4 - impact_idx), fish_right, scale=1.08)
        flash = Image.new("RGBA", SIZE, (255, 250, 255, 0))
        flash.putalpha(48 + impact_idx * 18)
        bg.alpha_composite(flash)
        bg = place(bg, avatar("attack", 4), fish_right, scale=1.68, rotation=1.0)
    else:
        victory_idx = frame_index - 8
        bg = layer_screen(bg, fx(RING, max(1, 4 - victory_idx)), impact_left, scale=1.0 + victory_idx * 0.02)
        bg = layer_screen(bg, fx(ORB, max(1, 4 - victory_idx)), fish_right, scale=0.8 + victory_idx * 0.04)
        fish = avatar("victory", min(4, victory_idx + 1))
        bg = place(bg, fish, fish_right, scale=1.85 - victory_idx * 0.03)
        tail_glow = alpha_glow(fish.resize((240, 240), Image.Resampling.LANCZOS), (170, 232, 255, 110), 18)
        bg.alpha_composite(tail_glow, (fish_right[0] - tail_glow.width // 2, fish_right[1] - tail_glow.height // 2))
        if frame_index >= 10:
            fade = Image.new("RGBA", SIZE, (2, 16, 35, 0))
            fade.putalpha(18 + (frame_index - 9) * 12)
            bg.alpha_composite(fade)

    return bg


def make_contact_sheet(frames: list[Image.Image]) -> Image.Image:
    thumb_w = 384
    thumb_h = 216
    margin = 18
    canvas = Image.new("RGBA", (thumb_w * 4 + margin * 5, thumb_h * 3 + margin * 4), (5, 17, 32, 255))
    font = ImageFont.load_default()
    for idx, frame in enumerate(frames):
        thumb = ImageOps.contain(frame, (thumb_w, thumb_h), Image.Resampling.LANCZOS)
        panel = Image.new("RGBA", (thumb_w, thumb_h), (11, 35, 64, 255))
        panel.alpha_composite(thumb, ((thumb_w - thumb.width) // 2, (thumb_h - thumb.height) // 2))
        draw = ImageDraw.Draw(panel)
        draw.rounded_rectangle((0, 0, thumb_w - 1, thumb_h - 1), radius=16, outline=(177, 223, 255, 255), width=3)
        draw.text((10, 8), f"{idx + 1:02d}", fill=(244, 248, 252, 255), font=font)
        x = margin + (idx % 4) * (thumb_w + margin)
        y = margin + (idx // 4) * (thumb_h + margin)
        canvas.alpha_composite(panel, (x, y))
    return canvas


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    frames: list[Image.Image] = []
    for i in range(12):
        frame = make_frame(i)
        out = OUT / f"abu-finisher-12f-{i + 1:02d}.png"
        frame.save(out)
        frames.append(frame)
        print(out)

    gif_path = OUT / "abu-finisher-12f-preview.gif"
    frames[0].save(
        gif_path,
        save_all=True,
        append_images=frames[1:],
        duration=[240, 240, 220, 180, 160, 160, 170, 190, 220, 260, 320, 520],
        loop=0,
        disposal=2,
    )
    print(gif_path)

    sheet = make_contact_sheet(frames)
    sheet_path = OUT / "abu-finisher-12f-contact-sheet.png"
    sheet.save(sheet_path)
    print(sheet_path)


if __name__ == "__main__":
    main()
