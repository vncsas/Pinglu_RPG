# Animacoes procedurais do monstro do pantano: Idle, Walk, Run, Attack, Death (todas "no lugar").
# Eixos: rotacao X positiva = osso que aponta para baixo vai para TRAS; osso que aponta para cima vai para FRENTE (frente = -Y).
import bpy, math
base = "C:/Users/Thiago/Documentos/rig-pantano/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_empe.blend")
arm = bpy.data.objects["MonstroArmature"]; bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
pb = arm.pose.bones
for b in pb: b.rotation_mode = 'XYZ'
D = math.radians; TAU = 2 * math.pi; S = math.sin; C = math.cos


def zerar(): return {b.name: [0.0, 0.0, 0.0] for b in pb}
def suave(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)


def marcha(t, A, K, B, lean, bob, tors, cot):
    """ciclo de caminhada/corrida. A: balanco da coxa, K: dobra do joelho, B: balanco do braco, tors: torcao, cot: dobra do cotovelo"""
    P = zerar()
    for s, off in (("L", 0.0), ("R", 0.5)):
        q = TAU * (t + off); th = -A * C(q); fl = K * max(0.0, -S(q)) ** 0.8 + 0.15 * K * max(0.0, S(q))
        P[s + "_Thigh"][0] += D(th); P[s + "_Shin"][0] += D(fl)
        P[s + "_Foot"][0] += D(-(th + fl) * 0.75 + 8 * max(0.0, S(q))); P[s + "_Toe"][0] += D(10 * max(0.0, -S(q)))
    for s, off in (("L", 0.5), ("R", 0.0)):  # braco acompanha a perna oposta
        q = TAU * (t + off)
        P[s + "_Arm"][0] += D(-B * C(q)); P[s + "_Forearm"][0] += D(-(cot + 0.5 * B * max(0.0, -S(q + 0.6))))
        P[s + "_Hand"][0] += D(6 * S(q)); P[s + "_Fingers"][0] += D(-10)
    w = S(TAU * t)
    P["Hips"][1] += D(tors) * w; P["Spine1"][1] += D(-0.5 * tors) * w; P["Spine2"][1] += D(-0.7 * tors) * w; P["Chest"][1] += D(-0.5 * tors) * w
    P["Hips"][2] += D(2.5 * (tors / 5.0)) * w; P["Chest"][2] += D(-3 * (tors / 5.0)) * w
    P["Spine1"][0] += D(lean); P["Spine2"][0] += D(lean * 0.6); P["Chest"][0] += D(lean * 0.5)
    P["Neck"][0] += D(-lean * 0.6) + D(2.5) * S(TAU * 2 * t + 0.5); P["Head"][0] += D(-lean * 0.4) + D(2) * S(TAU * 2 * t)
    P["Neck"][1] += D(-0.4 * tors) * w; P["Head"][1] += D(2.0) * S(TAU * t + 0.8)
    loc = (0.025 * w * (bob / 0.03), bob * (abs(C(TAU * t)) - 0.6))
    return P, loc


def anim_idle(t):
    P = zerar(); r = S(TAU * t)
    P["Spine1"][0] += D(1.2) * r; P["Spine2"][0] += D(1.5) * r; P["Chest"][0] += D(2.0) * r; P["Neck"][0] += D(-1.5) * r; P["Head"][0] += D(2) * S(TAU * t + 0.8)
    P["Hips"][1] += D(2.5) * S(TAU * t + 0.3); P["Chest"][1] += D(-3) * S(TAU * t + 0.3); P["Head"][1] += D(8) * S(TAU * t + 1.5); P["Neck"][1] += D(4) * S(TAU * t + 1.5)
    P["Hips"][2] += D(1.5) * S(TAU * t)
    for s, ph in (("L", 0.0), ("R", 0.6)):
        a = S(TAU * t + ph); P[s + "_Arm"][0] += D(4 * a); P[s + "_Arm"][2] += D(3 if s == "L" else -3) * (1 + 0.5 * a); P[s + "_Forearm"][0] += D(-8 - 4 * a)
        P[s + "_Hand"][0] += D(5 * S(TAU * t + ph + 1)); P[s + "_Fingers"][0] += D(-12 - 6 * S(TAU * 2 * t + ph)); P[s + "_Clavicle"][2] += D(2 if s == "L" else -2) * r
        P[s + "_Thigh"][0] += D(-3); P[s + "_Shin"][0] += D(6); P[s + "_Foot"][0] += D(-3)
    P["L_Thigh"][0] += D(1.5) * S(TAU * t); P["R_Shin"][0] += D(1.5) * S(TAU * t + 1.0)
    return P, (0.012 * S(TAU * t + 0.3), -0.012 * (0.5 - 0.5 * C(TAU * 2 * t)))


def anim_walk(t): return marcha(t, 24, 42, 22, 6, 0.04, 6, 22)


def anim_run(t):
    P, loc = marcha(t, 40, 80, 40, 20, 0.09, 8, 70)
    for s in "LR": P[s + "_Fingers"][0] = D(-25)
    return P, loc


