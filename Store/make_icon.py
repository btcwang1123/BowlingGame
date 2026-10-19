import sys, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops

S = 1024
SS = 2  # supersample
W = S * SS
out = sys.argv[1]

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

# background: deep night gradient
bg = Image.new("RGB", (W, W))
px = bg.load()
top, bot = (18, 6, 48), (4, 2, 18)
for y in range(W):
    c = lerp(top, bot, y / W)
    for x in range(W):
        px[x, y] = c
# radial warm glow behind the pins
glow = Image.new("L", (W, W), 0)
gd = ImageDraw.Draw(glow)
gd.ellipse((W*0.18, W*0.02, W*0.82, W*0.52), fill=150)
glow = glow.filter(ImageFilter.GaussianBlur(W*0.09))
bg = Image.composite(Image.new("RGB", (W, W), (120, 30, 160)), bg, glow)

def neon_layer(draw_fn, color, core=(255, 255, 255), width=10, blur=28):
    """Draw shape as colored glow + bright core; returns RGB image to add."""
    m = Image.new("L", (W, W), 0)
    draw_fn(ImageDraw.Draw(m), width * SS)
    g1 = m.filter(ImageFilter.GaussianBlur(blur * SS))
    g2 = m.filter(ImageFilter.GaussianBlur(blur * SS * 0.35))
    col = Image.new("RGB", (W, W), color)
    layer = Image.new("RGB", (W, W), 0)
    layer = ImageChops.add(layer, Image.composite(col, layer, g1.point(lambda v: min(255, v * 3))))
    layer = ImageChops.add(layer, Image.composite(col, Image.new("RGB", (W, W), 0), g2.point(lambda v: min(255, v * 2))))
    cm = Image.new("L", (W, W), 0)
    draw_fn(ImageDraw.Draw(cm), max(2, int(width * 0.45)) * SS)
    layer = ImageChops.add(layer, Image.composite(Image.new("RGB", (W, W), core), Image.new("RGB", (W, W), 0), cm))
    return layer

img = bg
# lane: perspective lines converging to vanishing point
vp = (W * 0.5, W * 0.30)
def lane(d, w):
    for xb, xt in ((0.06, 0.40), (0.94, 0.60)):
        d.line([(W * xb, W * 1.02), (W * xt, W * 0.40)], fill=255, width=w)
img = ImageChops.add(img, neon_layer(lane, (0, 220, 255), core=(200, 250, 255), width=9, blur=22))
# lane arrows
def arrows(d, w):
    for i, x in enumerate((0.38, 0.5, 0.62)):
        cx, cy, s = W * x, W * 0.93 - (abs(i - 1)) * W * 0.02, W * 0.035
        d.line([(cx - s, cy + s * 0.9), (cx, cy - s * 0.6), (cx + s, cy + s * 0.9)], fill=255, width=w, joint="curve")
img = ImageChops.add(img, neon_layer(arrows, (0, 200, 255), core=(180, 245, 255), width=6, blur=14))

# pins: filled white pins with neon pink glow and stripes
def prof_r(t):
    knots = [(0, 0.15), (0.12, 0.22), (0.32, 0.255), (0.52, 0.19), (0.68, 0.095), (0.76, 0.10), (0.86, 0.135), (0.94, 0.115), (0.985, 0.06), (1.0, 0.0)]
    for (t0, r0), (t1, r1) in zip(knots, knots[1:]):
        if t <= t1:
            u = (t - t0) / (t1 - t0); u = u * u * (3 - 2 * u)
            return r0 + (r1 - r0) * u
    return 0
def pin_poly(cx, base, h):
    N = 60
    right = [(cx + prof_r(i / N) * h * 0.78, base - i / N * h) for i in range(N + 1)]
    left = [(cx - x + cx, y) for x, y in reversed(right)]
    left = [(2 * cx - x, y) for x, y in reversed(right)]
    return right + left
