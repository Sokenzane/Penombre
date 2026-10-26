"""Generates assets/penombre.ico and assets/logo.png (a crescent moon) with Pillow.

    python tools/make_icon.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SCALE = 4  # draw big, then downscale for smooth edges
MOON = (255, 226, 160, 255)
ROOT = Path(__file__).resolve().parent.parent


def moon(size: int) -> Image.Image:
    s = size * SCALE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    m = s * 0.08
    d.ellipse((m, m, s - m, s - m), fill=MOON)
    # Cut the crescent with a transparent disc offset to the upper right.
    cut = Image.new("L", (s, s), 0)
    ImageDraw.Draw(cut).ellipse((s * 0.34, -s * 0.10, s * 1.10, s * 0.66), fill=255)
    img.putalpha(Image.composite(Image.new("L", (s, s), 0), img.getchannel("A"), cut))
    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    out = ROOT / "assets" / "penombre.ico"
    out.parent.mkdir(exist_ok=True)
    big = moon(256)
    big.save(out, sizes=[(n, n) for n in SIZES], append_images=[moon(n) for n in SIZES[:-1]])
    print(f"Written: {out}")
    logo = ROOT / "assets" / "logo.png"
    moon(256).save(logo, optimize=True)  # plain PNG, no metadata
    print(f"Written: {logo}")


if __name__ == "__main__":
    main()
