"""Builds all icons (via make_icons) and the 1024x500 feature graphic."""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import make_icons as mi
from make_icons import Canvas, draw_ring, draw_car, make_background, SS, CYAN, FONT, DOCS_DIR


def feature():
    W, H = 1024, 500
    S2 = 2
    big = make_background(1024)
    bgw = big.crop((0, 380, 1024, 880)).convert("RGBA")
    sq = Canvas(1024, 0.50, 745, 512, None)
    draw_ring(sq)
    draw_car(sq)
    car = sq.final(1024).crop((0, 262, 1024, 762))
    base = Image.alpha_composite(bgw, car)
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
    big2 = base.resize((W * S2, H * S2), Image.LANCZOS)
    t = Image.new("RGBA", big2.size, (0, 0, 0, 0))
    td = ImageDraw.Draw(t)
    x0 = 80 * S2
    ft = ImageFont.truetype(FONT, 118 * S2)
    td.text((x0, 62 * S2), "AR", font=ft, fill=CYAN + (255,))
    aw = td.textlength("AR ", font=ft)
    td.text((x0 + aw, 62 * S2), "Car", font=ft, fill=(255, 255, 255, 255))
    td.text((x0, 168 * S2), "Viewer", font=ft, fill=(255, 168, 28, 255))
    td.rectangle([x0 + 4, 322 * S2, x0 + 150 * S2, 325 * S2], fill=CYAN + (255,))
    td.text((x0 + 2, 336 * S2), "Augmented reality car showroom", font=ImageFont.truetype(FONT, 34 * S2), fill=(205, 228, 240, 255))
    td.text((x0 + 2, 382 * S2), "by Maheshwaran", font=ImageFont.truetype(FONT, 30 * S2), fill=CYAN + (255,))
    sh = Image.new("RGBA", t.size, (0, 0, 0, 0))
    sh.putalpha(t.split()[3].filter(ImageFilter.GaussianBlur(10)).point(lambda v: min(170, v * 2)))
    out = Image.alpha_composite(Image.alpha_composite(big2, sh), t)
    out.resize((W, H), Image.LANCZOS).convert("RGB").save(os.path.join(DOCS_DIR, "feature-1024x500.png"))


if __name__ == "__main__":
    mi.main()
    feature()
