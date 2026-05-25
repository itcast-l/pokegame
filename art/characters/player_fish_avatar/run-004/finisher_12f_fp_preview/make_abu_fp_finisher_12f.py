from pathlib import Path
import math

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame")
OUT = ROOT / "art/characters/player_fish_avatar/run-004/finisher_12f_fp_preview"
AVATAR = ROOT / "art/characters/player_fish_avatar/run-001"
BG = ROOT / "art/maps/underwater_card_table_no_deck_pads_v1_1920x1080.png"
BEAM = ROOT / "art/fx/fish_attack_burst/run-001/processed"
BUBBLE = ROOT / "art/fx/attack_bubble_burst/run-001/processed"
ORBS = ROOT / "art/fx/score_fill_orbs/run-001/processed"

SIZE = (1536, 864)
SOURCE = (1210, 228)
TARGET = (792, 792)


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def cover(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    src = img.convert("RGBA")
    scale = max(size[0] / src.width, size[1] / src.height)
    resized = src.resize((int(src.width * scale), int(src.height * scale)), Image.Resampling.LANCZOS)
    left = (resized.width - size[0]) // 2
    top = (resized.height - size[1]) // 2
    return resized.crop((left, top, left + size[0], top + size[1]))


def with_alpha(img: Image.Image, alpha: int) -> Image.Image:
    out = img.copy()
    a = out.getchannel("A").point(lambda p: int(p * alpha / 255))
    out.putalpha(a)
    return out


def screen_blend(base: Image.Image, layer: Image.Image) -> Image.Image:
    rgb = ImageChops.screen(base.convert("RGB"), layer.convert("RGB")).convert("RGBA")
    rgb.putalpha(ImageChops.lighter(base.getchannel("A"), layer.getchannel("A")))
    return rgb


def centered(layer: Image.Image, center: tuple[int, int]) -> tuple[Image.Image, tuple[int, int]]:
    return layer, (int(center[0] - layer.width / 2), int(center[1] - layer.height / 2))


def put_alpha(base: Image.Image, layer: Image.Image, center: tuple[int, int]) -> None:
    img, pos = centered(layer, center)
    base.alpha_composite(img, pos)


def put_screen(base: Image.Image, layer: Image.Image, center: tuple[int, int]) -> Image.Image:
    canvas = Image.new("RGBA", base.size, (0, 0, 0, 0))
    img, pos = centered(layer, center)
    canvas.alpha_composite(img, pos)
    return screen_blend(base, canvas)


def scale_rotate(img: Image.Image, scale_xy: tuple[float, float], rotation_deg: float, mirror: bool = False) -> Image.Image:
    out = ImageOps.mirror(img) if mirror else img
    w = max(1, int(out.width * scale_xy[0]))
    h = max(1, int(out.height * scale_xy[1]))
    out = out.resize((w, h), Image.Resampling.LANCZOS)
    if rotation_deg:
        out = out.rotate(rotation_deg, resample=Image.Resampling.BICUBIC, expand=True)
    return out


def beam_to_target(frame_idx: int, travel_t: float, power_t: float) -> tuple[Image.Image, tuple[int, int]]:
    beam = load(BEAM / f"fx-{min(4, frame_idx)}.png")
    vec_x = TARGET[0] - SOURCE[0]
    vec_y = TARGET[1] - SOURCE[1]
    distance = math.hypot(vec_x, vec_y)
    angle = math.degrees(math.atan2(vec_y, vec_x) - math.pi)
    width_scale = (distance / beam.width) * (0.36 + travel_t * 0.96)
    height_scale = 0.58 + power_t * 1.10
    beam = scale_rotate(beam, (width_scale, height_scale), angle)
    center = (
        int(SOURCE[0] + vec_x * (0.28 + travel_t * 0.31)),
        int(SOURCE[1] + vec_y * (0.28 + travel_t * 0.31)),
    )
    return beam, center


def glow_for(img: Image.Image, color: tuple[int, int, int, int], blur: int) -> Image.Image:
    glow = Image.new("RGBA", img.size, color)
    glow.putalpha(img.getchannel("A").filter(ImageFilter.GaussianBlur(blur)))
    return glow


def fish_sprite(action: str, idx: int, scale: float, rotation: float) -> Image.Image:
    sprite = load(AVATAR / f"{action}/processed/{action}-{idx}.png")
    sprite = scale_rotate(sprite, (scale, scale), rotation)
    return sprite


def background(frame_idx: int) -> Image.Image:
    bg = cover(load(BG), SIZE)
    zoom = 1.0 + frame_idx * 0.012 + (0.02 if frame_idx >= 7 else 0.0)
    if zoom != 1.0:
        scaled = bg.resize((int(bg.width * zoom), int(bg.height * zoom)), Image.Resampling.LANCZOS)
        shake_x = {7: 14, 8: -16, 9: 10, 10: -6}.get(frame_idx, 0)
        shake_y = {7: -8, 8: 12, 9: -10, 10: 6}.get(frame_idx, 0)
        left = min(max(0, 150 + frame_idx * 12 + shake_x), scaled.width - SIZE[0])
        top = min(max(0, 35 + frame_idx * 5 + shake_y), scaled.height - SIZE[1])
        bg = scaled.crop((left, top, left + SIZE[0], top + SIZE[1]))

    tint = Image.new("RGBA", SIZE, (8, 30, 55, 110))
    bg.alpha_composite(tint)

    vignette = Image.new("L", SIZE, 255)
    px = vignette.load()
    cx, cy = SIZE[0] / 2, SIZE[1] / 2
    for y in range(SIZE[1]):
        for x in range(SIZE[0]):
            dx = abs(x - cx) / cx
            dy = abs(y - cy) / cy
            d = min(1.0, math.sqrt(dx * dx + dy * dy))
            px[x, y] = int(255 * (1.0 - 0.38 * d))
    bg.putalpha(vignette)
    return bg


def charge_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    orb = load(BUBBLE / f"fx-{frame_idx + 1}.png")
    ring = load(ORBS / f"fx-{min(4, frame_idx + 2)}.png")
    fish = fish_sprite("attack", max(1, frame_idx), 1.55 + frame_idx * 0.05, 8 - frame_idx * 2)
    aim = scale_rotate(load(ORBS / "fx-1.png"), (0.45 + frame_idx * 0.08, 0.45 + frame_idx * 0.08), 0)

    base = put_screen(base, scale_rotate(orb, (1.0 + frame_idx * 0.14, 1.0 + frame_idx * 0.14), 0), SOURCE)
    base = put_screen(base, scale_rotate(ring, (0.95 + frame_idx * 0.16, 0.95 + frame_idx * 0.16), 0), SOURCE)
    base = put_screen(base, with_alpha(aim, 72 + frame_idx * 18), TARGET)
    put_alpha(base, glow_for(fish, (150, 230, 255, 130), 20), SOURCE)
    put_alpha(base, fish, SOURCE)
    return base


def fire_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    local = frame_idx - 3
    fish = fish_sprite("attack", min(4, local + 1), 1.70 + local * 0.04, -6 - local * 2)
    beam, beam_center = beam_to_target(min(4, local + 1), 0.18 + local * 0.22, 0.32 + local * 0.22)
    muzzle = scale_rotate(load(BUBBLE / f"fx-{min(4, local + 1)}.png"), (0.82 + local * 0.08, 0.82 + local * 0.08), 0)
    target_fx = scale_rotate(load(BUBBLE / f"fx-{min(4, local + 1)}.png"), (0.75 + local * 0.26, 0.75 + local * 0.26), 0)
    target_glint = scale_rotate(load(ORBS / f"fx-{min(4, local + 1)}.png"), (0.85 + local * 0.22, 0.85 + local * 0.22), 0)

    base = put_screen(base, beam, beam_center)
    base = put_screen(base, muzzle, SOURCE)
    base = put_screen(base, with_alpha(target_fx, 150 + local * 18), TARGET)
    base = put_screen(base, with_alpha(target_glint, 110 + local * 16), (TARGET[0] - 12, TARGET[1] - 10))
    put_alpha(base, fish, SOURCE)
    return base


def impact_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    local = frame_idx - 7
    fish = fish_sprite("victory", min(4, local + 1), 1.62 - local * 0.04, -10 + local * 2)
    beam, beam_center = beam_to_target(4, 0.96, 1.40)
    target_ring = scale_rotate(load(BUBBLE / "fx-4.png"), (2.65 + local * 0.34, 2.65 + local * 0.34), 0)
    target_orb = scale_rotate(load(ORBS / "fx-4.png"), (2.35 + local * 0.32, 2.35 + local * 0.32), 0)
    target_wash = scale_rotate(load(BUBBLE / "fx-4.png"), (3.45 + local * 0.38, 3.45 + local * 0.38), 0)
    spray = scale_rotate(load(BEAM / "fx-4.png"), (1.40 + local * 0.18, 1.55 + local * 0.15), 18)

    base = put_screen(base, beam, beam_center)
    base = put_screen(base, with_alpha(target_ring, 165 - local * 12), TARGET)
    base = put_screen(base, with_alpha(target_orb, 155 - local * 10), (TARGET[0] - 10, TARGET[1] - 18))
    base = put_screen(base, with_alpha(target_wash, 120 - local * 8), (TARGET[0] - 18, TARGET[1] + 10))
    base = put_screen(base, with_alpha(spray, 140 - local * 16), (TARGET[0] + 150, TARGET[1] - 120))

    splash = Image.new("RGBA", SIZE, (255, 255, 255, 0))
    splash_alpha = [48, 90, 118, 74][local]
    splash.putalpha(splash_alpha)
    base.alpha_composite(splash)

    droplets = scale_rotate(load(ORBS / f"fx-{max(1, 4 - local)}.png"), (3.6 + local * 0.24, 3.6 + local * 0.24), -14)
    lens_left = scale_rotate(load(BUBBLE / f"fx-{max(1, 3 - local)}.png"), (1.05 + local * 0.10, 1.05 + local * 0.10), 0)
    lens_right = scale_rotate(load(ORBS / f"fx-{max(1, 2 - local)}.png"), (0.95 + local * 0.10, 0.95 + local * 0.10), 0)
    base = put_screen(base, with_alpha(droplets, 158 - local * 18), (TARGET[0] - 14, TARGET[1] - 24))
    base = put_screen(base, with_alpha(lens_left, 108 - local * 12), (208, 734))
    base = put_screen(base, with_alpha(lens_right, 96 - local * 10), (1296, 708))
    put_alpha(base, fish, (SOURCE[0] + local * 8, SOURCE[1] + local * 3))
    return base


def aftermath_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    local = frame_idx - 10
    fish = fish_sprite("idle", 1, 1.85 - local * 0.05, -2 + local * 2)
    lens_orb = scale_rotate(load(BUBBLE / f"fx-{max(1, 3 - local)}.png"), (1.35 - local * 0.12, 1.35 - local * 0.12), 0)
    residue = scale_rotate(load(ORBS / f"fx-{max(1, 2 - local)}.png"), (1.0 - local * 0.10, 1.0 - local * 0.10), 0)

    base = put_screen(base, lens_orb, (TARGET[0] - 46, TARGET[1] - 34))
    base = put_screen(base, residue, (TARGET[0] + 32, TARGET[1] - 112))
    fade = Image.new("RGBA", SIZE, (8, 24, 45, 54 + local * 22))
    base.alpha_composite(fade)
    put_alpha(base, fish, (SOURCE[0] - local * 10, SOURCE[1] + local * 8))
    return base


def build_frame(frame_idx: int) -> Image.Image:
    base = background(frame_idx)
    if frame_idx <= 2:
        return charge_phase(base, frame_idx)
    if frame_idx <= 6:
        return fire_phase(base, frame_idx)
    if frame_idx <= 10:
        return impact_phase(base, frame_idx)
    return aftermath_phase(base, frame_idx)


def contact_sheet(frames: list[Image.Image]) -> Image.Image:
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
        frame = build_frame(i)
        out = OUT / f"abu-fp-finisher-12f-{i + 1:02d}.png"
        frame.save(out)
        frames.append(frame)
        print(out)

    gif_path = OUT / "abu-fp-finisher-12f-preview.gif"
    frames[0].save(
        gif_path,
        save_all=True,
        append_images=frames[1:],
        duration=[180, 180, 170, 130, 120, 120, 130, 150, 170, 210, 260, 360],
        loop=0,
        disposal=2,
    )
    print(gif_path)

    sheet_path = OUT / "abu-fp-finisher-12f-contact-sheet.png"
    contact_sheet(frames).save(sheet_path)
    print(sheet_path)


if __name__ == "__main__":
    main()
