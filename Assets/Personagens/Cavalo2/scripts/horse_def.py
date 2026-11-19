# Esqueleto do cavalo de tracao (ja esta em pe, simetrico): posicoes em metros, Z para cima, cabeca em -Y.
from mathutils import Vector as V
XM = 0.0
def leg(sx, F):
    # sx = lado (+1 esquerda / -1 direita do cavalo)
    if F:
        return dict(top=V((0.16 * sx, -0.52, 1.20)), cot=V((0.165 * sx, -0.45, 0.78)), pul=V((0.155 * sx, -0.43, 0.42)), bol=V((0.17 * sx, -0.41, 0.21)), cor=V((0.19 * sx, -0.435, 0.105)), casco=V((0.19 * sx, -0.445, 0.0)))
    return dict(top=V((0.20 * sx, 0.60, 1.22)), cot=V((0.21 * sx, 0.45, 0.86)), pul=V((0.205 * sx, 0.82, 0.56)), bol=V((0.24 * sx, 0.885, 0.23)), cor=V((0.245 * sx, 0.85, 0.105)), casco=V((0.25 * sx, 0.81, 0.0)))
TROTE = {"LF": leg(1, True), "RF": leg(-1, True), "LH": leg(1, False), "RH": leg(-1, False)}
COLUNA = [
    ("Root", V((XM, 0.35, 0.0)), V((XM, 0.35, 0.25)), None),
    ("Pelvis", V((XM, 1.00, 1.27)), V((XM, 0.65, 1.28)), "Root"),
    ("Spine1", V((XM, 0.65, 1.28)), V((XM, 0.30, 1.27)), "Pelvis"),
    ("Spine2", V((XM, 0.30, 1.27)), V((XM, -0.05, 1.30)), "Spine1"),
    ("Chest", V((XM, -0.05, 1.30)), V((XM, -0.50, 1.46)), "Spine2"),
    ("Neck1", V((XM, -0.50, 1.46)), V((XM, -0.78, 1.50)), "Chest"),
    ("Neck2", V((XM, -0.78, 1.50)), V((XM, -0.98, 1.56)), "Neck1"),
    ("Neck3", V((XM, -0.98, 1.56)), V((XM, -1.10, 1.64)), "Neck2"),
    ("Head", V((XM, -1.10, 1.64)), V((XM, -1.27, 1.18)), "Neck3"),
    ("Jaw", V((XM, -1.18, 1.30)), V((XM, -1.27, 1.10)), "Head"),
    ("Ear_R", V((-0.07, -1.14, 1.72)), V((-0.07, -1.13, 1.80)), "Head"),
    ("Ear_L", V((0.07, -1.14, 1.72)), V((0.07, -1.13, 1.80)), "Head"),
    ("Tail1", V((XM, 1.03, 1.27)), V((XM, 1.13, 1.10)), "Pelvis"),
    ("Tail2", V((XM, 1.13, 1.10)), V((XM, 1.20, 0.88)), "Tail1"),
    ("Tail3", V((XM, 1.20, 0.88)), V((XM, 1.24, 0.62)), "Tail2"),
    ("Tail4", V((XM, 1.24, 0.62)), V((XM, 1.27, 0.38)), "Tail3"),
    ("Tail5", V((XM, 1.27, 0.38)), V((XM, 1.29, 0.15)), "Tail4"),
    ("Saddle", V((XM, 0.20, 1.38)), V((XM, 0.20, 1.48)), "Spine2"),
]
