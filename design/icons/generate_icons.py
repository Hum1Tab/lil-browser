from pathlib import Path
import base64
import subprocess
import tempfile

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
CONCEPTS = ROOT / "concepts"
PREVIEWS = ROOT / "previews"
FINAL = ROOT / "final"
FINAL.mkdir(exist_ok=True)
CONCEPTS.mkdir(exist_ok=True)
PREVIEWS.mkdir(exist_ok=True)

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SIZE = 1024


def svg(body: str, defs: str = "") -> str:
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{SIZE}" height="{SIZE}" viewBox="0 0 {SIZE} {SIZE}">
<defs>{defs}</defs>
{body}
</svg>
'''


def grad(gid, stops, x1="0%", y1="0%", x2="100%", y2="100%"):
    s = "".join(f'<stop offset="{o}" stop-color="{c}"/>' for o, c in stops)
    return f'<linearGradient id="{gid}" x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}">{s}</linearGradient>'


def radial(gid, stops):
    s = "".join(f'<stop offset="{o}" stop-color="{c}"/>' for o, c in stops)
    return f'<radialGradient id="{gid}" cx="35%" cy="28%" r="80%">{s}</radialGradient>'


concepts = {}

# 01 Orbit: a portal ring with a satellite dot. No G-bar, no sparkle.
defs = grad("g1", [("0", "#F7E6A8"), (".52", "#D3AA67"), ("1", "#7A4E18")]) + \
       radial("r1", [("0", "#F7E6A8"), ("0.28", "#171817"), ("1", "#34D6C3")])
body = '''
<mask id="ring">
  <rect width="1024" height="1024" fill="white"/>
  <circle cx="500" cy="500" r="150" fill="black"/>
  <circle cx="748" cy="746" r="118" fill="black"/>
</mask>
<circle cx="500" cy="500" r="292" fill="url(#g1)" mask="url(#ring)"/>
<circle cx="748" cy="746" r="76" fill="#171817" stroke="#D3AA67" stroke-width="10"/>
'''
concepts["01-orbit"] = svg(body, defs)

# 02 Fold: a folded L-ribbon. Two interlocking arms, one negative slit.
defs = grad("g2", [("0", "#E7C276"), (".46", "#D3AA67"), ("1", "#8C5E22")]) + \
       grad("g2b", [("0", "#F7E6A8"), (".55", "#D3AA67"), ("1", "#6B4214")], "100%", "0%", "0%", "100%")
body = '''
<path d="M280 250c0-55 45-100 100-100h60c55 0 100 45 100 100v350h210c55 0 100 45 100 100v60c0 55-45 100-100 100H380c-55 0-100-45-100-100V250z" fill="url(#g2)"/>
<path d="M560 210h80c50 0 90 40 90 90v250h-90V330c0-22-18-40-40-40h-40v-80z" fill="url(#g2b)" opacity="0.95"/>
<circle cx="610" cy="650" r="46" fill="#171817" stroke="#D3AA67" stroke-width="10" opacity="0.95"/>
'''
concepts["02-fold"] = svg(body, defs)

# 03 Duo: two rounded capsules crossing; a browser tab / link silhouette.
defs = grad("g3", [("0", "#F0D58C"), (".5", "#D3AA67"), ("1", "#7A4E18")]) + \
       grad("g3b", [("0", "#D3AA67"), (".55", "#E7C276"), ("1", "#8C5E22")], "0%", "100%", "100%", "0%")
body = '''
<g transform="rotate(-28 512 512)">
  <rect x="210" y="430" width="604" height="164" rx="82" fill="url(#g3)"/>
</g>
<g transform="rotate(28 512 512)">
  <rect x="210" y="430" width="604" height="164" rx="82" fill="url(#g3b)" opacity="0.92"/>
</g>
<circle cx="512" cy="512" r="70" fill="#171817" stroke="#D3AA67" stroke-width="10"/>
'''
concepts["03-duo"] = svg(body, defs)

# 04 Prism: rounded triangle with an aperture. Speed / portal.
defs = grad("g4", [("0", "#D3AA67"), (".5", "#E7C276"), ("1", "#7A4E18")]) + \
       radial("r4", [("0", "#F7E6A8"), ("1", "#171817")])
body = '''
<mask id="tri-hole">
  <path d="M512 156 L850 780 H174 Z" fill="white" stroke="white" stroke-width="150" stroke-linejoin="round"/>
  <circle cx="512" cy="570" r="104" fill="black"/>
</mask>
<path d="M512 156 L850 780 H174 Z" fill="url(#g4)" stroke="url(#g4)" stroke-width="150" stroke-linejoin="round" mask="url(#tri-hole)"/>
<circle cx="512" cy="570" r="104" fill="#171817" stroke="#D3AA67" stroke-width="10" opacity="0.95"/>
'''
concepts["04-prism"] = svg(body, defs)

# 05 Bloom: four soft lobes around a calm centre. Not a four-point sparkle.
defs = radial("g5", [("0", "#F7E6A8"), (".45", "#D3AA67"), (".78", "#B8843A"), ("1", "#7A4E18")])
body = '''
<g fill="url(#g5)">
  <circle cx="512" cy="330" r="158"/>
  <circle cx="694" cy="512" r="158"/>
  <circle cx="512" cy="694" r="158"/>
  <circle cx="330" cy="512" r="158"/>
