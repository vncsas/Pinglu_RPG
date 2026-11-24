# Cena final do trailer: tudo preto e, aos poucos, abre o olho do dragão (sem mostrar o corpo). Gera um mp4 mudo.
# uso: python olho.py saida.mp4 [duracao]
import sys, subprocess, numpy as np
W, H, FPS = 1920, 1080, 60
DUR = float(sys.argv[2]) if len(sys.argv) > 2 else 9.0
rng = np.random.default_rng(7)

def suave(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)
def ruido(w, h, esc, seed):                                    # ruído suave (valor interpolado)
    r = np.random.default_rng(seed); g = r.random((h // esc + 3, w // esc + 3)).astype(np.float32)
    ys = np.arange(h) / esc; xs = np.arange(w) / esc; y0 = ys.astype(int); x0 = xs.astype(int); fy = suave(ys - y0)[:, None]; fx = suave(xs - x0)[None, :]
    a = g[y0][:, x0]; b = g[y0][:, x0 + 1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
X, Y = np.meshgrid(np.arange(W, dtype=np.float32) - W / 2 - 40, np.arange(H, dtype=np.float32) - H / 2 + 10)
R = np.sqrt(X * X + Y * Y); ANG = np.arctan2(Y, X)
# escamas ao redor: células irregulares com bordas escuras
n1 = ruido(W, H, 90, 1); n2 = ruido(W, H, 40, 2); n3 = ruido(W, H, 14, 3)
escamas = 0.55 + 0.45 * np.sin(X / 23 + n1 * 9) * np.sin(Y / 19 + n2 * 7); escamas = escamas ** 1.5 * (0.7 + 0.5 * n3)
# textura da íris: fibras radiais
fib = ruido(W, H, 6, 4) * 0.6 + ruido(W, H, 18, 5) * 0.4
fibrad = 0.5 + 0.5 * np.sin(ANG * 38 + n2 * 6 + n3 * 3)
A_EIXO, H_EIXO = 560.0, 230.0                                  # meia-largura e meia-altura do olho aberto
def quadro(t):
    # abertura da pálpebra: preto -> fresta -> olho aberto
    o = suave((t - 2.1) / 3.4); o = o ** 1.1
    img = np.zeros((H, W, 3), np.float32)
    brilho = suave((t - 1.2) / 2.5) 
    if o <= 0.002 and brilho <= 0.002: return img
    # contorno da amêndoa
    u = np.clip(X / A_EIXO, -1, 1); borda = np.sqrt(np.clip(1 - u * u, 0, 1))
    alt = H_EIXO * o * borda * (1 + 0.1 * (u > 0) * 0 ) ; sup = -alt * 1.05 - 6 * o; inf = alt * 0.8 + 6 * o
    dentro = (Y > sup) & (Y < inf) & (np.abs(X) < A_EIXO)
    dist_borda = np.minimum(Y - sup, inf - Y); dist_borda = np.where(dentro, dist_borda, 0)
    # brilho que escapa pelo olho iluminando as escamas
    glow = np.exp(-R / 330.0) * brilho
    quente = np.stack([1.0 * np.ones_like(R), 0.42 * np.ones_like(R), 0.08 * np.ones_like(R)], -1)
    img += (glow * (0.18 + 0.55 * escamas) * 0.75)[..., None] * quente
    # íris
    pup_larg = (26 - 14 * suave((t - 5.2) / 1.4) + 3 * np.sin(t * 2.2)) * (0.4 + 0.6 * o)
    pup_alt = (H_EIXO * 0.92) * o
    r_ir = np.sqrt((X / (A_EIXO * 0.46)) ** 2 + (Y / max(1.0, H_EIXO * o * 1.05)) ** 2)
    ir = np.clip(1 - r_ir, 0, 1)
    d_p = np.sqrt((X / max(pup_larg, 1)) ** 2 + (Y / max(pup_alt, 1)) ** 2)
    cor_in = np.array([1.0, 0.72, 0.14], np.float32); cor_out = np.array([0.8, 0.14, 0.02], np.float32)
    mix = np.clip(d_p / 6.0, 0, 1)[..., None]
    tex = (0.55 + 0.7 * fib * (0.6 + 0.4 * fibrad))[..., None]
    iris = (cor_in * (1 - mix) + cor_out * mix) * tex * (0.7 + 0.5 * np.exp(-d_p / 3.0))[..., None]
    iris *= (0.35 + 0.65 * np.clip(ir * 2.2, 0, 1))[..., None]
    esclera = np.array([0.55, 0.12, 0.03], np.float32) * (0.5 + 0.6 * fib)[..., None]          # fora da íris: pele em brasa
    interior = np.where((r_ir < 1)[..., None], iris, esclera * 0.55)
    escuro_borda = np.clip(dist_borda / (38 * o + 1), 0, 1) ** 0.8                           # a pálpebra sombreia a íris
    interior *= escuro_borda[..., None]
    pupila = (d_p < 1.0)
    interior = np.where(pupila[..., None], np.array([0.01, 0.0, 0.0], np.float32), interior)
    # borda da pupila brilhante
    aro = np.exp(-((d_p - 1.0) / 0.12) ** 2)[..., None] * np.array([1.0, 0.8, 0.3], np.float32) * 0.6
    interior += np.where(pupila[..., None], 0, aro) * escuro_borda[..., None]
    # reflexo
    refl = np.exp(-(((X + 150) / 38) ** 2 + ((Y + 70 * o) / 26) ** 2)) * o
    interior += refl[..., None] * 0.9
    img = np.where(dentro[..., None], interior * (0.6 + 0.4 * brilho), img)
    # fresta de luz quando ainda quase fechado
    fresta = np.exp(-((Y / (3 + 40 * o)) ** 2)) * np.exp(-(np.abs(X) / (A_EIXO * 0.55)) ** 2) * brilho * (1 - suave(o * 3))
    img += fresta[..., None] * np.array([1.0, 0.45, 0.1], np.float32) * 0.8
    # pulsar leve e vinheta
    img *= (1 + 0.06 * np.sin(t * 3.1))
    img *= np.clip(1.15 - R / 1250.0, 0, 1)[..., None]
    return img
p = subprocess.Popen(['ffmpeg', '-loglevel', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
                      '-an', '-c:v', 'libx264', '-preset', 'fast', '-crf', '15', '-pix_fmt', 'yuv420p', sys.argv[1]], stdin=subprocess.PIPE)
for i in range(int(DUR * FPS)):
    f = quadro(i / FPS)
    f = f * (1 - suave((i / FPS - (DUR - 2.8)) / 2.5))          # o olho fica aberto até o fim e some no preto
    f = f / (1 + f * 0.35)                                   # compressão suave dos brilhos
    f = np.clip(f ** 0.95 * 1.08, 0, 1)
    p.stdin.write((f * 255).astype(np.uint8).tobytes())
p.stdin.close(); p.wait(); print('pronto', sys.argv[1])
