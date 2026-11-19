# Esqueleto do Angler (pose original do scan, escala x3). Blender: z cima, frente = -Y. L = +x, R = -x
from mathutils import Vector as V
def v(x, y, z): return V((x, y, z))
COLUNA = [
 ("Root",   v(0, 0.30, 0.0),  v(0, 0.30, 0.30), None),
 ("Hips",   v(0, 0.35, 1.25), v(0, 0.10, 1.35), "Root"),
 ("Spine1", v(0, 0.10, 1.35), v(0, -0.15, 1.50), "Hips"),
 ("Spine2", v(0, -0.15, 1.50), v(0, -0.40, 1.68), "Spine1"),
 ("Chest",  v(0, -0.40, 1.68), v(0, -0.62, 1.85), "Spine2"),
 ("Neck",   v(0, -0.62, 1.85), v(0, -0.80, 2.00), "Chest"),
 ("Head",   v(0, -0.80, 2.00), v(0, -1.30, 2.02), "Neck"),
 ("Jaw",    v(0, -0.78, 1.78), v(0, -1.20, 1.48), "Head"),
 ("Lure1",  v(0, -1.32, 2.15), v(0, -1.30, 2.00), "Head"),
 ("Lure2",  v(0, -1.30, 2.00), v(0, -1.28, 1.82), "Lure1"),
]
CAUDA = [v(0, 0.35, 1.26), v(0, 0.55, 1.18), v(0, 0.75, 1.05), v(0, 0.95, 0.87), v(0, 1.15, 0.65), v(0, 1.30, 0.42), v(0, 1.47, 0.27)]
PERNAS = {s: dict(quadril=v(x * 0.42, 0.25, 1.15), joelho=v(x * 0.55, -0.25, 0.85), jarrete=v(x * 0.50, 0.30, 0.48), bola=v(x * 0.58, 0.0, 0.10), ponta=v(x * 0.64, -0.30, 0.03)) for s, x in (("L", 1), ("R", -1))}
BRACOS = {s: dict(clav=v(x * 0.25, -0.30, 1.78), ombro=v(x * 0.72, -0.10, 1.50), cotovelo=v(x * 0.90, -0.22, 1.05), pulso=v(x * 1.05, -0.50, 0.58), mao=v(x * 1.05, -0.55, 0.32), dedos=v(x * 1.05, -0.55, 0.06)) for s, x in (("L", 1), ("R", -1))}
def ossos():
    L = list(COLUNA)
    for i in range(6): L.append(("Tail%d" % (i + 1), CAUDA[i], CAUDA[i + 1], "Hips" if i == 0 else "Tail%d" % i))
    for s, p in PERNAS.items():
        L += [(s + "_Thigh", p["quadril"], p["joelho"], "Hips"), (s + "_Shin", p["joelho"], p["jarrete"], s + "_Thigh"),
              (s + "_Foot", p["jarrete"], p["bola"], s + "_Shin"), (s + "_Toe", p["bola"], p["ponta"], s + "_Foot")]
    for s, b in BRACOS.items():
        L += [(s + "_Clavicle", b["clav"], b["ombro"], "Chest"), (s + "_Arm", b["ombro"], b["cotovelo"], s + "_Clavicle"),
              (s + "_Forearm", b["cotovelo"], b["pulso"], s + "_Arm"), (s + "_Hand", b["pulso"], b["mao"], s + "_Forearm"), (s + "_Fingers", b["mao"], b["dedos"], s + "_Hand")]
    return L
RAIO = {"Hips": 0.45, "Spine1": 0.45, "Spine2": 0.5, "Chest": 0.55, "Neck": 0.3, "Head": 0.45, "Jaw": 0.3, "Lure1": 0.06, "Lure2": 0.06,
        "Tail1": 0.25, "Tail2": 0.2, "Tail3": 0.2, "Tail4": 0.22, "Tail5": 0.3, "Tail6": 0.4,
        "Thigh": 0.32, "Shin": 0.22, "Foot": 0.2, "Toe": 0.16, "Clavicle": 0.3, "Arm": 0.25, "Forearm": 0.22, "Hand": 0.22, "Fingers": 0.16}
LIM = {"Thigh": 0.42, "Shin": 0.30, "Arm": 0.36, "Forearm": 0.30, "Clavicle": 0.34, "Hand": 0.30, "Fingers": 0.26}
