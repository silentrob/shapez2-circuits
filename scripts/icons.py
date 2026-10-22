"""Draws the Circuits mod's toolbar icons in the game's icon style: chunky shapes with a thick dark outline,
a near-white body shaded to light grey towards the bottom, and orange accents shaded the same way.

Run from anywhere: python scripts/icons.py. Writes 256x256 PNGs into Circuits/Resources. Shapes are drawn at
4x and scaled down, for smooth edges. Colours were sampled from vanilla toolbar icons.

The board icons are the game's own foundation icons with its wires bolt on them, read from the mod's icon-dump
folder (circuits.dumpicons in the game's console writes it); without that folder they are left as they are.
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZE = 256
SCALE = 4
S = SIZE * SCALE
OUTLINE = 9 * SCALE  # outline thickness at final size: 9 px

DARK = (36, 40, 46)
WHITE = ((253, 253, 253), (200, 204, 206))  # top, bottom
SILVER = ((214, 218, 220), (150, 156, 160))
ORANGE = ((248, 164, 64), (224, 116, 38))
GROOVE = ((120, 126, 132), (96, 102, 108))

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Circuits', 'Resources')
DUMP = os.path.join(os.environ.get('USERPROFILE', ''), 'AppData', 'LocalLow', 'tobspr Games', 'shapez 2', 'mods',
                    'Circuits', 'icon-dump')


def px(v):
    """Final-size pixels to drawing pixels."""
    return int(round(v * SCALE))


class Icon:
    def __init__(self):
        self.image = Image.new('RGBA', (S, S), (0, 0, 0, 0))

    def shape(self, draw_mask, colours, outline=True):
        """Adds one shape: draw_mask(draw) paints it white on a mask. Outlined, then filled with a vertical
        gradient between the shape's own top and bottom."""
        mask = Image.new('L', (S, S), 0)
        draw_mask(ImageDraw.Draw(mask))
        box = mask.getbbox()
        if box is None:
            return self
        if outline:
            ring = mask.filter(ImageFilter.MaxFilter(OUTLINE * 2 + 1))
            self.image.paste(Image.new('RGBA', (S, S), DARK + (255,)), (0, 0), ring)
        top, bottom = colours
        gradient = Image.new('RGBA', (S, S))
        g = ImageDraw.Draw(gradient)
        y0, y1 = box[1], box[3]
        for y in range(S):
            t = min(max((y - y0) / max(y1 - y0, 1), 0), 1)
            g.line([(0, y), (S, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,))
        self.image.paste(gradient, (0, 0), mask)
        return self

    def save(self, name):
        self.image.resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(OUT, name))
        print('wrote', name)


def rect(x0, y0, x1, y1, r=0):
    return lambda d: d.rounded_rectangle([px(x0), px(y0), px(x1), px(y1)], radius=px(r), fill=255)


def ellipse(cx, cy, r):
    return lambda d: d.ellipse([px(cx - r), px(cy - r), px(cx + r), px(cy + r)], fill=255)


def polygon(*points):
    return lambda d: d.polygon([(px(x), px(y)) for x, y in points], fill=255)


def union(*parts):
    def draw(d):
        for part in parts:
            part(d)
    return draw


def chip(width=1, height=1, name='IC_Icon.png'):
    """A chip of width x height tiles, drawn the way the game draws its display sizes: one body, split into its
    tiles by grooves, with legs on every tile edge and the pin-1 dot in the north-west corner."""
    icon = Icon()
    cell = 140 if max(width, height) == 1 else 84
    w, h = cell * width, cell * height
    x0, y0 = (SIZE - w) / 2, (SIZE - h) / 2
    reach = 32 if max(width, height) == 1 else 26
    offsets = (-40, 0, 40) if max(width, height) == 1 else (-17, 17)
    legs = []
    for i in range(width):
        for off in offsets:
            x = x0 + cell * (i + 0.5) + off
            legs += [rect(x - 8, y0 - reach, x + 8, y0 + 12), rect(x - 8, y0 + h - 12, x + 8, y0 + h + reach)]
    for j in range(height):
        for off in offsets:
            y = y0 + cell * (j + 0.5) + off
            legs += [rect(x0 - reach, y - 8, x0 + 12, y + 8), rect(x0 + w - 12, y - 8, x0 + w + reach, y + 8)]
    icon.shape(union(*legs), SILVER)
    icon.shape(rect(x0, y0, x0 + w, y0 + h, 20), WHITE)
    grooves = [rect(x0 + cell * i - 3, y0 + 14, x0 + cell * i + 3, y0 + h - 14) for i in range(1, width)]
    grooves += [rect(x0 + 14, y0 + cell * j - 3, x0 + w - 14, y0 + cell * j + 3) for j in range(1, height)]
    if grooves:
        icon.shape(union(*grooves), GROOVE, outline=False)
    dot = 15 if max(width, height) == 1 else 11
    icon.shape(ellipse(x0 + dot + 19, y0 + dot + 19, dot), ORANGE)
    icon.save(name)


