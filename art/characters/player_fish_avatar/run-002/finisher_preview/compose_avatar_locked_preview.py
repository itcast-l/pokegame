from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageFilter, ImageOps


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame/art/characters/player_fish_avatar/run-002/finisher_preview")
RAW = ROOT / "raw"
OUT = ROOT / "processed"
SRC = Path("/Volumes/KIOXIA11/godotproject/pokegame/art/characters/player_fish_avatar/run-001")


@dataclass(frozen=True)
class FrameSpec:
    background: str
    sprite: str
    output: str
    center: tuple[int, int]
    scale: float
    rotation: float = 0.0
    flip: bool = False
    cleanup_offset: tuple[int, int] = (0, 0)
    cleanup_size: tuple[int, int] = (300, 220)
    glow_color: tuple[int, int, int, int] = (180, 230, 255, 170)
    glow_blur: int = 18
    shadow_color: tuple[int, int, int, int] = (8, 25, 40, 110)
    shadow_offset: tuple[int, int] = (0, 16)
    shadow_blur: int = 14


SPECS = [
    FrameSpec(
        background="frame-01.png",
        sprite="idle/processed/idle-1.png",
        output="frame-01-avatarlock.png",
        center=(771, 745),
        scale=1.62,
        cleanup_offset=(-280, 70),
        cleanup_size=(250, 185),
    ),
    FrameSpec(
        background="frame-02.png",
        sprite="attack/processed/attack-2.png",
        output="frame-02-avatarlock.png",
        center=(955, 742),
        scale=2.25,
        rotation=-11,
        flip=True,
        cleanup_offset=(-350, -35),
        cleanup_size=(395, 265),
    ),
    FrameSpec(
        background="frame-03.png",
        sprite="hit/processed/hit-2.png",
        output="frame-03-avatarlock.png",
        center=(785, 622),
        scale=1.95,
        rotation=-14,
        cleanup_offset=(-260, 85),
        cleanup_size=(305, 265),
        glow_color=(215, 245, 255, 210),
        glow_blur=24,
    ),
    FrameSpec(
        background="frame-04.png",
        sprite="defeat/processed/defeat-4.png",
        output="frame-04-avatarlock.png",
        center=(776, 771),
        scale=1.70,
        rotation=5,
        cleanup_offset=(-220, 60),
        cleanup_size=(270, 210),
        glow_color=(140, 205, 245, 120),
        glow_blur=15,
    ),
]


def fit_box(box: tuple[int, int, int, int], size: tuple[int, int]) -> tuple[int, int, int, int]:
    left, top, right, bottom = box
    width, height = size
    if left < 0:
        right -= left
        left = 0
    if top < 0:
        bottom -= top
        top = 0
    if right > width:
        left -= right - width
        right = width
    if bottom > height:
        top -= bottom - height
        bottom = height
    return max(0, left), max(0, top), min(width, right), min(height, bottom)


def cleanup_background(bg: Image.Image, spec: FrameSpec) -> Image.Image:
    cx, cy = spec.center
    w, h = spec.cleanup_size
    cleanup_box = fit_box((cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2), bg.size)
    dx, dy = spec.cleanup_offset
    source_box = fit_box(
        (
            cleanup_box[0] + dx,
            cleanup_box[1] + dy,
            cleanup_box[2] + dx,
            cleanup_box[3] + dy,
        ),
        bg.size,
    )
    patch = bg.crop(source_box).resize((cleanup_box[2] - cleanup_box[0], cleanup_box[3] - cleanup_box[1]), Image.Resampling.LANCZOS)
    patch = patch.filter(ImageFilter.GaussianBlur(3))

    mask = Image.new("L", patch.size, 0)
    inset = 18
    for y in range(patch.height):
        for x in range(patch.width):
            nx = min(x, patch.width - 1 - x)
            ny = min(y, patch.height - 1 - y)
            edge = min(nx, ny)
            value = 255 if edge >= inset else max(0, int(255 * edge / inset))
            mask.putpixel((x, y), value)

    result = bg.copy()
    result.paste(patch, cleanup_box[:2], mask)
    return result


