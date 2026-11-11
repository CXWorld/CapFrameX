"""Renders the artwork of the anime starter profiles (CHERRY BLOSSOM, NEON NIGHT, LUNAR).

Every picture is drawn here from SVG primitives: no existing character, logo, brand mark or font
glyph is used. The background patterns (seigaiha waves, hui wen fret, auspicious clouds) are
traditional public-domain motifs. Each theme renders two PNGs through headless Microsoft Edge:
  <theme>-banner.png  760 x 320 SVG units, the banner behind the channel name
  <theme>-figure.png  332 x 360 SVG units, the character whose head rises above the banner
Both are exported at 0.8 device pixels per unit, 1.6 times the size of their tiles (380 x 160 and
166 x 180 at the design's scale of 0.8 before resolution scaling). The random seeds are fixed, so a
run reproduces the PNGs embedded in overlay-templates/Anime-*.json.

Usage: python -I scripts\\New-AnimeOverlayArt.py <output-dir> [cherry-blossom|neon-night|lunar ...]
"""
import math
import os
import random
import subprocess
import sys
import tempfile
import time

W, H = 760, 320     # banner
CW, CH = 332, 360   # figure
SCALE = 0.8         # device pixels per SVG unit
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


def f(v):
    return f"{v:.1f}".rstrip("0").rstrip(".")


# ----------------------------------------------------------------------------------------------
# Character (local box 300 x 320, face centred on x = 150)
# ----------------------------------------------------------------------------------------------

def eye(cx, cy, s, p, uid):
    """s = -1 for the left eye (outer corner left), +1 for the right one."""
    line = p["line"]
    if p.get("wink") == s:
        return (f'<path d="M {f(cx-17)} {f(cy+2)} Q {f(cx)} {f(cy-12)} {f(cx+17)} {f(cy+2)}" stroke="{line}" '
                f'stroke-width="4.5" fill="none" stroke-linecap="round"/>'
                f'<path d="M {f(cx+15*s)} {f(cy-1)} L {f(cx+24*s)} {f(cy-6)}" stroke="{line}" stroke-width="2.5" stroke-linecap="round"/>')
    outer, inner = cx + 20 * s, cx - 14 * s
    # A soft lid that drops toward the outer corner, its lash flicking outward: a kind look.
    return f'''
    <clipPath id="ec{uid}"><ellipse cx="{f(cx)}" cy="{f(cy+1)}" rx="18" ry="21"/></clipPath>
    <ellipse cx="{f(cx)}" cy="{f(cy+1)}" rx="18" ry="21" fill="#FFFFFF"/>
    <g clip-path="url(#ec{uid})">
      <ellipse cx="{f(cx)}" cy="{f(cy+4)}" rx="15" ry="20" fill="url(#iris)"/>
      <ellipse cx="{f(cx)}" cy="{f(cy+4)}" rx="15" ry="20" fill="none" stroke="{p['eye_ring']}" stroke-width="2"/>
      <ellipse cx="{f(cx)}" cy="{f(cy+3)}" rx="7" ry="10" fill="{p['eye_pupil']}"/>
      <ellipse cx="{f(cx)}" cy="{f(cy+16)}" rx="10" ry="5" fill="{p['eye_glow']}" opacity="0.75"/>
      <rect x="{f(cx-20)}" y="{f(cy-22)}" width="40" height="9" fill="{p['eye_ring']}" opacity="0.35"/>
    </g>
    <ellipse cx="{f(cx-6)}" cy="{f(cy-5)}" rx="5.5" ry="7" fill="#FFFFFF"/>
    <circle cx="{f(cx+6)}" cy="{f(cy+10)}" r="2.6" fill="#FFFFFF"/>
    <path d="M {f(inner)} {f(cy-14)} Q {f(cx+2*s)} {f(cy-30)} {f(outer)} {f(cy-7)}" stroke="{line}" stroke-width="4.5"
      fill="none" stroke-linecap="round"/>
    <path d="M {f(cx+15*s)} {f(cy-15)} L {f(cx+27*s)} {f(cy-9)} L {f(cx+20*s)} {f(cy-3)} Z" fill="{line}"/>
    <path d="M {f(cx+7*s)} {f(cy+22)} Q {f(cx+15*s)} {f(cy+19)} {f(cx+18*s)} {f(cy+12)}" stroke="{line}"
      stroke-width="2" fill="none" stroke-linecap="round"/>'''


