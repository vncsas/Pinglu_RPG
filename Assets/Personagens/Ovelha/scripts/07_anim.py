# Animacoes procedurais do cavalo (todas "no lugar"; o movimento e feito pelo jogo): Idle, Walk, Trot, Gallop, Comer_Entrar, Comer, Comer_Sair
import bpy, sys, math
base = "C:/Users/Thiago/Documentos/rig-ovelha/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_base.blend")
arm = bpy.data.objects["CavaloArmature"]; mesh = bpy.data.objects["Ovelha"]
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
pb = arm.pose.bones
for b in pb: b.rotation_mode = 'XYZ'
D = math.radians
TAU = 2 * math.pi
LEGS = {"LF": "F", "RF": "F", "LH": "H", "RH": "H"}

def fase(p, s):
    p %= 1.0
    if p < s: return -1.0 + 2.0 * p / s, 0.0
    q = (p - s) / (1.0 - s); return math.cos(math.pi * q), math.sin(math.pi * q)

def zerar(): return {b.name: [0.0, 0.0, 0.0] for b in pb}

def pernas(P, t, off, s, A, K, extra=None):
    for leg, tipo in LEGS.items():
        phi, L = fase(t + off[leg], s)
        if tipo == "F":
            a, k = A["F"], K["F"]
            P[leg + "_Shoulder"][0] += D(0.10 * a * phi); P[leg + "_Arm"][0] += D(0.22 * a * phi)
            P[leg + "_Cannon"][0] += D(k * L - 0.2 * a * phi); P[leg + "_Pastern"][0] += D(0.35 * k * L)
            P[leg + "_Forearm"][0] += D(1.1 * a * phi)
        else:
            a, ks, kh = A["H"], K["Hs"], K["Hh"]
            P[leg + "_Thigh"][0] += D(a * phi); P[leg + "_Shin"][0] += D(-0.55 * a * phi + ks * L)
            P[leg + "_Cannon"][0] += D(0.25 * a * phi - kh * L); P[leg + "_Pastern"][0] += D(0.35 * kh * L)

def cauda(P, t, amp, lev=0.0, vel=1.0):
    for i in range(1, 6):
        P["Tail%d" % i][2] += D(amp) * math.sin(TAU * (t * vel - 0.12 * i)); P["Tail%d" % i][0] += D(lev / 5.0)

def anim_idle(t):
    P = zerar(); r = math.sin(TAU * t * 1)
    P["Chest"][0] += D(0.8) * r; P["Spine2"][0] += D(0.5) * r
    for n in ("Neck1", "Neck2"): P[n][0] += D(1.2) * math.sin(TAU * (t * 1 + 0.15))
    P["Head"][0] += D(2.0) * math.sin(TAU * (t * 2)); P["Neck2"][1] += D(4.0) * math.sin(TAU * t)      # olhar leve para os lados
    P["Ear_L"][0] += D(10) * max(0, math.sin(TAU * (t * 3 + 0.2))) ** 6; P["Ear_R"][0] += D(8) * max(0, math.sin(TAU * (t * 2 + 0.6))) ** 8
    cauda(P, t, 7, 0.0, 1.0); P["Tail1"][0] += D(3) * math.sin(TAU * t)
    P["RH_Thigh"][0] += D(2) * math.sin(TAU * t); P["LF_Arm"][0] += D(1.0) * math.sin(TAU * t)
    return P

def anim_walk(t):
    P = zerar(); off = {"RH": 0.0, "RF": 0.25, "LH": 0.50, "LF": 0.75}
    pernas(P, t, off, 0.62, {"F": 22, "H": 16}, {"F": 34, "Hs": 16, "Hh": 18})
    P["Neck1"][0] += D(3) * math.sin(TAU * (2 * t - 0.1)); P["Neck3"][0] += D(2) * math.sin(TAU * (2 * t - 0.3)); P["Head"][0] += D(2) * math.sin(TAU * (2 * t - 0.4))
    P["Spine1"][2] += D(2.0) * math.sin(TAU * t); P["Spine2"][2] += D(-2.0) * math.sin(TAU * t); P["Neck2"][2] += D(2) * math.sin(TAU * (t - 0.2)); P["Pelvis"][2] += D(2) * math.sin(TAU * (t + 0.5))
    P["Pelvis"][0] += D(1.5) * math.sin(TAU * 2 * t); cauda(P, t, 6, 0.0, 1.0)
    return P

