#!/usr/bin/env python3
# Modelo do Caio (versão real): ESPECIFICAÇÃO + visualizador (z-buffer em Python puro) + gerador do C#.
# Uso: python3 caio.py render <saida.png> <vista> ;  python3 caio.py cs <saida.cs>
import math, sys, struct, zlib
from PIL import Image

# ---------------------------------------------------------------- materiais (rgb 0..1)
MAT = {
    'pele':   (0.93, 0.76, 0.66),
    'pelesom':(0.84, 0.64, 0.55),
    'barba':  (0.86, 0.69, 0.59),
    'cabelo': (0.46, 0.29, 0.17),
    'cabeloL':(0.60, 0.42, 0.24),
    'sobr':   (0.38, 0.25, 0.15),
    'oculos': (0.05, 0.05, 0.06),
    'branco': (0.95, 0.95, 0.93),
    'iris':   (0.28, 0.20, 0.12),
    'pupila': (0.03, 0.03, 0.03),
    'labio':  (0.82, 0.55, 0.50),
    'boca':   (0.25, 0.08, 0.08),
    'dente':  (0.96, 0.94, 0.88),
    'tunica': (0.10, 0.20, 0.62),
    'tunicaE':(0.07, 0.14, 0.46),
    'colete': (0.16, 0.12, 0.10),
    'calca':  (0.09, 0.11, 0.20),
    'bota':   (0.25, 0.15, 0.09),
    'cinto':  (0.30, 0.18, 0.09),
    'ouro':   (0.85, 0.68, 0.25),
    'madeira':(0.38, 0.24, 0.12),
    'cristal':(0.35, 0.65, 1.00),
}

# ---------------------------------------------------------------- juntas: nome -> (pai, posição local)
JOINTS = [
    ('raiz', None, (0, 0, 0)),
    ('pelvis', 'raiz', (0, 0.95, 0)),
    ('peito', 'pelvis', (0, 0.10, 0)),
    ('pescoco', 'peito', (0, 0.50, 0)),
    ('cabeca', 'pescoco', (0, 0.07, 0)),
    ('ombroE', 'peito', (0.215, 0.43, 0)), ('cotoveloE', 'ombroE', (0, -0.30, 0)), ('punhoE', 'cotoveloE', (0, -0.27, 0)),
    ('ombroD', 'peito', (-0.215, 0.43, 0)), ('cotoveloD', 'ombroD', (0, -0.30, 0)), ('punhoD', 'cotoveloD', (0, -0.27, 0)),
    ('quadrilE', 'pelvis', (0.095, -0.02, 0)), ('joelhoE', 'quadrilE', (0, -0.45, 0)), ('tornozeloE', 'joelhoE', (0, -0.43, 0)),
    ('quadrilD', 'pelvis', (-0.095, -0.02, 0)), ('joelhoD', 'quadrilD', (0, -0.45, 0)), ('tornozeloD', 'joelhoD', (0, -0.43, 0)),
]

PARTS = []
def P(nome, junta, forma, pos, esc, mat, rot=(0, 0, 0), extra=1.0, tag=''):
    PARTS.append(dict(nome=nome, junta=junta, forma=forma, pos=pos, esc=esc, mat=mat, rot=rot, extra=extra, tag=tag))

