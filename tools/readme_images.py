"""Builds the README pictures from the captures of tools/shots.bat.

    pip install pillow
    python tools/readme_images.py

Reads dist/shots/ and writes, in assets/readme/:
  banner.jpg      the game without and with Pénombre, side by side
  moods.jpg       the game without filter and with each mood
  settings-*.png  settings pages (Mood, Display)
and dist/social-preview.jpg, the 1280 x 640 picture shown when the repository link is shared
(GitHub: Settings > General > Social preview).

Only the scene is kept: the game interface (chat, spells, map) is cropped out.
Files are written by Pillow without any metadata.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
SHOTS = ROOT / "dist" / "shots"
OUT = ROOT / "assets" / "readme"
FONT = ROOT / "assets" / "fonts" / "Nunito-Black.ttf"

MOODS = ["Nuit bleutée", "Clair de lune", "Crépuscule", "Nuit noire", "Nuit d'antan"]
BANNER_MOOD = 0
BACK = (22, 21, 27)
TEXT = (236, 235, 242)
GREEN = (198, 234, 106)


def scene(name: str) -> Image.Image:
    """The game picture without its interface (a 1920 x 1017 capture: the map name and position end near y=85, the chat and the spells start near y=840)."""
    img = Image.open(SHOTS / f"{name}.png").convert("RGB")
    w, h = img.size
    return img.crop((0, int(h * 0.09), w, int(h * 0.82)))


def label(draw: ImageDraw.ImageDraw, xy, text: str, size: int, color, anchor: str = "la") -> None:
    font = ImageFont.truetype(str(FONT), size)
    x, y = xy
    for dx, dy in ((0, 2), (1, 2), (-1, 2)):
        draw.text((x + dx, y + dy), text, font=font, fill=(0, 0, 0), anchor=anchor)
    draw.text((x, y), text, font=font, fill=color, anchor=anchor)


def pill(img: Image.Image, xy, text: str, size: int, color, right: bool = False) -> None:
    draw = ImageDraw.Draw(img, "RGBA")
    font = ImageFont.truetype(str(FONT), size)
    x0, y0, x1, y1 = draw.textbbox((0, 0), text, font=font)
    pad_x, pad_y = size * 0.7, size * 0.45
    w, h = x1 - x0 + 2 * pad_x, y1 - y0 + 2 * pad_y
    x, y = xy
    if right:
        x -= w
    draw.rounded_rectangle((x, y, x + w, y + h), radius=h / 2, fill=(22, 21, 27, 200), outline=(106, 104, 120, 255), width=2)
    draw.text((x + pad_x - x0, y + pad_y - y0), text, font=font, fill=color)


def banner() -> None:
    off, on = scene("game-off"), scene(f"game-mood{BANNER_MOOD}")
    w, h = off.size
    out = off.copy()
    out.paste(on.crop((w // 2, 0, w, h)), (w // 2, 0))
    draw = ImageDraw.Draw(out, "RGBA")
    glow = Image.new("RGBA", out.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).rectangle((w // 2 - 6, 0, w // 2 + 6, h), fill=GREEN + (160,))
    out.paste(glow.filter(ImageFilter.GaussianBlur(8)), (0, 0), glow.filter(ImageFilter.GaussianBlur(8)))
    draw.rectangle((w // 2 - 2, 0, w // 2 + 1, h), fill=GREEN)
    out = out.resize((1600, round(1600 * h / w)), Image.LANCZOS)
    pill(out, (24, 22), "SANS", 26, TEXT)
    pill(out, (1600 - 24, 22), "AVEC PÉNOMBRE", 26, GREEN, right=True)
    out.save(OUT / "banner.jpg", quality=88, optimize=True, progressive=True)


def moods() -> None:
    names = ["game-off"] + [f"game-mood{i}" for i in range(len(MOODS))]
    titles = ["Sans filtre"] + MOODS
    tile_w, cap, gap = 520, 46, 14
    first = scene(names[0])
    # the middle of the scene, where the light changes the most
    w, h = first.size
    box = (int(w * 0.24), int(h * 0.06), int(w * 0.76), int(h * 0.94))
    tile_h = round(tile_w * (box[3] - box[1]) / (box[2] - box[0]))
    cols, rows = 3, 2
    out = Image.new("RGB", (cols * tile_w + (cols + 1) * gap, rows * (tile_h + cap) + (rows + 1) * gap), BACK)
    draw = ImageDraw.Draw(out)
    for i, (name, title) in enumerate(zip(names, titles)):
        tile = scene(name).crop(box).resize((tile_w, tile_h), Image.LANCZOS)
        x = gap + (i % cols) * (tile_w + gap)
        y = gap + (i // cols) * (tile_h + cap + gap)
        out.paste(tile, (x, y))
        draw.rectangle((x - 1, y - 1, x + tile_w, y + tile_h), outline=(106, 104, 120))
        label(draw, (x + tile_w // 2, y + tile_h + cap // 2), title, 24, GREEN if i else TEXT, anchor="mm")
    out.save(OUT / "moods.jpg", quality=88, optimize=True, progressive=True)


def settings() -> None:
    for page, name in ((1, "mood"), (4, "display")):
        img = Image.open(SHOTS / f"settings{page}.png").convert("RGB")
        Image.frombytes("RGB", img.size, img.tobytes()).save(OUT / f"settings-{name}.png", optimize=True)


def social() -> None:
    off, on = scene("game-off"), scene(f"game-mood{BANNER_MOOD}")
    w, h = off.size
    # 2:1, centred on the scene
    ch = min(h, w // 2)
    cw = ch * 2
    box = ((w - cw) // 2, (h - ch) // 2, (w - cw) // 2 + cw, (h - ch) // 2 + ch)
    left, right = off.crop(box).resize((1280, 640), Image.LANCZOS), on.crop(box).resize((1280, 640), Image.LANCZOS)
    out = left.copy()
    out.paste(right.crop((640, 0, 1280, 640)), (640, 0))
    # a dark band in the middle for the title, the split line above and below it
    shade = Image.new("L", out.size, 0)
    ImageDraw.Draw(shade).rounded_rectangle((250, 205, 1030, 435), radius=28, fill=200)
    shade = shade.filter(ImageFilter.GaussianBlur(30))
    out = Image.composite(Image.new("RGB", out.size, BACK), out, shade)
    draw = ImageDraw.Draw(out)
    draw.rectangle((639, 0, 641, 190), fill=GREEN)
    draw.rectangle((639, 450, 641, 640), fill=GREEN)
    logo = Image.open(ROOT / "assets" / "logo.png").convert("RGBA").resize((170, 170), Image.LANCZOS)
    out.paste(logo, (300, 235), logo)
    label(draw, (490, 300), "Pénombre", 96, TEXT, anchor="lm")
    label(draw, (494, 378), "Le mode nuit de Dofus 3", 36, GREEN, anchor="lm")
    pill(out, (24, 22), "SANS", 24, TEXT)
    pill(out, (1280 - 24, 22), "AVEC PÉNOMBRE", 24, GREEN, right=True)
    dist = ROOT / "dist"
    out.save(dist / "social-preview.jpg", quality=90, optimize=True, progressive=True)  # GitHub takes up to 1 MB
    print(f"Written: {dist / 'social-preview.jpg'}")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    banner()
    moods()
    settings()
    social()
    for f in sorted(OUT.iterdir()):
        print(f"Written: {f} ({f.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