pins = [(0.29, 0.46, 0.27), (0.71, 0.46, 0.27), (0.5, 0.50, 0.32)]
pm = Image.new("L", (W, W), 0)
pd = ImageDraw.Draw(pm)
for x, b, h in pins: pd.polygon(pin_poly(W * x, W * b, W * h), fill=255)
pglow = pm.filter(ImageFilter.GaussianBlur(W * 0.03))
img = ImageChops.add(img, Image.composite(Image.new("RGB", (W, W), (255, 30, 160)), Image.new("RGB", (W, W), 0), pglow.point(lambda v: min(255, v * 2))))
for x, b, h in pins:
    cx, base, hh = W * x, W * b, W * h
    one = Image.new("L", (W, W), 0); ImageDraw.Draw(one).polygon(pin_poly(cx, base, hh), fill=255)
    pin = Image.new("RGB", (W, W), 0); pp = ImageDraw.Draw(pin)
    # vertical shading: bright center, pinkish edges
    for i in range(int(hh * 0.78 * 0.26) + 2, -1, -1):
        f = i / (hh * 0.78 * 0.26)
        pp.rectangle((cx - i, base - hh, cx + i, base), fill=lerp((255, 250, 255), (255, 150, 215), f ** 1.5))
    for dt in (0.63, 0.70):
        pp.rectangle((cx - hh * 0.2, base - dt * hh - hh * 0.022, cx + hh * 0.2, base - dt * hh + hh * 0.022), fill=(255, 20, 120))
    img.paste(pin, (0, 0), one)
# ball: solid glowing sphere in front
bc, br = (W * 0.5, W * 0.665), W * 0.205
# outer glow
gm = Image.new("L", (W, W), 0)
ImageDraw.Draw(gm).ellipse((bc[0]-br, bc[1]-br, bc[0]+br, bc[1]+br), fill=255)
halo = gm.filter(ImageFilter.GaussianBlur(W * 0.05))
img = ImageChops.add(img, Image.composite(Image.new("RGB", (W, W), (150, 60, 255)), Image.new("RGB", (W, W), 0), halo))
# sphere shading
ball = Image.new("RGB", (W, W), 0)
bp = ball.load()
x0, y0, x1, y1 = int(bc[0]-br), int(bc[1]-br), int(bc[0]+br), int(bc[1]+br)
lx, ly, lz = -0.45, -0.6, 0.66
for y in range(y0, y1):
    for x in range(x0, x1):
        nx, ny = (x - bc[0]) / br, (y - bc[1]) / br
        r2 = nx * nx + ny * ny
        if r2 > 1: continue
        nz = math.sqrt(1 - r2)
        diff = max(0, nx * lx + ny * ly + nz * lz)
        spec = max(0, diff) ** 40
        rim = (1 - nz) ** 3
        base = lerp((40, 8, 90), (130, 50, 255), diff)
        c = [base[0] + rim * 255 * 0.9 + spec * 255, base[1] + rim * 60 + spec * 255, base[2] + rim * 200 + spec * 255]
        # swirl
        sw = 0.5 + 0.5 * math.sin(nx * 7 + ny * 4 + math.sin(ny * 5) * 1.5)
        c = [c[0] + sw * 25, c[1] + sw * 5, c[2] + sw * 30]
        bp[x, y] = tuple(min(255, int(v)) for v in c)
img.paste(ball, (0, 0), gm)
# finger holes
hd = ImageDraw.Draw(img)
for hx, hy, hr in ((-0.20, -0.38, 0.085), (0.06, -0.44, 0.085), (-0.04, -0.14, 0.10)):
    cx, cy, r = bc[0] + hx * br, bc[1] + hy * br, hr * br
    hd.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(12, 2, 30))
    hd.arc((cx - r, cy - r, cx + r, cy + r), 200, 340, fill=(90, 40, 170), width=3 * SS)
# ball rim neon line
img = ImageChops.add(img, neon_layer(lambda d, w: d.ellipse((bc[0]-br, bc[1]-br, bc[0]+br, bc[1]+br), outline=255, width=w),
                                     (190, 90, 255), core=(235, 210, 255), width=6, blur=12))
# speed streaks trailing the ball
def streaks(d, w):
    for k, (dy, ln) in enumerate(((-0.55, 0.9), (-0.05, 1.25), (0.45, 0.8))):
        y = bc[1] + dy * br
        xr = bc[0] - math.sqrt(max(0, 1 - dy * dy)) * br - W * 0.03
        d.line([(xr - ln * br * 0.75, y + ln * br * 0.35), (xr, y)], fill=255, width=w)
pass  # img = ImageChops.add(img, neon_layer(streaks, (0, 230, 255), core=(210, 250, 255), width=10, blur=14))
img = img.resize((S, S), Image.LANCZOS).convert("RGB")
img.save(out)