def transform_sprite(spec: FrameSpec) -> Image.Image:
    sprite = Image.open(SRC / spec.sprite).convert("RGBA")
    if spec.flip:
        sprite = ImageOps.mirror(sprite)

    size = (
        max(1, int(sprite.width * spec.scale)),
        max(1, int(sprite.height * spec.scale)),
    )
    sprite = sprite.resize(size, Image.Resampling.LANCZOS)
    if spec.rotation:
        sprite = sprite.rotate(spec.rotation, resample=Image.Resampling.BICUBIC, expand=True)
    return sprite


def alpha_glow(sprite: Image.Image, color: tuple[int, int, int, int], blur: int) -> Image.Image:
    alpha = sprite.getchannel("A")
    glow_alpha = alpha.filter(ImageFilter.GaussianBlur(blur))
    glow = Image.new("RGBA", sprite.size, color)
    glow.putalpha(glow_alpha)
    return glow


def paste_center(base: Image.Image, layer: Image.Image, center: tuple[int, int], offset: tuple[int, int] = (0, 0)) -> None:
    x = int(center[0] - layer.width / 2 + offset[0])
    y = int(center[1] - layer.height / 2 + offset[1])
    base.alpha_composite(layer, (x, y))


def compose_frame(spec: FrameSpec) -> Path:
    background = Image.open(RAW / spec.background).convert("RGBA")
    background = cleanup_background(background, spec)
    sprite = transform_sprite(spec)

    shadow = alpha_glow(sprite, spec.shadow_color, spec.shadow_blur)
    glow = alpha_glow(sprite, spec.glow_color, spec.glow_blur)

    result = background.copy()
    paste_center(result, shadow, spec.center, spec.shadow_offset)
    paste_center(result, glow, spec.center)
    paste_center(result, sprite, spec.center)

    out_path = OUT / spec.output
    out_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(out_path)
    return out_path


def export_gif(frame_paths: list[Path]) -> Path:
    frames = [Image.open(path).convert("RGBA") for path in frame_paths]
    out_path = OUT / "abu-finisher-preview-avatarlock.gif"
    frames[0].save(
        out_path,
        save_all=True,
        append_images=frames[1:],
        duration=[420, 320, 220, 700],
        loop=0,
        disposal=2,
    )
    return out_path


def export_contact_sheet(frame_paths: list[Path]) -> Path:
    frames = [Image.open(path).convert("RGBA") for path in frame_paths]
    thumb_w = 768
    thumb_h = 512
    margin = 24
    canvas = Image.new("RGBA", (thumb_w * 2 + margin * 3, thumb_h * 2 + margin * 3), (5, 20, 38, 255))
    for idx, frame in enumerate(frames):
        thumb = ImageOps.contain(frame, (thumb_w, thumb_h), Image.Resampling.LANCZOS)
        x = margin + (idx % 2) * (thumb_w + margin)
        y = margin + (idx // 2) * (thumb_h + margin)
        panel = Image.new("RGBA", (thumb_w, thumb_h), (11, 39, 70, 255))
        px = (thumb_w - thumb.width) // 2
        py = (thumb_h - thumb.height) // 2
        panel.alpha_composite(thumb, (px, py))
        canvas.alpha_composite(panel, (x, y))
    out_path = OUT / "abu-finisher-contact-sheet-avatarlock.png"
    canvas.save(out_path)
    return out_path


def main() -> None:
    frame_paths = [compose_frame(spec) for spec in SPECS]
    gif_path = export_gif(frame_paths)
    contact_path = export_contact_sheet(frame_paths)
    for path in frame_paths:
        print(path)
    print(gif_path)
    print(contact_path)


if __name__ == "__main__":
    main()
