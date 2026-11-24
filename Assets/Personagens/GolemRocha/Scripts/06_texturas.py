# Textura de pedra com rachaduras de lava, TILEAVEL (o pacote gratis so trouxe a telha UDIM do peito).
import numpy as np
from PIL import Image
import shutil
N = 2048; rng = np.random.RandomState(11)
def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def fbm(beta, seed):
    r = np.random.RandomState(seed); f = np.fft.fftfreq(N)[:, None] ** 2 + np.fft.fftfreq(N)[None, :] ** 2; f[0, 0] = 1
    sp = np.fft.fft2(r.randn(N, N)) / (f ** (beta / 4.0)); sp[0, 0] = 0; x = np.real(np.fft.ifft2(sp)); x -= x.min(); return (x / x.max()).astype(np.float32)
def worley(G, seed, warp=0.0):
    r = np.random.RandomState(seed); pts = r.rand(G, G, 2).astype(np.float32); ids = r.rand(G, G).astype(np.float32)
    y, x = np.mgrid[0:N, 0:N].astype(np.float32); x = x / N * G; y = y / N * G
    if warp > 0: x = x + (fbm(4.2, seed + 40) - 0.5) * warp * 2; y = y + (fbm(4.2, seed + 41) - 0.5) * warp * 2
    cx = np.floor(x).astype(int); cy = np.floor(y).astype(int)
    f1 = np.full((N, N), 9, np.float32); f2 = np.full((N, N), 9, np.float32); cid = np.zeros((N, N), np.float32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            gx = (cx + dx) % G; gy = (cy + dy) % G; px = cx + dx + pts[gy, gx, 0]; py = cy + dy + pts[gy, gx, 1]
            d = np.sqrt((x - px) ** 2 + (y - py) ** 2)
            m1 = d < f1; f2 = np.where(m1, f1, np.minimum(f2, d)); cid = np.where(m1, ids[gy, gx], cid); f1 = np.where(m1, d, f1)
    return f1, f2, cid
f1, f2, cid = worley(6, 5, 0.55); g1, g2, _ = worley(15, 9, 0.4)
borda = f2 - f1; borda_f = g2 - g1
cracks = 1 - smooth(0.02, 0.075, borda)                                   # rachaduras grandes entre as placas
cracks2 = (1 - smooth(0.012, 0.05, borda_f)) * 0.35                       # rachaduras finas
fis = np.clip(cracks + cracks2, 0, 1)
a = fbm(2.6, 1); b = fbm(1.4, 2); c = fbm(3.2, 3)
altura = 0.45 * a + 0.25 * b + 0.30 * cid - 0.55 * fis
altura = (altura - altura.min()) / (altura.max() - altura.min())
escuro = np.array([0.035, 0.03, 0.028]); claro = np.array([0.20, 0.17, 0.15]); quente = np.array([0.16, 0.08, 0.05])
t = smooth(0.25, 0.85, altura)[..., None]
cor = escuro * (1 - t) + claro * t
cor = cor * (0.55 + 0.9 * b[..., None]) * (0.7 + 0.6 * c[..., None]) * (1 - 0.8 * fis[..., None]) + quente * (cid[..., None] * 0.35) * (1 - fis[..., None])
brilho = fis * smooth(0.38, 0.72, c) * (0.45 + 0.55 * a)                  # so parte das rachaduras brilha
laranja = np.array([1.0, 0.32, 0.04]); amarelo = np.array([1.0, 0.72, 0.18])
emi = (laranja * (1 - brilho[..., None] ** 2) + amarelo * brilho[..., None] ** 2) * (brilho[..., None] ** 1.4) * 1.6
cor = np.clip(cor + emi * 0.18, 0, 1)
# normal a partir da altura
dx = np.roll(altura, -1, 1) - np.roll(altura, 1, 1); dy = np.roll(altura, -1, 0) - np.roll(altura, 1, 0); forca = 9.0
nx = -dx * forca; ny = dy * forca; nz = np.ones_like(nx); l = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2); nrm = np.stack([nx / l, ny / l, nz / l], -1) * 0.5 + 0.5
rug = np.clip(0.88 - 0.25 * smooth(0.5, 1.0, altura) - 0.2 * fis, 0, 1)
Image.fromarray((np.clip(cor, 0, 1) * 255).astype(np.uint8)).save("saida/Golem/Golem_cor.jpg", quality=92)
Image.fromarray((np.clip(emi, 0, 1) * 255).astype(np.uint8)).save("saida/Golem/Golem_emissivo.jpg", quality=92)
Image.fromarray((nrm * 255).astype(np.uint8)).save("saida/Golem/Golem_normal.png")
Image.fromarray(((1 - rug) * 255).astype(np.uint8)).save("saida/Golem/Golem_suavidade.png")        # suavidade = 1 - rugosidade
T = "C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/textures/"
for src, dst in (("CHARACTER_ROCK_BAJA_EYES_Emissive.1023.png", "Golem_olhos_emissivo.png"), ("CHARACTER_ROCK_BAJA_EYES_Normal.1023.png", "Golem_olhos_normal.png")):
    Image.open(T + src).convert("RGB").resize((1024, 1024)).save("saida/Golem/" + dst)
prev = Image.fromarray((np.clip(cor, 0, 1) * 255).astype(np.uint8)).resize((600, 600)); pe = Image.fromarray((np.clip(emi, 0, 1) * 255).astype(np.uint8)).resize((600, 600))
W = Image.new("RGB", (1200, 600)); W.paste(prev, (0, 0)); W.paste(pe, (600, 0)); W.save("saida/tex_nova.png"); print("ok")
