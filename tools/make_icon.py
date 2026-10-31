"""Generates the Pénombre icons from vector drawings.

    pip install pillow playwright
    python -m playwright install chromium
    python tools/make_icon.py

Writes, in assets/:
  penombre.ico     the exe and window icon (moon egg, 16 to 256 px)
  logo.png         the 256 px logo used by the README
  tray-icons.png   the notification area icons: 3 styles x 4 states, at 16, 20, 24, 32, 40 and 48 px

Small sizes use simplified drawings (thicker outlines, no sparkles) so they stay readable.
The PNG files are written by Pillow without any metadata.
"""
from io import BytesIO
from pathlib import Path

from PIL import Image
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "assets"

EGG = "M60 8C88 8 103 50 103 72C103 99 84 113 60 113C36 113 17 99 17 72C17 50 32 8 60 8Z"
EGG_IN = "M60 12C85 12 99 50 99 72C99 96 82 109 60 109C38 109 21 96 21 72C21 50 35 12 60 12Z"

# Shell colours for each state: dark, middle, light, rim, inner rim, core glow, halo, motif fill, motif outline
STATES = {
    "night":    dict(c1="#0b1f5c", c2="#2f63c9", c3="#a8dcff", rim="#0a1640", rim2="#8fd0ff", core="#7fc4ff", glow="#6fb3ff", glow_op=0.35, m1="#ffd46a", m2="#8a5a12", spark=True),
    "twilight": dict(c1="#3a124f", c2="#b4457f", c3="#ffc08a", rim="#2a0c38", rim2="#ffb3c8", core="#ffb07a", glow="#ff9a6a", glow_op=0.2, m1="#ffcf7a", m2="#7a2a3a", spark=True),
    "day":      dict(c1="#9a5d12", c2="#e8b04a", c3="#fff6d8", rim="#5a3608", rim2="#fff1b8", core="#fff3c4", glow="#ffe08a", glow_op=0.0, m1="#ffffff", m2="#b8781c", spark=True),
    "off":      dict(c1="#2a2a2a", c2="#777777", c3="#d6d6d6", rim="#1a1a1a", rim2="#e0e0e0", core="#bdbdbd", glow="#000000", glow_op=0.0, m1="#9a9a9a", m2="#4a4a4a", spark=False),
}
STATE_ORDER = ["night", "twilight", "day", "off"]
STYLE_ORDER = ["moon_egg", "watcher_egg", "moon"]
TRAY_SIZES = [16, 20, 24, 32, 40, 48]
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def egg(state: str, motif: str, detailed: bool, glow: bool) -> str:
    s = STATES[state]
    parts = [
        "<defs>",
        f'<radialGradient id="b" cx="42%" cy="38%" r="68%"><stop offset="0" stop-color="{s["c3"]}"/>'
        f'<stop offset="0.5" stop-color="{s["c2"]}"/><stop offset="1" stop-color="{s["c1"]}"/></radialGradient>',
        f'<radialGradient id="g" cx="50%" cy="55%" r="50%"><stop offset="0" stop-color="{s["glow"]}" stop-opacity="0.9"/>'
        f'<stop offset="1" stop-color="{s["glow"]}" stop-opacity="0"/></radialGradient>',
        f'<clipPath id="c"><path d="{EGG}"/></clipPath>',
        "</defs>",
    ]
    if glow and s["glow_op"] > 0:
        parts.append(f'<circle cx="60" cy="62" r="60" fill="url(#g)" opacity="{min(1, s["glow_op"] * 2)}"/>')
    if detailed:
        parts.append(f'<path d="{EGG}" fill="url(#b)"/>')
        parts.append('<g clip-path="url(#c)">')
        parts.append(f'<circle cx="60" cy="70" r="30" fill="{s["core"]}" opacity="0.55"/>')
        if motif == "moon":
            parts.append(f'<path d="M40 92q-10-20 6-34" fill="none" stroke="{s["m1"]}" stroke-width="10" stroke-linecap="round" opacity="0.35"/>')
            parts.append(f'<path d="M40 92q-10-20 6-34" fill="none" stroke="{s["m1"]}" stroke-width="4" stroke-linecap="round"/>')
            parts.append(f'<path d="M68 48a20 20 0 1 0 12 30a15 15 0 1 1-12-30z" fill="{s["m1"]}" stroke="{s["m2"]}" stroke-width="2.5"/>')
        elif motif == "sun":
            parts.append(f'<circle cx="62" cy="70" r="12" fill="{s["m1"]}" stroke="{s["m2"]}" stroke-width="2.5"/>')
            parts.append(f'<path d="M62 50q20 4 18 24M42 80q-6-16 8-26" fill="none" stroke="{s["m1"]}" stroke-width="4" stroke-linecap="round"/>')
        elif motif == "eyes_open":
            parts.append('<ellipse cx="47" cy="72" rx="7" ry="10" fill="#120c06"/><ellipse cx="73" cy="72" rx="7" ry="10" fill="#120c06"/>'
                         '<circle cx="49" cy="68" r="3" fill="#ffffff"/><circle cx="75" cy="68" r="3" fill="#ffffff"/>')
        elif motif == "eyes_closed":
            parts.append('<path d="M40 74q7 6 14 0M66 74q7 6 14 0" fill="none" stroke="#120c06" stroke-width="4" stroke-linecap="round"/>')
        if s["spark"]:
            parts.append('<g fill="#ffffff"><circle cx="34" cy="62" r="2"/><circle cx="82" cy="40" r="1.8"/>'
                         '<circle cx="86" cy="88" r="2.2"/><circle cx="50" cy="100" r="1.6"/><circle cx="74" cy="26" r="1.4"/></g>')
        if state == "off" and motif == "moon":
            parts.append('<path d="M50 18l6 18-8 10 12 14-4 16" fill="none" stroke="#2b2b2b" stroke-width="3" stroke-linejoin="round" stroke-linecap="round"/>')
        parts.append('<path d="M92 84q-6 20-28 26q18-10 24-28z" fill="#ffffff" opacity="0.35"/>')
        parts.append("</g>")
        parts.append(f'<path d="{EGG}" fill="none" stroke="{s["rim"]}" stroke-width="4"/>')
        parts.append(f'<path d="{EGG_IN}" fill="none" stroke="{s["rim2"]}" stroke-width="3" opacity="0.8"/>')
        parts.append('<path d="M32 46q6-26 26-32q-16 12-20 34z" fill="#ffffff" opacity="0.85"/>')
        parts.append('<circle cx="40" cy="58" r="3.5" fill="#ffffff" opacity="0.85"/>')
    else:
        parts.append(f'<path d="{EGG}" fill="url(#b)" stroke="{s["rim"]}" stroke-width="10"/>')
        if motif == "moon":
            parts.append(f'<path d="M66 46a24 24 0 1 0 14 36a18 18 0 1 1-14-36z" fill="{s["m1"]}" stroke="{s["m2"]}" stroke-width="5"/>')
        elif motif == "sun":
            parts.append(f'<circle cx="60" cy="70" r="16" fill="{s["m1"]}" stroke="{s["m2"]}" stroke-width="5"/>')
        elif motif == "eyes_open":
            parts.append('<ellipse cx="45" cy="72" rx="9" ry="13" fill="#120c06"/><ellipse cx="75" cy="72" rx="9" ry="13" fill="#120c06"/>')
        elif motif == "eyes_closed":
            parts.append('<path d="M36 74q9 9 18 0M66 74q9 9 18 0" fill="none" stroke="#120c06" stroke-width="7" stroke-linecap="round"/>')
        if state == "off" and motif == "moon":
            parts.append('<path d="M50 18l8 18-8 12 12 16-6 16" fill="none" stroke="#2b2b2b" stroke-width="6" stroke-linejoin="round"/>')
        parts.append('<path d="M32 48q6-28 28-34q-18 12-22 36z" fill="#ffffff" opacity="0.85"/>')
    return "".join(parts)


