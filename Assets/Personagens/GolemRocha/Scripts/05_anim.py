# Animacoes procedurais do Golem (tanque): no lugar; o jogo move o personagem.
# Eixos do MUNDO: x = lado (+x = esquerda do personagem), y = frente/tras (FRENTE = -y), z = cima.
# Braco/perna que apontam para baixo: -x_graus = para FRENTE. Coluna: +x_graus = inclina para FRENTE. +z_graus = gira para a esquerda (+x).
# Ordem de aplicacao em cada osso: y (abre/rola), depois x (pitch), depois z (guinada).
import bpy, sys, math
sys.path.insert(0, "C:/Users/Thiago/Documentos/rig-golem/scripts")
from mathutils import Quaternion, Vector as V
import gutil as G
base = "C:/Users/Thiago/Documentos/rig-golem/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/golem_base.blend")
arm = bpy.data.objects["Golem_Rig"]; bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
sc = bpy.context.scene; sc.render.fps = 30
pb = arm.pose.bones
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
D = math.radians; TAU = 2 * math.pi; S = math.sin; C = math.cos
N = {"hip": "CC_Base_Hip", "pel": "CC_Base_Pelvis", "wai": "CC_Base_Waist", "s1": "CC_Base_Spine01", "s2": "CC_Base_Spine02", "head": "CC_Base_Head",
     "n1": "CC_Base_NeckTwist01", "n2": "CC_Base_NeckTwist02"}
for s, L in (("L", "L"), ("R", "R")):
    N["cl" + s] = "CC_Base_%s_Clavicle" % L; N["ua" + s] = "CC_Base_%s_Upperarm" % L; N["fa" + s] = "CC_Base_%s_Forearm" % L; N["ha" + s] = "CC_Base_%s_Hand" % L
    N["th" + s] = "CC_Base_%s_Thigh" % L; N["ca" + s] = "CC_Base_%s_Calf" % L; N["fo" + s] = "CC_Base_%s_Foot" % L; N["to" + s] = "CC_Base_%s_ToeBase" % L
SIDE = {"L": 1, "R": -1}
for k, v in N.items(): assert v in pb, v


