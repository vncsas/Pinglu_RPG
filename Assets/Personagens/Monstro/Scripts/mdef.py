# Definicao do esqueleto do monstro do pantano (pose ORIGINAL do scan, ja escalada x12). Eixos Blender: z cima, frente = -Y.
from mathutils import Vector as V
def v(x, y, z): return V((x, y, z))
COLUNA = [  # nome, head, tail, parent
 ("Root",   v(0, 0.03, 0.0),   v(0, 0.03, 0.25), None),
 ("Hips",   v(0, 0.03, 0.92),  v(0, 0.02, 1.06), "Root"),
 ("Spine1", v(0, 0.02, 1.06),  v(-0.03, 0.0, 1.27), "Hips"),
 ("Spine2", v(-0.03, 0.0, 1.27), v(0, -0.07, 1.47), "Spine1"),
 ("Chest",  v(0, -0.07, 1.47), v(0.03, -0.33, 1.80), "Spine2"),
 ("Neck",   v(0.03, -0.33, 1.80), v(0.12, -0.52, 2.02), "Chest"),
 ("Head",   v(0.12, -0.52, 2.02), v(0.18, -0.58, 2.27), "Neck"),
]
# pernas: lado -> dict. L = +x, R = -x
PERNAS = {
 "L": dict(quadril=v(0.21, -0.03, 0.93), joelho=v(0.24, -0.45, 0.67), tornozelo=v(0.26, -0.20, 0.22), bola=v(0.26, -0.38, 0.06), ponta=v(0.26, -0.50, 0.02)),
 "R": dict(quadril=v(-0.19, 0.08, 0.93), joelho=v(-0.35, 0.45, 0.55), tornozelo=v(-0.41, 0.90, 0.22), bola=v(-0.41, 0.72, 0.06), ponta=v(-0.41, 0.62, 0.02)),
}
BRACOS = {
 "L": dict(clav=v(0.08, -0.20, 1.66), ombro=v(0.34, -0.14, 1.62), cotovelo=v(0.52, -0.10, 1.20), pulso=v(0.50, 0.00, 0.93), mao=v(0.50, 0.05, 0.80), dedos=v(0.50, 0.06, 0.70)),
 "R": dict(clav=v(-0.02, -0.30, 1.70), ombro=v(-0.34, -0.42, 1.68), cotovelo=v(-0.37, -0.80, 1.64), pulso=v(-0.30, -1.05, 1.50), mao=v(-0.29, -1.18, 1.42), dedos=v(-0.28, -1.28, 1.30)),
}
# direcoes alvo ("em pe"): (osso, direcao)
ALVO = {
 "L_Thigh": v(0.08, -0.35, -0.93), "L_Shin": v(0.0, 0.25, -0.97), "L_Foot": v(0, -0.75, -0.65), "L_Toe": v(0, -0.95, -0.30),
 "R_Thigh": v(-0.10, -0.62, -0.78), "R_Shin": v(0.0, 0.45, -0.89), "R_Foot": v(0, -0.75, -0.65), "R_Toe": v(0, -0.95, -0.30),
 "L_Arm": v(0.14, -0.10, -0.98), "L_Forearm": v(0.0, -0.30, -0.95), "L_Hand": v(0, -0.15, -0.99), "L_Fingers": v(0, -0.10, -0.99),
 "R_Arm": v(-0.14, -0.10, -0.98), "R_Forearm": v(0.0, -0.30, -0.95), "R_Hand": v(0, -0.15, -0.99), "R_Fingers": v(0, -0.10, -0.99),
}
ALVO.update({"Spine1": v(0, -0.03, 1), "Spine2": v(0, -0.10, 1), "Chest": v(0, -0.20, 1), "Neck": v(0, -0.28, 1), "Head": v(0, -0.12, 1),
             "L_Clavicle": v(1, -0.12, 0.06), "R_Clavicle": v(-1, -0.12, 0.06)})
ALVO["L_Thigh"] = v(0.05, -0.35, -0.93); ALVO["R_Thigh"] = v(-0.05, -0.50, -0.86); ALVO["L_Shin"] = v(0.0, 0.25, -0.97); ALVO["R_Shin"] = v(0.0, 0.35, -0.94)
ALVO["L_Arm"] = v(0.10, -0.12, -0.98); ALVO["R_Arm"] = v(-0.10, -0.12, -0.98)
def ossos():
    L = list(COLUNA)
    for s, p in PERNAS.items():
        L += [(s+"_Thigh", p["quadril"], p["joelho"], "Hips"), (s+"_Shin", p["joelho"], p["tornozelo"], s+"_Thigh"),
              (s+"_Foot", p["tornozelo"], p["bola"], s+"_Shin"), (s+"_Toe", p["bola"], p["ponta"], s+"_Foot")]
    for s, b in BRACOS.items():
        L += [(s+"_Clavicle", b["clav"], b["ombro"], "Chest"), (s+"_Arm", b["ombro"], b["cotovelo"], s+"_Clavicle"),
              (s+"_Forearm", b["cotovelo"], b["pulso"], s+"_Arm"), (s+"_Hand", b["pulso"], b["mao"], s+"_Forearm"),
              (s+"_Fingers", b["mao"], b["dedos"], s+"_Hand")]
    return L
# raio de captura (geodesico) e limite euclidiano por osso
RAIO = {"Hips": 0.32, "Spine1": 0.30, "Spine2": 0.32, "Chest": 0.42, "Neck": 0.16, "Head": 0.20, "Thigh": 0.20, "Shin": 0.15, "Foot": 0.12, "Toe": 0.09,
        "Clavicle": 0.14, "Arm": 0.12, "Forearm": 0.12, "Hand": 0.10, "Fingers": 0.07}
LIM = {"Thigh": 0.30, "Shin": 0.22, "Arm": 0.20, "Forearm": 0.20, "Clavicle": 0.22}
