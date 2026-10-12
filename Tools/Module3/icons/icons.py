"""Module 3 icon set: the source of Assets/1.TeamWorkspace/Module3/Resources/M3Icons/*.png.

White line icons on a 64-unit grid, tinted in Unity by Image.color. Edit a body below and run
    python3 Tools/Module3/icons/icons.py && Tools/Module3/icons/rasterize.sh
(rasterize.sh compiles a small AppKit tool; macOS only). Writes one .svg per icon next to this
file and a contact sheet (sheet.svg) for checking them together on a dark background.
"""
import os

S = 'fill="none" stroke="white" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"'
T = 'fill="none" stroke="white" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"'
F = 'fill="white"'

HOUSE = f'<path {S} d="M7 31 L32 10 L57 31"/><path {S} d="M14 26 V55 H50 V26"/>'
PERSON = f'<circle {S} cx="32" cy="19" r="9"/><path {S} d="M13 56 C13 37 51 37 51 56"/>'
THERMO = (f'<path {S} d="M{{x}} 41 V13 a6 6 0 0 1 12 0 V41"/><circle {S} cx="{{cx}}" cy="47" r="9"/>'
          f'<path {S} d="M{{cx}} 47 V24"/>')
CUP = f'<path {S} d="M17 18 H47 L43 55 H21 Z"/><path {T} d="M19 31 H45"/>'
NET = (f'<path {S} d="M32 5 V10"/><path {S} d="M11 50 Q13 28 32 10 Q51 28 53 50"/><path {T} d="M32 10 Q24 30 22 50 M32 10 Q40 30 42 50 M32 10 V50"/>'
       f'<path {S} d="M6 52 H58"/>')

