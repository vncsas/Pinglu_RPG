# Animacoes procedurais do Angler (no lugar; o jogo move o personagem).
# Convencoes (eixos locais ~ mundo): X = pitch. Ossos para frente/horizontais: +X = bico para BAIXO. Cauda: +X = ponta para CIMA.
# Ossos que apontam para baixo (bracos/pernas): +X = para TRAS, -X = para FRENTE. Jaw: rest = ABERTA, negativo = fecha.
# Z = curva lateral (+Z: ponta vai para -x / lado R). Y = torcao no eixo do osso. Root: loc (x lado, y SOBE, z FRENTE).
import bpy, math
base = "C:/Users/Thiago/Documentos/rig-angler/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_scan.blend")
arm = bpy.data.objects["AnglerArmature"]; bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
pb = arm.pose.bones
for b in pb: b.rotation_mode = 'XYZ'
NOMES = [b.name for b in pb]
D = math.radians; TAU = 2 * math.pi; S = math.sin; C = math.cos
TAIL = ["Tail%d" % i for i in range(1, 7)]
SP = ["Hips", "Spine1", "Spine2", "Chest"]


def zerar(): return {n: [0.0, 0.0, 0.0] for n in NOMES}
def suave(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def sai(x): x = max(0.0, min(1.0, x)); return 1 - (1 - x) ** 3          # chega rapido
def entra(x): x = max(0.0, min(1.0, x)); return x ** 3                  # arranca devagar, estala no fim
EASE = {"s": suave, "o": sai, "i": entra}


def espelha(d):
    o = {}
    for k, v in d.items():
        if k == "_loc": o[k] = (-v[0], v[1], v[2]); continue
        k2 = ("R_" + k[2:]) if k.startswith("L_") else (("L_" + k[2:]) if k.startswith("R_") else k)
        o[k2] = (v[0], -v[1], -v[2]) if len(v) == 3 else v
    return o


def mix(keys, t):
    """keys: [(tempo, pose, ease)], pose = {osso: (rx, ry, rz) graus, "_loc": (x, y, z) m}. Retorna (P em rad, loc)."""
    for i in range(len(keys) - 1):
        t0, p0, _ = keys[i]; t1, p1, e = keys[i + 1]
        if t0 <= t <= t1:
            k = EASE[e]((t - t0) / (t1 - t0) if t1 > t0 else 1.0); P = zerar(); loc = [0, 0, 0]
            for n in P:
                a = p0.get(n, (0, 0, 0)); b = p1.get(n, (0, 0, 0)); P[n] = [D(a[j] + (b[j] - a[j]) * k) for j in range(3)]
            a = p0.get("_loc", (0, 0, 0)); b = p1.get("_loc", (0, 0, 0)); loc = [a[j] + (b[j] - a[j]) * k for j in range(3)]
            return P, loc
    return zerar(), [0, 0, 0]


def add(P, n, rx=0.0, ry=0.0, rz=0.0):
    P[n][0] += D(rx); P[n][1] += D(ry); P[n][2] += D(rz)


def cauda_onda(P, t, amp, vel=1.0, atraso=0.10, lev=0.0, fase=0.0):
    for i, n in enumerate(TAIL): add(P, n, lev / 6.0, 0, amp * (0.6 + 0.2 * i) * S(TAU * (t * vel - atraso * i) + fase))


# ---------------------------------------------------------------- loops
def anim_idle(t):
    P = zerar(); r = S(TAU * t)
    add(P, "Chest", -1.5 * r); add(P, "Spine2", -1.0 * r); add(P, "Neck", 1.5 * r); add(P, "Head", 2.0 * S(TAU * t + 0.8), 0, 4 * S(TAU * t + 1.4))
    add(P, "Jaw", -6 - 6 * S(TAU * 2 * t + 0.5)); add(P, "Neck", 0, 0, 6 * S(TAU * t + 1.4)); add(P, "Hips", 0, 0, 2 * S(TAU * t))
    add(P, "Lure1", 6 * S(TAU * 2 * t), 0, 8 * S(TAU * t)); add(P, "Lure2", 10 * S(TAU * 2 * t - 0.6), 0, 12 * S(TAU * t - 0.5))
    for s, ph, sg in (("L", 0.0, 1), ("R", 0.7, -1)):
        q = S(TAU * t + ph); add(P, s + "_Arm", 2.5 * q); add(P, s + "_Forearm", 2 * q); add(P, s + "_Clavicle", 0, 0, 2 * q * sg)
        add(P, s + "_Fingers", 6 + 6 * S(TAU * 2 * t + ph)); add(P, s + "_Hand", 3 * S(TAU * t + ph + 1))
    add(P, "L_Thigh", 1.5 * S(TAU * t + 2)); add(P, "R_Thigh", 1.5 * S(TAU * t + 3.5))
    cauda_onda(P, t, 7, 1.0, 0.13, 0.0); return P, [0.012 * S(TAU * t), -0.012 * (0.5 - 0.5 * C(TAU * 2 * t)), 0]


def andadura(t, off, A, K, B, Kb, lean, bob, tors, cabeca, tail_amp):
    """off: fase por membro. A: balanco coxa, K: dobra do jarrete, B: balanco braco, Kb: dobra cotovelo."""
    P = zerar()
    for s in "LR":
        q = TAU * (t + off[s + "H"]); th = -A * C(q); lift = max(0.0, -S(q))
        add(P, s + "_Thigh", th - 6 * lift); add(P, s + "_Shin", K * lift ** 0.8 + 0.1 * K * max(0.0, S(q)))
        add(P, s + "_Foot", -(th + K * lift) * 0.8 + 6 * max(0.0, S(q))); add(P, s + "_Toe", 14 * lift)
        q = TAU * (t + off[s + "F"]); lift = max(0.0, -S(q))
        add(P, s + "_Arm", -B * C(q) - 6 * lift); add(P, s + "_Forearm", -Kb * lift ** 0.8 + 3 * max(0, S(q))); add(P, s + "_Hand", 18 * lift - 6 * max(0, S(q))); add(P, s + "_Fingers", 10 + 25 * lift)
        add(P, s + "_Clavicle", 0, 0, 3 * S(q) * (1 if s == "L" else -1))
    w = S(TAU * t)
    add(P, "Hips", 0, tors * w * 0.6); add(P, "Spine1", 0, -tors * w * 0.4); add(P, "Spine2", 0, -tors * w * 0.7); add(P, "Chest", 0, -tors * w * 0.5)
    add(P, "Hips", 0, 0, 3.0 * w); add(P, "Chest", 0, 0, -4.0 * w); add(P, "Neck", 0, 0, 5.0 * w)
    for n, f in zip(SP, (0.4, 0.5, 0.6, 0.5)): add(P, n, lean * f + 1.5 * S(TAU * 2 * t + 0.5) * f)
    add(P, "Neck", -lean * 0.5 + cabeca * S(TAU * 2 * t)); add(P, "Head", -lean * 0.3 + cabeca * 0.6 * S(TAU * 2 * t - 0.5), 0, 6 * S(TAU * t + 1.0))
    add(P, "Jaw", -10 - 5 * S(TAU * 2 * t)); add(P, "Lure1", 12 * S(TAU * 2 * t + 1), 0, 10 * S(TAU * t)); add(P, "Lure2", 16 * S(TAU * 2 * t - 0.2), 0, 14 * S(TAU * t - 0.6))
    cauda_onda(P, t, tail_amp, 1.0, 0.12, 2.0)
    return P, [0.03 * w * (bob / 0.04), bob * (abs(C(TAU * t)) - 0.6), 0]


def anim_walk(t): return andadura(t, {"LF": 0.0, "RH": 0.25, "RF": 0.5, "LH": 0.75}, 20, 34, 26, 38, 3, 0.04, 8, 4, 10)


def anim_run(t):
    P, loc = andadura(t, {"LF": 0.0, "RF": 0.07, "LH": 0.50, "RH": 0.57}, 36, 62, 48, 70, 10, 0.12, 5, 4, 6)
    add(P, "Spine1", 7 * S(TAU * t + 0.4)); add(P, "Spine2", 9 * S(TAU * t + 0.4)); add(P, "Chest", -6 * S(TAU * t + 0.4)); add(P, "Hips", -8 * S(TAU * t + 0.4))
    add(P, "Jaw", 12 - 8 * S(TAU * t)); loc[2] = 0.0
    for n in TAIL: add(P, n, 4)
    return P, loc


# ---------------------------------------------------------------- ataques
def aditivo_cauda(P, t, amp=8, vel=2.0):
    cauda_onda(P, t, amp, vel, 0.12, 0.0)


def anim_mordida(t):
    """Recua o corpo, arreganha a mandibula e se lanca para a frente fechando a boca, sacode a cabeca e volta."""
    idle = {}
    recua = {"_loc": (0, -0.10, -0.30), "Hips": (-3, 0, 0), "Spine1": (-4, 0, 0), "Spine2": (-6, 0, 0), "Chest": (-8, 0, 0), "Neck": (-14, 0, 0), "Head": (-10, 0, 0), "Jaw": (22, 0, 0),
             "L_Arm": (24, 0, 0), "R_Arm": (24, 0, 0), "L_Forearm": (-14, 0, 0), "R_Forearm": (-14, 0, 0), "L_Thigh": (-22, 0, 0), "R_Thigh": (-22, 0, 0), "L_Shin": (24, 0, 0), "R_Shin": (24, 0, 0), "L_Foot": (-6, 0, 0), "R_Foot": (-6, 0, 0),
             "Tail1": (12, 0, 0), "Tail2": (12, 0, 0), "Tail3": (10, 0, 0), "Tail4": (8, 0, 0), "Lure1": (-30, 0, 0), "Lure2": (-30, 0, 0), "L_Fingers": (-14, 0, 0), "R_Fingers": (-14, 0, 0)}
    bote = {"_loc": (0, -0.28, 1.05), "Hips": (14, 0, 0), "Spine1": (16, 0, 0), "Spine2": (20, 0, 0), "Chest": (22, 0, 0), "Neck": (42, 0, 0), "Head": (8, 0, 0), "Jaw": (-38, 0, 0),
            "L_Arm": (-60, 0, -12), "R_Arm": (-60, 0, 12), "L_Forearm": (-26, 0, 0), "R_Forearm": (-26, 0, 0), "L_Hand": (-18, 0, 0), "R_Hand": (-18, 0, 0), "L_Fingers": (-34, 0, 0), "R_Fingers": (-34, 0, 0),
            "L_Thigh": (36, 0, 0), "R_Thigh": (36, 0, 0), "L_Shin": (-28, 0, 0), "R_Shin": (-28, 0, 0), "L_Foot": (-14, 0, 0), "R_Foot": (-14, 0, 0),
            "Tail1": (-14, 0, 0), "Tail2": (-12, 0, 0), "Tail3": (-8, 0, 0), "Tail4": (-4, 0, 0), "Lure1": (60, 0, 0), "Lure2": (50, 0, 0)}
    arrasta = dict(bote); arrasta.update({"_loc": (0, -0.22, 0.90), "Neck": (30, 0, 0), "Head": (-6, 0, 0), "Jaw": (-32, 0, 0)})
    keys = [(0.0, idle, "s"), (0.34, recua, "s"), (0.42, recua, "s"), (0.50, bote, "i"), (0.74, arrasta, "s"), (0.92, {}, "s"), (1.0, {}, "s")]
    P, loc = mix(keys, t)
    if 0.50 <= t <= 0.76:                           # sacode a presa de um lado para o outro
        k = (t - 0.50) / 0.26; a = S(TAU * 3.2 * k) * (1 - k * 0.5)
        add(P, "Neck", 0, 4 * a, 22 * a); add(P, "Head", 0, 8 * a, 26 * a); add(P, "Chest", 0, -3 * a, -8 * a); add(P, "Spine2", 0, 0, -5 * a); add(P, "Hips", 0, 0, 4 * a)
        for n in TAIL: add(P, n, 0, 0, -10 * a * 0.8)
    if 0.34 <= t < 0.50: add(P, "Head", 0, 0, 3 * S(TAU * 14 * t)); add(P, "Jaw", 4 * S(TAU * 11 * t))   # treme antes de atacar
    aditivo_cauda(P, t, 5, 3.0); return P, loc



def guinada(d, k=1.0, resto=0.15):
    """a torcao que escrevi em Y (rolagem do tronco) vira guinada (giro para os lados, eixo Z) nos ossos da coluna/pescoco"""
    o = {}
    for n, v in d.items():
        if n in ("Hips", "Spine1", "Spine2", "Chest", "Neck", "Head") and len(v) == 3: o[n] = (v[0], v[1] * resto, v[2] + v[1] * k)
        else: o[n] = v
    return o


GARRA_R_PREP = {"_loc": (0.05, -0.08, -0.10), "R_Clavicle": (0, 0, 14), "R_Arm": (46, 0, 62), "R_Forearm": (22, 0, 0), "R_Hand": (-24, 0, 10), "R_Fingers": (-46, 0, 0),
                "L_Arm": (8, 0, -6), "L_Forearm": (-6, 0, 0), "Hips": (0, -8, 0), "Spine1": (0, 12, 0), "Spine2": (-4, 18, 0), "Chest": (-8, 22, 0), "Neck": (-6, 12, 0), "Head": (-6, 12, 0), "Jaw": (12, 0, 0),
                "R_Thigh": (-8, 0, 0), "L_Thigh": (8, 0, 0), "R_Shin": (10, 0, 0), "L_Shin": (-6, 0, 0), "Tail1": (0, 0, -10), "Tail2": (0, 0, -16), "Tail3": (0, 0, -22), "Tail4": (0, 0, -26), "Tail5": (0, 0, -28), "Tail6": (0, 0, -30)}
GARRA_R_CORTE = {"_loc": (-0.10, -0.12, 0.40), "R_Clavicle": (0, 0, -6), "R_Arm": (-72, 0, -22), "R_Forearm": (-46, 0, 0), "R_Hand": (-8, 0, -14), "R_Fingers": (-10, 0, 0),
                 "L_Arm": (-10, 0, 10), "L_Forearm": (-12, 0, 0), "Hips": (6, 14, 0), "Spine1": (8, -16, 0), "Spine2": (10, -24, 0), "Chest": (14, -32, 0), "Neck": (18, -18, 0), "Head": (10, -16, 0), "Jaw": (-14, 0, 0),
                "R_Thigh": (16, 0, 0), "L_Thigh": (-20, 0, 0), "R_Shin": (-10, 0, 0), "L_Shin": (22, 0, 0), "Tail1": (0, 0, 12), "Tail2": (0, 0, 20), "Tail3": (0, 0, 28), "Tail4": (0, 0, 34), "Tail5": (0, 0, 38), "Tail6": (0, 0, 40)}
GARRA_R_FIM = dict(GARRA_R_CORTE); GARRA_R_FIM.update({"R_Arm": (-34, 0, -30), "R_Forearm": (-30, 0, 0), "_loc": (-0.12, -0.14, 0.50), "Chest": (14, -38, 0), "Spine2": (10, -28, 0), "Neck": (22, -22, 0)})
GARRA_R_PREP, GARRA_R_CORTE, GARRA_R_FIM = guinada(GARRA_R_PREP), guinada(GARRA_R_CORTE), guinada(GARRA_R_FIM)
GARRA_L_PREP = espelha(GARRA_R_PREP); GARRA_L_CORTE = espelha(GARRA_R_CORTE); GARRA_L_FIM = espelha(GARRA_R_FIM)
for _d in (GARRA_L_PREP, GARRA_L_CORTE, GARRA_L_FIM):    # a segunda garra e mais pesada: abre bem a boca
    _d["Jaw"] = (22, 0, 0) if _d is GARRA_L_PREP else (-4, 0, 0)


def anim_garras(t):
    """Combo: garra direita em diagonal para dentro, volta o corpo e garra esquerda de revés, mais forte."""
    keys = [(0.00, {}, "s"), (0.16, GARRA_R_PREP, "s"), (0.22, GARRA_R_PREP, "s"), (0.30, GARRA_R_CORTE, "i"), (0.38, GARRA_R_FIM, "o"), (0.50, GARRA_L_PREP, "s"), (0.56, GARRA_L_PREP, "s"),
            (0.65, GARRA_L_CORTE, "i"), (0.74, GARRA_L_FIM, "o"), (0.90, {}, "s"), (1.0, {}, "s")]
    P, loc = mix(keys, t)
    if 0.16 <= t < 0.22 or 0.50 <= t < 0.56: add(P, "Head", 0, 0, 3 * S(TAU * 12 * t)); add(P, "R_Fingers", 6 * S(TAU * 10 * t)); add(P, "L_Fingers", 6 * S(TAU * 10 * t + 1))
    aditivo_cauda(P, t, 5, 2.0); return P, loc


def anim_cauda(t):
    """Enrola o corpo, gira o tronco e chicoteia a cauda numa varredura larga (a onda percorre a cauda) terminando em batida no chao."""
    enrola = {"_loc": (0.06, -0.12, 0.05), "Hips": (0, -16, -10), "Spine1": (0, 10, 10), "Spine2": (-4, 14, 14), "Chest": (-8, 16, 18), "Neck": (-6, 6, 20), "Head": (-4, 4, 18), "Jaw": (14, 0, 0),
              "L_Arm": (-14, 0, 8), "R_Arm": (-6, 0, 10), "L_Thigh": (-10, 0, 0), "R_Thigh": (6, 0, 0), "L_Shin": (14, 0, 0), "R_Shin": (-6, 0, 0)}
    varre = {"_loc": (-0.05, -0.10, 0.0), "Hips": (4, 22, 12), "Spine1": (0, -14, -12), "Spine2": (6, -22, -18), "Chest": (10, -30, -22), "Neck": (12, -12, -26), "Head": (8, -12, -24), "Jaw": (-10, 0, 0),
             "L_Arm": (-30, 0, -14), "R_Arm": (-34, 0, 26), "L_Forearm": (-20, 0, 0), "R_Forearm": (-24, 0, 0), "L_Thigh": (14, 0, 0), "R_Thigh": (-16, 0, 0), "L_Shin": (-10, 0, 0), "R_Shin": (18, 0, 0)}
    enrola, varre = guinada(enrola, 0.6, 0.25), guinada(varre, 0.6, 0.25)
    keys = [(0.0, {}, "s"), (0.32, enrola, "s"), (0.40, enrola, "s"), (0.58, varre, "i"), (0.74, varre, "s"), (0.92, {}, "s"), (1.0, {}, "s")]
    P, loc = mix(keys, t)
    # cauda: ponta vai para o lado R na preparacao, depois varre para o lado L com atraso crescente (efeito chicote)
    for i, n in enumerate(TAIL):
        w = 1 + 0.55 * i
        pre = suave(t / 0.34) * (1 - suave((t - 0.40) / 0.06))
        sw = suave((t - 0.40 - 0.014 * i) / 0.18); rec = suave((t - 0.74) / 0.2)
        add(P, n, 0, 0, (-22 * w * 0.35 * pre) + (46 * w * 0.35 * sw) * (1 - rec) + 6 * S(TAU * 2.0 * t - 0.5 * i) * 0.3)
        add(P, n, -26 * suave((t - 0.58 - 0.01 * i) / 0.12) * (1 - suave((t - 0.78) / 0.14)) + 10 * pre)
    return P, loc


def anim_salto(t):
    agacha = {"_loc": (0, -0.38, -0.12), "Hips": (-4, 0, 0), "Spine1": (10, 0, 0), "Spine2": (14, 0, 0), "Chest": (18, 0, 0), "Neck": (-8, 0, 0), "Head": (-12, 0, 0), "Jaw": (6, 0, 0),
              "L_Arm": (12, 0, 12), "R_Arm": (12, 0, -12), "L_Forearm": (-24, 0, 0), "R_Forearm": (-24, 0, 0), "L_Thigh": (-44, 0, 0), "R_Thigh": (-44, 0, 0), "L_Shin": (54, 0, 0), "R_Shin": (54, 0, 0), "L_Foot": (-16, 0, 0), "R_Foot": (-16, 0, 0),
              "Tail1": (8, 0, 0), "Tail2": (10, 0, 0), "Tail3": (12, 0, 0), "Tail4": (12, 0, 0), "Lure1": (-24, 0, 0), "Lure2": (-30, 0, 0)}
    voo = {"_loc": (0, 0.95, 1.00), "Hips": (-3, 0, 0), "Spine1": (-2, 0, 0), "Spine2": (-4, 0, 0), "Chest": (-5, 0, 0), "Neck": (4, 0, 0), "Head": (4, 0, 0), "Jaw": (26, 0, 0),
           "L_Arm": (-92, 0, -26), "R_Arm": (-92, 0, 26), "L_Forearm": (-14, 0, 0), "R_Forearm": (-14, 0, 0), "L_Hand": (-10, 0, 0), "R_Hand": (-10, 0, 0), "L_Fingers": (-16, 0, 0), "R_Fingers": (-16, 0, 0),
           "L_Thigh": (46, 0, 0), "R_Thigh": (46, 0, 0), "L_Shin": (-30, 0, 0), "R_Shin": (-30, 0, 0), "L_Foot": (-20, 0, 0), "R_Foot": (-20, 0, 0),
           "Tail1": (-18, 0, 0), "Tail2": (-16, 0, 0), "Tail3": (-12, 0, 0), "Tail4": (-8, 0, 0), "Lure1": (70, 0, 0), "Lure2": (60, 0, 0)}
    desce = dict(voo); desce.update({"_loc": (0, 0.30, 1.35), "Hips": (12, 0, 0), "Spine1": (10, 0, 0), "Spine2": (16, 0, 0), "Chest": (24, 0, 0), "Neck": (26, 0, 0), "Jaw": (-34, 0, 0),
                                       "L_Arm": (-72, 0, -34), "R_Arm": (-72, 0, 34), "L_Fingers": (-6, 0, 0), "R_Fingers": (-6, 0, 0), "L_Thigh": (30, 0, 0), "R_Thigh": (30, 0, 0), "L_Shin": (-12, 0, 0), "R_Shin": (-12, 0, 0)})
    pousa = {"_loc": (0, -0.30, 1.45), "Hips": (-2, 0, 0), "Spine1": (14, 0, 0), "Spine2": (20, 0, 0), "Chest": (26, 0, 0), "Neck": (30, 0, 0), "Head": (4, 0, 0), "Jaw": (-24, 0, 0),
             "L_Arm": (-46, 0, -20), "R_Arm": (-46, 0, 20), "L_Forearm": (-34, 0, 0), "R_Forearm": (-34, 0, 0), "L_Fingers": (-30, 0, 0), "R_Fingers": (-30, 0, 0),
             "L_Thigh": (-40, 0, 0), "R_Thigh": (-40, 0, 0), "L_Shin": (48, 0, 0), "R_Shin": (48, 0, 0), "L_Foot": (-14, 0, 0), "R_Foot": (-14, 0, 0), "Tail1": (6, 0, 0), "Tail2": (8, 0, 0), "Tail3": (10, 0, 0), "Tail4": (10, 0, 0)}
    fim = {"_loc": (0, 0, 0.6)}
    keys = [(0.0, {}, "s"), (0.26, agacha, "s"), (0.33, agacha, "s"), (0.46, voo, "o"), (0.56, desce, "s"), (0.62, pousa, "i"), (0.70, pousa, "s"), (0.90, fim, "s"), (1.0, {}, "s")]
    P, loc = mix(keys, t)
    if 0.62 <= t <= 0.74: add(P, "Chest", -4 * S(TAU * 6 * (t - 0.62))); add(P, "Head", 0, 0, 3 * S(TAU * 10 * t))
    for n in TAIL: add(P, n, 3 * S(TAU * 2 * t))
    return P, loc


def anim_rugido(t):
    ergue = {"_loc": (0, 0.05, -0.15), "Hips": (-10, 0, 0), "Spine1": (-14, 0, 0), "Spine2": (-18, 0, 0), "Chest": (-24, 0, 0), "Neck": (-26, 0, 0), "Head": (-18, 0, 0), "Jaw": (30, 0, 0),
             "L_Arm": (-18, 0, -34), "R_Arm": (-18, 0, 34), "L_Forearm": (-34, 0, 0), "R_Forearm": (-34, 0, 0), "L_Hand": (-24, 0, 0), "R_Hand": (-24, 0, 0), "L_Fingers": (30, 0, 0), "R_Fingers": (30, 0, 0), "L_Clavicle": (0, 0, -8), "R_Clavicle": (0, 0, 8),
             "L_Thigh": (-14, 0, 0), "R_Thigh": (-14, 0, 0), "L_Shin": (16, 0, 0), "R_Shin": (16, 0, 0), "Tail1": (10, 0, 0), "Tail2": (12, 0, 0), "Tail3": (14, 0, 0), "Tail4": (16, 0, 0), "Lure1": (-40, 0, 0), "Lure2": (-40, 0, 0)}
    keys = [(0.0, {}, "s"), (0.30, ergue, "s"), (0.84, ergue, "s"), (1.0, {}, "s")]
    P, loc = mix(keys, t)
    if 0.30 <= t <= 0.84:
        k = suave((t - 0.30) / 0.1) * (1 - suave((t - 0.78) / 0.06)); f = S(TAU * 16 * t)
        add(P, "Head", 2.5 * f * k, 0, 3 * S(TAU * 9 * t) * k); add(P, "Jaw", 8 * S(TAU * 7 * t) * k); add(P, "Chest", 1.8 * f * k); add(P, "Neck", 2 * S(TAU * 13 * t) * k)
        for s in "LR": add(P, s + "_Arm", 3 * S(TAU * 11 * t) * k); add(P, s + "_Fingers", 12 * S(TAU * 6 * t) * k)
        loc = [loc[0], loc[1] + 0.012 * f * k, loc[2]]
    cauda_onda(P, t, 14, 3.0, 0.14, 0.0); return P, loc


def anim_dano(t):
    golpe = {"_loc": (0.06, -0.08, -0.40), "Hips": (-6, 0, 4), "Spine1": (-10, 0, 6), "Spine2": (-14, 0, 9), "Chest": (-20, 6, 14), "Neck": (-20, 0, 20), "Head": (-18, 0, 20), "Jaw": (24, 0, 0),
             "L_Arm": (10, 0, 12), "R_Arm": (22, 0, -6), "L_Forearm": (-18, 0, 0), "R_Forearm": (-12, 0, 0), "L_Thigh": (-10, 0, 0), "R_Thigh": (-14, 0, 0), "L_Shin": (14, 0, 0), "R_Shin": (18, 0, 0),
             "Tail1": (8, 0, -8), "Tail2": (8, 0, -12), "Tail3": (8, 0, -16), "Tail4": (6, 0, -18), "Lure1": (-50, 0, 20), "Lure2": (-40, 0, 30)}
    keys = [(0.0, {}, "s"), (0.18, golpe, "o"), (0.40, golpe, "s"), (1.0, {}, "s")]
    P, loc = mix(keys, t)
    if 0.18 <= t <= 0.6: k = 1 - (t - 0.18) / 0.42; add(P, "Head", 0, 0, 6 * S(TAU * 9 * t) * k); add(P, "Jaw", 6 * S(TAU * 8 * t) * k)
    return P, loc


def anim_morte(t):
    q = suave(t / 0.45); f = suave((t - 0.30) / 0.5); g = suave((t - 0.65) / 0.35)
    P = zerar(); loc = [0.0, -0.62 * q - 0.10 * f, -0.30 * q + 0.35 * f]
    for s, sg in (("L", 1), ("R", -1)):
        add(P, s + "_Thigh", -62 * q + 18 * f); add(P, s + "_Shin", 78 * q - 10 * f); add(P, s + "_Foot", -26 * q); add(P, s + "_Toe", 14 * q)
        add(P, s + "_Arm", 14 * q - 62 * f, 0, -sg * 0 + (-sg) * 40 * f); add(P, s + "_Forearm", -30 * q - 20 * f); add(P, s + "_Hand", -10 * f); add(P, s + "_Fingers", -20 * f, 0, 0)
    for n, a in zip(SP, (6, 18, 24, 30)): add(P, n, a * q)
    add(P, "Neck", 32 * f, 0, 14 * g); add(P, "Head", 14 * f, 0, 10 * g); add(P, "Jaw", 26 * f); add(P, "Lure1", 24 * f); add(P, "Lure2", 40 * f)
    for i, n in enumerate(TAIL): add(P, n, -18 * f, 0, (8 + 6 * i) * g * 0.5 + 3 * S(TAU * 1.5 * t - i * 0.5) * (1 - f))
    add(P, "Hips", 0, 0, 6 * g); add(P, "Chest", 0, 0, 8 * g); P["Root"][2] += D(-6) * g
    return P, loc


mesh_obj = bpy.data.objects["Angler"]
FRENTE = [i for i, v in enumerate(mesh_obj.data.vertices) if v.co.y < 0.45]      # corpo sem a cauda: usado para nao atravessar o chao


def zmin_corpo():
    bpy.context.view_layer.update(); me = mesh_obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    z = min(me.vertices[i].co.z for i in FRENTE); mesh_obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear(); return z


def gravar(nome, frames, fn, loop=True, chao=True):
    act = bpy.data.actions.new(nome); arm.animation_data_create(); arm.animation_data.action = act
    for b in pb: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
    ajustes = []
    for f in range(0, frames + 1):
        t = (f % frames) / frames if loop else f / frames
        P, loc = fn(t)
        for b in pb:
            v = P.get(b.name, (0, 0, 0)); b.rotation_euler = (v[0], v[1], v[2])
        root = pb["Root"]; root.location = (loc[0], loc[1], loc[2])
        if chao:
            z = zmin_corpo()
            if z < 0: root.location = (loc[0], loc[1] - z, loc[2]); ajustes.append(-z)
        for b in pb:
            b.keyframe_insert("rotation_euler", frame=f + 1)
            if b.name == "Root": b.keyframe_insert("location", frame=f + 1)
    act.use_fake_user = True; act.frame_range = (1, frames + 1)
    if ajustes: print("AJUSTE", nome, "max %.2f" % max(ajustes), "em %d/%d quadros" % (len(ajustes), frames + 1))


gravar("Idle", 120, anim_idle); gravar("Walk", 48, anim_walk); gravar("Run", 24, anim_run)
gravar("Attack_Mordida", 54, anim_mordida, False); gravar("Attack_Garras", 66, anim_garras, False); gravar("Attack_Cauda", 60, anim_cauda, False); gravar("Attack_Salto", 66, anim_salto, False)
gravar("Rugido", 84, anim_rugido, False); gravar("Dano", 30, anim_dano, False); gravar("Morte", 90, anim_morte, False)
arm.animation_data.action = bpy.data.actions["Idle"]; bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_anim.blend"); print("ACOES", [a.name for a in bpy.data.actions])
