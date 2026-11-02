"""한지·먹·수묵 UI 그림을 코드로 만든다. numpy, Pillow 필요.
실행: python Tools/gen_ink_art.py  (Assets/_Project/Art/Resources/UI 에 PNG를 쓴다)"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = 'Assets/_Project/Art/Resources/UI/'
FONTS = 'Assets/_Project/Art/Resources/Fonts/'
PAPER = np.array([233, 222, 198], float)
INK = np.array([30, 27, 24], float)
CINNABAR = np.array([158, 36, 28], float)
rng = np.random.default_rng(7)


def noise(h, w, scale):
    """주기적으로 이어지는 부드러운 잡음 (0~1). scale이 클수록 큼직하다."""
    f = np.fft.fft2(rng.standard_normal((h, w)))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    f *= np.exp(-((fx ** 2 + fy ** 2) * (scale ** 2)) * 8)
    n = np.real(np.fft.ifft2(f))
    return (n - n.min()) / (n.max() - n.min())


def save(rgb, alpha, name):
    a = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha, 0, 1) * 255]).astype('uint8')
    Image.fromarray(a, 'RGBA').save(OUT + name)


def paper_rgb(h, w):
    n = 0.5 * noise(h, w, 40) + 0.3 * noise(h, w, 8) + 0.2 * noise(h, w, 2)
    img = PAPER[None, None, :] * (0.93 + 0.1 * n[..., None])
    # 닥종이 섬유
    fibers = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(fibers)
    for _ in range(h * w // 900):
        x, y = rng.integers(0, w), rng.integers(0, h)
        a = rng.uniform(0, 6.28)
        l = rng.integers(6, 26)
        d.line([x, y, x + np.cos(a) * l, y + np.sin(a) * l], fill=int(rng.integers(40, 110)))
    f = np.asarray(fibers.filter(ImageFilter.GaussianBlur(0.6)), float) / 255
    return img * (1 - 0.14 * f[..., None])


def panel():
    s = 256
    rgb = paper_rgb(s, s)
    y, x = np.mgrid[0:s, 0:s]
    edge = np.minimum.reduce([x, y, s - 1 - x, s - 1 - y]).astype(float)
    wobble = (noise(s, s, 10) - 0.5) * 9 + (noise(s, s, 1.5) - 0.5) * 3
    d = edge + wobble
    ink = np.clip(1 - np.abs(d - 18) / 6.5, 0, 1) * (0.72 + 0.4 * noise(s, s, 3))
    wash = np.clip(1 - (d - 18) / 38, 0, 1) * 0.2
    rgb = rgb * (1 - wash[..., None]) + INK * wash[..., None]
    rgb = rgb * (1 - ink[..., None]) + INK * ink[..., None]
    save(rgb, np.clip((d - 9) / 3, 0, 1), 'panel.png')


def stroke(w, h, color, name, fade=1.0):
    """가로 붓 획. 양 끝이 거칠고 결이 남는다."""
    y, x = np.mgrid[0:h, 0:w]
    grain = noise(h, w * 3, 2)[:, ::3]  # 가로 결
    streak = noise(h, w, 1.2)
    u = np.minimum(x, w - 1 - x) / (w * 0.5)
    v = np.abs(y - h / 2) / (h / 2)
    wavy = v + (noise(1, w, 14)[0] - 0.5) * 0.28
    body = np.clip((0.9 - wavy) * 9, 0, 1) * np.clip(u * 16 + (grain - 0.5) * 2.2, 0, 1)
    dry = 1 - np.clip((0.16 - u) * 8, 0, 1) * np.clip((streak - 0.42) * 7, 0, 1)
    alpha = np.clip(body * dry, 0, 1) * fade
    rgb = np.ones((h, w, 3)) * color * (0.95 + 0.14 * grain[..., None])
    save(rgb, alpha, name)


def seal(ch, name, size=96):
    im = Image.new('L', (size, size), 0)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([3, 3, size - 4, size - 4], radius=9, fill=255)
    font = ImageFont.truetype(FONTS + 'YujiBoku-Regular.ttf', int(size * 0.72))
    box = d.textbbox((0, 0), ch, font=font)
    d.text(((size - (box[2] - box[0])) / 2 - box[0], (size - (box[3] - box[1])) / 2 - box[1]), ch, fill=0, font=font)
    a = np.asarray(im.filter(ImageFilter.GaussianBlur(0.7)), float) / 255
    a = np.clip(a * (0.68 + 0.5 * noise(size, size, 4)), 0, 1)
    save(np.ones((size, size, 3)) * CINNABAR, a, name)


def ridge(w, seed, rough):
    r = np.random.default_rng(seed)
    h = np.zeros(w)
    for o in range(1, 9):
        p = r.uniform(0, 6.28)
        h += np.sin(np.arange(w) / w * 6.28 * o * rough + p) * r.uniform(0.4, 1) / o ** 1.05
    return (h - h.min()) / (h.max() - h.min())


def landscape(w, h, name, dark=0.0, ground=False):
    rgb = paper_rgb(h, w)
    tall = noise(h, w, 16)
    layers = [(0.30, 0.34, 0.20, 1.2), (0.43, 0.30, 0.34, 1.7), (0.56, 0.26, 0.52, 2.3), (0.70, 0.22, 0.74, 3.0)]
    for i, (base, amp, depth, rough) in enumerate(layers):
        top = (base + (0.5 - ridge(w, 20 + i, rough)) * amp) * h
        y = np.arange(h)[:, None]
        below = (y - top[None, :]) / h
        # 능선에서 진하고 아래로 갈수록 안개로 풀린다
        a = np.clip(below * 60, 0, 1) * np.clip(1 - below / (0.20 + 0.05 * i), 0, 1) ** 1.5 * depth
        a = a * (0.55 + 0.75 * tall)
        rgb = rgb * (1 - a[..., None]) + INK * a[..., None]
    if ground:
        y = (np.arange(h)[:, None] / h)
        g = np.clip((y - 0.72 + (noise(h, w, 22) - 0.5) * 0.12) * 5, 0, 1) * (0.5 + 0.35 * noise(h, w, 10))
        rgb = rgb * (1 - g[..., None]) + INK * g[..., None]
    rgb = rgb * (1 - dark)
    save(rgb, np.ones((h, w)), name)


def meditate(name, w=560, h=668, s=2):
    """좌선하는 옆모습. 일주천의 경로가 이 위에 그려진다."""
    mask = Image.new('L', (w * s, h * s), 0)
    d = ImageDraw.Draw(mask)
    def px(pts):
        return [(x * w * s, y * h * s) for x, y in pts]
    d.ellipse(px([(.42, .05), (.62, .23)]), fill=255)   # 머리
    d.ellipse(px([(.45, .01), (.52, .07)]), fill=255)   # 상투
    d.polygon(px([(.46, .21), (.56, .21), (.60, .30), (.66, .36), (.67, .56), (.64, .74), (.36, .78),
                  (.30, .62), (.31, .42), (.37, .30)]), fill=255)  # 몸통
    d.ellipse(px([(.26, .68), (.92, .86)]), fill=255)   # 접은 다리
    d.ellipse(px([(.50, .74), (.80, .90)]), fill=255)   # 무릎 아래
    soft = mask.filter(ImageFilter.GaussianBlur(4 * s)).resize((w, h), Image.LANCZOS)
    alpha = np.asarray(soft, float) / 255
    alpha = np.clip((alpha - 0.35) * 3, 0, 1) * (0.34 + 0.22 * noise(h, w, 14))
    save(np.ones((h, w, 1)) * INK, alpha, name)


def frame(name, s=1024, margin=36):
    """9-slice 패널. 한지 위에 손으로 그은 듯 흔들리는 먹선 테두리. USS에서 slice 64로 쓴다."""
    rgb = paper_rgb(s, s)
    y, x = np.mgrid[0:s, 0:s]
    edge = np.minimum.reduce([x, y, s - 1 - x, s - 1 - y]).astype(float)
    wobble = (noise(s, s, 24) - 0.5) * 5 + (noise(s, s, 3) - 0.5) * 1.6
    d = edge + wobble
    ink = np.clip(1 - np.abs(d - margin) / 2.6, 0, 1) * (0.8 + 0.3 * noise(s, s, 6))
    thin = np.clip(1 - np.abs(d - margin - 7) / 1.1, 0, 1) * 0.45
    wash = np.clip(1 - (d - margin) / 26, 0, 1) * np.clip((d - margin) / 2, 0, 1) * 0.08
    for a in (wash, thin, ink):
        rgb = rgb * (1 - a[..., None]) + INK * a[..., None]
    save(rgb, np.clip((d - margin + 12) / 3, 0, 1), name)


os.makedirs(OUT, exist_ok=True)
tile = paper_rgb(512, 512)
save(tile, np.ones((512, 512)), 'paper.png')
stroke(240, 64, INK, 'btn.png')
stroke(240, 64, CINNABAR, 'btn_hover.png')
stroke(240, 64, np.array([120, 112, 100], float), 'btn_off.png', 0.75)
stroke(640, 18, INK, 'rule.png')
seal('流', 'seal.png')
landscape(1280, 720, 'landscape.png')
landscape(1280, 720, 'battle_bg.png', ground=True)
meditate('meditate.png')
frame('frame.png')
print('done')