def text(x, y, words, size, font='ariblk.ttf'):
    """Text centred on (x, y), as vanilla gate icons carry a symbol (the AND gate's &)."""
    face = ImageFont.truetype(os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', font), px(size))
    return lambda d: d.text((px(x), px(y)), words, font=face, anchor='mm', fill=255)


def latch():
    icon = Icon()
    # A gate body, flat on the left and round on the right, marked SR (set, reset) as the AND gate is marked &,
    # with its reset input below, as the latch's model shows it.
    icon.shape(rect(186, 108, 232, 132, 6), ORANGE)
    icon.shape(ellipse(96, 214, 18), ORANGE)
    icon.shape(union(rect(26, 50, 120, 182, 10), ellipse(120, 116, 66)), WHITE)
    icon.shape(text(108, 118, 'SR', 64), ORANGE)
    icon.save('Latch_Icon.png')


def pin(output):
    icon = Icon()
    # A 4x1 pin strip with contact fingers, and an arrow into the board (input) or out of it (output); the board
    # is to the right, as a pin's wire end faces the board.
    icon.shape(rect(22, 96, 234, 160, 12), WHITE)
    icon.shape(union(*[rect(x, 110, x + 14, 146, 3) for x in (44, 70, 96)]), ORANGE)
    if output:
        arrow = polygon((128, 128), (180, 92), (180, 112), (236, 112), (236, 144), (180, 144), (180, 164))
    else:
        arrow = polygon((236, 128), (184, 92), (184, 112), (128, 112), (128, 144), (184, 144), (184, 164))
    icon.shape(arrow, ORANGE)
    icon.save('IC_OutputPin_Icon.png' if output else 'IC_InputPin_Icon.png')


def board(width, height, name):
    icon = Icon()
    # A board of width x height chunks, each with a notch on its outer edges, traces and a pad per chunk.
    margin = 22
    cell = (SIZE - 2 * margin) / max(width, height)
    w, h = cell * width, cell * height
    x0, y0 = (SIZE - w) / 2, (SIZE - h) / 2
    notch = min(cell * 0.3, 34)
    depth = 14

    def body(d):
        d.rounded_rectangle([px(x0), px(y0), px(x0 + w), px(y0 + h)], radius=px(14), fill=255)
        for i in range(width):
            cx = x0 + cell * (i + 0.5)
            d.rectangle([px(cx - notch / 2), px(y0 - 1), px(cx + notch / 2), px(y0 + depth)], fill=0)
            d.rectangle([px(cx - notch / 2), px(y0 + h - depth), px(cx + notch / 2), px(y0 + h + 1)], fill=0)
        for j in range(height):
            cy = y0 + cell * (j + 0.5)
            d.rectangle([px(x0 - 1), px(cy - notch / 2), px(x0 + depth), px(cy + notch / 2)], fill=0)
            d.rectangle([px(x0 + w - depth), px(cy - notch / 2), px(x0 + w + 1), px(cy + notch / 2)], fill=0)

    icon.shape(body, WHITE)
    traces = []
    for i in range(width):
        for j in range(height):
            cx, cy = x0 + cell * (i + 0.5), y0 + cell * (j + 0.5)
            k = cell / 4
            traces += [rect(cx - k, cy - k - 4, cx + k * 0.3, cy - k + 4), rect(cx + k * 0.3 - 4, cy - k - 4, cx + k * 0.3 + 4, cy + k),
                       ellipse(cx - k, cy - k, 9), ellipse(cx + k * 0.3, cy + k, 9)]
    icon.shape(union(*traces), ORANGE)
    icon.save(name)


def integer_gate(glyph, name, size=72):
    """An integer gate: a gate body (flat left, round right, as the latch's) with its operation in orange."""
    icon = Icon()
    icon.shape(rect(186, 112, 232, 136, 6), ORANGE)
    icon.shape(union(rect(26, 50, 120, 196, 10), ellipse(120, 123, 73)), WHITE)
    icon.shape(text(112, 125, glyph, size), ORANGE)
    icon.save(name)


def wired_foundation(foundation, name):
    """A vanilla foundation icon, as large as fits, with the wires category's bolt over its centre."""
    base_file = os.path.join(DUMP, foundation + '.png')
    bolt_file = os.path.join(DUMP, '02 ToolbarCategoryWires.png')
    if not (os.path.exists(base_file) and os.path.exists(bolt_file)):
        print('skipped', name, '(no icon dump)')
        return
    canvas = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    # One scale for every foundation, so they keep their sizes relative to each other as vanilla shows them:
    # the largest (119 px across in the game's atlas) fills the icon.
    base = Image.open(base_file).convert('RGBA')
    scale = 236 / 119
    base = base.resize((round(base.width * scale), round(base.height * scale)), Image.LANCZOS)
    canvas.alpha_composite(base, ((SIZE - base.width) // 2, (SIZE - base.height) // 2))
    bolt = fit(Image.open(bolt_file).convert('RGBA'), 150, int(min(150, base.height * 0.82)))
    canvas.alpha_composite(bolt, ((SIZE - bolt.width) // 2, (SIZE - bolt.height) // 2))
    canvas.save(os.path.join(OUT, name))
    print('wrote', name)


def fit(image, width, height):
    scale = min(width / image.width, height / image.height)
    return image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)


if __name__ == '__main__':
    chip()
    chip(2, 1, 'IC_Chip2x1_Icon.png')
    chip(2, 2, 'IC_Chip2x2_Icon.png')
    latch()
    pin(output=False)
    pin(output=True)
    integer_gate('&|', 'CircuitsBitwiseGate_Icon.png', 64)
    integer_gate('~', 'CircuitsNotGate_Icon.png', 96)
    integer_gate('<<', 'CircuitsShiftGate_Icon.png', 60)
    integer_gate('+-', 'CircuitsArithmeticGate_Icon.png', 72)
    wired_foundation('05-06-00 FoundationGroup_1x1', 'IC_Board_Icon.png')
    wired_foundation('05-06-04 FoundationGroup_1x2', 'IC_Board2x1_Icon.png')
    wired_foundation('05-07-00 FoundationGroup_2x2', 'IC_Board2x2_Icon.png')