def moon(state: str, detailed: bool) -> str:
    w = 7 if detailed else 10
    if state == "night":
        out = f'<circle cx="60" cy="60" r="58" fill="#f2c46b" opacity="0.3"/><circle cx="60" cy="60" r="46" fill="#f2c46b" stroke="#120c06" stroke-width="{w}"/>'
        if detailed:
            out += '<circle cx="46" cy="50" r="8" fill="#d9a548"/><circle cx="72" cy="74" r="10" fill="#d9a548"/><circle cx="74" cy="42" r="5" fill="#d9a548"/>'
        return out
    if state == "day":
        return (f'<circle cx="60" cy="60" r="40" fill="none" stroke="#120c06" stroke-width="{w * 2 + 4}"/>'
                f'<circle cx="60" cy="60" r="40" fill="none" stroke="#f2c46b" stroke-width="{w + 2}"/>')
    fill = "#9a9a9a" if state == "off" else "#f2c46b"
    return f'<path d="M74 18a44 44 0 1 0 26 72a36 36 0 1 1-26-72z" fill="{fill}" stroke="#120c06" stroke-width="{w}" stroke-linejoin="round"/>'


def drawing(style: str, state: str, size: int, glow: bool = True) -> str:
    detailed = size >= 40
    if style == "moon":
        return moon(state, detailed)
    if style == "moon_egg":
        motif = "sun" if state == "day" else "moon"
    else:
        motif = "eyes_open" if state in ("night", "twilight") else "eyes_closed"
    return egg(state, motif, detailed, glow)