def character(p):
    line, hair, hair_dark, hair_light = p["line"], p["hair"], p["hair_dark"], p["hair_light"]
    skin, shade = "#FFE6DA", "#F5C3B4"
    out = []
    out.append(f'''<defs>
      <linearGradient id="iris" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="{p['eye_top']}"/><stop offset="1" stop-color="{p['eye_bottom']}"/>
      </linearGradient>
      <linearGradient id="hairg" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="{hair}"/><stop offset="1" stop-color="{hair_dark}"/>
      </linearGradient>
      <clipPath id="faceclip"><path d="M 84 130 C 84 66 216 66 216 130 L 216 152 C 216 205 182 240 150 244 C 118 240 84 205 84 152 Z"/></clipPath>
    </defs>''')

    # Hair buns sit behind the head.
    if p.get("buns"):
        for x in (82, 218):
            out.append(f'<circle cx="{x}" cy="56" r="30" fill="url(#hairg)" stroke="{line}" stroke-width="2.5"/>')
            out.append(f'<path d="M {x-18} 46 Q {x} 30 {x+16} 44" stroke="{hair_light}" stroke-width="4" fill="none" opacity="0.6" stroke-linecap="round"/>')
        out.append(f'<path d="M 66 74 L 52 96 L 72 90 Z M 234 74 L 248 96 L 228 90 Z" fill="{p["ribbon"]}" stroke="{line}" stroke-width="2"/>')

    # Long hair behind the shoulders.
    out.append(f'<path d="M 60 150 C 40 70 95 22 150 22 C 205 22 260 70 240 150 C 238 210 256 262 266 322 '
               f'L 34 322 C 44 262 62 210 60 150 Z" fill="url(#hairg)" stroke="{line}" stroke-width="2.5"/>')

    # Dragon horns grow from the top of the head.
    if p.get("horns"):
        for s in (-1, 1):
            x0 = 150 + 40 * s
            out.append(f'<path d="M {x0-8*s} 54 C {x0+4*s} 26 {x0+30*s} 10 {x0+52*s} -6 C {x0+40*s} 16 {x0+22*s} 34 {x0+12*s} 60 Z" '
                       f'fill="url(#gold)" stroke="{line}" stroke-width="2.5" stroke-linejoin="round"/>')
            out.append(f'<path d="M {x0+18*s} 30 L {x0+34*s} 26 L {x0+24*s} 38 Z" fill="url(#gold)" stroke="{line}" stroke-width="2"/>')

    # Neck, then the outfit.
    out.append(f'<path d="M 132 222 L 168 222 L 170 268 L 130 268 Z" fill="{shade}" stroke="{line}" stroke-width="2.5"/>')
    out.append(p["outfit"])

    # Face with the cel shadow under the bangs.
    out.append(f'<path d="M 84 130 C 84 66 216 66 216 130 L 216 152 C 216 205 182 240 150 244 C 118 240 84 205 84 152 Z" '
               f'fill="{skin}" stroke="{line}" stroke-width="2.5"/>')
    out.append(f'<g clip-path="url(#faceclip)"><path d="M 80 128 Q 120 160 150 150 Q 182 160 220 126 L 220 60 L 80 60 Z" fill="{shade}"/></g>')

    # Eyes, nose, mouth and blush.
    out.append(eye(120, 170, -1, p, "l"))
    out.append(eye(180, 170, 1, p, "r"))
    out.append(f'<path d="M 149 192 L 151 195" stroke="{line}" stroke-width="2" stroke-linecap="round" opacity="0.6"/>')
    out.append(p.get("mouth", f'<path d="M 140 206 Q 150 218 160 206 Q 150 209 140 206 Z" fill="#B8344E" stroke="{line}" stroke-width="1.8" stroke-linejoin="round"/>'
                               f'<path d="M 145 211 Q 150 215 155 211 Q 150 210 145 211 Z" fill="#FF8C9E"/>'))
    for x in (106, 194):
        out.append(f'<ellipse cx="{x}" cy="198" rx="15" ry="8" fill="#FF7E9C" opacity="0.45"/>')
        for k in (-6, 0, 6):
            out.append(f'<path d="M {x+k-2} 202 L {x+k+3} 194" stroke="#E0506E" stroke-width="1.6" stroke-linecap="round" opacity="0.7"/>')

    # Side locks framing the face, then the bangs over the forehead.
    out.append(f'<path d="M 86 118 C 68 170 70 226 94 262 C 92 226 98 186 104 150 Z" fill="url(#hairg)" stroke="{line}" stroke-width="2.5" stroke-linejoin="round"/>')
    out.append(f'<path d="M 214 118 C 232 170 230 226 206 262 C 208 226 202 186 196 150 Z" fill="url(#hairg)" stroke="{line}" stroke-width="2.5" stroke-linejoin="round"/>')
    out.append(f'<path d="M 74 164 C 60 80 108 36 150 36 C 192 36 240 80 226 164 '
               f'Q 222 146 216 134 Q 214 150 206 158 Q 204 138 194 126 Q 190 140 180 148 Q 176 132 166 124 '
               f'Q 162 138 152 146 Q 148 132 140 124 Q 134 140 124 148 Q 122 134 112 126 Q 108 146 98 156 '
               f'Q 94 142 86 134 Q 80 150 74 164 Z" fill="url(#hairg)" stroke="{line}" stroke-width="2.5" stroke-linejoin="round"/>')
    # Brows show through the bangs, as anime draws them.
    out.append(f'<path d="M 106 137 Q 118 129 131 133 M 169 133 Q 182 129 194 137" stroke="{line}" stroke-width="2.4" '
               f'fill="none" stroke-linecap="round" opacity="0.55"/>')
    out.append(f'<path d="M 96 74 Q 150 46 204 74" stroke="{hair_light}" stroke-width="7" fill="none" stroke-linecap="round" opacity="0.75"/>')
    out.append(f'<path d="M 106 70 Q 116 64 126 61" stroke="#FFFFFF" stroke-width="3" fill="none" stroke-linecap="round" opacity="0.7"/>')

    out.append(p.get("accessory", ""))
    return "\n".join(out)