</g>
<circle cx="512" cy="512" r="98" fill="#171817" stroke="#D3AA67" stroke-width="10"/>
'''
concepts["05-bloom"] = svg(body, defs)

# 06 Monogram: lIl pillars, optically spaced with a shared diagonal gradient.
defs = grad("g6", [("0", "#F7E6A8"), (".48", "#D3AA67"), ("1", "#7A4E18")], "0%", "100%", "100%", "0%")
body = '''
<g fill="url(#g6)">
  <rect x="236" y="338" width="150" height="442" rx="75"/>
  <rect x="437" y="226" width="150" height="664" rx="75"/>
  <rect x="638" y="338" width="150" height="442" rx="75"/>
</g>
<circle cx="512" cy="512" r="36" fill="#171817" stroke="#D3AA67" stroke-width="10"/>
'''
concepts["06-monogram"] = svg(body, defs)

for name, content in concepts.items():
    (CONCEPTS / f"{name}.svg").write_text(content, encoding="utf-8")


def edge_shot(html: str, width: int, height: int, target: Path):
    work = Path(tempfile.mkdtemp(prefix="lil-icon-"))
    page = work / "page.html"
    output = work / "shot.png"
    page.write_text(html, encoding="utf-8")
    args = [
        EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars",
        "--force-device-scale-factor=1", "--default-background-color=00000000",
        "--virtual-time-budget=1200", f"--window-size={width},{height}",
        f"--screenshot={output}", page.as_uri(),
    ]
    subprocess.run(args, check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
    if not output.exists():
        raise RuntimeError(f"Edge did not render {target}")
    Image.open(output).convert("RGBA").save(target)


# Per-size grid: exact pixel comparison from the same SVG geometry.
for size in (16, 32, 128):
    pad = 20
    step = size + 40
    width = pad * 2 + step * len(concepts) - 40
    height = size + pad * 2
    imgs = "".join(
        f'<img src="data:image/svg+xml;base64,{base64.b64encode(content.encode()).decode()}" '
        f'width="{size}" height="{size}" style="display:block;position:absolute;left:{pad + i * step}px;top:{pad}px">'
        for i, content in enumerate(concepts.values())
    )
    html = f'<html><body style="margin:0;background:transparent">{imgs}</body></html>'
    shot = PREVIEWS / f"grid-{size}.png"
    edge_shot(html, width, height, shot)
    grid = Image.open(shot).convert("RGBA")
    for i, name in enumerate(concepts):
        box = (pad + i * step, pad, pad + i * step + size, pad + size)
        grid.crop(box).save(PREVIEWS / f"{name}-{size}.png")

# 1024 final render (same sheet later chooses the candidate).
final_svg = concepts["01-orbit"]
(FINAL / "lil-service-icon.svg").write_text(final_svg, encoding="utf-8")
html = f'<html><body style="margin:0;background:transparent"><img src="data:image/svg+xml;base64,{base64.b64encode(final_svg.encode()).decode()}" width="1024" height="1024" style="display:block"></body></html>'
edge_shot(html, 1024, 1024, FINAL / "lil-service-icon-1024.png")

# Comparison sheets.
font_path = r"C:\Windows\Fonts\YuGothR.ttc"
bold_path = r"C:\Windows\Fonts\YuGothB.ttc"
try:
    font = ImageFont.truetype(font_path, 22)
    small = ImageFont.truetype(font_path, 16)
    bold = ImageFont.truetype(bold_path, 26)
except Exception:
    font = ImageFont.load_default()
    small = font
    bold = font

names = {
    "01-orbit": "01  Orbit / portal",
    "02-fold": "02  Folded L",
    "03-duo": "03  Duo link",
    "04-prism": "04  Prism aperture",
    "05-bloom": "05  Soft bloom",
    "06-monogram": "06  lIl pillars",
}


def sheet(bg, fg, muted, grid, path):
    W, H = 1040, 1080
    im = Image.new("RGBA", (W, H), bg)
    d = ImageDraw.Draw(im)
    d.text((44, 32), "lIl service icon - size test", font=bold, fill=fg)
    d.text((44, 70), "16 / 32 / 128 px, rendered from the same SVG geometry", font=small, fill=muted)
    d.text((44, 96), "lIl = lowercase l / capital I / lowercase l", font=small, fill=muted)
    cols = [410, 570, 790]
    for x, label in zip(cols, ["16 px", "32 px", "128 px"]):
        d.text((x - 18, 120), label, font=small, fill=muted)
    d.line((44, 148, W - 44, 148), fill=grid, width=1)
    y = 170
    for key, label in names.items():
        d.text((44, y + 64), label, font=font, fill=fg)
        for x, size in zip(cols, (16, 32, 128)):
            icon = Image.open(PREVIEWS / f"{key}-{size}.png").convert("RGBA")
            ix = x - size // 2
            iy = y + 70 - size // 2
            im.alpha_composite(icon, (ix, iy))
        d.line((44, y + 145, W - 44, y + 145), fill=grid, width=1)
        y += 145
    im.convert("RGB").save(path)


sheet("#F7F4EE", "#171817", "#77736B", "#DED7CA", ROOT / "comparison-light.png")
sheet("#171817", "#F4EFE4", "#9A958B", "#343631", ROOT / "comparison-dark.png")
print("generated", len(concepts), "concepts and previews")