def render(page, body: str, size: int) -> Image.Image:
    html = (f'<html><body style="margin:0;background:transparent">'
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 120 120">{body}</svg>'
            f'</body></html>')
    page.set_viewport_size({"width": size, "height": size})
    page.set_content(html)
    png = page.screenshot(omit_background=True, clip={"x": 0, "y": 0, "width": size, "height": size})
    return Image.open(BytesIO(png)).convert("RGBA")


def clean(img: Image.Image) -> Image.Image:
    """A fresh image with the same pixels and no metadata at all."""
    return Image.frombytes("RGBA", img.size, img.convert("RGBA").tobytes())


def main() -> None:
    ASSETS.mkdir(exist_ok=True)
    with sync_playwright() as p:
        browser = p.chromium.launch()
        page = browser.new_page()

        # Notification area sprite: one row per style and state, one block per size.
        width = sum(TRAY_SIZES)
        rows = len(STYLE_ORDER) * len(STATE_ORDER)
        sheet = Image.new("RGBA", (width, rows * max(TRAY_SIZES)), (0, 0, 0, 0))
        for si, style in enumerate(STYLE_ORDER):
            for ti, state in enumerate(STATE_ORDER):
                x = 0
                for size in TRAY_SIZES:
                    sheet.paste(render(page, drawing(style, state, size), size), (x, (si * 4 + ti) * max(TRAY_SIZES)))
                    x += size
        clean(sheet).save(ASSETS / "tray-icons.png", optimize=True)

        # Exe icon: the night moon egg, without its halo so it stays sharp.
        frames = [clean(render(page, drawing("moon_egg", "night", s, glow=False), s)) for s in ICO_SIZES]
        frames[-1].save(ASSETS / "penombre.ico", format="ICO", sizes=[(s, s) for s in ICO_SIZES], append_images=frames[:-1])

        # Logo for the README: with its halo.
        clean(render(page, drawing("moon_egg", "night", 256), 256)).save(ASSETS / "logo.png", optimize=True)
        browser.close()
    for name in ("tray-icons.png", "penombre.ico", "logo.png"):
        print(f"Written: {ASSETS / name}")


if __name__ == "__main__":
    main()