def suave(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def sai(x): x = max(0.0, min(1.0, x)); return 1 - (1 - x) ** 3
def entra(x): x = max(0.0, min(1.0, x)); return x ** 3
EASE = {"s": suave, "o": sai, "i": entra}


def P(**kw):
    """P(s2=dict(x=10, z=5), hip=(dx,dy,dz)) -> pose {chave: {eixo: graus}} ; 'hip' = deslocamento do quadril em metros (mundo)"""
    return dict(kw)


def somar(a, b, k=1.0):
    o = {n: dict(v) if isinstance(v, dict) else v for n, v in a.items()}
    for n, v in b.items():
        if n == "hip":
            h = o.get("hip", (0, 0, 0)); o["hip"] = (h[0] + v[0] * k, h[1] + v[1] * k, h[2] + v[2] * k)
        else:
            d = o.setdefault(n, {})
            for e, g in v.items(): d[e] = d.get(e, 0.0) + g * k
    return o


def misturar(p0, p1, k):
    chaves = set(p0) | set(p1); o = {}
    for n in chaves:
        if n == "hip":
            a = p0.get("hip", (0, 0, 0)); b = p1.get("hip", (0, 0, 0)); o["hip"] = tuple(a[i] + (b[i] - a[i]) * k for i in range(3)); continue
        a = p0.get(n, {}); b = p1.get(n, {}); o[n] = {e: a.get(e, 0.0) + (b.get(e, 0.0) - a.get(e, 0.0)) * k for e in set(a) | set(b)}
    return o


def keys(lista, t):
    """lista: [(tempo, pose, ease_da_chegada)] ; t em 0..1"""
    for i in range(len(lista) - 1):
        t0, p0, _ = lista[i]; t1, p1, e = lista[i + 1]
        if t0 <= t <= t1: return misturar(p0, p1, EASE[e]((t - t0) / (t1 - t0) if t1 > t0 else 1.0))
    return dict(lista[-1][1])


def aplicar(pose):
    for p in pb: p.rotation_mode = 'QUATERNION'; p.rotation_quaternion = Quaternion((1, 0, 0, 0)); p.location = (0, 0, 0)
    for chave, v in pose.items():
        if chave == "hip": continue
        osso = pb[N[chave]]; q = Quaternion((1, 0, 0, 0))
        for e in ("y", "x", "z"):
            g = v.get(e, 0.0)
            if abs(g) > 1e-6: q = G.q_mundo(osso, e, g) @ q
        osso.rotation_quaternion = q
    h = pose.get("hip", (0, 0, 0)); hb = pb[N["hip"]]; Rr = hb.bone.matrix_local.to_quaternion(); hb.location = Rr.inverted() @ V(h)


def zmin_corpo():
    bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get(); z = 1e9
    for m in meshes:
        me = m.evaluated_get(dg).to_mesh(); z = min(z, min(v.co.z for v in me.vertices)); m.evaluated_get(dg).to_mesh_clear()
    return z


# ------------------------------------------------------------------ loops
def anim_idle(t):
    r = S(TAU * t); p = P()
    p = somar(p, P(s1=dict(x=2.0 * r), s2=dict(x=2.5 * r, z=3 * S(TAU * t + 0.6)), head=dict(x=-1.5 * r, z=6 * S(TAU * t + 1.3)), hip=(0.012 * S(TAU * t), 0, -0.012 * (0.5 - 0.5 * C(TAU * 2 * t)))))
    for s, ph in (("L", 0.0), ("R", 0.7)):
        a = S(TAU * t + ph); sg = SIDE[s]
        p = somar(p, {"ua" + s: dict(x=3 * a, y=-sg * 2 * (1 + a)), "fa" + s: dict(x=-3 * a), "cl" + s: dict(z=sg * 2 * r)})
    return p


def marcha(t, A, K, B, bob, tors, incl):
    p = P(); w = S(TAU * t)
    for s, off in (("L", 0.0), ("R", 0.5)):
        q = TAU * (t + off); th = -A * C(q); lev = max(0.0, -S(q))
        p = somar(p, {"th" + s: dict(x=th - 4 * lev), "ca" + s: dict(x=K * lev ** 0.8 + 0.1 * K * max(0.0, S(q))), "fo" + s: dict(x=-(th + K * lev) * 0.7), "to" + s: dict(x=10 * lev)})
        o = "R" if s == "L" else "L"; q2 = TAU * (t + off + 0.5)       # braco oposto
        p = somar(p, {"ua" + o: dict(x=-B * C(q)), "fa" + o: dict(x=-8 - 0.4 * B * max(0.0, -S(q + 0.6)))})
    p = somar(p, P(pel=dict(z=tors * 0.5 * w), wai=dict(z=-tors * 0.4 * w), s1=dict(z=-tors * 0.5 * w, x=incl, y=2.5 * w), s2=dict(z=-tors * 0.4 * w, x=incl * 0.7), head=dict(z=tors * 0.6 * w, x=-incl * 0.8),
                   hip=(0.03 * w * (bob / 0.04), 0, bob * (abs(C(TAU * t)) - 0.6))))
    return p


def anim_walk(t): return marcha(t, 17, 34, 9, 0.045, 5, 3)
def anim_run(t): return marcha(t, 30, 55, 20, 0.09, 8, 12)


def anim_provocar(t):
    """bate no peito com as duas maos, alternando, e levanta a cabeca para rugir"""
    ciclo = 4; fase = (t * ciclo) % 1.0; lado = int(t * ciclo) % 2; p = P()
    ergue = suave(min(t / 0.12, 1.0)) * (1 - suave((t - 0.9) / 0.1))
    p = somar(p, P(s1=dict(x=-6 * ergue), s2=dict(x=-8 * ergue), head=dict(x=-18 * ergue), hip=(0, 0.04 * ergue, 0.04 * ergue)))
    for s in "LR":
        mine = (lado == 0) == (s == "L"); k = (1 - abs(2 * fase - 1)) if mine else 0.15           # sobe e bate
        sg = SIDE[s]
        p = somar(p, {"ua" + s: dict(x=-55 * k * ergue - 20 * ergue, y=-sg * 25 * ergue, z=-sg * 28 * k * ergue), "fa" + s: dict(x=-70 * k * ergue)})
    bate = max(0.0, S(TAU * fase)) ** 6 * ergue
    p = somar(p, P(s1=dict(x=3 * bate), hip=(0, 0, -0.025 * bate)))
    return p


# ------------------------------------------------------------------ ataques e reacoes (poses-chave)
def pose_arma_prep():
    return P(hip=(0.04, 0.05, -0.14), s1=dict(z=-18, x=-4), s2=dict(z=-24, x=-6), wai=dict(z=-8), head=dict(z=-10, x=-4),
             uaR=dict(x=52, y=60), faR=dict(x=55), haR=dict(x=-10), uaL=dict(x=-14, y=-6), faL=dict(x=-18),
             thL=dict(x=-12), thR=dict(x=10), caL=dict(x=18), caR=dict(x=-6), clR=dict(z=-8))
def pose_arma_golpe():
    return P(hip=(-0.04, -0.22, -0.16), s1=dict(z=26, x=14), s2=dict(z=34, x=16), wai=dict(z=12), head=dict(z=14, x=8),
             uaR=dict(x=-78, y=10, z=-22), faR=dict(x=-26), haR=dict(x=-6), uaL=dict(x=10, y=-4), faL=dict(x=-10),
             thL=dict(x=-30), thR=dict(x=18), caL=dict(x=30), caR=dict(x=14), foL=dict(x=10), clR=dict(z=10))
def anim_pancada(t):
    ks = [(0.0, P(), "s"), (0.32, pose_arma_prep(), "s"), (0.40, pose_arma_prep(), "s"), (0.50, pose_arma_golpe(), "i"), (0.70, pose_arma_golpe(), "s"), (1.0, P(), "s")]
    p = keys(ks, t)
    if 0.50 <= t <= 0.75: a = S(TAU * 4 * (t - 0.50)) * (1 - (t - 0.50) / 0.25); p = somar(p, P(s2=dict(x=2.5 * a), hip=(0, 0, 0.02 * a)))
    return p


def pose_marteladas_prep():
    return P(hip=(0, 0.06, 0.02), s1=dict(x=-14), s2=dict(x=-16), wai=dict(x=-4), head=dict(x=-12),
             uaL=dict(x=-150, y=-6, z=-12), uaR=dict(x=-150, y=6, z=12), faL=dict(x=-24), faR=dict(x=-24), clL=dict(x=-8), clR=dict(x=-8),
             thL=dict(x=8), thR=dict(x=8), caL=dict(x=-4), caR=dict(x=-4))
def pose_marteladas_agacha():
    return P(hip=(0, 0.05, -0.12), s1=dict(x=-10), s2=dict(x=-12), head=dict(x=-8), uaL=dict(x=-160, z=-14), uaR=dict(x=-160, z=14), faL=dict(x=-30), faR=dict(x=-30),
             thL=dict(x=-14), thR=dict(x=-14), caL=dict(x=26), caR=dict(x=26))
def pose_marteladas_golpe():
    return P(hip=(0, -0.24, -0.22), s1=dict(x=26), s2=dict(x=30), wai=dict(x=8), head=dict(x=14),
             uaL=dict(x=-36, z=-6), uaR=dict(x=-36, z=6), faL=dict(x=-14), faR=dict(x=-14), clL=dict(x=-6), clR=dict(x=-6),
             thL=dict(x=-26), thR=dict(x=-26), caL=dict(x=42), caR=dict(x=42), foL=dict(x=-8), foR=dict(x=-8))
def anim_martelada(t):
    ks = [(0.0, P(), "s"), (0.20, pose_marteladas_agacha(), "s"), (0.36, pose_marteladas_prep(), "o"), (0.48, pose_marteladas_prep(), "s"), (0.56, pose_marteladas_golpe(), "i"), (0.74, pose_marteladas_golpe(), "s"), (1.0, P(), "s")]
    p = keys(ks, t)
    if 0.56 <= t <= 0.80: a = S(TAU * 5 * (t - 0.56)) * (1 - (t - 0.56) / 0.24) ** 1.5; p = somar(p, P(s2=dict(x=3.5 * a), head=dict(x=-3 * a), hip=(0, 0, 0.04 * a)))
    return p


def pose_defesa():
    return P(hip=(0, 0.03, -0.14), s1=dict(x=10), s2=dict(x=12), head=dict(x=10), uaL=dict(x=-82, z=-30), uaR=dict(x=-82, z=30), faL=dict(x=-78, z=-12), faR=dict(x=-78, z=12), clL=dict(x=-6, z=-4), clR=dict(x=-6, z=4),
             thL=dict(x=-12, y=-3), thR=dict(x=-12, y=3), caL=dict(x=28), caR=dict(x=28), foL=dict(x=-6), foR=dict(x=-6))
def anim_def_entrar(t): return keys([(0.0, P(), "s"), (0.7, somar(pose_defesa(), P(s2=dict(x=-3))), "o"), (1.0, pose_defesa(), "s")], t)
def anim_def_segurar(t): p = pose_defesa(); a = S(TAU * t); return somar(p, P(s2=dict(x=1.5 * a), hip=(0, 0, -0.01 * (0.5 + 0.5 * a)), uaL=dict(z=-1.5 * a), uaR=dict(z=1.5 * a)))
def anim_def_sair(t): return keys([(0.0, pose_defesa(), "s"), (1.0, P(), "s")], t)


def anim_hit(t):
    golpe = P(hip=(0.03, 0.14, -0.06), s1=dict(x=-14, z=8, y=5), s2=dict(x=-18, z=10), head=dict(x=-14, z=-14), uaL=dict(x=14, y=-6), uaR=dict(x=18, y=6), faL=dict(x=-10), faR=dict(x=-10), thL=dict(x=-6), thR=dict(x=-8), caL=dict(x=10), caR=dict(x=12))
    p = keys([(0.0, P(), "s"), (0.18, golpe, "o"), (0.40, golpe, "s"), (1.0, P(), "s")], t)
    if 0.18 <= t <= 0.6: a = S(TAU * 4 * t) * (1 - (t - 0.18) / 0.42); p = somar(p, P(head=dict(z=5 * a), s2=dict(z=3 * a)))
    return p


def anim_morrer(t):
    k1 = P(hip=(0, 0.02, -0.10), s1=dict(x=6), s2=dict(x=8), head=dict(x=8), uaL=dict(x=8), uaR=dict(x=8), thL=dict(x=-20), thR=dict(x=-20), caL=dict(x=36), caR=dict(x=36), foL=dict(x=-12), foR=dict(x=-12))
    k2 = P(hip=(0, -0.15, -0.55), s1=dict(x=18), s2=dict(x=22), wai=dict(x=8), head=dict(x=18), uaL=dict(x=-10, y=-14), uaR=dict(x=-10, y=14), faL=dict(x=-18), faR=dict(x=-18),
           thL=dict(x=-70), thR=dict(x=-70), caL=dict(x=110), caR=dict(x=110), foL=dict(x=-30), foR=dict(x=-30))
    k3 = P(hip=(0, -0.55, -0.78), s1=dict(x=44), s2=dict(x=48), wai=dict(x=14), head=dict(x=22), uaL=dict(x=-22, y=-34), uaR=dict(x=-22, y=34), faL=dict(x=-12), faR=dict(x=-12),
           thL=dict(x=-82), thR=dict(x=-82), caL=dict(x=120), caR=dict(x=120), foL=dict(x=-36), foR=dict(x=-36))
    return keys([(0.0, P(), "s"), (0.25, k1, "s"), (0.55, k2, "i"), (1.0, k3, "o")], t)


def gravar(nome, frames, fn, loop=True, chao=True):
    act = bpy.data.actions.new(nome); arm.animation_data_create(); arm.animation_data.action = act
    ajustes = []
    for f in range(0, frames + 1):
        t = (f % frames) / frames if loop else f / frames
        pose = fn(t); aplicar(pose)
        if chao:
            z = zmin_corpo()
            if z < 0:
                h = pose.get("hip", (0, 0, 0)); pose = dict(pose); pose["hip"] = (h[0], h[1], h[2] - z); aplicar(pose); ajustes.append(-z)
        for p in pb:
            p.keyframe_insert("rotation_quaternion", frame=f + 1)
            if p.name == N["hip"]: p.keyframe_insert("location", frame=f + 1)
    act.use_fake_user = True; act.frame_range = (1, frames + 1)
    if ajustes: print("AJUSTE", nome, "max %.2f" % max(ajustes), "em %d/%d" % (len(ajustes), frames + 1))


gravar("Idle", 120, anim_idle); gravar("Walk", 48, anim_walk); gravar("Run", 28, anim_run); gravar("Provocar", 72, anim_provocar, False)
gravar("Attack_Pancada", 54, anim_pancada, False); gravar("Attack_Martelada", 66, anim_martelada, False)
gravar("Defender_Entrar", 20, anim_def_entrar, False); gravar("Defender_Segurar", 40, anim_def_segurar); gravar("Defender_Sair", 20, anim_def_sair, False)
gravar("Levar_Golpe", 26, anim_hit, False); gravar("Morrer", 90, anim_morrer, False)
arm.animation_data.action = bpy.data.actions["Idle"]; bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/golem_anim.blend"); print("ACOES", [a.name for a in bpy.data.actions])