# ---------------------------------------------------------------- cabeça (a junta "cabeca" fica na base do crânio)
P('craneo', 'cabeca', 'ell', (0, 0.118, 0), (0.192, 0.255, 0.215), 'pele')
P('queixo', 'cabeca', 'ell', (0, 0.03, 0.03), (0.15, 0.15, 0.15), 'pele')
P('barba', 'cabeca', 'ell', (0, 0.035, 0.03), (0.158, 0.12, 0.14), 'barba')
P('bigode', 'cabeca', 'ell', (0, 0.064, 0.104), (0.07, 0.018, 0.03), 'barba')
P('orelhaE', 'cabeca', 'ell', (0.103, 0.115, 0.0), (0.03, 0.062, 0.042), 'pele')
P('orelhaD', 'cabeca', 'ell', (-0.103, 0.115, 0.0), (0.03, 0.062, 0.042), 'pele')
# cabelo: volume em cima e para trás, laterais curtas e topete levantado na frente
P('cabeloTopo', 'cabeca', 'ell', (0, 0.22, -0.015), (0.205, 0.14, 0.235), 'cabelo')
P('topete', 'cabeca', 'ell', (0, 0.262, 0.045), (0.17, 0.075, 0.13), 'cabeloL', rot=(-18, 0, 0))
P('topete2', 'cabeca', 'ell', (0.02, 0.255, 0.085), (0.11, 0.05, 0.08), 'cabeloL', rot=(-34, 10, 0))
P('cabeloLadoE', 'cabeca', 'ell', (0.088, 0.165, -0.015), (0.05, 0.13, 0.17), 'cabelo')
P('cabeloLadoD', 'cabeca', 'ell', (-0.088, 0.165, -0.015), (0.05, 0.13, 0.17), 'cabelo')
P('tufo1', 'cabeca', 'ell', (0.045, 0.28, 0.02), (0.09, 0.06, 0.11), 'cabeloL', rot=(-10, 0, 15))
P('tufo2', 'cabeca', 'ell', (-0.05, 0.275, 0.0), (0.09, 0.055, 0.11), 'cabelo', rot=(-8, 0, -15))
P('tufo3', 'cabeca', 'ell', (0.0, 0.29, -0.035), (0.1, 0.06, 0.1), 'cabelo')
P('cabeloNuca', 'cabeca', 'ell', (0, 0.105, -0.085), (0.19, 0.17, 0.12), 'cabelo')
# olhos
for lado, s in (('E', 1), ('D', -1)):
    P('olhoBranco' + lado, 'cabeca', 'ell', (0.046 * s, 0.128, 0.103), (0.042, 0.03, 0.026), 'branco')
    P('iris' + lado, 'cabeca', 'ell', (0.046 * s, 0.128, 0.1155), (0.024, 0.024, 0.012), 'iris')
    P('pupila' + lado, 'cabeca', 'ell', (0.046 * s, 0.128, 0.1215), (0.012, 0.012, 0.006), 'pupila')
    P('palpebra' + lado, 'cabeca', 'ell', (0.046 * s, 0.1415, 0.106), (0.046, 0.004, 0.03), 'pele', tag='palpebra')
    P('sobrancelha' + lado, 'cabeca', 'cap', (0.047 * s, 0.158, 0.108), (0.012, 0.056, 0.014), 'sobr', rot=(0, 0, 90 + 8 * s), tag='sobrancelha')
# nariz
P('nariz', 'cabeca', 'ell', (0, 0.092, 0.117), (0.034, 0.055, 0.048), 'pele')
P('narizPonta', 'cabeca', 'ell', (0, 0.075, 0.134), (0.034, 0.028, 0.032), 'pelesom')
# boca (sorriso)
P('bocaInterna', 'cabeca', 'ell', (0, 0.042, 0.1), (0.056, 0.004, 0.022), 'boca', tag='boca')
P('dentes', 'cabeca', 'ell', (0, 0.046, 0.109), (0.046, 0.008, 0.012), 'dente', tag='dentes')
P('labioSup', 'cabeca', 'ell', (0, 0.052, 0.109), (0.052, 0.009, 0.026), 'labio')
P('labioInf', 'cabeca', 'ell', (0, 0.037, 0.108), (0.046, 0.01, 0.026), 'labio', tag='labioInf')
P('cantoE', 'cabeca', 'ell', (0.032, 0.049, 0.104), (0.02, 0.012, 0.022), 'labio', rot=(0, 0, 25))
P('cantoD', 'cabeca', 'ell', (-0.032, 0.049, 0.104), (0.02, 0.012, 0.022), 'labio', rot=(0, 0, -25))
P('bochechaE', 'cabeca', 'ell', (0.052, 0.07, 0.092), (0.03, 0.03, 0.026), 'pele')
P('bochechaD', 'cabeca', 'ell', (-0.052, 0.07, 0.092), (0.03, 0.03, 0.026), 'pele')
# óculos (armação escura)
for lado, s in (('E', 1), ('D', -1)):
    P('aro' + lado, 'cabeca', 'tor', (0.047 * s, 0.128, 0.121), (0.082, 0.06, 0.008), 'oculos', extra=3.2)
    P('haste' + lado, 'cabeca', 'cap', (0.0985 * s, 0.132, 0.05), (0.007, 0.145, 0.007), 'oculos', rot=(90, 0, 0))
P('ponte', 'cabeca', 'cap', (0, 0.134, 0.127), (0.007, 0.025, 0.007), 'oculos', rot=(0, 0, 90))
# pescoço
P('pescoco', 'pescoco', 'cap', (0, 0.0, 0), (0.105, 0.17, 0.105), 'pele')

