# Esqueleto da ovelha (ja em pe): metros, Z para cima, cabeca em -Y.
from mathutils import Vector as V
XM = 0.02
def leg(x, y, F):
    if F: return dict(top=V((x, y, 0.50)), cot=V((x, y, 0.32)), pul=V((x, y, 0.15)), bol=V((x, y, 0.075)), cor=V((x, y - 0.01, 0.035)), casco=V((x, y - 0.015, 0.0)))
    return dict(top=V((x, y, 0.54)), cot=V((x, y - 0.03, 0.38)), pul=V((x, y + 0.03, 0.17)), bol=V((x, y + 0.02, 0.075)), cor=V((x, y + 0.005, 0.035)), casco=V((x, y - 0.005, 0.0)))
TROTE = {"LF": leg(0.10, -0.07, True), "RF": leg(-0.06, -0.10, True), "LH": leg(0.115, 0.43, False), "RH": leg(-0.075, 0.425, False)}
COLUNA = [
    ("Root", V((XM, 0.15, 0.0)), V((XM, 0.15, 0.15)), None),
    ("Pelvis", V((XM, 0.48, 0.55)), V((XM, 0.32, 0.56)), "Root"),
    ("Spine1", V((XM, 0.32, 0.56)), V((XM, 0.14, 0.56)), "Pelvis"),
    ("Spine2", V((XM, 0.14, 0.56)), V((XM, -0.04, 0.57)), "Spine1"),
    ("Chest", V((XM, -0.04, 0.57)), V((XM, -0.15, 0.60)), "Spine2"),
    ("Neck1", V((XM, -0.15, 0.60)), V((XM, -0.22, 0.66)), "Chest"),
    ("Neck2", V((XM, -0.22, 0.66)), V((XM, -0.27, 0.70)), "Neck1"),
    ("Neck3", V((XM, -0.27, 0.70)), V((XM, -0.30, 0.71)), "Neck2"),
    ("Head", V((XM, -0.30, 0.71)), V((XM, -0.43, 0.55)), "Neck3"),
    ("Jaw", V((XM, -0.35, 0.58)), V((XM, -0.43, 0.50)), "Head"),
    ("Ear_R", V((-0.07, -0.30, 0.70)), V((-0.14, -0.29, 0.68)), "Head"),
    ("Ear_L", V((0.11, -0.30, 0.70)), V((0.18, -0.29, 0.68)), "Head"),
    ("Tail1", V((XM, 0.52, 0.55)), V((XM, 0.58, 0.50)), "Pelvis"),
    ("Tail2", V((XM, 0.58, 0.50)), V((XM, 0.62, 0.44)), "Tail1"),
    ("Tail3", V((XM, 0.62, 0.44)), V((XM, 0.64, 0.40)), "Tail2"),
    ("Tail4", V((XM, 0.64, 0.40)), V((XM, 0.65, 0.37)), "Tail3"),
    ("Tail5", V((XM, 0.65, 0.37)), V((XM, 0.66, 0.34)), "Tail4"),
    ("Saddle", V((XM, 0.2, 0.68)), V((XM, 0.2, 0.75)), "Spine2"),
]