def mix(frames, t):
    for (t0, p0), (t1, p1) in zip(frames, frames[1:]):
        if t0 <= t <= t1:
            k = suave((t - t0) / (t1 - t0) if t1 > t0 else 1.0); P = zerar()
            for n in P:
                a = p0.get(n, (0, 0, 0)); b = p1.get(n, (0, 0, 0)); P[n] = [a[i] + (b[i] - a[i]) * k for i in range(3)]
            return P
    return zerar()


def rad(d): return {k: [D(x) for x in v] for k, v in d.items()}


LEVANTA = rad({"L_Arm": (-150, 0, 18), "R_Arm": (-150, 0, -18), "L_Forearm": (-35, 0, 0), "R_Forearm": (-35, 0, 0), "Chest": (-14, 0, 0), "Spine2": (-8, 0, 0), "Spine1": (-4, 0, 0), "Neck": (-10, 0, 0), "Head": (-14, 0, 0),
               "L_Hand": (-20, 0, 0), "R_Hand": (-20, 0, 0), "L_Fingers": (-30, 0, 0), "R_Fingers": (-30, 0, 0), "L_Thigh": (8, 0, 0), "R_Thigh": (8, 0, 0), "L_Shin": (4, 0, 0), "R_Shin": (4, 0, 0)})
BATE = rad({"L_Arm": (-25, 0, 6), "R_Arm": (-25, 0, -6), "L_Forearm": (-25, 0, 0), "R_Forearm": (-25, 0, 0), "Chest": (22, 0, 0), "Spine2": (14, 0, 0), "Spine1": (10, 0, 0), "Neck": (6, 0, 0), "Head": (8, 0, 0), "Hips": (8, 0, 0),
            "L_Thigh": (-26, 0, 0), "R_Thigh": (-18, 0, 0), "L_Shin": (30, 0, 0), "R_Shin": (24, 0, 0), "L_Foot": (-6, 0, 0), "R_Foot": (-6, 0, 0), "L_Fingers": (-45, 0, 0), "R_Fingers": (-45, 0, 0)})


def anim_attack(t):
    P = mix([(0.0, {}), (0.38, LEVANTA), (0.48, LEVANTA), (0.58, BATE), (0.72, BATE), (1.0, {})], t)
    z = -0.07 * suave(t / 0.38) * (1 - suave((t - 0.38) / 0.2)) - 0.05 * suave((t - 0.38) / 0.2) * (1 - suave((t - 0.72) / 0.28))
    return P, (0.0, z)


def anim_death(t):
    q = suave(t / 0.62); f = suave((t - 0.45) / 0.55)
    P = zerar()
    P["L_Thigh"][0] = D(-45 * q + 45 * f); P["R_Thigh"][0] = D(-60 * q + 60 * f); P["L_Shin"][0] = D(95 * q - 80 * f); P["R_Shin"][0] = D(105 * q - 85 * f)
    P["L_Foot"][0] = D(-30 * q + 30 * f); P["R_Foot"][0] = D(-30 * q + 30 * f)
    P["Spine1"][0] = D(-10 * f + 8 * q); P["Spine2"][0] = D(-8 * f + 6 * q); P["Chest"][0] = D(-10 * f); P["Neck"][0] = D(-14 * f + 10 * q); P["Head"][0] = D(-12 * f + 12 * q * (1 - f))
    for s, sg in (("L", 1), ("R", -1)):
        P[s + "_Arm"][0] = D(-30 * q + 55 * f); P[s + "_Arm"][2] = D(-sg * 70 * f); P[s + "_Forearm"][0] = D(-30 * q * (1 - f) - 10 * f); P[s + "_Fingers"][0] = D(-20 * q)
    P["Root"][0] = D(-84 * f * f)
    return P, (0.0, -0.45 * q * (1 - f) + 0.05 * f)


def gravar(nome, frames, fn, loop=True):
    act = bpy.data.actions.new(nome); arm.animation_data_create(); arm.animation_data.action = act
    for b in pb: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
    n = frames
    for f in range(0, n + 1):
        t = (f % n) / n if loop else f / n
        P, loc = fn(t)
        for b in pb:
            v = P.get(b.name, (0, 0, 0)); b.rotation_euler = (v[0], v[1], v[2]); b.keyframe_insert("rotation_euler", frame=f + 1)
            if b.name == "Root": b.location = (loc[0], loc[1], 0.0); b.keyframe_insert("location", frame=f + 1)
    act.use_fake_user = True; act.frame_range = (1, n + 1)


gravar("Idle", 120, anim_idle); gravar("Walk", 40, anim_walk); gravar("Run", 24, anim_run)
gravar("Attack", 44, anim_attack, loop=False); gravar("Death", 70, anim_death, loop=False)
arm.animation_data.action = bpy.data.actions["Idle"]; bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_anim.blend"); print("ACOES", [a.name for a in bpy.data.actions])