# ---------------------------------------------------------------- tronco (junta "peito" fica na cintura)
P('torso', 'peito', 'fru', (0, 0.26, 0), (0.30, 0.52, 0.205), 'tunica', extra=1.3)
P('ombros', 'peito', 'cap', (0, 0.465, 0), (0.115, 0.38, 0.13), 'tunica', rot=(0, 0, 90))
P('gola', 'peito', 'tor', (0, 0.5, 0.0), (0.2, 0.15, 0.04), 'colete', rot=(90, 0, 0))
P('cintoC', 'pelvis', 'fru', (0, 0.07, 0), (0.30, 0.07, 0.205), 'cinto', extra=1.0)
P('fivela', 'pelvis', 'box', (0, 0.07, 0.108), (0.05, 0.05, 0.012), 'ouro')
P('saia', 'pelvis', 'fru', (0, -0.14, 0), (0.37, 0.34, 0.27), 'tunica', extra=0.84)
P('bolsa', 'pelvis', 'box', (-0.19, -0.04, 0.03), (0.07, 0.11, 0.06), 'cinto')

# braços (manga até o cotovelo, antebraço de pele)
for lado, s in (('E', 1), ('D', -1)):
    P('ombro' + lado, 'ombro' + lado, 'ell', (0, 0.0, 0), (0.115, 0.115, 0.115), 'tunica')
    P('braco' + lado, 'ombro' + lado, 'cap', (0, -0.14, 0), (0.088, 0.31, 0.088), 'tunica')
    P('manga' + lado, 'cotovelo' + lado, 'fru', (0, 0.0, 0), (0.093, 0.06, 0.093), 'tunicaE', extra=0.95)
    P('antebraco' + lado, 'cotovelo' + lado, 'cap', (0, -0.14, 0), (0.07, 0.30, 0.07), 'pele')
    P('mao' + lado, 'punho' + lado, 'ell', (0, -0.055, 0.005), (0.078, 0.115, 0.05), 'pele')
    P('dedos' + lado, 'punho' + lado, 'ell', (0.0, -0.105, 0.012), (0.07, 0.07, 0.045), 'pele')
    P('polegar' + lado, 'punho' + lado, 'cap', (0.036 * s, -0.045, 0.02), (0.024, 0.07, 0.024), 'pele', rot=(0, 0, 20 * s))

# pernas
for lado, s in (('E', 1), ('D', -1)):
    P('coxa' + lado, 'quadril' + lado, 'cap', (0, -0.215, 0), (0.15, 0.50, 0.155), 'calca')
    P('canela' + lado, 'joelho' + lado, 'cap', (0, -0.215, 0), (0.115, 0.46, 0.12), 'calca')
    P('cano' + lado, 'tornozelo' + lado, 'fru', (0, 0.07, 0), (0.125, 0.16, 0.13), 'bota', extra=1.08)
    P('pe' + lado, 'tornozelo' + lado, 'ell', (0, -0.015, 0.05), (0.115, 0.085, 0.29), 'bota')

# cajado na mão direita (do personagem)
P('cajado', 'punhoD', 'cap', (0, -0.05, 0.02), (0.032, 1.9, 0.032), 'madeira', rot=(0, 0, 0))
P('cajadoTopo', 'punhoD', 'ell', (0, 0.9, 0.02), (0.09, 0.09, 0.09), 'ouro')
P('cristal', 'punhoD', 'ell', (0, 0.99, 0.02), (0.075, 0.13, 0.075), 'cristal', tag='cristal')

# ================================================================ geometria (mesma em Python e C#)
def ell_mesh(sx, sy, sz, lon=20, lat=12):
    V, N, T = [], [], []
    rx, ry, rz = sx / 2, sy / 2, sz / 2
    for i in range(lat + 1):
        th = math.pi * i / lat
        for j in range(lon + 1):
            ph = 2 * math.pi * j / lon
            x, y, z = math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph)
            V.append((x * rx, y * ry, z * rz)); N.append(norm((x / rx, y / ry, z / rz)))
    for i in range(lat):
        for j in range(lon):
            a = i * (lon + 1) + j; b = a + lon + 1
            T += [(a, b, a + 1), (a + 1, b, b + 1)]
    return V, N, T

