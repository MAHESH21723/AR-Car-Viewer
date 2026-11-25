"""Procedural logo set for AR Car Viewer by Maheshwaran. numpy + Pillow only."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ICON_DIR = os.path.join(ROOT, "Assets", "CarViewer", "Icon")
DOCS_DIR = os.path.join(ROOT, "Docs")
FONT = os.path.join(HERE, "fonts", "Rajdhani-Bold.ttf")
os.makedirs(ICON_DIR, exist_ok=True)
os.makedirs(DOCS_DIR, exist_ok=True)

SS = 3  # supersampling factor
CYAN = (40, 224, 255)
MID = (7, 14, 38)
GRAPH = (38, 42, 54)


def catmull(pts, closed=True, n=20):
    pts = [np.array(p, float) for p in pts]
    out = []
    m = len(pts)
    rng = range(m) if closed else range(m - 1)
    for i in rng:
        def g(k):
            return pts[k % m] if closed else pts[min(max(k, 0), m - 1)]
        p0, p1, p2, p3 = g(i - 1), g(i), g(i + 1), g(i + 2)
        for t in np.linspace(0, 1, n, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                              + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(pts[-1])
    return [tuple(p) for p in out]


BODY = [(62, 300), (50, 262), (62, 228), (110, 212), (230, 204), (330, 192), (400, 160), (460, 130),
        (525, 118), (590, 124), (670, 162), (760, 192), (860, 210), (930, 228), (964, 258),
        (958, 292), (925, 312), (800, 320), (600, 322), (400, 322), (220, 320), (110, 316)]
WINDOW = [(452, 164), (498, 136), (552, 131), (610, 144), (668, 174), (648, 184), (452, 184)]
WHEELS = [(255, 298), (775, 298)]
GROUND_Y = 362


class Canvas:
    """Draws in design space (car ~ x 40..970, y 95..420) onto a supersampled canvas."""

    def __init__(self, size, k, cx, cy, bg=None):
        self.size = size * SS
        self.k = k * SS
        self.cx = cx * SS
        self.cy = cy * SS
        self.img = bg.resize((self.size, self.size), Image.LANCZOS).convert("RGBA") if bg else \
            Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))

    def P(self, p):
        return (self.cx + (p[0] - 500) * self.k, self.cy + (p[1] - 250) * self.k)

    def Ps(self, pts):
        return [self.P(p) for p in pts]

    def layer(self):
        return Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))

    def glow(self, lay, radius, strength=1.0):
        b = lay.filter(ImageFilter.GaussianBlur(radius * self.k))
        if strength != 1.0:
            a = np.array(b).astype(float)
            a[..., 3] = np.clip(a[..., 3] * strength, 0, 255)
            b = Image.fromarray(a.astype(np.uint8))
        self.img = Image.alpha_composite(self.img, b)

    def add(self, lay):
        self.img = Image.alpha_composite(self.img, lay)

    def final(self, size_out):
        return self.img.resize((size_out, size_out), Image.LANCZOS)


def grad_fill(size, y0, y1, stops):
    """Vertical gradient RGB image with stops [(t, (r,g,b))]."""
    ys = np.linspace(0, 1, size)
    t = np.clip((ys * size - y0) / max(y1 - y0, 1), 0, 1)
    ts = [s[0] for s in stops]
    cols = np.stack([np.interp(t, ts, [s[1][c] for s in stops]) for c in range(3)], -1)
    arr = np.repeat(cols[:, None, :], size, 1).astype(np.uint8)
    return Image.fromarray(arr, "RGB")


def make_background(size, variant="icon"):
    """Midnight to graphite diagonal gradient, cyan floor glow, faint perspective grid."""
    s = size * SS
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32) / s
    t = np.clip(0.55 * yy + 0.45 * (1 - xx) * 0.6 + 0.0, 0, 1)
    t = np.clip(t * 1.1 - 0.05, 0, 1)
    c0, c1 = np.array(MID, np.float32), np.array(GRAPH, np.float32)
    arr = c0 + (c1 - c0) * t[..., None]
    # cyan floor glow
    d = np.sqrt(((xx - 0.5) / 0.55) ** 2 + ((yy - 0.66) / 0.30) ** 2)
    g = np.clip(1 - d, 0, 1) ** 2
    arr += g[..., None] * np.array([0, 70, 95], np.float32)
    # warm top-left rim glow
    d2 = np.sqrt(((xx - 0.15) / 0.6) ** 2 + ((yy - 0.1) / 0.5) ** 2)
    g2 = np.clip(1 - d2, 0, 1) ** 2
    arr += g2[..., None] * np.array([40, 24, 8], np.float32)
    # vignette
    v = np.sqrt((xx - .5) ** 2 + (yy - .5) ** 2)
    arr *= (1 - 0.35 * np.clip(v * 1.4 - 0.3, 0, 1))[..., None]
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    # perspective floor grid
    lay = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    dr = ImageDraw.Draw(lay)
    hor = 0.60 * s
    vx = 0.5 * s
    for i in range(-14, 15):
        x_bot = vx + i * 0.16 * s
        dr.line([(vx + i * 0.012 * s, hor), (x_bot, s)], fill=(40, 224, 255, 26), width=max(1, SS))
    for j in range(1, 9):
        y = hor + (s - hor) * (j / 8) ** 1.8
        dr.line([(0, y), (s, y)], fill=(40, 224, 255, 26), width=max(1, SS))
    # fade grid toward horizon
    a = np.array(lay).astype(np.float32)
    fade = np.clip((np.arange(s) - hor) / (0.25 * s), 0, 1)[:, None]
    a[..., 3] *= fade
    img = Image.alpha_composite(img, Image.fromarray(a.astype(np.uint8)))
    return img.resize((size, size), Image.LANCZOS).convert("RGB")


def draw_ring(c, strong=True):
    cx, cy, rx, ry = 500, GROUND_Y + 2, 455, 66
    # soft cyan disc
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    for i in range(10):
        f = 1 - i / 10
        box = [c.P((cx - rx * f, cy - ry * f)), c.P((cx + rx * f, cy + ry * f))]
        d.ellipse(box, fill=(20, 150, 200, 14))
    c.add(lay)
    # glow rings
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    for f, w, a in ((1.0, 9, 255), (0.80, 5, 200), (0.58, 4, 150)):
        box = [c.P((cx - rx * f, cy - ry * f)), c.P((cx + rx * f, cy + ry * f))]
        d.ellipse(box, outline=CYAN + (a,), width=int(w * c.k))
    c.glow(lay, 9, 1.4)
    c.add(lay)
    # tick marks / dashes on outer ring
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    f = 1.10
    for i in range(12):
        a0 = i * 2 * np.pi / 12 + 0.12
        seg = np.linspace(a0, a0 + 0.30, 12)
        pts = [c.P((cx + np.cos(a) * rx * f, cy + np.sin(a) * ry * f)) for a in seg]
        d.line(pts, fill=(255, 255, 255, 235), width=int(6 * c.k), joint="curve")
    c.glow(lay, 6, 1.2)
    c.add(lay)


def draw_car(c):
    # ground shadow
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    d.ellipse([c.P((90, GROUND_Y - 22)), c.P((930, GROUND_Y + 28))], fill=(0, 0, 0, 200))
    c.add(lay.filter(ImageFilter.GaussianBlur(14 * c.k)))
    # cyan underglow
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    d.ellipse([c.P((150, 318)), c.P((860, 372))], fill=(40, 224, 255, 150))
    c.add(lay.filter(ImageFilter.GaussianBlur(14 * c.k)))

    # body mask (minus wheel arches)
    m = Image.new("L", (c.size, c.size), 0)
    d = ImageDraw.Draw(m)
    d.polygon(c.Ps(catmull(BODY, True, 24)), fill=255)
    arch = Image.new("L", (c.size, c.size), 0)
    da = ImageDraw.Draw(arch)
    for wx, wy in WHEELS:
        r = 80
        da.ellipse([c.P((wx - r, wy - r)), c.P((wx + r, wy + r))], fill=255)
    m = Image.fromarray(np.clip(np.array(m).astype(int) - np.array(arch).astype(int), 0, 255).astype(np.uint8))

    # rear wing (behind body)
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    d.polygon(c.Ps([(60, 224), (66, 186), (170, 190), (176, 210)]), fill=(150, 50, 6, 255))
    d.polygon(c.Ps([(62, 218), (68, 186), (126, 188), (112, 214)]), fill=(255, 140, 20, 255))
    d.line(c.Ps([(100, 206), (98, 228)]), fill=(120, 40, 5, 255), width=int(9 * c.k))
    c.add(lay)

    # tires behind body (wheel wells dark)
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    for wx, wy in WHEELS:
        r = 80
        d.ellipse([c.P((wx - r, wy - r)), c.P((wx + r, wy + r))], fill=(8, 10, 18, 255))
    c.add(lay)

    # body gradient
    g = grad_fill(c.size, c.P((0, 96))[1], c.P((0, 326))[1],
                  [(0, (255, 214, 80)), (0.30, (255, 160, 20)), (0.65, (240, 92, 6)), (1, (150, 40, 4))])
    body = Image.new("RGBA", (c.size, c.size), (0, 0, 0, 0))
    body.paste(g, (0, 0), m)
    c.add(body)

    # clip layer helper
    def clipped(draw_fn, blur=0):
        lay2 = c.layer()
        draw_fn(ImageDraw.Draw(lay2))
        if blur:
            lay2 = lay2.filter(ImageFilter.GaussianBlur(blur * c.k))
        a = np.minimum(np.array(lay2)[..., 3], np.array(m))
        arr = np.array(lay2)
        arr[..., 3] = a
        c.add(Image.fromarray(arr))

    # lower body shade
    clipped(lambda d: d.polygon(c.Ps([(0, 268), (1000, 262), (1000, 340), (0, 340)]), fill=(90, 20, 0, 120)), 8)
    # long shoulder highlight
    clipped(lambda d: d.line(c.Ps(catmull([(80, 236), (300, 224), (520, 226), (760, 222), (940, 240)], False, 20)),
                             fill=(255, 240, 190, 200), width=int(5 * c.k)), 1.2)
    # hood/roof sheen following the top edge
    clipped(lambda d: d.line(c.Ps(catmull([(400, 176), (460, 144), (525, 132), (590, 138), (670, 176), (760, 206), (860, 224)], False, 20)),
                             fill=(255, 255, 235, 150), width=int(5 * c.k)), 1.5)
    # door line + side sill
    clipped(lambda d: (d.line(c.Ps([(520, 176), (516, 296)]), fill=(120, 36, 4, 200), width=int(4 * c.k)),
                       d.line(c.Ps([(340, 186), (352, 296)]), fill=(120, 36, 4, 150), width=int(4 * c.k)),
                       d.line(c.Ps([(330, 300), (700, 300)]), fill=(95, 24, 2, 220), width=int(7 * c.k))))
    # side intake scoop
    clipped(lambda d: d.polygon(c.Ps([(560, 232), (700, 236), (690, 262), (574, 262)]), fill=(70, 16, 2, 150)), 0.5)

    # window
    wm = Image.new("L", (c.size, c.size), 0)
    ImageDraw.Draw(wm).polygon(c.Ps(catmull(WINDOW, True, 16)), fill=255)
    wg = grad_fill(c.size, c.P((0, 112))[1], c.P((0, 176))[1], [(0, (70, 200, 235)), (0.5, (14, 40, 78)), (1, (6, 14, 36))])
    win = Image.new("RGBA", (c.size, c.size), (0, 0, 0, 0))
    win.paste(wg, (0, 0), wm)
    c.add(win)
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    d.polygon(c.Ps([(488, 124), (520, 118), (500, 172), (470, 172)]), fill=(255, 255, 255, 70))
    arr = np.array(lay)
    arr[..., 3] = np.minimum(arr[..., 3], np.array(wm))
    c.add(Image.fromarray(arr))
    # window pillar
    lay = c.layer()
    ImageDraw.Draw(lay).line(c.Ps([(535, 114), (545, 176)]), fill=(150, 44, 4, 255), width=int(9 * c.k))
    arr = np.array(lay)
    arr[..., 3] = np.minimum(arr[..., 3], np.array(wm))
    c.add(Image.fromarray(arr))

    # lights: headlight (cyan white) and tail light (red)
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    d.polygon(c.Ps(catmull([(872, 212), (934, 228), (950, 248), (900, 238)], True, 10)), fill=(210, 250, 255, 255))
    c.glow(lay, 7, 1.5)
    c.add(lay)
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    d.polygon(c.Ps(catmull([(58, 232), (84, 224), (84, 242), (56, 252)], True, 8)), fill=(255, 40, 60, 255))
    c.glow(lay, 6, 1.4)
    c.add(lay)

    # wheels
    lay = c.layer()
    d = ImageDraw.Draw(lay)
    for wx, wy in WHEELS:
        for r, col in ((64, (14, 16, 24, 255)), (46, (205, 214, 226, 255)), (38, (26, 30, 42, 255))):
            d.ellipse([c.P((wx - r, wy - r)), c.P((wx + r, wy + r))], fill=col)
        for i in range(5):
            a = i * 2 * np.pi / 5 - np.pi / 2
            d.line([c.P((wx, wy)), c.P((wx + np.cos(a) * 42, wy + np.sin(a) * 42))],
                   fill=(225, 232, 242, 255), width=int(9 * c.k))
        d.ellipse([c.P((wx - 11, wy - 11)), c.P((wx + 11, wy + 11))], fill=(255, 150, 20, 255))
        d.arc([c.P((wx - 64, wy - 64)), c.P((wx + 64, wy + 64))], 200, 300, fill=(80, 100, 130, 255), width=int(3 * c.k))
    c.add(lay)


def draw_brackets(c, margin, length, width, alpha=255):
    s = c.size
    m, L, w = margin * SS, length * SS, int(width * SS)
    lay = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    col = CYAN + (alpha,)
    for sx, sy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        x = m if sx == 0 else s - m
        y = m if sy == 0 else s - m
        dx = 1 if sx == 0 else -1
        dy = 1 if sy == 0 else -1
        d.line([(x, y + dy * L), (x, y), (x + dx * L, y)], fill=col, width=w, joint="curve")
        for p in ((x, y + dy * L), (x + dx * L, y)):
            d.ellipse([p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2], fill=col)
    c.glow(lay, 0, 1.0) if False else None
    b = lay.filter(ImageFilter.GaussianBlur(w * 0.9))
    c.img = Image.alpha_composite(c.img, b)
    c.img = Image.alpha_composite(c.img, lay)


def scene(size, k, cy, bg=None, brackets=True, ring=True):
    c = Canvas(size, k, size / 2, cy, bg)
    if ring:
        draw_ring(c)
    draw_car(c)
    if brackets:
        draw_brackets(c, size * 0.085, size * 0.15, size * 0.014)
    return c


def main():
    N = 1024
    bg = make_background(N)
    # App icon: car group ~ 84% width of canvas
    c = scene(N, 0.88 * N / 1000, N * 0.50 - 12, bg)
    icon = c.final(N).convert("RGB")
    icon.save(os.path.join(ICON_DIR, "AppIcon.png"))
    bg.save(os.path.join(ICON_DIR, "AdaptiveBackground.png"))
    # Adaptive fg: ring+car inside central 60% (614 px)
    c = scene(N, 0.56 * N / 1000, N * 0.50 - 8, None, brackets=False)
    c.final(N).save(os.path.join(ICON_DIR, "AdaptiveForeground.png"))
    # Splash logo (transparent)
    c = scene(N, 0.88 * N / 1000, N * 0.55, None, brackets=True)
    c.final(N).save(os.path.join(ICON_DIR, "Logo.png"))
    icon.resize((512, 512), Image.LANCZOS).save(os.path.join(DOCS_DIR, "icon-512.png"))


if __name__ == "__main__":
    main()


    feature()


def feature():
    W, H = 1024, 500
    S2 = 2
    big = make_background(1024)
    bgw = big.crop((0, 380, 1024, 880)).convert("RGBA")
    # car on a square canvas, then crop to the wide frame
    sq = Canvas(1024, 0.50, 745, 512, None)
    draw_ring(sq)
    draw_car(sq)
    car = sq.final(1024).crop((0, 262, 1024, 762))
    base = Image.alpha_composite(bgw, car)
    # frame brackets
    lay = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(lay)
    m, L, w = 22 * SS, 52 * SS, 4 * SS
    for sx in (0, 1):
        for sy in (0, 1):
            x = m if sx == 0 else W * SS - m
            y = m if sy == 0 else H * SS - m
            dx = 1 if sx == 0 else -1
            dy = 1 if sy == 0 else -1
            d.line([(x, y + dy * L), (x, y), (x + dx * L, y)], fill=CYAN + (255,), width=w, joint="curve")
    gl = lay.filter(ImageFilter.GaussianBlur(5 * SS))
    base = Image.alpha_composite(base, Image.alpha_composite(gl, lay).resize((W, H), Image.LANCZOS))
    # text at 2x
    big2 = base.resize((W * S2, H * S2), Image.LANCZOS)
    t = Image.new("RGBA", big2.size, (0, 0, 0, 0))
    td = ImageDraw.Draw(t)
    x0 = 80 * S2
    ft = ImageFont.truetype(FONT, 118 * S2)
    td.text((x0, 112 * S2), "AR", font=ft, fill=CYAN + (255,))
    aw = td.textlength("AR ", font=ft)
    td.text((x0 + aw, 112 * S2), "Car", font=ft, fill=(255, 255, 255, 255))
    td.text((x0, 218 * S2), "Viewer", font=ft, fill=(255, 168, 28, 255))
    td.rectangle([x0 + 4, 352 * S2, x0 + 150 * S2, 355 * S2], fill=CYAN + (255,))
    td.text((x0 + 2, 366 * S2), "Augmented reality car showroom", font=ImageFont.truetype(FONT, 34 * S2), fill=(205, 228, 240, 255))
    td.text((x0 + 2, 410 * S2), "by Maheshwaran", font=ImageFont.truetype(FONT, 30 * S2), fill=CYAN + (255,))
    sh = Image.new("RGBA", t.size, (0, 0, 0, 0))
    sh.putalpha(t.split()[3].filter(ImageFilter.GaussianBlur(10)).point(lambda v: min(170, v * 2)))
    out = Image.alpha_composite(Image.alpha_composite(big2, sh), t)
    out.resize((W, H), Image.LANCZOS).convert("RGB").save(os.path.join(DOCS_DIR, "feature-1024x500.png"))


if __name__ == "__main__":
    main()