ICONS = {
    # HUD
    "clock": f'<circle {S} cx="32" cy="33" r="23"/><path {S} d="M32 33 V19 M32 33 L42 39"/><path {S} d="M26 5 H38"/>',
    "net": NET,
    "day": f'<circle {S} cx="32" cy="32" r="11"/>' + "".join(
        f'<path {S} d="M{32+17*c:.1f} {32+17*s:.1f} L{32+25*c:.1f} {32+25*s:.1f}"/>'
        for c, s in [(1,0),(0.707,0.707),(0,1),(-0.707,0.707),(-1,0),(-0.707,-0.707),(0,-1),(0.707,-0.707)]),
    # what you notice
    "fine": f'<circle {S} cx="32" cy="32" r="24"/><circle {F} cx="24" cy="27" r="3.5"/><circle {F} cx="40" cy="27" r="3.5"/><path {S} d="M21 38 Q32 48 43 38"/>',
    "fever": THERMO.format(x=26, cx=32),
    "feverDown": THERMO.format(x=16, cx=22) + f'<path {S} d="M47 12 V42 M38 33 L47 42 L56 33"/>',
    "bleeding": f'<path {F} d="M32 7 C32 7 13 31 13 41 A19 19 0 0 0 51 41 C51 31 32 7 32 7 Z"/>',
    "vomiting": f'<path {S} d="M14 24 H50 L46 55 H18 Z"/><path {S} d="M16 24 Q32 2 48 24"/><path {T} d="M17 34 H47"/>',
    "noWater": CUP + f'<path {S} d="M9 56 L55 8"/>',
    "netUp": NET,
    "healthCentre": f'<path {S} d="M7 29 L32 9 L57 29"/><path {S} d="M13 25 V56 H51 V25"/><path {F} d="M28.5 29 h7 v7 h7 v7 h-7 v7 h-7 v-7 h-7 v-7 h7 Z"/>',
    # what you can do
    "water": CUP + f'<path {F} d="M32 3 C32 3 26 10 26 13 A6 6 0 0 0 38 13 C38 10 32 3 32 3 Z"/>',
    "rest": f'<path {S} d="M7 22 V54 M57 40 V54"/><path {S} d="M7 44 H57 V40 H7"/><rect {S} x="12" y="33" width="13" height="7" rx="3"/><path {T} d="M34 10 H44 L34 20 H44 M47 4 H53 L47 10 H53"/>',
    "repellent": f'<rect {S} x="20" y="26" width="22" height="31" rx="5"/><path {S} d="M25 26 V17 H37 V26"/><path {S} d="M37 20 H45"/>' + "".join(
        f'<circle {F} cx="{x}" cy="{y}" r="2.6"/>' for x, y in [(52,13),(55,20),(52,27),(58,9),(59,26)]),
    "closeAtDusk": f'<path {S} d="M8 26 H56"/><path {S} d="M22 26 A10 10 0 0 1 42 26"/><path {T} d="M32 6 V10 M17 12 L20 15 M47 12 L44 15"/><rect {S} x="15" y="33" width="34" height="23" rx="2"/><path {S} d="M32 33 V56"/>',
    "fan": f'<circle {S} cx="32" cy="27" r="21"/><circle {F} cx="32" cy="27" r="4"/>'
           f'<path {F} d="M32 27 C27 19 30 11 36 10 C40 15 37 22 32 27 Z"/><path {F} d="M32 27 C41 26 46 32 44 38 C38 39 35 33 32 27 Z"/><path {F} d="M32 27 C28 35 20 38 17 33 C19 27 26 26 32 27 Z"/>'
           f'<path {S} d="M32 48 V57 M21 58 H43"/>',
    "screen": f'<rect {S} x="9" y="9" width="46" height="46" rx="3"/><path {T} d="M20.5 9 V55 M32 9 V55 M43.5 9 V55 M9 20.5 H55 M9 32 H55 M9 43.5 H55"/>',
    # feedback and navigation
    "check": f'<path fill="none" stroke="white" stroke-width="7" stroke-linecap="round" stroke-linejoin="round" d="M13 34 L27 48 L52 19"/>',
    "close": f'<path {S} d="M16 16 L48 48 M48 16 L16 48"/>',
    "arrow": f'<path {S} d="M10 32 H52 M38 18 L52 32 L38 46"/>',
    "house": HOUSE,
    "houseUnvisited": HOUSE + f'<path {T} d="M27 35 A5.5 5.5 0 1 1 33 41 V44"/><circle {F} cx="33" cy="50" r="2.4"/>',
    "person": PERSON,
    "personNew": f'<g transform="translate(-6 0)">{PERSON}</g><path {S} d="M52 8 V22 M45 15 H59"/>',
    "people": f'<g transform="translate(-3 9) scale(0.72)">{PERSON}</g><g transform="translate(21 9) scale(0.72)">{PERSON}</g>',
    "hiddenPerson": f'<circle {S} stroke-dasharray="5 6" cx="32" cy="19" r="9"/><path {S} stroke-dasharray="5 6" d="M13 56 C13 37 51 37 51 56"/>',
    "eye": f'<path {S} d="M5 32 Q32 6 59 32 Q32 58 5 32 Z"/><circle {F} cx="32" cy="32" r="8"/>',
    "calendar": f'<rect {S} x="9" y="13" width="46" height="42" rx="4"/><path {S} d="M9 25 H55 M21 7 V17 M43 7 V17"/>',
    "watch": f'<path {S} d="M24 15 L26 5 H38 L40 15 M24 49 L26 59 H38 L40 49"/><circle {S} cx="32" cy="32" r="16"/><path {T} d="M32 32 V24 M32 32 L38 35"/>',
    "headset": f'<rect {S} x="7" y="20" width="50" height="24" rx="11"/><path {S} d="M28 44 Q32 37 36 44"/><path {T} d="M7 30 H3 M57 30 H61"/>',
    "talk": f'<path {S} d="M8 12 H56 V42 H28 L16 54 V42 H8 Z"/>',
    "shield": f'<path {S} d="M32 6 L53 14 V30 C53 44 43 53 32 58 C21 53 11 44 11 30 V14 Z"/><path {S} d="M22 32 L29 39 L43 24"/>',
    "noNet": NET + f'<path {S} d="M9 58 L55 6"/>',
    "mosquito": f'<path {S} d="M32 22 V50"/><circle {F} cx="32" cy="18" r="5"/><path {T} d="M32 14 L32 4"/><path {S} d="M31 28 Q14 14 8 24 Q14 32 31 32 M33 28 Q50 14 56 24 Q50 32 33 32"/><path {T} d="M30 36 L18 46 M34 36 L46 46 M30 42 L22 56 M34 42 L42 56"/>',
}

def wrap(body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">{body}</svg>\n'

here = os.path.dirname(os.path.abspath(__file__))
for name, body in ICONS.items():
    with open(os.path.join(here, f"{name}.svg"), "w") as f:
        f.write(wrap(body))

cols = 8
rows = (len(ICONS) + cols - 1) // cols
sheet = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {cols*80} {rows*96}"><rect width="{cols*80}" height="{rows*96}" fill="#14171c"/>']
for i, (name, body) in enumerate(ICONS.items()):
    x, y = (i % cols) * 80 + 8, (i // cols) * 96 + 6
    sheet.append(f'<g transform="translate({x} {y})">{body}</g>'
                 f'<text x="{x+32}" y="{y+84}" fill="#9aa" font-size="10" font-family="Helvetica" text-anchor="middle">{name}</text>')
sheet.append("</svg>")
with open(os.path.join(here, "sheet.svg"), "w") as f:
    f.write("".join(sheet))
print(len(ICONS), "icons")