def cap_mesh(sx, sy, sz, lon=18, lat=6):
    rx, rz = sx / 2, sz / 2; ry = max(rx, rz) * 0.95; h = max(0.0, sy - 2 * ry)
    V, N, T = [], [], []
    rows = []
    for i in range(lat + 1):                       # meia esfera de cima: do polo até o equador
        th = (math.pi / 2) * (1 - i / lat); rows.append((math.cos(th), math.sin(th), h / 2))
    for i in range(lat + 1):                       # meia esfera de baixo: do equador até o polo
        th = (math.pi / 2) * i / lat; rows.append((math.cos(th), -math.sin(th), -h / 2))
    # rows: (raio_rel, y_rel, deslocamento)
    for r, yy, off in rows:
        for j in range(lon + 1):
            ph = 2 * math.pi * j / lon; cx, cz = math.cos(ph), math.sin(ph)
            V.append((cx * rx * r, off + yy * ry, cz * rz * r)); N.append(norm((cx * r / rx, yy / ry, cz * r / rz)))
    for i in range(len(rows) - 1):
        for j in range(lon):
            a = i * (lon + 1) + j; b = a + lon + 1
            T += [(a, b, a + 1), (a + 1, b, b + 1)]
    return V, N, T

def fru_mesh(sx, sy, sz, k, lon=20):
    rx, rz = sx / 2, sz / 2; V, N, T = [], [], []
    for yy, s in ((-sy / 2, 1.0), (sy / 2, k)):
        for j in range(lon + 1):
            ph = 2 * math.pi * j / lon; cx, cz = math.cos(ph), math.sin(ph)
            V.append((cx * rx * s, yy, cz * rz * s)); N.append(norm((cx / rx, (1 - k) * (rx + rz) * 0.5 / max(sy, 1e-4) * 0.5, cz / rz)))
    for j in range(lon):
        T += [(j, lon + 1 + j, j + 1), (j + 1, lon + 1 + j, lon + 2 + j)]
    # tampas
    for yy, s, ny in ((-sy / 2, 1.0, -1), (sy / 2, k, 1)):
        c = len(V); V.append((0, yy, 0)); N.append((0, ny, 0))
        for j in range(lon + 1):
            ph = 2 * math.pi * j / lon; V.append((math.cos(ph) * rx * s, yy, math.sin(ph) * rz * s)); N.append((0, ny, 0))
        for j in range(lon): T.append((c, c + 1 + j, c + 2 + j))
    return V, N, T

def box_mesh(sx, sy, sz):
    V, N, T = [], [], []
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    faces = [((1, 0, 0), [(hx, -hy, -hz), (hx, hy, -hz), (hx, hy, hz), (hx, -hy, hz)]), ((-1, 0, 0), [(-hx, -hy, hz), (-hx, hy, hz), (-hx, hy, -hz), (-hx, -hy, -hz)]),
             ((0, 1, 0), [(-hx, hy, -hz), (-hx, hy, hz), (hx, hy, hz), (hx, hy, -hz)]), ((0, -1, 0), [(-hx, -hy, hz), (-hx, -hy, -hz), (hx, -hy, -hz), (hx, -hy, hz)]),
             ((0, 0, 1), [(-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]), ((0, 0, -1), [(hx, -hy, -hz), (-hx, -hy, -hz), (-hx, hy, -hz), (hx, hy, -hz)])]
    for n, vs in faces:
        c = len(V)
        for v in vs: V.append(v); N.append(n)
        T += [(c, c + 1, c + 2), (c, c + 2, c + 3)]
    return V, N, T

def tor_mesh(sx, sy, sz, ex=2.0, seg=36, tube=8):
    RX, RY, r = sx / 2, sy / 2, sz / 2; V, N, T = [], [], []
    for i in range(seg + 1):
        a = 2 * math.pi * i / seg; cx, cy = sgn(math.cos(a)) * abs(math.cos(a)) ** (2.0 / ex), sgn(math.sin(a)) * abs(math.sin(a)) ** (2.0 / ex)
        for j in range(tube + 1):
            b = 2 * math.pi * j / tube; cb, sb = math.cos(b), math.sin(b)
            # normal do tubo (aproximada) e posição
            nx, ny, nz = cx * cb, cy * cb, sb
            V.append((cx * RX + nx * r, cy * RY + ny * r, nz * r)); N.append(norm((nx, ny, nz)))
    for i in range(seg):
        for j in range(tube):
            a = i * (tube + 1) + j; b = a + tube + 1
            T += [(a, b, a + 1), (a + 1, b, b + 1)]
    return V, N, T

def sgn(v): return 1.0 if v >= 0 else -1.0

def norm(v):
    l = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2) or 1.0
    return (v[0] / l, v[1] / l, v[2] / l)

