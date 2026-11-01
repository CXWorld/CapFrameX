"""Renders the board artwork of the BLACKWELL THERMALS starter profiles.

Three GeForce RTX 50 boards, each with the GPU package in the centre, its GDDR7 modules around it
and the traces between them. NVIDIA numbers the modules by memory partition, counter-clockwise
from the corner opposite the gold pin-1 mark of the package, each partition with modules 0 and 1.

- gb202: the RTX 5090 Founders Edition, sixteen modules of a 512-bit bus: four above the package,
  five on each side and two below; pin 1 at the top left, partitions A to H from the bottom right.
- gb203: an RTX 5080 / 5070 Ti partner board (MSI Gaming Trio), eight modules of a 256-bit bus:
  two above, four on the right, two below, the power stages on the left. The package is turned by
  90 degrees, so pin 1 is at the top right and partitions A to D run from the bottom left.
- gb205: an RTX 5070 partner board (ASUS TUF) with the same layout: six modules of a 192-bit bus,
  partitions A to C; the two places of partition D above the package stay empty.
- gb206: an RTX 5060 / 5060 Ti 8 GB partner board (ASUS PRIME), four modules of a 128-bit bus: two
  above the package and two on the right, the power stages on the left. The package stands upright
  like the 5090's: pin 1 at the top left, partition A on the right, B above.

It is drawn here from SVG primitives: no photograph, logo, brand mark or font glyph is used. Each
PNG is the background of a design's board tile, 440 x 384 design units, exported at 2 device
pixels per unit through headless Microsoft Edge. The chip and die geometry below matches the tiles
of overlay-templates/Blackwell-Thermals-RTX-50*.json; change both together. The random seed is
fixed, so a run reproduces the embedded PNGs.

Usage: python -I scripts\\New-BlackwellBoardArt.py <output-dir>
"""
import os
import random
import subprocess
import sys
import tempfile
import time

W, H = 440, 384
SCALE = 2
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

CHIP_W, CHIP_H = 64, 44
TOP_X = (83, 153, 223, 293)          # above the package
BOTTOM_X = (124, 252)                # below the package
COL_Y = (68, 122, 176, 230, 284)     # left and right columns
TOP_Y, BOTTOM_Y = 14, 326
LEFT_X, RIGHT_X = 12, 364
PARTNER_ROW_X = (150, 226)           # above and below the package of the partner boards
PARTNER_COL_Y = (86, 142, 198, 254)  # their right column
GB206_COL_Y = (130, 210)             # the right column of the GB206 board
PACKAGE = (128, 100, 184, 184)       # x, y, width, height
DIE = (160, 126, 120, 132)
CX, CY = W / 2, H / 2

GREEN = "#76B900"
TRACE = "#1E3A2B"
GOLD = "#C9A64E"


def f(v):
    return f"{v:.1f}".rstrip("0").rstrip(".")


def pads(board):
    """Every module place of the board as (name, x, y, side), named by memory partition."""
    if board == "gb202":
        top = [(name, x, TOP_Y, "top") for name, x in zip(("E1", "D0", "D1", "C0"), TOP_X)]
        right = [(name, RIGHT_X, y, "right") for name, y in zip(("C1", "B0", "B1", "A0", "A1"), COL_Y)]
        bottom = [(name, x, BOTTOM_Y, "bottom") for name, x in zip(("H1", "H0"), BOTTOM_X)]
        left = [(name, LEFT_X, y, "left") for name, y in zip(("E0", "F1", "F0", "G1", "G0"), COL_Y)]
        return top + right + bottom + left
    if board == "gb206":
        # Counter-clockwise from the bottom right: A up the right, B along the top.
        right = [(name, RIGHT_X, y, "right") for name, y in zip(("A0", "A1"), GB206_COL_Y)]
        top = [(name, x, TOP_Y, "top") for name, x in zip(("B0", "B1"), PARTNER_ROW_X)]
        return top + right
    # Counter-clockwise from the bottom left: A along the bottom, B and C up the right, D along the top.
    bottom = [(name, x, BOTTOM_Y, "bottom") for name, x in zip(("A1", "A0"), PARTNER_ROW_X)]
    right = [(name, RIGHT_X, y, "right") for name, y in zip(("C0", "C1", "B0", "B1"), PARTNER_COL_Y)]
    top = [(name, x, TOP_Y, "top") for name, x in zip(("D0", "D1"), PARTNER_ROW_X)]
    return top + right + bottom


