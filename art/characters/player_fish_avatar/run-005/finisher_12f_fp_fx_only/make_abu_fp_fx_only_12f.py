from pathlib import Path
import math

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame")
OUT = ROOT / "art/characters/player_fish_avatar/run-005/finisher_12f_fp_fx_only"
BG = ROOT / "art/maps/underwater_card_table_no_deck_pads_v1_1920x1080.png"
BEAM = ROOT / "art/fx/fish_attack_burst/run-001/processed"
BUBBLE = ROOT / "art/fx/attack_bubble_burst/run-001/processed"
ORBS = ROOT / "art/fx/score_fill_orbs/run-001/processed"

SIZE = (1536, 864)
ORIGIN = (780, 126)
IMPACT = (768, 804)


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def cover(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    src = img.convert("RGBA")
    scale = max(size[0] / src.width, size[1] / src.height)
    resized = src.resize((int(src.width * scale), int(src.height * scale)), Image.Resampling.LANCZOS)
    left = (resized.width - size[0]) // 2
    top = (resized.height - size[1]) // 2
    return resized.crop((left, top, left + size[0], top + size[1]))


def centered(layer: Image.Image, center: tuple[int, int]) -> tuple[Image.Image, tuple[int, int]]:
    return layer, (int(center[0] - layer.width / 2), int(center[1] - layer.height / 2))


def with_alpha(img: Image.Image, alpha: int) -> Image.Image:
    out = img.copy()
    a = out.getchannel("A").point(lambda p: int(p * alpha / 255))
    out.putalpha(a)
    return out


def screen_blend(base: Image.Image, layer: Image.Image) -> Image.Image:
    rgb = ImageChops.screen(base.convert("RGB"), layer.convert("RGB")).convert("RGBA")
    rgb.putalpha(ImageChops.lighter(base.getchannel("A"), layer.getchannel("A")))
    return rgb


def put_alpha(base: Image.Image, layer: Image.Image, center: tuple[int, int]) -> None:
    img, pos = centered(layer, center)
    base.alpha_composite(img, pos)


def put_screen(base: Image.Image, layer: Image.Image, center: tuple[int, int]) -> Image.Image:
    canvas = Image.new("RGBA", base.size, (0, 0, 0, 0))
    img, pos = centered(layer, center)
    canvas.alpha_composite(img, pos)
    return screen_blend(base, canvas)


def scale_rotate(img: Image.Image, scale_xy: tuple[float, float], rotation_deg: float) -> Image.Image:
    w = max(1, int(img.width * scale_xy[0]))
    h = max(1, int(img.height * scale_xy[1]))
    out = img.resize((w, h), Image.Resampling.LANCZOS)
    if rotation_deg:
        out = out.rotate(rotation_deg, resample=Image.Resampling.BICUBIC, expand=True)
    return out


def glow_for(img: Image.Image, color: tuple[int, int, int, int], blur: int) -> Image.Image:
    glow = Image.new("RGBA", img.size, color)
    glow.putalpha(img.getchannel("A").filter(ImageFilter.GaussianBlur(blur)))
    return glow


def background(frame_idx: int) -> Image.Image:
    bg = cover(load(BG), SIZE)
    zoom = 1.0 + frame_idx * 0.01 + (0.03 if frame_idx >= 7 else 0.0)
    scaled = bg.resize((int(bg.width * zoom), int(bg.height * zoom)), Image.Resampling.LANCZOS)
    shake_x = {7: 10, 8: -14, 9: 12, 10: -8}.get(frame_idx, 0)
    shake_y = {7: -10, 8: 14, 9: -8, 10: 6}.get(frame_idx, 0)
    left = min(max(0, 190 + frame_idx * 8 + shake_x), scaled.width - SIZE[0])
    top = min(max(0, 30 + frame_idx * 4 + shake_y), scaled.height - SIZE[1])
    bg = scaled.crop((left, top, left + SIZE[0], top + SIZE[1]))

    tint = Image.new("RGBA", SIZE, (10, 30, 54, 112))
    bg.alpha_composite(tint)

    haze = Image.new("RGBA", SIZE, (180, 235, 255, 0))
    if frame_idx >= 8:
        haze.putalpha(22 + (frame_idx - 8) * 12)
        bg.alpha_composite(haze)

    return bg


def beam_center(progress: float) -> tuple[int, int]:
    return (
        int(ORIGIN[0] + (IMPACT[0] - ORIGIN[0]) * progress),
        int(ORIGIN[1] + (IMPACT[1] - ORIGIN[1]) * progress),
    )


def beam_toward_camera(frame_idx: int, progress: float, thickness: float) -> tuple[Image.Image, tuple[int, int]]:
    beam = load(BEAM / f"fx-{min(4, frame_idx)}.png")
    vec_x = IMPACT[0] - ORIGIN[0]
    vec_y = IMPACT[1] - ORIGIN[1]
    distance = math.hypot(vec_x, vec_y)
    angle = math.degrees(math.atan2(vec_y, vec_x) - math.pi)
    length_scale = (distance / beam.width) * (0.12 + progress * 0.72)
    width_scale = 0.16 + thickness
    beam = scale_rotate(beam, (length_scale, width_scale), angle)
    return beam, beam_center(0.18 + progress * 0.42)


def charge_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    idx = min(4, frame_idx + 1)
    orb = scale_rotate(load(BUBBLE / f"fx-{idx}.png"), (0.28 + frame_idx * 0.08, 0.28 + frame_idx * 0.08), 0)
    core = scale_rotate(load(ORBS / f"fx-{idx}.png"), (0.22 + frame_idx * 0.06, 0.22 + frame_idx * 0.06), 0)
    guide = scale_rotate(load(BEAM / "fx-1.png"), (0.12 + frame_idx * 0.05, 0.08 + frame_idx * 0.02), 90)

    base = put_screen(base, with_alpha(orb, 180), ORIGIN)
    base = put_screen(base, with_alpha(core, 160), ORIGIN)
    base = put_screen(base, with_alpha(guide, 90 + frame_idx * 25), (ORIGIN[0], ORIGIN[1] + 78 + frame_idx * 16))
    return base


def travel_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    local = frame_idx - 3
    beam, center = beam_toward_camera(local + 1, 0.12 + local * 0.22, 0.18 + local * 0.20)
    origin_burst = scale_rotate(load(BUBBLE / f"fx-{min(4, local + 1)}.png"), (0.34 + local * 0.06, 0.34 + local * 0.06), 0)
    trail = scale_rotate(load(ORBS / f"fx-{min(4, local + 1)}.png"), (0.20 + local * 0.10, 0.20 + local * 0.10), 0)
    front_hint = scale_rotate(load(BUBBLE / f"fx-{min(4, local + 1)}.png"), (0.48 + local * 0.15, 0.48 + local * 0.15), 0)
    front_center = beam_center(0.44 + local * 0.10)

    base = put_screen(base, beam, center)
    base = put_screen(base, with_alpha(origin_burst, 175), ORIGIN)
    base = put_screen(base, with_alpha(trail, 120), front_center)
    base = put_screen(base, with_alpha(front_hint, 96 + local * 24), (front_center[0], front_center[1] + 32))
    return base


def impact_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    local = frame_idx - 7
    beam, center = beam_toward_camera(4, 0.96, 0.92 + local * 0.14)
    hit_core = scale_rotate(load(BUBBLE / "fx-4.png"), (2.0 + local * 0.30, 2.0 + local * 0.30), 0)
    hit_ring = scale_rotate(load(ORBS / "fx-4.png"), (1.9 + local * 0.34, 1.9 + local * 0.34), 0)
    spill = scale_rotate(load(BEAM / "fx-4.png"), (1.45 + local * 0.24, 1.18 + local * 0.14), 90)
    lens_left = scale_rotate(load(BUBBLE / f"fx-{max(1, 3 - local)}.png"), (0.85 + local * 0.10, 0.85 + local * 0.10), 0)
    lens_right = scale_rotate(load(ORBS / f"fx-{max(1, 2 - local)}.png"), (0.70 + local * 0.12, 0.70 + local * 0.12), 0)

    base = put_screen(base, beam, center)
    base = put_screen(base, with_alpha(hit_core, 170 - local * 10), IMPACT)
    base = put_screen(base, with_alpha(hit_ring, 150 - local * 8), (IMPACT[0], IMPACT[1] - 18))
    base = put_screen(base, with_alpha(spill, 145 - local * 14), (IMPACT[0], IMPACT[1] - 120))
    base = put_screen(base, with_alpha(lens_left, 105 - local * 12), (224, 740))
    base = put_screen(base, with_alpha(lens_right, 95 - local * 10), (1308, 728))

    flash = Image.new("RGBA", SIZE, (255, 255, 255, 0))
    flash.putalpha([42, 78, 102, 70][local])
    base.alpha_composite(flash)

    return base


def aftermath_phase(base: Image.Image, frame_idx: int) -> Image.Image:
    local = frame_idx - 10
    residue = scale_rotate(load(BUBBLE / f"fx-{max(1, 3 - local)}.png"), (1.22 - local * 0.08, 1.22 - local * 0.08), 0)
    core = scale_rotate(load(ORBS / f"fx-{max(1, 2 - local)}.png"), (0.92 - local * 0.08, 0.92 - local * 0.08), 0)
    streak = scale_rotate(load(BEAM / f"fx-{max(1, 2 - local)}.png"), (0.60 - local * 0.10, 0.34 - local * 0.04), 90)
    fade = Image.new("RGBA", SIZE, (8, 22, 40, 58 + local * 24))

    base = put_screen(base, with_alpha(residue, 135 - local * 18), IMPACT)
    base = put_screen(base, with_alpha(core, 110 - local * 16), (IMPACT[0], IMPACT[1] - 26))
    base = put_screen(base, with_alpha(streak, 75 - local * 15), (IMPACT[0], IMPACT[1] - 180))
    base.alpha_composite(fade)
    return base


def build_frame(frame_idx: int) -> Image.Image:
    base = background(frame_idx)
    if frame_idx <= 2:
        return charge_phase(base, frame_idx)
    if frame_idx <= 6:
        return travel_phase(base, frame_idx)
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
        out = OUT / f"abu-fp-fx-only-12f-{i + 1:02d}.png"
        frame.save(out)
        frames.append(frame)
        print(out)

    gif_path = OUT / "abu-fp-fx-only-12f-preview.gif"
    frames[0].save(
        gif_path,
        save_all=True,
        append_images=frames[1:],
        duration=[180, 180, 170, 120, 120, 120, 140, 150, 170, 200, 240, 320],
        loop=0,
        disposal=2,
    )
    print(gif_path)

    sheet_path = OUT / "abu-fp-fx-only-12f-contact-sheet.png"
    contact_sheet(frames).save(sheet_path)
    print(sheet_path)


if __name__ == "__main__":
    main()
