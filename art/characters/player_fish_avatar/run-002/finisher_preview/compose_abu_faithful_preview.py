from pathlib import Path

from PIL import Image, ImageChops, ImageFilter, ImageOps


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame")
OUT = ROOT / "art/characters/player_fish_avatar/run-002/finisher_preview/processed"
MAP_BG = ROOT / "art/maps/underwater_card_table_no_deck_pads_v1_1920x1080.png"
AVATAR = ROOT / "art/characters/player_fish_avatar/run-001"
SCORE_ORBS = ROOT / "art/fx/score_fill_orbs/run-001/processed"
BUBBLE_BURST = ROOT / "art/fx/attack_bubble_burst/run-001/processed"
FISH_BURST = ROOT / "art/fx/fish_attack_burst/run-001/processed"


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def fit(base: Image.Image, overlay: Image.Image, center: tuple[int, int], scale: float = 1.0, rotation: float = 0.0, flip: bool = False) -> tuple[Image.Image, tuple[int, int]]:
    img = overlay
    if flip:
        img = ImageOps.mirror(img)
    if scale != 1.0:
        img = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.Resampling.LANCZOS)
    if rotation:
        img = img.rotate(rotation, resample=Image.Resampling.BICUBIC, expand=True)
    pos = (int(center[0] - img.width / 2), int(center[1] - img.height / 2))
    return img, pos


def alpha_glow(img: Image.Image, color: tuple[int, int, int, int], blur: int) -> Image.Image:
    alpha = img.getchannel("A").filter(ImageFilter.GaussianBlur(blur))
    glow = Image.new("RGBA", img.size, color)
    glow.putalpha(alpha)
    return glow


def add_overlay(base: Image.Image, overlay: Image.Image, center: tuple[int, int], scale: float = 1.0, rotation: float = 0.0, flip: bool = False, mode: str = "alpha") -> None:
    img, pos = fit(base, overlay, center, scale, rotation, flip)
    if mode == "screen":
        layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
        layer.alpha_composite(img, pos)
        rgb = ImageChops.screen(base.convert("RGB"), layer.convert("RGB")).convert("RGBA")
        rgb.putalpha(base.getchannel("A"))
        base.paste(rgb)
        alpha = ImageChops.lighter(base.getchannel("A"), layer.getchannel("A"))
        base.putalpha(alpha)
    else:
        base.alpha_composite(img, pos)


def make_base() -> Image.Image:
    bg = load_rgba(MAP_BG)
    dark = Image.new("RGBA", bg.size, (6, 25, 55, 130))
    bg.alpha_composite(dark)
    vignette = Image.new("L", bg.size, 255)
    for y in range(bg.height):
        for x in range(bg.width):
            dx = abs(x - bg.width / 2) / (bg.width / 2)
            dy = abs(y - bg.height / 2) / (bg.height / 2)
            d = min(1.0, (dx * dx + dy * dy) ** 0.5)
            vignette.putpixel((x, y), int(255 * (1.0 - 0.42 * d)))
    bg.putalpha(vignette)
    return bg


