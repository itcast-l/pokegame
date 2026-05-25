from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path("/Volumes/KIOXIA11/godotproject/pokegame/art/characters/player_fish_avatar/run-002/finisher_preview")
REFS = ROOT / "refs"


def fit_center(img: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = Image.new("RGBA", size, (10, 39, 72, 255))
    src = img.convert("RGBA")
    src.thumbnail(size, Image.Resampling.NEAREST)
    x = (size[0] - src.width) // 2
    y = (size[1] - src.height) // 2
    canvas.alpha_composite(src, (x, y))
    return canvas


def make_reference_board() -> None:
    idle = Image.open(REFS / "abu-idle-reference.png")
    defeat = Image.open(REFS / "abu-defeat-reference.png")

    board = Image.new("RGBA", (1536, 768), (7, 30, 56, 255))
    draw = ImageDraw.Draw(board)
    draw.rounded_rectangle((24, 24, 1512, 744), radius=36, fill=(13, 52, 94, 255), outline=(161, 217, 255, 255), width=6)
    draw.rounded_rectangle((48, 84, 736, 684), radius=28, fill=(18, 73, 121, 255), outline=(224, 244, 255, 255), width=4)
    draw.rounded_rectangle((800, 84, 1488, 684), radius=28, fill=(18, 73, 121, 255), outline=(224, 244, 255, 255), width=4)

    left = fit_center(idle, (620, 520))
    right = fit_center(defeat, (620, 520))
    board.alpha_composite(left, (82, 124))
    board.alpha_composite(right, (834, 124))

    board.save(REFS / "abu-reference-board.png")


def write_prompts() -> None:
    prompts = {
        "frame-01.prompt.txt": (
            "Use the image just shown as the visual reference for Abu, the same small blue-gray fish hero with a large golden eye, pale cream belly, rounded body, soft hand-painted outline, and cute but brave underwater fantasy look. "
            "Create a polished full-screen 16:9 cinematic finisher keyframe for a game defeat animation. Scene 1: Abu is alone in bright turquoise shallow water above a coral arena, looking alarmed as the water behind him darkens into a giant shark-like deep-sea predator silhouette. "
            "Keep Abu small in the lower middle foreground and preserve his exact identity from the reference. Strong underwater god rays, bubble trails, swirling current lines, elegant ocean fantasy lighting, same project-native polished illustrated style, not pixel art, not realistic. "
            "High contrast, dramatic but non-gory, no blood, no text, no UI, no watermark."
        ),
        "frame-02.prompt.txt": (
            "Use the image just shown as the visual reference for Abu, the same small blue-gray fish hero with a large golden eye, pale cream belly, rounded body, soft hand-painted outline, and cute but brave underwater fantasy look. "
            "Create a polished full-screen 16:9 cinematic finisher keyframe for a game defeat animation. Scene 2: the deep-sea predator lunges from the dark water, jaws implied by shadow and glowing teeth shapes, while Abu twists sideways in panic inside a spiraling vortex of bubbles and light. "
            "Preserve Abu's exact identity and project-native polished ocean fantasy illustration style. Strong motion, diagonal composition, blue-cyan water with dark navy threat shape, dramatic lighting, non-gory, no blood, no text, no UI, no watermark."
        ),
        "frame-03.prompt.txt": (
            "Use the image just shown as the visual reference for Abu, the same small blue-gray fish hero with a large golden eye, pale cream belly, rounded body, soft hand-painted outline, and cute but brave underwater fantasy look. "
            "Create a polished full-screen 16:9 cinematic finisher keyframe for a game defeat animation. Scene 3: a huge violent burst of white-blue water, bubbles, and predator shadow closes around Abu at the center, with the attack impact mostly hidden inside foam and refracted light. "
            "Abu should still be readable as the same fish hero but overwhelmed by the finisher impact. Keep it stylish and readable, non-gory, no blood, no dismemberment, no text, no UI, no watermark. Same polished underwater fantasy illustration style."
        ),
        "frame-04.prompt.txt": (
            "Use the image just shown as the visual reference for Abu, the same small blue-gray fish hero with a large golden eye, pale cream belly, rounded body, soft hand-painted outline, and cute but brave underwater fantasy look. "
            "Create a polished full-screen 16:9 cinematic finisher keyframe for a game defeat animation. Scene 4: the water becomes calm and dim after the strike, with drifting bubbles and a fading predator silhouette in the distance, while Abu floats limp in the foreground, defeated but intact. "
            "Preserve Abu's exact identity and same project-native hand-painted ocean fantasy style. Melancholic blue lighting, elegant aftermath composition, no gore, no blood, no text, no UI, no watermark."
        ),
    }

    for name, text in prompts.items():
        (ROOT / name).write_text(text, encoding="utf-8")


def main() -> None:
    make_reference_board()
    write_prompts()


if __name__ == "__main__":
    main()