# ----------------------------------------------------------------------------------------------
# Shared pieces
# ----------------------------------------------------------------------------------------------

def blossom(x, y, r, rot, petal="#FFB7CF", edge="#F4789F", centre="#FFF4C2"):
    parts = [f'<g transform="translate({f(x)} {f(y)}) rotate({f(rot)})">']
    for k in range(5):
        a = k * 72
        parts.append(f'<path d="M 0 0 C {f(-r*0.55)} {f(-r*0.35)} {f(-r*0.5)} {f(-r*1.0)} 0 {f(-r*1.05)} '
                     f'C {f(r*0.08)} {f(-r*0.86)} {f(r*0.16)} {f(-r*0.98)} {f(r*0.2)} {f(-r*1.0)} '
                     f'C {f(r*0.55)} {f(-r*0.9)} {f(r*0.55)} {f(-r*0.35)} 0 0 Z" fill="{petal}" stroke="{edge}" '
                     f'stroke-width="1.2" transform="rotate({a})"/>')
    parts.append(f'<circle r="{f(r*0.22)}" fill="{centre}"/>')
    for k in range(5):
        a = math.radians(k * 72 + 36)
        parts.append(f'<circle cx="{f(math.sin(a)*r*0.38)}" cy="{f(-math.cos(a)*r*0.38)}" r="{f(r*0.07)}" fill="{edge}"/>')
    parts.append("</g>")
    return "".join(parts)


def petal(x, y, size, rot, color="#FFC2D6", opacity=0.9):
    return (f'<path transform="translate({f(x)} {f(y)}) rotate({f(rot)}) scale({f(size)})" '
            f'd="M 0 -6 C 5 -4 6 3 0 7 C -6 3 -5 -4 0 -6 Z M 0 -6 L 1.5 -3 L 0 -4.2 Z" fill="{color}" opacity="{opacity}"/>')


def seigaiha(x0, x1, y0, y1, stroke, fill, opacity):
    """Rows of overlapping concentric waves (seigaiha, a traditional Japanese pattern)."""
    rows, r = [], 22
    y, row = y0, 0
    while y < y1 + r:
        x = x0 - (r if row % 2 else 0)
        while x < x1 + r:
            g = [f'<circle cx="{f(x)}" cy="{f(y)}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>']
            for k in (16, 10, 4):
                g.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{k}" fill="none" stroke="{stroke}" stroke-width="1.6"/>')
            rows.append("".join(g))
            x += 2 * r
        y += r / 2
        row += 1
    return f'<g mask="url(#fade)" opacity="{opacity}">' + "".join(rows) + "</g>"


