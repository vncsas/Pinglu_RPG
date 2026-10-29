# Spell clips for the sorceress, built on the retargeted Quaternius "Spell_Simple" mocap (anim_rt.blend):
#   - the right arm holds the staff upright at her side during the spells/walks (the mocap arm hangs
#     and pointed the staff backwards through the coat)
#   - Magia_Lancar: a push layered on the mocap shot (hand drives forward, chest recoils, fingers open)
#   - floating versions: legs + height from the hover cycle, upper body from the spell
#     (Flutuar_Magia_Pronta, Flutuar_Magia_Lancar)
#   - ground clips: hips lifted on frames where the body would go under the floor
# usage: blender -b anim_rt.blend -P maga_spells.py -- <scripts_dir> <out.blend>
import bpy, sys, math, numpy as np
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
sys.argv = sys.argv[:sys.argv.index("--")+1]+[args[0], OUT]      # (maga_anim reads its own args)
import maga_anim as MA
from posekit import smooth
K, P, arm, sc = MA.K, MA.P, MA.arm, MA.sc
body = bpy.data.objects["Body"]
TAU = 2*math.pi
UPPER = ["Spine", "Chest", "UpperChest", "Neck", "Head"]+[s+n for s in ("Left", "Right") for n in
         ("Shoulder", "UpperArm", "LowerArm", "LowerArm_Twist", "Hand")]+[b.name for b in arm.data.bones if b.name.startswith("Left") and any(f in b.name for f in ("Thumb", "Index", "Middle", "Ring", "Little"))]
LEGS = [s+n for s in ("Left", "Right") for n in ("UpperLeg", "LowerLeg", "Foot", "Toes")]

def set_action(name):
    a = bpy.data.actions[name]; arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
    return a
def sample(name):
    a = set_action(name); f0, f1 = (int(x) for x in a.frame_range); out = []
    for f in range(f0, f1+1):
        sc.frame_set(f); out.append({pb.name: pb.matrix_basis.copy() for pb in P})
    return out
def staff_upright(lag=0.0):
    H = P["Hips"].matrix
    grip = H.translation+(H.to_3x3() @ Vector((-0.26, 0.20, 0.14)))   # beside her right hip, a bit forward (Hips: X left, Y up, Z front)
    up = Vector((0.02, -0.05, 1)).normalized()
    MA.staff_hand(grip, up, (-0.6, 0.5, -0.4))
def floor_lift(G):
    dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh()
    c = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", c); z = c.reshape(-1, 3)[:, 2].min(); be.to_mesh_clear()
    if z < 0:
        m = P["Hips"].matrix.copy(); m.translation.z -= z; P["Hips"].matrix = m; K.upd()
def write(name, poses, ground=True, post=None):
    MA.new_action(name, len(poses))
    for i, ps in enumerate(poses):
        MA.apply(ps); f = i+1
        if post: post(i, len(poses))
        if ground: floor_lift(None)
        K.key_all(f)
    print("ACTION", name, len(poses), "quadros")

src = {n: sample(n) for n in ("Magia_Entrar", "Magia_Pronta", "Magia_Lancar", "Magia_Sair", "Andar", "Correr")}

def staff_post(i, n): staff_upright()
def narrow_feet(half_width):
    """walk/run: the T-pose rest has the legs slightly open and the retarget kept it, so the feet travelled
    23 cm apart under 14 cm hips. Each thigh is turned in (about the forward axis at the hip) until its ankle
    is half_width from the centre line between the hips."""
    c = (P["LeftUpperLeg"].head.x+P["RightUpperLeg"].head.x)/2
    for S, sx in (("Left", 1), ("Right", -1)):
        up = P[S+"UpperLeg"]; ank = P[S+"Foot"].head
        dx = (c+sx*half_width)-ank.x; L = max(0.2, up.head.z-ank.z)
        K.rot_world(S+"UpperLeg", 'Y', -math.degrees(math.asin(max(-0.5, min(0.5, dx/L)))))
def walk_post(i, n): staff_upright(); narrow_feet(0.055)
for n in ("Magia_Entrar", "Magia_Pronta", "Magia_Sair"):
    write(n, src[n], post=staff_post)
for n in ("Andar", "Correr"):
    write(n, src[n], post=walk_post)

def shot_post(i, n):
    """the push: anticipation (pull back, frames 0-30%), thrust (30-50%), hold and settle"""
    u = i/(n-1)
    pull = math.sin(math.pi*min(1, u/0.3))*(u < 0.3)
    thrust = smooth((u-0.28)/0.14)*(1-smooth((u-0.6)/0.4))
    staff_upright()
    lh, le = P["LeftHand"].head.copy(), P["LeftLowerArm"].head.copy()
    d = (lh-P["LeftUpperArm"].head).normalized()
    target = lh+d*(0.10*thrust-0.07*pull)
    K.two_bone("LeftUpperArm", "LeftLowerArm", target, le-(P["LeftUpperArm"].head+target)/2)
    K.rot_world("Chest", 'Z', 6*pull-4*thrust); K.rot_world("UpperChest", 'X', 4*thrust-3*pull)
    m = P["LeftHand"].matrix.copy(); m.translation = P["LeftHand"].head.copy(); P["LeftHand"].matrix = m; K.upd()
    K.rot_local("LeftHand", 'X', -25*thrust+15*pull)               # palm opens toward the target on the shot
    K.fist("Left", 0.5*pull-0.35*thrust)
write("Magia_Lancar", src["Magia_Lancar"], post=shot_post)
SHOT_FRAME = round(0.38*(len(src["Magia_Lancar"])-1))+1