def anim_trot(t):
    P = zerar(); off = {"LF": 0.0, "RH": 0.0, "RF": 0.5, "LH": 0.5}
    pernas(P, t, off, 0.46, {"F": 26, "H": 16}, {"F": 40, "Hs": 18, "Hh": 22})
    P["Neck1"][0] += D(2.5) * math.sin(TAU * (2 * t + 0.1)); P["Head"][0] += D(2.0) * math.sin(TAU * (2 * t))
    P["Pelvis"][0] += D(2.5) * math.sin(TAU * 2 * t); P["Chest"][0] += D(-2.0) * math.sin(TAU * 2 * t)
    P["Spine1"][2] += D(1.5) * math.sin(TAU * t); cauda(P, t, 5, 0.18, 2.0)
    return P

def anim_gallop(t):
    P = zerar(); off = {"LH": 0.0, "RH": 0.10, "LF": 0.24, "RF": 0.34}
    pernas(P, t, off, 0.34, {"F": 20, "H": 15}, {"F": 30, "Hs": 14, "Hh": 18})
    P["Pelvis"][0] += D(9) * math.sin(TAU * (t + 0.05)); P["Spine1"][0] += D(-4) * math.sin(TAU * (t + 0.05)); P["Spine2"][0] += D(-5) * math.sin(TAU * (t + 0.05)); P["Chest"][0] += D(-6) * math.sin(TAU * (t + 0.05))
    P["Neck1"][0] += D(9) * math.sin(TAU * (t - 0.2)); P["Neck2"][0] += D(6) * math.sin(TAU * (t - 0.28)); P["Head"][0] += D(5) * math.sin(TAU * (t - 0.34))
    for n in ("Neck1", "Neck2"): P[n][0] += D(8)    # pescoco mais esticado para frente/baixo
    cauda(P, t, 6, 0.55, 2.0)
    P["Ear_L"][0] += D(-12); P["Ear_R"][0] += D(-12)
    return P

def anim_comer(t, cabeca):
    """cabeca: 0 = em pe, 1 = cabeca no chao. t = fase da mastigacao"""
    P = zerar(); c = cabeca
    P["Chest"][0] += D(10) * c; P["Neck1"][0] += D(46) * c; P["Neck2"][0] += D(14) * c; P["Neck3"][0] += D(4) * c; P["Head"][0] += D(-8) * c
    m = math.sin(TAU * t * 3)
    P["Jaw"][0] += D(14) * max(0, m) * c; P["Head"][0] += D(3) * math.sin(TAU * t * 1.5) * c; P["Neck3"][0] += D(2.5) * math.sin(TAU * t * 1.5 + 1) * c
    P["Ear_L"][0] += D(8) * max(0, math.sin(TAU * (t * 2 + 0.3))) ** 6; cauda(P, t, 8, 0.0, 1.0)
    return P

LOC = {}
def bob(nome, t):
    # deslocamento (m) do Root: sobe/desce e balanca de lado, em fase com a andadura
    z = {"Walk": 0.012 * math.sin(TAU * 2 * t + 1.0), "Trot": 0.035 * abs(math.sin(TAU * 2 * t + 0.4)), "Gallop": 0.06 * math.sin(TAU * (t + 0.15))}.get(nome, 0.0)
    x = {"Walk": 0.012 * math.sin(TAU * t), "Trot": 0.008 * math.sin(TAU * t), "Gallop": 0.0}.get(nome, 0.0)
    return x, z
def gravar(nome, frames, fn, loop=True):
    act = bpy.data.actions.new(nome); arm.animation_data_create(); arm.animation_data.action = act
    for b in pb: b.rotation_euler = (0, 0, 0)
    n = frames
    for f in range(0, n + 1):
        t = (f % n) / n if loop else f / n
        P = fn(t)
        for b in pb:
            v = P.get(b.name, (0, 0, 0)); b.rotation_euler = (v[0], v[1], v[2]); b.keyframe_insert("rotation_euler", frame=f + 1)
            if b.name == "Root":
                bx, bz = bob(nome, t); b.location = (bx, 0.0, bz); b.keyframe_insert("location", frame=f + 1)
    act.use_fake_user = True; act.frame_range = (1, n + 1); return act

gravar("Idle", 120, anim_idle)
gravar("Walk", 36, anim_walk)
gravar("Trot", 24, anim_trot)
gravar("Gallop", 18, anim_gallop)
gravar("Comer", 90, lambda t: anim_comer(t, 1.0))
gravar("Comer_Entrar", 30, lambda t: anim_comer(t * 0.001, 0.5 - 0.5 * math.cos(math.pi * t)), loop=False)
gravar("Comer_Sair", 30, lambda t: anim_comer(t * 0.001, 0.5 + 0.5 * math.cos(math.pi * t)), loop=False)
arm.animation_data.action = bpy.data.actions["Idle"]
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_anim.blend")
print("ACOES", [a.name for a in bpy.data.actions])