def frame01() -> Image.Image:
    base = make_base()
    abu = load_rgba(AVATAR / "idle/processed/idle-1.png")
    orb = load_rgba(SCORE_ORBS / "fx-4.png")
    charge = load_rgba(BUBBLE_BURST / "fx-4.png")
    center = (760, 700)
    add_overlay(base, orb, center, scale=2.0, mode="screen")
    add_overlay(base, charge, center, scale=1.7, mode="screen")
    glow = alpha_glow(abu.resize((235, 235), Image.Resampling.LANCZOS), (150, 235, 255, 110), 24)
    gpos = (center[0] - glow.width // 2, center[1] - glow.height // 2)
    base.alpha_composite(glow, gpos)
    add_overlay(base, abu, center, scale=1.8)
    return base


def frame02() -> Image.Image:
    base = make_base()
    burst = load_rgba(FISH_BURST / "fx-4.png")
    abu = load_rgba(AVATAR / "attack/processed/attack-2.png")
    start = (550, 560)
    add_overlay(base, burst, (860, 580), scale=2.2, flip=False, mode="screen")
    add_overlay(base, burst, (720, 610), scale=1.8, flip=False, mode="screen")
    add_overlay(base, abu, start, scale=1.4, rotation=-18)
    return base


def frame03() -> Image.Image:
    base = make_base()
    impact = load_rgba(BUBBLE_BURST / "fx-4.png")
    fish_burst = load_rgba(FISH_BURST / "fx-4.png")
    abu = load_rgba(AVATAR / "hit/processed/hit-2.png")
    center = (980, 575)
    add_overlay(base, fish_burst, center, scale=2.6, mode="screen")
    add_overlay(base, impact, center, scale=2.0, mode="screen")
    glow = alpha_glow(abu.resize((245, 245), Image.Resampling.LANCZOS), (220, 245, 255, 180), 18)
    gpos = (center[0] - 85, center[1] - 10)
    base.alpha_composite(glow, gpos)
    add_overlay(base, abu, (930, 620), scale=1.7, rotation=-26)
    return base


def frame04() -> Image.Image:
    base = make_base()
    orb = load_rgba(SCORE_ORBS / "fx-1.png")
    ring = load_rgba(BUBBLE_BURST / "fx-4.png")
    abu = load_rgba(AVATAR / "defeat/processed/defeat-4.png")
    center = (880, 690)
    add_overlay(base, ring, center, scale=1.35, mode="screen")
    add_overlay(base, orb, (900, 650), scale=0.95, mode="screen")
    add_overlay(base, abu, center, scale=1.85)
    shadow = Image.new("RGBA", (250, 82), (0, 0, 0, 0))
    shadow_alpha = Image.new("L", shadow.size, 0)
    for y in range(shadow.height):
        for x in range(shadow.width):
            dx = (x - shadow.width / 2) / (shadow.width / 2)
            dy = (y - shadow.height / 2) / (shadow.height / 2)
            value = max(0, int(135 * (1 - min(1, dx * dx + dy * dy))))
            shadow_alpha.putpixel((x, y), value)
    shadow.putalpha(shadow_alpha.filter(ImageFilter.GaussianBlur(10)))
    base.alpha_composite(shadow, (center[0] - 125, center[1] + 45))
    return base


def export_contact_sheet(frames: list[Image.Image], path: Path) -> None:
    thumb_w = 768
    thumb_h = 512
    margin = 24
    canvas = Image.new("RGBA", (thumb_w * 2 + margin * 3, thumb_h * 2 + margin * 3), (5, 20, 38, 255))
    for idx, frame in enumerate(frames):
        thumb = ImageOps.contain(frame, (thumb_w, thumb_h), Image.Resampling.LANCZOS)
        panel = Image.new("RGBA", (thumb_w, thumb_h), (11, 39, 70, 255))
        panel.alpha_composite(thumb, ((thumb_w - thumb.width) // 2, (thumb_h - thumb.height) // 2))
        x = margin + (idx % 2) * (thumb_w + margin)
        y = margin + (idx // 2) * (thumb_h + margin)
        canvas.alpha_composite(panel, (x, y))
    canvas.save(path)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    builders = [frame01, frame02, frame03, frame04]
    frames: list[Image.Image] = []
    for idx, builder in enumerate(builders, start=1):
        img = builder()
        path = OUT / f"abu-faithful-frame-0{idx}.png"
        img.save(path)
        frames.append(img)
        print(path)

    gif_path = OUT / "abu-faithful-finisher-preview.gif"
    frames[0].save(
        gif_path,
        save_all=True,
        append_images=frames[1:],
        duration=[420, 260, 220, 700],
        loop=0,
        disposal=2,
    )
    print(gif_path)

    sheet_path = OUT / "abu-faithful-finisher-contact-sheet.png"
    export_contact_sheet(frames, sheet_path)
    print(sheet_path)


if __name__ == "__main__":
    main()