# floating spells: hover legs/height + spell upper body
def floating(name, spell, n_cycle=96, post=None):
    sp = src[spell]; n = len(sp); poses = []
    for i in range(n):
        MA.hover_pose((i % n_cycle)/n_cycle); hv = {pb.name: pb.matrix_basis.copy() for pb in P}
        ps = dict(sp[i])
        for b in LEGS: ps[b] = hv[b]
        Hm = sp[i]["Hips"].copy(); Hm.translation = hv["Hips"].translation; ps["Hips"] = Hm
        poses.append(ps)
    write(name, poses, ground=False, post=post)
# the hover bob of a loop must close: stretch Magia_Pronta (64 frames) to one 96-frame bob
pr = src["Magia_Pronta"]; src["Magia_Pronta_96"] = [MA.blend(pr[int(i*63/96)], pr[min(63, int(i*63/96)+1)], (i*63/96) % 1) for i in range(96)]
floating("Flutuar_Magia_Pronta", "Magia_Pronta_96", post=staff_post)
floating("Flutuar_Magia_Lancar", "Magia_Lancar", post=shot_post)

# ---------------------------------------------------------------- staff lightning (Raio_Cajado)
# raises the staff (anticipation, leaning back), swings it down to point the crystal at the target (frame
# RAIO_FRAME: the lightning leaves the StaffTip), small recoil and hold, then back to her stance.
# The sword-like path is a Catmull-Rom through the key poses, the body follows with lean and twist.
def cr(pts, u):
    n = len(pts); x = u*(n-1); i = min(int(x), n-2); t = x-i
    p0, p1, p2, p3 = pts[max(i-1, 0)], pts[i], pts[i+1], pts[min(i+2, n-1)]
    return 0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)
RAIO_N, RAIO_FRAME = 40, 16
KEYF = [1, 10, 16, 19, 28, 40]                        # rest, raised, strike, recoil, hold, rest
def raio_pose(f, base_fn):
    base_fn()
    H = P["Hips"].head.copy()
    rest_grip = P["RightHandWeapon"].matrix.translation.copy(); rest_up = P["RightHandWeapon"].matrix.to_3x3().col[1].normalized()
    G = [rest_grip, H+Vector((-0.24, 0.06, 0.42)), H+Vector((-0.17, -0.42, 0.30)), H+Vector((-0.18, -0.38, 0.31)),
         H+Vector((-0.17, -0.41, 0.30)), rest_grip]
    U = [rest_up, Vector((0.10, 0.35, 1)), Vector((0.06, -1, 0.22)), Vector((0.06, -1, 0.30)), Vector((0.06, -1, 0.24)), rest_up]
    # piecewise time -> key index (smooth inside each segment, so it does not stop dead at the keys)
    for k in range(len(KEYF)-1):
        if KEYF[k] <= f <= KEYF[k+1]:
            s_ = smooth((f-KEYF[k])/(KEYF[k+1]-KEYF[k])); u = (k+s_)/(len(KEYF)-1); break
    grip = cr(G, u); up = cr([x.normalized() for x in U], u).normalized()
    raised = math.sin(math.pi*min(1, max(0, (f-1)/(RAIO_FRAME-1))))*(f < RAIO_FRAME)          # 0..1..0 up to the strike
    strike = smooth((f-12)/5)*(1-smooth((f-28)/12))
    K.rot_world("Spine", 'X', -5*raised+9*strike); K.rot_world("Chest", 'X', -3*raised+4*strike)
    K.rot_world("Chest", 'Z', 6*raised-10*strike); K.rot_world("Head", 'X', 3*raised-6*strike)
    m = P["Hips"].matrix.copy(); m.translation.z -= 0.035*strike; m.translation.y -= 0.03*strike; P["Hips"].matrix = m; K.upd()
    MA.staff_hand(grip, up, (-0.7, 0.4, -0.3))
    K.aim("LeftUpperArm", (0.55, -0.15*strike, -0.83+0.25*raised)); K.aim("LeftLowerArm", (0.6, -0.35*strike-0.1, -0.7+0.3*raised))
    K.fist("Left", 0.2-0.4*strike)
def ground_base():
    MA.ground_pose(0.0)
def write_fn(name, n, fn, ground=True):
    MA.new_action(name, n)
    for f in range(1, n+1):
        fn(f)
        if ground: floor_lift(None)
        K.key_all(f)
    print("ACTION", name, n, "quadros")
write_fn("Raio_Cajado", RAIO_N, lambda f: raio_pose(f, ground_base))
write_fn("Flutuar_Raio", RAIO_N, lambda f: raio_pose(f, lambda: MA.hover_pose(((f-1) % 96)/96)), ground=False)
print("raio (evento SoltarRaio) no quadro", RAIO_FRAME, "de Raio_Cajado e Flutuar_Raio")

for a in ("Magia_Entrar", "Magia_Pronta", "Magia_Lancar", "Magia_Sair", "Andar", "Correr", "Levar_Golpe", "Morrer",
          "Parado", "Flutuar", "Flutuar_Frente", "Comecar_Flutuar", "Pousar", "Flutuar_Magia_Pronta", "Flutuar_Magia_Lancar",
          "Raio_Cajado", "Flutuar_Raio"):
    bpy.data.actions[a].use_fake_user = True
for a in [a for a in bpy.data.actions if a.name in ("Esquivar",)]: bpy.data.actions.remove(a)
arm["quadro_do_disparo"] = SHOT_FRAME
print("disparo (evento) no quadro", SHOT_FRAME, "de Magia_Lancar e Flutuar_Magia_Lancar")
set_action("Parado")
bpy.ops.wm.save_as_mainfile(filepath=OUT)
