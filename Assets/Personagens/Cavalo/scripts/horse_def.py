# Definicao compartilhada do esqueleto do cavalo (posicoes em metros, Z para cima, cabeca em -Y).
from mathutils import Vector as V

XM = -0.02   # linha central do corpo

def lado(sinal):  # -1 = direita do cavalo (-X), +1 = esquerda (+X)
    return sinal

import math
# comprimentos (m) de baixo para cima: casco, quartela, canela, antebraco/tibia, braco/coxa  (iguais nas 4 pernas = pernas simetricas)
COMP = {"F": (0.10, 0.10, 0.28, 0.38, 0.30), "H": (0.10, 0.11, 0.32, 0.38, 0.36)}
# linha central medida de cada perna no scan, do casco para cima (x, y, z)
POLI = {
    "RF": [(-0.193, -0.40, 0.10), (-0.20, -0.428, 0.25), (-0.207, -0.501, 0.35), (-0.206, -0.509, 0.45), (-0.201, -0.504, 0.55), (-0.197, -0.417, 0.65), (-0.194, -0.314, 0.75), (-0.19, -0.21, 0.85), (-0.17, -0.20, 1.12)],
    "LF": [(0.010, -0.015, 0.0), (0.016, 0.026, 0.25), (0.027, 0.0, 0.45), (0.056, -0.054, 0.65), (0.051, -0.052, 0.75), (0.055, -0.08, 0.90), (0.06, -0.20, 1.12)],
    "RH": [(-0.093, 1.16, 0.0), (-0.092, 1.227, 0.25), (-0.109, 1.194, 0.45), (-0.126, 1.154, 0.55), (-0.166, 1.082, 0.65), (-0.194, 1.006, 0.75), (-0.20, 0.93, 0.88), (-0.20, 1.08, 1.20)],
    "LH": [(0.127, 0.87, 0.17), (0.129, 0.879, 0.25), (0.132, 0.856, 0.35), (0.129, 0.928, 0.45), (0.123, 1.077, 0.55), (0.133, 1.114, 0.65), (0.156, 0.993, 0.75), (0.16, 0.95, 0.88), (0.17, 1.08, 1.18)],
}
def _amostra(poly, dist):
    pts = [V(p) for p in poly]; acc = 0.0
    for i in range(len(pts) - 1):
        seg = (pts[i + 1] - pts[i]); l = seg.length
        if acc + l >= dist: return pts[i] + seg * ((dist - acc) / l)
        acc += l
    return pts[-1] + (pts[-1] - pts[-2]).normalized() * (dist - acc)
def juntas(nome, poly):
    """casco(ponta), coroa, boleto, pulso/jarrete, cotovelo/joelho, topo -> posicoes ao longo da linha central com os comprimentos padrao"""
    L = COMP["F" if nome.endswith("F") else "H"]; d = [0.0]
    for l in L: d.append(d[-1] + l)
    P = [_amostra(poly, x) for x in d]
    return dict(casco=P[0], cor=P[1], bol=P[2], pul=P[3], cot=P[4], top=P[5])
TROTE = {n: juntas(n, POLI[n]) for n in POLI}
ANG = {"F": (15, -4, 0, -38, -42), "H": (-20, 24, -2, -30, -35)}   # arm/coxa, antebraco/tibia, canela, quartela, casco (graus da vertical; + = para tras)
def em_pe():
    out = {}
    for nome, j in TROTE.items():
        k = "F" if nome.endswith("F") else "H"; base = ANG[k]; top = j["top"]
        L = ((j["casco"] - j["cor"]).length, (j["cor"] - j["bol"]).length, (j["bol"] - j["pul"]).length, (j["pul"] - j["cot"]).length, (j["cot"] - j["top"]).length)   # comprimentos reais (corda) dos ossos
        def montar(f):
            pos = [V(top)]; segs = [L[4], L[3], L[2], L[1], L[0]]
            for lseg, ang in zip(segs, base):
                a = math.radians(ang * f); pos.append(pos[-1] + V((0, math.sin(a), -math.cos(a))) * lseg)
            return pos
        lo, hi = 0.0, 3.0
        for _ in range(40):
            mid = (lo + hi) / 2; z = montar(mid)[-1].z
            if z > 0: hi = mid
            else: lo = mid
        # a soma das quedas deve levar o casco ao chao (z=0): queda diminui com f
        f = (lo + hi) / 2; p = montar(f)
        out[nome] = dict(top=p[0], cot=p[1], pul=p[2], bol=p[3], cor=p[4], casco=p[5], f=f)
    return out

# ---- tronco (nao muda entre trote e em pe)
COLUNA = [  # nome, head, tail, parent
    ("Root", V((XM, 0.55, 0.0)), V((XM, 0.55, 0.20)), None),
    ("Pelvis", V((XM, 1.15, 1.22)), V((XM, 0.95, 1.24)), "Root"),
    ("Spine1", V((XM, 0.95, 1.24)), V((XM, 0.60, 1.22)), "Pelvis"),
    ("Spine2", V((XM, 0.60, 1.22)), V((XM, 0.30, 1.28)), "Spine1"),
    ("Chest", V((XM, 0.30, 1.28)), V((XM, 0.05, 1.42)), "Spine2"),
    ("Neck1", V((XM, 0.05, 1.42)), V((XM, -0.22, 1.68)), "Chest"),
    ("Neck2", V((XM, -0.22, 1.68)), V((XM, -0.40, 1.80)), "Neck1"),
    ("Neck3", V((XM, -0.40, 1.80)), V((XM, -0.50, 1.85)), "Neck2"),
    ("Head", V((XM, -0.50, 1.85)), V((XM, -0.84, 1.60)), "Neck3"),
    ("Jaw", V((XM, -0.62, 1.64)), V((XM, -0.80, 1.55)), "Head"),
    ("Ear_R", V((-0.085, -0.52, 1.93)), V((-0.085, -0.50, 2.04)), "Head"),
    ("Ear_L", V((0.035, -0.52, 1.93)), V((0.035, -0.50, 2.04)), "Head"),
    ("Tail1", V((XM, 1.28, 1.48)), V((XM, 1.45, 1.43)), "Pelvis"),
    ("Tail2", V((XM, 1.45, 1.43)), V((XM, 1.62, 1.30)), "Tail1"),
    ("Tail3", V((XM, 1.62, 1.30)), V((XM, 1.76, 1.12)), "Tail2"),
    ("Tail4", V((XM, 1.76, 1.12)), V((XM, 1.88, 1.00)), "Tail3"),
    ("Tail5", V((XM, 1.88, 1.00)), V((XM, 1.98, 0.95)), "Tail4"),
    ("Saddle", V((XM, 0.45, 1.38)), V((XM, 0.45, 1.48)), "Spine2"),
]
FRENTE = ("Shoulder", "Arm", "Forearm", "Cannon", "Pastern", "Hoof")
TRAS = ("Thigh", "Shin", "Cannon", "Pastern", "Hoof")
