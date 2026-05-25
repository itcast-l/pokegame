from pathlib import Path
import math

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame")
OUT = ROOT / "art/characters/player_fish_avatar/run-006/fp_fx_asset_preview"
BEAM = ROOT / "art/fx/fish_attack_burst/run-001/processed"
BUBBLE = ROOT / "art/fx/attack_bubble_burst/run-001/processed"
ORBS = ROOT / "art/fx/score_fill_orbs/run-001/processed"

SIZE = (1024, 1024)
ORIGIN = (512, 118)
IMPACT = (512, 836)
FRAME_COUNT = 20


def load(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def centered(layer: Image.Image, center: tuple[int, int]) -> tuple[Image.Image, tuple[int, int]]:
    return layer, (int(center[0] - layer.width / 2), int(center[1] - layer.height / 2))


def with_alpha(img: Image.Image, alpha: int) -> Image.Image:
    out = img.copy()
    a = out.getchannel("A").point(lambda p: int(p * alpha / 255))
    out.putalpha(a)
    return out


def put_alpha(base: Image.Image, layer: Image.Image, center: tuple[int, int]) -> None:
    img, pos = centered(layer, center)
    base.alpha_composite(img, pos)


def screen_blend(base: Image.Image, layer: Image.Image) -> Image.Image:
    rgb = ImageChops.screen(base.convert("RGB"), layer.convert("RGB")).convert("RGBA")
    rgb.putalpha(ImageChops.lighter(base.getchannel("A"), layer.getchannel("A")))
    return rgb


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


def blank() -> Image.Image:
    return Image.new("RGBA", SIZE, (0, 0, 0, 0))


def beam_center(progress: float) -> tuple[int, int]:
    return (
        int(ORIGIN[0] + (IMPACT[0] - ORIGIN[0]) * progress),
        int(ORIGIN[1] + (IMPACT[1] - ORIGIN[1]) * progress),
    )


def ease_out_cubic(t: float) -> float:
    return 1.0 - pow(1.0 - max(0.0, min(1.0, t)), 3)


def beam_toward_camera(progress: float, width_t: float, frame_pick: int) -> tuple[Image.Image, tuple[int, int]]:
    beam = load(BEAM / f"fx-{frame_pick}.png")
    vec_x = IMPACT[0] - ORIGIN[0]
    vec_y = IMPACT[1] - ORIGIN[1]
    distance = math.hypot(vec_x, vec_y)
    angle = math.degrees(math.atan2(vec_y, vec_x) - math.pi)
    length_scale = (distance / beam.width) * (0.10 + progress * 0.84)
    width_scale = 0.13 + width_t
    beam = scale_rotate(beam, (length_scale, width_scale), angle)
    return beam, beam_center(0.10 + progress * 0.40)


def charge_frame(local_t: float) -> Image.Image:
    frame = blank()
    idx = min(4, 1 + int(local_t * 3.2))
    orb = scale_rotate(load(BUBBLE / f"fx-{idx}.png"), (0.22 + local_t * 0.22, 0.22 + local_t * 0.22), 0)
    core = scale_rotate(load(ORBS / f"fx-{idx}.png"), (0.18 + local_t * 0.16, 0.18 + local_t * 0.16), 0)
    aim = scale_rotate(load(BEAM / "fx-1.png"), (0.08 + local_t * 0.12, 0.06 + local_t * 0.04), 90)

    frame = put_screen(frame, with_alpha(orb, 150 + int(local_t * 50)), ORIGIN)
    frame = put_screen(frame, with_alpha(core, 120 + int(local_t * 60)), ORIGIN)
    frame = put_screen(frame, with_alpha(aim, 60 + int(local_t * 70)), (ORIGIN[0], ORIGIN[1] + 84 + int(local_t * 40)))
    return frame


def travel_frame(local_t: float) -> Image.Image:
    frame = blank()
    t = ease_out_cubic(local_t)
    frame_pick = min(4, 1 + int(local_t * 3.8))
    beam, center = beam_toward_camera(t, 0.10 + t * 0.48, frame_pick)
    origin_burst = scale_rotate(load(BUBBLE / f"fx-{frame_pick}.png"), (0.26 + t * 0.14, 0.26 + t * 0.14), 0)
    travel_orb = scale_rotate(load(ORBS / f"fx-{frame_pick}.png"), (0.14 + t * 0.28, 0.14 + t * 0.28), 0)
    front_center = beam_center(0.22 + t * 0.56)

    frame = put_screen(frame, beam, center)
    frame = put_screen(frame, with_alpha(origin_burst, 160), ORIGIN)
    frame = put_screen(frame, with_alpha(travel_orb, 105 + int(t * 35)), front_center)
    return frame


def impact_frame(local_t: float) -> Image.Image:
    frame = blank()
    t = ease_out_cubic(local_t)
    beam, center = beam_toward_camera(0.98, 0.68 + t * 0.20, 4)
    hit_core = scale_rotate(load(BUBBLE / "fx-4.png"), (1.25 + t * 1.60, 1.25 + t * 1.60), 0)
    hit_ring = scale_rotate(load(ORBS / "fx-4.png"), (1.05 + t * 1.38, 1.05 + t * 1.38), 0)
    spill = scale_rotate(load(BEAM / "fx-4.png"), (0.90 + t * 0.74, 0.88 + t * 0.42), 90)
    splash_left = scale_rotate(load(BUBBLE / f"fx-{max(1, 3 - int(local_t * 2.4))}.png"), (0.46 + t * 0.40, 0.46 + t * 0.40), 0)
    splash_right = scale_rotate(load(ORBS / f"fx-{max(1, 2 - int(local_t * 1.8))}.png"), (0.34 + t * 0.36, 0.34 + t * 0.36), 0)

    frame = put_screen(frame, beam, center)
    frame = put_screen(frame, with_alpha(hit_core, 150 - int(t * 22)), IMPACT)
    frame = put_screen(frame, with_alpha(hit_ring, 136 - int(t * 18)), (IMPACT[0], IMPACT[1] - 14))
    frame = put_screen(frame, with_alpha(spill, 120 - int(t * 24)), (IMPACT[0], IMPACT[1] - 150))
    frame = put_screen(frame, with_alpha(splash_left, 98 - int(t * 18)), (180, 818))
    frame = put_screen(frame, with_alpha(splash_right, 88 - int(t * 16)), (844, 806))

    flash = Image.new("RGBA", SIZE, (255, 255, 255, int(24 + t * 42)))
    frame.alpha_composite(flash)
    return frame


def aftermath_frame(local_t: float) -> Image.Image:
    frame = blank()
    t = local_t
    residue = scale_rotate(load(BUBBLE / f"fx-{max(1, 3 - int(t * 2.0))}.png"), (1.12 - t * 0.26, 1.12 - t * 0.26), 0)
    core = scale_rotate(load(ORBS / f"fx-{max(1, 2 - int(t * 1.8))}.png"), (0.80 - t * 0.18, 0.80 - t * 0.18), 0)
    streak = scale_rotate(load(BEAM / f"fx-{max(1, 2 - int(t * 1.6))}.png"), (0.38 - t * 0.10, 0.24 - t * 0.06), 90)

    frame = put_screen(frame, with_alpha(residue, 108 - int(t * 42)), IMPACT)
    frame = put_screen(frame, with_alpha(core, 90 - int(t * 34)), (IMPACT[0], IMPACT[1] - 26))
    frame = put_screen(frame, with_alpha(streak, 70 - int(t * 30)), (IMPACT[0], IMPACT[1] - 166))
    return frame


def build_frame(frame_idx: int) -> Image.Image:
    if frame_idx <= 3:
        return charge_frame(frame_idx / 3.0)
    if frame_idx <= 11:
        return travel_frame((frame_idx - 4) / 7.0)
    if frame_idx <= 16:
        return impact_frame((frame_idx - 12) / 4.0)
    return aftermath_frame((frame_idx - 17) / 2.0)


def composite_preview(frame: Image.Image) -> Image.Image:
    bg = Image.new("RGBA", SIZE, (6, 28, 46, 255))
    bloom = frame.filter(ImageFilter.GaussianBlur(18))
    bg = put_screen(bg, with_alpha(bloom, 96), (SIZE[0] // 2, SIZE[1] // 2))
    bg.alpha_composite(frame)
    return bg


def contact_sheet(frames: list[Image.Image]) -> Image.Image:
    thumb_w = 256
    thumb_h = 256
    margin = 18
    canvas = Image.new("RGBA", (thumb_w * 5 + margin * 6, thumb_h * 4 + margin * 5), (5, 17, 32, 255))
    font = ImageFont.load_default()
    for idx, frame in enumerate(frames):
        preview = composite_preview(frame)
        thumb = ImageOps.contain(preview, (thumb_w, thumb_h), Image.Resampling.LANCZOS)
        panel = Image.new("RGBA", (thumb_w, thumb_h), (10, 36, 58, 255))
        panel.alpha_composite(thumb, ((thumb_w - thumb.width) // 2, (thumb_h - thumb.height) // 2))
        draw = ImageDraw.Draw(panel)
        draw.rounded_rectangle((0, 0, thumb_w - 1, thumb_h - 1), radius=16, outline=(177, 223, 255, 255), width=3)
        draw.text((10, 8), f"{idx + 1:02d}", fill=(244, 248, 252, 255), font=font)
        x = margin + (idx % 5) * (thumb_w + margin)
        y = margin + (idx // 5) * (thumb_h + margin)
        canvas.alpha_composite(panel, (x, y))
    return canvas


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    transparent_frames: list[Image.Image] = []
    preview_frames: list[Image.Image] = []
    for i in range(FRAME_COUNT):
        frame = build_frame(i)
        transparent_path = OUT / f"abu-fp-fx-asset-{i + 1:02d}.png"
        frame.save(transparent_path)
        print(transparent_path)
        transparent_frames.append(frame)
        preview_frames.append(composite_preview(frame))

    gif_path = OUT / "abu-fp-fx-asset-preview.gif"
    preview_frames[0].save(
        gif_path,
        save_all=True,
        append_images=preview_frames[1:],
        duration=[70] * FRAME_COUNT,
        loop=0,
        disposal=2,
    )
    print(gif_path)

    sheet_path = OUT / "abu-fp-fx-asset-contact-sheet.png"
    contact_sheet(transparent_frames).save(sheet_path)
    print(sheet_path)


if __name__ == "__main__":
    main()