def huiwen(x0, x1, y, stroke):
    """A band of nested squares (hui wen, a traditional Chinese fret)."""
    out, x = [], x0
    while x + 14 <= x1:
        out.append(f'<rect x="{f(x)}" y="{f(y)}" width="14" height="14" fill="none" stroke="{stroke}" stroke-width="1.6"/>'
                   f'<rect x="{f(x+4)}" y="{f(y+4)}" width="6" height="6" fill="none" stroke="{stroke}" stroke-width="1.6"/>')
        x += 20
    return f'<g mask="url(#fade)" opacity="0.7">' + "".join(out) + "</g>"


def banner_svg(body, defs=""):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">
<defs>
  <clipPath id="card"><rect width="{W}" height="{H}" rx="16"/></clipPath>
  <filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="18"/></filter>
  <filter id="glow" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="4"/></filter>
  <linearGradient id="fadeg" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#FFFFFF"/><stop offset="0.35" stop-color="#FFFFFF" stop-opacity="0.6"/><stop offset="0.55" stop-color="#FFFFFF" stop-opacity="0"/></linearGradient>
  <mask id="fade"><rect width="{W}" height="{H}" fill="url(#fadeg)"/></mask>
  <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFE7A3"/><stop offset="1" stop-color="#D99A2B"/></linearGradient>
  {defs}
</defs>
<g clip-path="url(#card)">
{body}
</g>
</svg>'''


def figure_svg(p):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{CW}" height="{CH}" viewBox="0 0 {CW} {CH}">
<defs>
  <filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="18"/></filter>
  <filter id="glow" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="4"/></filter>
  <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFE7A3"/><stop offset="1" stop-color="#D99A2B"/></linearGradient>
</defs>
<g transform="translate(4 14) scale(1.08)">{character(p)}</g>
</svg>'''


# ----------------------------------------------------------------------------------------------
# CHERRY BLOSSOM: pastel, cherry blossoms, seigaiha waves
# ----------------------------------------------------------------------------------------------

def cherry_blossom():
    rnd = random.Random(7)
    p = dict(line="#6A2E4F", hair="#FFA9C8", hair_dark="#E5779F", hair_light="#FFE0EC",
             eye_top="#0F5C55", eye_bottom="#5FE0C2", eye_pupil="#06302B", eye_ring="#0B4741", eye_glow="#D8FFF4")
    p["outfit"] = '''
      <path d="M 54 322 C 60 284 92 264 130 260 L 170 260 C 208 264 240 284 246 322 Z" fill="#FFFFFF" stroke="#6A2E4F" stroke-width="2.5"/>
      <path d="M 70 300 C 84 278 108 266 130 262 L 150 304 L 170 262 C 192 266 216 278 230 300 L 214 322 L 86 322 Z" fill="#FF7FAE" stroke="#6A2E4F" stroke-width="2.5" stroke-linejoin="round"/>
      <path d="M 82 302 C 94 286 112 276 128 272" stroke="#FFFFFF" stroke-width="3" fill="none"/>
      <path d="M 218 302 C 206 286 188 276 172 272" stroke="#FFFFFF" stroke-width="3" fill="none"/>
      <path d="M 150 300 L 122 286 L 126 312 Z M 150 300 L 178 286 L 174 312 Z" fill="#E8336F" stroke="#6A2E4F" stroke-width="2" stroke-linejoin="round"/>
      <circle cx="150" cy="300" r="6" fill="#E8336F" stroke="#6A2E4F" stroke-width="2"/>'''
    bow = ('<g transform="translate(104 40) rotate(-18)" stroke="#6A2E4F" stroke-width="2.5" stroke-linejoin="round">'
           '<path d="M -3 4 L -18 34 L -6 29 L 2 38 L 6 6 Z" fill="#E8336F"/>'
           '<path d="M 0 0 C -12 -26 -48 -30 -50 -6 C -52 14 -16 14 0 0 Z" fill="#FF5C9A"/>'
           '<path d="M 0 0 C 12 -26 48 -30 50 -6 C 52 14 16 14 0 0 Z" fill="#FF5C9A"/>'
           '<path d="M -10 -6 C -20 -18 -36 -18 -40 -8" fill="none" stroke="#FFC2D9" stroke-width="3"/>'
           '<path d="M 10 -6 C 20 -18 36 -18 40 -8" fill="none" stroke="#FFC2D9" stroke-width="3"/>'
           '<circle r="8" fill="#E8336F"/></g>')
    p["accessory"] = bow + blossom(206, 92, 15, 12, petal="#FFFFFF", edge="#F27BA3") + blossom(222, 108, 9, 40, petal="#FFD1E1", edge="#F27BA3")

    body = ['<rect width="760" height="320" fill="url(#sky)"/>']
    for _ in range(14):  # soft bokeh
        x, y, r = rnd.uniform(300, 760), rnd.uniform(0, 320), rnd.uniform(14, 46)
        body.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="#FFFFFF" opacity="{f(rnd.uniform(0.15, 0.35))}"/>')
    # Branch across the top right, blossoms along it.
    body.append('<path d="M 770 18 C 700 30 650 22 600 44 C 560 62 520 58 470 52 M 640 30 C 620 6 596 -2 570 -6 '
                'M 560 58 C 540 80 532 96 536 118" stroke="#7A4A3A" stroke-width="7" fill="none" stroke-linecap="round"/>')
    for x, y, r, rot in ((470, 52, 16, 10), (505, 62, 12, 40), (548, 50, 18, 70), (590, 0, 15, 20), (612, 36, 20, 0),
                         (660, 22, 17, 30), (700, 34, 14, 55), (742, 18, 19, 15), (536, 112, 13, 5), (566, 82, 11, 50)):
        body.append(blossom(x, y, r, rot))
    for _ in range(26):  # falling petals
        body.append(petal(rnd.uniform(240, 760), rnd.uniform(0, 320), rnd.uniform(0.9, 1.9), rnd.uniform(0, 360),
                          rnd.choice(["#FFC2D6", "#FFFFFF", "#FF9FC0"]), f(rnd.uniform(0.6, 0.95))))
    for _ in range(10):
        body.append(petal(rnd.uniform(20, 300), rnd.uniform(10, 310), rnd.uniform(0.8, 1.4), rnd.uniform(0, 360), "#FFB3CB", 0.55))
    body.append('<rect width="760" height="320" fill="url(#veil)"/>')
    body.append(seigaiha(0, 420, 196, 330, "#F7A8C6", "#FFF5F9", 0.55))
    defs = '''<linearGradient id="sky" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFF6FA"/><stop offset="0.55" stop-color="#FFE0EC"/><stop offset="1" stop-color="#E9D9FF"/></linearGradient>
  <linearGradient id="veil" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#FFF7FB" stop-opacity="0.85"/><stop offset="0.42" stop-color="#FFF7FB" stop-opacity="0.55"/><stop offset="0.6" stop-color="#FFF7FB" stop-opacity="0"/></linearGradient>'''
    return banner_svg("\n".join(body), defs), p