def mesh_de(p):
    f, e = p['forma'], p['esc']
    if f == 'ell': return ell_mesh(*e)
    if f == 'cap': return cap_mesh(*e)
    if f == 'fru': return fru_mesh(e[0], e[1], e[2], p['extra'])
    if f == 'box': return box_mesh(*e)
    if f == 'tor': return tor_mesh(e[0], e[1], e[2], p['extra'])
    raise ValueError(f)

# ================================================================ matrizes (rotação do Unity: Z, depois X, depois Y)
def rot_euler(rx, ry, rz):
    rx, ry, rz = math.radians(rx), math.radians(ry), math.radians(rz)
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]; Ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]; Rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]
    return mm(Ry, mm(Rx, Rz))

def mm(a, b): return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
def mv(m, v): return (m[0][0] * v[0] + m[0][1] * v[1] + m[0][2] * v[2], m[1][0] * v[0] + m[1][1] * v[1] + m[1][2] * v[2], m[2][0] * v[0] + m[2][1] * v[1] + m[2][2] * v[2])

def pose_juntas(pose=None):
    """posição e rotação mundial de cada junta (pose: nome -> (rx,ry,rz) em graus)"""
    pose = pose or {}
    mundo = {}
    for nome, pai, pos in JOINTS:
        rl = rot_euler(*pose.get(nome, (0, 0, 0)))
        if pai is None: mundo[nome] = ((0, 0, 0), rl)
        else:
            pp, pr = mundo[pai]; off = mv(pr, pos)
            mundo[nome] = ((pp[0] + off[0], pp[1] + off[1], pp[2] + off[2]), mm(pr, rl))
    return mundo

def triangulos(pose=None):
    mundo = pose_juntas(pose); out = []
    for p in PARTS:
        jp, jr = mundo[p['junta']]; lr = rot_euler(*p['rot']); R = mm(jr, lr)
        off = mv(jr, p['pos']); base = (jp[0] + off[0], jp[1] + off[1], jp[2] + off[2])
        V, N, T = mesh_de(p); col = MAT[p['mat']]
        W = [(lambda q: (base[0] + q[0], base[1] + q[1], base[2] + q[2]))(mv(R, v)) for v in V]
        NN = [mv(R, n) for n in N]
        for a, b, c in T: out.append((W[a], W[b], W[c], NN[a], NN[b], NN[c], col))
    return out

# ================================================================ renderizador
def render(tris, w, h, centro, altura, yaw=0, pitch=0, fundo=(40, 44, 54), luz=(0.4, 0.7, 0.6), supersample=2):
    W, H = w * supersample, h * supersample
    zb = [[-1e9] * W for _ in range(H)]; img = [[None] * W for _ in range(H)]
    esc = H / altura
    ry = rot_euler(0, -yaw, 0); rx = rot_euler(pitch, 0, 0); cam = mm(rx, ry)
    L = norm(mv(cam, norm(luz)))     # luz fixa em relação à câmera
    def proj(p):
        q = mv(cam, (p[0] - centro[0], p[1] - centro[1], p[2] - centro[2]))
        return (W / 2 + q[0] * esc, H / 2 - q[1] * esc, q[2]), q
    for (a, b, c, na, nb, nc, col) in tris:
        pa, qa = proj(a); pb, qb = proj(b); pc, qc = proj(c)
        n_a, n_b, n_c = mv(cam, na), mv(cam, nb), mv(cam, nc)
        minx = max(0, int(min(pa[0], pb[0], pc[0]))); maxx = min(W - 1, int(max(pa[0], pb[0], pc[0])) + 1)
        miny = max(0, int(min(pa[1], pb[1], pc[1]))); maxy = min(H - 1, int(max(pa[1], pb[1], pc[1])) + 1)
        den = (pb[1] - pc[1]) * (pa[0] - pc[0]) + (pc[0] - pb[0]) * (pa[1] - pc[1])
        if abs(den) < 1e-9: continue
        for y in range(miny, maxy + 1):
            for x in range(minx, maxx + 1):
                l1 = ((pb[1] - pc[1]) * (x + 0.5 - pc[0]) + (pc[0] - pb[0]) * (y + 0.5 - pc[1])) / den
                l2 = ((pc[1] - pa[1]) * (x + 0.5 - pc[0]) + (pa[0] - pc[0]) * (y + 0.5 - pc[1])) / den
                l3 = 1 - l1 - l2
                if l1 < -1e-4 or l2 < -1e-4 or l3 < -1e-4: continue
                z = l1 * pa[2] + l2 * pb[2] + l3 * pc[2]
                if z <= zb[y][x]: continue
                nx = l1 * n_a[0] + l2 * n_b[0] + l3 * n_c[0]; ny = l1 * n_a[1] + l2 * n_b[1] + l3 * n_c[1]; nz = l1 * n_a[2] + l2 * n_b[2] + l3 * n_c[2]
                nl = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
                if nz / nl < 0: nx, ny, nz = -nx, -ny, -nz        # face de trás: inverte a normal
                d = max(0.0, (nx * L[0] + ny * L[1] + nz * L[2]) / nl)
                s = 0.38 + 0.72 * d
                zb[y][x] = z; img[y][x] = (min(255, int(col[0] * s * 255)), min(255, int(col[1] * s * 255)), min(255, int(col[2] * s * 255)))
    im = Image.new('RGB', (W, H), fundo); px = im.load()
    for y in range(H):
        for x in range(W):
            if img[y][x] is not None: px[x, y] = img[y][x]
    return im.resize((w, h), Image.LANCZOS) if supersample > 1 else im