def chips(board):
    """The populated module places: a 192-bit GB205 leaves partition D empty."""
    return [pad for pad in pads(board) if board != "gb205" or pad[0][0] != "D"]


def traces(board):
    """Four traces from the inner edge of every module place, fanning in towards the package."""
    px, py, pw, ph = PACKAGE
    out = []
    for _, x, y, side in pads(board):
        if side in ("top", "bottom"):
            c = x + CHIP_W / 2
            y0, step, edge = (y + CHIP_H, 9, py) if side == "top" else (y, -9, py + ph)
            for k in range(4):
                o = k * 9 - 13.5
                tx = CX + (c - CX) * 0.52 + o * 0.6
                out.append(f"M {f(c + o)} {y0} L {f(c + o)} {y0 + step} L {f(tx)} {edge - step} L {f(tx)} {edge}")
        else:
            c = y + CHIP_H / 2
            x0, step, edge = (x + CHIP_W, 9, px) if side == "left" else (x, -9, px + pw)
            for k in range(4):
                o = k * 8 - 12
                ty = CY + (c - CY) * 0.52 + o * 0.6
                out.append(f"M {x0} {f(c + o)} L {x0 + step} {f(c + o)} L {edge - step} {f(ty)} L {edge} {f(ty)}")
    return out


def board(board):
    rnd = random.Random(5090)
    px, py, pw, ph = PACKAGE
    dx, dy, dw, dh = DIE
    # Flat fills only: Edge dithers gradients, and the noise would triple the size of the PNG.
    # Stacked translucent shapes give the die its sheen and the modules their bevel instead.
    out = [f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">
<defs>
  <filter id="wide" x="-80%" y="-80%" width="260%" height="260%"><feGaussianBlur stdDeviation="40"/></filter>
  <filter id="blur" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="3"/></filter>
  <filter id="glow" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="4"/></filter>
</defs>
<rect x="8" y="8" width="{W - 16}" height="{H - 16}" rx="12" fill="#0E1412"/>''']
    # A sparse field of vias gives the board its texture.
    vias = []
    for gx in range(20, W - 12, 12):
        for gy in range(20, H - 12, 12):
            if rnd.random() < 0.11:
                vias.append(f'<circle cx="{gx}" cy="{gy}" r="1.1"/>')
    out.append(f'<g fill="#2A3B33" opacity="0.8">{"".join(vias)}</g>')
    out.append(f'<circle cx="{f(CX)}" cy="{f(CY)}" r="96" fill="{GREEN}" opacity="0.2" filter="url(#wide)"/>')
    out.append(f'<g fill="none" stroke="{TRACE}" stroke-width="1.4" stroke-linejoin="round" stroke-linecap="round">'
               + "".join(f'<path d="{d}"/>' for d in traces(board)) + "</g>")
    # Chokes of the power stages: blocks in the top corners, rows beside the lower modules. The
    # partner boards keep their power stages in a column on the left instead.
    if board == "gb202":
        chokes = [(ox + kx, 15 + ky) for ox in (16, W - 60) for kx in (0, 24) for ky in (0, 23)]
        chokes += [(x, 340) for x in (18, 44, 70, 96, W - 116, W - 90, W - 64, W - 38)]
    else:
        chokes = [(W - 60 + kx, 15 + ky) for kx in (0, 24) for ky in (0, 23)]
        chokes += [(x, 340) for x in (W - 116, W - 90, W - 64, W - 38)]
    for x, y in chokes:
        out.append(f'<rect x="{x}" y="{y}" width="20" height="20" rx="3" fill="#1A1F22" stroke="#3A4247" stroke-width="1"/>')
        out.append(f'<rect x="{x + 5}" y="{y + 5}" width="10" height="10" rx="2" fill="#2A3135"/>')
    if board == "gb202":
        # Small parts between the two lower modules.
        for x, y, w, h in ((200, 336, 14, 8), (226, 336, 14, 8), (200, 352, 8, 5), (216, 352, 8, 5), (232, 352, 8, 5)):
            out.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="1.2" fill="#1A1F22" stroke="#3A4247" stroke-width="0.8"/>')
    else:
        # One phase per row: choke, power stage and capacitor.
        for i in range(7):
            y = 34 + i * 46
            out.append(f'<rect x="20" y="{y}" width="26" height="26" rx="3" fill="#1A1F22" stroke="#3A4247" stroke-width="1"/>')
            out.append(f'<rect x="27" y="{y + 7}" width="12" height="12" rx="2" fill="#2A3135"/>')
            out.append(f'<rect x="54" y="{y + 6}" width="14" height="14" rx="1.5" fill="#101417" stroke="#2E363B" stroke-width="0.8"/>')
            out.append(f'<circle cx="86" cy="{y + 13}" r="7.5" fill="#1A1F22" stroke="#5E6A70" stroke-width="1.2"/>')
            out.append(f'<circle cx="86" cy="{y + 13}" r="3" fill="#2A3135"/>')
    # Mounting holes of the cooler around the package.
    for hx, hy in ((112, 84), (W - 112, 84), (112, H - 84), (W - 112, H - 84)):
        out.append(f'<circle cx="{hx}" cy="{hy}" r="6.5" fill="#0A0D0C" stroke="#7E7350" stroke-width="2" opacity="0.9"/>')
    # Package with its ring of capacitors, then the die with a green edge glow and a sheen.
    out.append(f'<rect x="{px + 2}" y="{py + 4}" width="{pw}" height="{ph}" rx="10" fill="#000" opacity="0.5" filter="url(#blur)"/>')
    out.append(f'<rect x="{px}" y="{py}" width="{pw}" height="{ph}" rx="10" fill="#1D2723" stroke="#34433C" stroke-width="1"/>')
    caps = []
    for i in range(9):
        t = px + 22 + i * 17.5
        caps.append(f'<rect x="{f(t)}" y="{py + 9}" width="6" height="3" rx="0.6"/>')
        caps.append(f'<rect x="{f(t)}" y="{py + ph - 12}" width="6" height="3" rx="0.6"/>')
    for i in range(9):
        t = py + 22 + i * 17.5
        caps.append(f'<rect x="{px + 9}" y="{f(t)}" width="3" height="6" rx="0.6"/>')
        caps.append(f'<rect x="{px + pw - 12}" y="{f(t)}" width="3" height="6" rx="0.6"/>')
    out.append(f'<g fill="#B89F5A" opacity="0.6">{"".join(caps)}</g>')
    # The gold pin-1 mark; partition A lies opposite. The GB203 and GB205 boards turn the package to the right.
    if board in ("gb202", "gb206"):
        out.append(f'<path d="M {px + 5} {py + 5} L {px + 18} {py + 5} L {px + 5} {py + 18} Z" fill="{GOLD}"/>')
    else:
        out.append(f'<path d="M {px + pw - 5} {py + 5} L {px + pw - 18} {py + 5} L {px + pw - 5} {py + 18} Z" fill="{GOLD}"/>')
    out.append(f'<rect x="{dx - 3}" y="{dy - 3}" width="{dw + 6}" height="{dh + 6}" rx="6" fill="none" stroke="{GREEN}" '
               f'stroke-width="3" opacity="0.55" filter="url(#glow)"/>')
    out.append(f'<rect x="{dx}" y="{dy}" width="{dw}" height="{dh}" rx="4" fill="#161B20" stroke="#5B6872" stroke-width="1"/>')
    for reach, opacity in ((0.78, 0.035), (0.6, 0.04), (0.42, 0.045)):
        out.append(f'<path d="M {dx + 1} {dy + 1} L {f(dx + dw * reach)} {dy + 1} L {dx + 1} {f(dy + dh * reach)} Z" '
                   f'fill="#FFFFFF" opacity="{opacity}"/>')
    out.append(f'<rect x="{dx + 0.5}" y="{dy + 0.5}" width="{dw - 1}" height="{dh - 1}" rx="3.5" fill="none" stroke="{GREEN}" stroke-width="1" opacity="0.5"/>')
    # Empty module places: a field of solder pads.
    populated = chips(board)
    for pad in pads(board):
        if pad in populated:
            continue
        _, x, y, _ = pad
        out.append(f'<rect x="{x}" y="{y}" width="{CHIP_W}" height="{CHIP_H}" rx="4" fill="#111715" stroke="#2C3833" '
                   f'stroke-width="1" stroke-dasharray="3 2"/>')
        out.append('<g fill="#3A4A43">' + "".join(f'<rect x="{x + 9 + c * 8}" y="{y + 8 + r * 7}" width="3" height="3" rx="0.6"/>'
                                                   for c in range(6) for r in range(5)) + "</g>")
    # The memory modules: a soft shadow, the body, a highlight along the top and the pin-1 mark.
    for _, x, y, _ in populated:
        out.append(f'<rect x="{x + 1}" y="{y + 3}" width="{CHIP_W}" height="{CHIP_H}" rx="4" fill="#000" opacity="0.55" filter="url(#blur)"/>')
        out.append(f'<rect x="{x}" y="{y}" width="{CHIP_W}" height="{CHIP_H}" rx="4" fill="#15191C" stroke="#3D474F" stroke-width="1"/>')
        out.append(f'<rect x="{x + 1}" y="{y + 1}" width="{CHIP_W - 2}" height="{CHIP_H * 0.45:.1f}" rx="3" fill="#FFFFFF" opacity="0.035"/>')
        out.append(f'<path d="M {x + 4} {y + 1} L {x + CHIP_W - 4} {y + 1}" stroke="#FFFFFF" stroke-opacity="0.12" stroke-width="1"/>')
        out.append(f'<circle cx="{x + 5}" cy="{y + 5}" r="1.6" fill="#6C7881"/>')
    out.append(f'<rect x="8.5" y="8.5" width="{W - 17}" height="{H - 17}" rx="11.5" fill="none" stroke="{GREEN}" stroke-opacity="0.28" stroke-width="1"/>')
    out.append("</svg>")
    return "\n".join(out)


def render(out_dir, name, content, w, h):
    """Writes <name>.svg and renders <name>.png with a transparent background."""
    svg_path = os.path.join(out_dir, name + ".svg")
    png_path = os.path.join(out_dir, name + ".png")
    with open(svg_path, "w", encoding="utf-8") as fh:
        fh.write(content)
    if os.path.exists(png_path):
        os.remove(png_path)
    with tempfile.TemporaryDirectory() as work:
        html_path = os.path.join(work, "page.html")
        with open(html_path, "w", encoding="utf-8") as fh:
            fh.write('<!doctype html><html><head><style>html,body{margin:0;padding:0;background:transparent;overflow:hidden}'
                     'img{display:block}</style></head><body>'
                     f'<img src="{"file:///" + svg_path.replace(os.sep, "/")}" width="{w}" height="{h}"></body></html>')
        subprocess.run([EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run",
                        f"--user-data-dir={os.path.join(work, 'profile')}", f"--force-device-scale-factor={SCALE}",
                        "--default-background-color=00000000", f"--window-size={w},{h}", f"--screenshot={png_path}",
                        "file:///" + html_path.replace(os.sep, "/")], check=True, capture_output=True, timeout=60)
        # Edge can return before the screenshot is on disk.
        for _ in range(100):
            if os.path.exists(png_path) and os.path.getsize(png_path) > 0:
                break
            time.sleep(0.2)
    print(f"{name}.png {os.path.getsize(png_path)} bytes")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for chip in ("gb202", "gb203", "gb205", "gb206"):
        render(out, "blackwell-board-" + chip, board(chip), W, H)