# ----------------------------------------------------------------------------------------------
# NEON NIGHT: rainy city at night, neon signs without text
# ----------------------------------------------------------------------------------------------

def neon_night():
    rnd = random.Random(11)
    p = dict(line="#140C2E", hair="#DCD0FF", hair_dark="#8C78DE", hair_light="#F8F5FF",
             eye_top="#5C0A49", eye_bottom="#FF5FD0", eye_pupil="#2A0322", eye_ring="#40062F", eye_glow="#FFD3F4",
             wink=-1)
    p["mouth"] = ('<path d="M 141 207 Q 152 214 161 204" stroke="#140C2E" stroke-width="2.4" fill="none" stroke-linecap="round"/>'
                  '<path d="M 156 208 Q 159 214 163 210" fill="#FF8CB0" stroke="#140C2E" stroke-width="1.4"/>')
    p["outfit"] = '''
      <path d="M 52 322 C 58 284 90 264 128 258 L 172 258 C 210 264 242 284 248 322 Z" fill="#1C1636" stroke="#140C2E" stroke-width="2.5"/>
      <path d="M 112 262 L 128 250 L 150 288 L 172 250 L 188 262 L 168 322 L 132 322 Z" fill="#2A2252" stroke="#140C2E" stroke-width="2.5" stroke-linejoin="round"/>
      <path d="M 120 264 L 150 312 L 180 264" stroke="#00E5FF" stroke-width="3" fill="none" filter="url(#glow)"/>
      <path d="M 120 264 L 150 312 L 180 264" stroke="#B9F7FF" stroke-width="1.5" fill="none"/>
      <path d="M 70 300 L 96 290 M 230 300 L 204 290" stroke="#FF2BD6" stroke-width="4" stroke-linecap="round"/>
      <rect x="130" y="236" width="40" height="9" rx="4" fill="#140C2E"/>
      <path d="M 150 245 L 145 254 L 150 260 L 155 254 Z" fill="#FF2BD6" stroke="#FFD3F4" stroke-width="1"/>'''
    # Headphones with cat-ear shells, glowing rims.
    hp = ['<path d="M 74 150 C 60 40 240 40 226 150" stroke="#1F1A3A" stroke-width="14" fill="none" stroke-linecap="round"/>',
          '<path d="M 74 150 C 60 40 240 40 226 150" stroke="#00E5FF" stroke-width="3" fill="none" opacity="0.9"/>']
    for s in (-1, 1):
        x = 150 + 80 * s
        hp.append(f'<path d="M {x-26*s} 66 L {x+4*s} 18 L {x+22*s} 70 Z" fill="#1F1A3A" stroke="#00E5FF" stroke-width="3" stroke-linejoin="round"/>')
        hp.append(f'<path d="M {x-14*s} 62 L {x+3*s} 34 L {x+12*s} 64 Z" fill="#FF2BD6" opacity="0.85"/>')
        hp.append(f'<rect x="{x-18 if s < 0 else x-10}" y="128" width="28" height="56" rx="12" fill="#1F1A3A" stroke="#140C2E" stroke-width="2.5"/>')
        hp.append(f'<rect x="{x-13 if s < 0 else x-5}" y="136" width="18" height="40" rx="8" fill="none" stroke="#00E5FF" stroke-width="3" filter="url(#glow)"/>')
        hp.append(f'<rect x="{x-13 if s < 0 else x-5}" y="136" width="18" height="40" rx="8" fill="none" stroke="#C8FAFF" stroke-width="1.4"/>')
    p["accessory"] = "\n".join(hp)

    body = ['<rect width="760" height="320" fill="url(#night)"/>']
    # Back row of towers, then a nearer row with lit windows and abstract neon signs.
    x = 0
    while x < 760:
        w, h = rnd.uniform(34, 70), rnd.uniform(120, 250)
        body.append(f'<rect x="{f(x)}" y="{f(320-h)}" width="{f(w)}" height="{f(h)}" fill="#1A1440"/>')
        x += w + rnd.uniform(2, 8)
    x = -10
    while x < 760:
        w, h = rnd.uniform(46, 96), rnd.uniform(70, 190)
        top = 320 - h
        body.append(f'<rect x="{f(x)}" y="{f(top)}" width="{f(w)}" height="{f(h)}" fill="#0F0B26"/>')
        for wy in range(int(top) + 10, 316, 14):
            for wx in range(int(x) + 6, int(x + w) - 6, 11):
                if rnd.random() < 0.3:
                    c = rnd.choice(["#FFE08A", "#7FF3FF", "#FF9DEB"])
                    body.append(f'<rect x="{wx}" y="{wy}" width="5" height="7" fill="{c}" opacity="{f(rnd.uniform(0.35, 0.8))}"/>')
        if rnd.random() < 0.55:
            c = rnd.choice(["#FF2BD6", "#00E5FF", "#B26BFF"])
            sx, sy, sh = x + w * 0.5 - 7, top + rnd.uniform(14, 40), rnd.uniform(40, 80)
            body.append(f'<rect x="{f(sx)}" y="{f(sy)}" width="14" height="{f(sh)}" rx="5" fill="none" stroke="{c}" stroke-width="4" filter="url(#glow)"/>')
            body.append(f'<rect x="{f(sx)}" y="{f(sy)}" width="14" height="{f(sh)}" rx="5" fill="none" stroke="#FFFFFF" stroke-width="1.2" opacity="0.8"/>')
            for k in range(int(sh // 16)):
                body.append(f'<rect x="{f(sx+4)}" y="{f(sy+6+k*16)}" width="6" height="8" rx="1.5" fill="{c}" opacity="0.9"/>')
        x += w + rnd.uniform(4, 14)
    body.append('<rect y="260" width="760" height="60" fill="url(#haze)"/>')
    body.append('<circle cx="600" cy="120" r="120" fill="#FF2BD6" opacity="0.18" filter="url(#soft)"/>')
    for _ in range(70):  # rain
        rx, ry, ln = rnd.uniform(0, 760), rnd.uniform(-20, 320), rnd.uniform(10, 26)
        body.append(f'<path d="M {f(rx)} {f(ry)} l -4 {f(ln)}" stroke="#9FDBFF" stroke-width="1.2" opacity="{f(rnd.uniform(0.15, 0.45))}"/>')
    body.append('<rect width="760" height="320" fill="url(#veil)"/>')
    body.append('<rect x="1.5" y="1.5" width="757" height="317" rx="15" fill="none" stroke="#FF2BD6" stroke-width="3" opacity="0.8"/>')
    defs = '''<linearGradient id="night" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#090722"/><stop offset="0.7" stop-color="#26104A"/><stop offset="1" stop-color="#3D0E52"/></linearGradient>
  <linearGradient id="haze" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FF2BD6" stop-opacity="0"/><stop offset="1" stop-color="#FF2BD6" stop-opacity="0.35"/></linearGradient>
  <linearGradient id="veil" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#07051A" stop-opacity="0.88"/><stop offset="0.45" stop-color="#07051A" stop-opacity="0.55"/><stop offset="0.62" stop-color="#07051A" stop-opacity="0"/></linearGradient>'''
    return banner_svg("\n".join(body), defs), p


# ----------------------------------------------------------------------------------------------
# LUNAR: red and gold, full moon, lanterns, auspicious clouds, hui wen fret
# ----------------------------------------------------------------------------------------------

def cloud(x, y, sc, stroke="#E8B64C", fill="#5A1010"):
    """Auspicious cloud: a row of curls with spiral ends (a traditional, public-domain motif)."""
    return (f'<g transform="translate({f(x)} {f(y)}) scale({f(sc)})" fill="{fill}" stroke="{stroke}" stroke-width="2.4" stroke-linecap="round">'
            '<path d="M -60 10 C -60 -8 -40 -14 -30 -4 C -28 -26 2 -30 8 -10 C 14 -26 44 -22 42 -2 C 60 -6 66 14 50 18 L -52 18 C -58 18 -60 14 -60 10 Z"/>'
            '<path d="M -30 -4 C -26 6 -14 6 -12 -2 C -10 -8 -18 -12 -22 -6" fill="none"/>'
            '<path d="M 8 -10 C 10 2 24 4 26 -4 C 28 -10 20 -14 16 -8" fill="none"/>'
            '<path d="M 42 -2 C 40 8 50 10 52 4" fill="none"/></g>')


def lantern(x, y, sc):
    return (f'<g transform="translate({f(x)} {f(y)}) scale({f(sc)})">'
            '<path d="M 0 -60 L 0 -30" stroke="#E8B64C" stroke-width="2"/>'
            '<rect x="-12" y="-32" width="24" height="7" rx="2" fill="url(#gold)"/>'
            '<ellipse cx="0" cy="0" rx="28" ry="26" fill="#D7262E" stroke="#7A0E14" stroke-width="2"/>'
            '<path d="M -14 -23 Q -22 0 -14 23 M 0 -26 L 0 26 M 14 -23 Q 22 0 14 23" stroke="#A3161D" stroke-width="2" fill="none"/>'
            '<ellipse cx="-9" cy="-8" rx="6" ry="10" fill="#FF8A70" opacity="0.5"/>'
            '<rect x="-12" y="25" width="24" height="7" rx="2" fill="url(#gold)"/>'
            '<path d="M -4 32 L -6 54 M 0 32 L 0 56 M 4 32 L 6 54" stroke="#E8B64C" stroke-width="2"/></g>')


def lunar():
    rnd = random.Random(23)
    p = dict(line="#2A0E12", hair="#3A2830", hair_dark="#160C12", hair_light="#8A6A7A",
             eye_top="#6A2A00", eye_bottom="#FFC54A", eye_pupil="#2A0E00", eye_ring="#4A1C00", eye_glow="#FFF0C2",
             horns=True, buns=True, ribbon="#E0242E")
    p["mouth"] = '<path d="M 142 207 Q 150 214 158 207" stroke="#2A0E12" stroke-width="2.4" fill="none" stroke-linecap="round"/>'
    p["outfit"] = '''
      <path d="M 52 322 C 58 284 90 264 128 258 L 172 258 C 210 264 242 284 248 322 Z" fill="#C61F2B" stroke="#2A0E12" stroke-width="2.5"/>
      <path d="M 124 252 L 176 252 L 178 270 Q 150 278 122 270 Z" fill="#C61F2B" stroke="#2A0E12" stroke-width="2.5" stroke-linejoin="round"/>
      <path d="M 124 262 Q 150 270 176 262" stroke="#E8B64C" stroke-width="3" fill="none"/>
      <path d="M 150 272 C 166 282 186 290 204 322" stroke="#E8B64C" stroke-width="4" fill="none"/>
      <path d="M 150 272 C 166 282 186 290 204 322" stroke="#7A0E14" stroke-width="1.2" fill="none"/>
      <g fill="url(#gold)" stroke="#7A0E14" stroke-width="1"><circle cx="166" cy="283" r="4.5"/><circle cx="182" cy="294" r="4.5"/><circle cx="194" cy="308" r="4.5"/></g>
      <path d="M 64 300 C 80 292 96 290 110 292 M 236 300 C 224 294 214 292 204 292" stroke="#E8B64C" stroke-width="2" fill="none" opacity="0.8"/>'''
    p["accessory"] = ('<g transform="translate(200 96)"><circle r="9" fill="#3FD1A0" stroke="#1F6B52" stroke-width="2"/>'
                      '<circle r="3.5" fill="#C9FFE9"/><path d="M 4 9 L 10 30 M -2 9 L -4 26" stroke="#E8B64C" stroke-width="2"/>'
                      '<circle cx="10" cy="32" r="3" fill="#E0242E"/><circle cx="-4" cy="28" r="3" fill="#E0242E"/></g>')

    body = ['<rect width="760" height="320" fill="url(#dusk)"/>']
    body.append('<circle cx="600" cy="120" r="130" fill="#FFD27A" opacity="0.35" filter="url(#soft)"/>')
    body.append('<circle cx="600" cy="120" r="104" fill="url(#moon)"/>')
    for cx, cy, r in ((560, 90, 14), (640, 150, 10), (618, 80, 7), (575, 160, 8)):
        body.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="#E9B65A" opacity="0.35"/>')
    # Ink mountains fading into the dusk.
    body.append('<path d="M 0 320 L 0 250 C 60 220 90 190 140 214 C 190 236 210 196 260 210 C 320 226 360 250 420 236 '
                'C 470 224 520 254 580 246 C 640 238 700 256 760 240 L 760 320 Z" fill="#2A0A0E" opacity="0.85"/>')
    body.append('<path d="M 0 320 L 0 280 C 80 262 140 276 220 266 C 300 256 360 286 460 276 C 560 266 640 288 760 270 L 760 320 Z" fill="#160406"/>')
    body.append(cloud(470, 196, 1.1))
    body.append(cloud(712, 210, 0.9))
    body.append(cloud(250, 120, 0.75, stroke="#B98A3A", fill="#4A0C0E"))
    body.append(lantern(330, 64, 0.9))
    body.append(lantern(392, 40, 0.7))
    for _ in range(40):  # gold sparkles
        x, y, r = rnd.uniform(200, 760), rnd.uniform(0, 300), rnd.uniform(1, 2.8)
        body.append(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(r)}" fill="#FFE7A3" opacity="{f(rnd.uniform(0.4, 0.95))}"/>')
    body.append('<rect width="760" height="320" fill="url(#veil)"/>')
    body.append(huiwen(24, 420, 286, "#E8B64C"))
    body.append('<rect x="5" y="5" width="750" height="310" rx="12" fill="none" stroke="#E8B64C" stroke-width="2.5" opacity="0.9"/>')
    body.append('<rect x="11" y="11" width="738" height="298" rx="8" fill="none" stroke="#E8B64C" stroke-width="1" opacity="0.55"/>')
    defs = '''<linearGradient id="dusk" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3B0610"/><stop offset="0.6" stop-color="#7A1320"/><stop offset="1" stop-color="#2A060A"/></linearGradient>
  <radialGradient id="moon" cx="0.4" cy="0.35" r="0.75"><stop offset="0" stop-color="#FFF6D6"/><stop offset="0.7" stop-color="#FFD88A"/><stop offset="1" stop-color="#F0B04A"/></radialGradient>
  <linearGradient id="veil" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#1E0407" stop-opacity="0.9"/><stop offset="0.42" stop-color="#1E0407" stop-opacity="0.55"/><stop offset="0.6" stop-color="#1E0407" stop-opacity="0"/></linearGradient>'''
    return banner_svg("\n".join(body), defs), p


THEMES = {"cherry-blossom": cherry_blossom, "neon-night": neon_night, "lunar": lunar}


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
    if len(sys.argv) < 2 or any(theme not in THEMES for theme in sys.argv[2:]):
        sys.exit(__doc__)
    out = os.path.abspath(sys.argv[1])
    os.makedirs(out, exist_ok=True)
    for theme in sys.argv[2:] or THEMES:
        banner, figure = THEMES[theme]()
        render(out, theme + "-banner", banner, W, H)
        render(out, theme + "-figure", figure_svg(figure), CW, CH)