if __name__ == '__main__':
    cmd = sys.argv[1]
    if cmd == 'render':
        saida, vista = sys.argv[2], sys.argv[3]
        tris = triangulos()
        if vista == 'corpo': im = render(tris, 360, 720, (0, 0.93, 0), 2.0, 0, 0)
        elif vista == 'lado': im = render(tris, 360, 720, (0, 0.93, 0), 2.0, 90, 0)
        elif vista == 'rosto': im = render(tris, 640, 640, (0, 1.70, 0.05), 0.55, 0, 0)
        elif vista == 'rosto34': im = render(tris, 640, 640, (0, 1.70, 0.05), 0.55, 30, 4)
        elif vista == 'busto': im = render(tris, 640, 640, (0, 1.55, 0.05), 1.05, 15, 3)
        im.save(saida)

# ================================================================ gerador do C# (dados)
def f(v): return ('%.4f' % v).rstrip('0').rstrip('.') + 'f' if abs(v) > 1e-9 else '0f'
def v3(t): return 'new Vector3(%s, %s, %s)' % (f(t[0]), f(t[1]), f(t[2]))

def gerar_cs(caminho):
    L = ['// ARQUIVO GERADO (não edite à mão): a especificação do Caio real fica em caio.py do scratchpad; rode "python3 caio.py cs <arquivo>" para atualizar.',
         'using UnityEngine;', '',
         '// Dados do modelo do Caio (coordenador do curso): juntas, peças e cores. O desenho e a animação ficam em CaioReal.cs.',
         'public static class CaioRealDados', '{',
         '    public struct Junta { public string nome, pai; public Vector3 pos; public Junta(string n, string p, Vector3 q) { nome = n; pai = p; pos = q; } }',
         '    public struct Parte', '    {',
         '        public string nome, junta, forma, mat, tag; public Vector3 pos, esc, rot; public float extra;',
         '        public Parte(string n, string j, string fo, Vector3 p, Vector3 e, string m, Vector3 r, float ex, string t) { nome = n; junta = j; forma = fo; pos = p; esc = e; mat = m; rot = r; extra = ex; tag = t; }',
         '    }', '',
         '    public static readonly Junta[] Juntas =', '    {']
    for n, p, pos in JOINTS: L.append('        new Junta("%s", "%s", %s),' % (n, p or '', v3(pos)))
    L += ['    };', '', '    public static Color Cor(string mat)', '    {', '        switch (mat)', '        {']
    for k, c in MAT.items(): L.append('            case "%s": return new Color(%s, %s, %s);' % (k, f(c[0]), f(c[1]), f(c[2])))
    L += ['        }', '        return Color.white;', '    }', '', '    public static readonly Parte[] Partes =', '    {']
    for p in PARTS: L.append('        new Parte("%s", "%s", "%s", %s, %s, "%s", %s, %s, "%s"),' % (p['nome'], p['junta'], p['forma'], v3(p['pos']), v3(p['esc']), p['mat'], v3(p['rot']), f(p['extra']), p['tag']))
    L += ['    };', '}', '']
    open(caminho, 'w', encoding='utf-8').write('\n'.join(L))

if __name__ == '__main__' and sys.argv[1] == 'cs': gerar_cs(sys.argv[2])
