# Test animations for the tank, on the retargeted Quaternius mocap (anim_rt.blend from retarget.py):
#   every clip gets wing motion on top (the mocap has no wings): slow "breathing" when idle, a sway that
#   follows the steps when walking/running, feet brought closer in walk/run.
#   Flight clips made by hand: Decolar, Voar, Voar_Frente, Pousar. Shield skill made by hand on the idle:
#   Escudo_Erguer, Escudo_Segurar, Escudo_Impacto, Escudo_Baixar.
#   Hips lifted on frames where the body would go under the floor.
# usage: blender -b anim_rt.blend -P tanque_anim.py -- <scripts_dir> <out.blend>
import bpy, sys, math, numpy as np
from mathutils import Matrix
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit, smooth
arm = bpy.data.objects["Armature"]; K = Kit(arm); P = arm.pose.bones; sc = bpy.context.scene
body = bpy.data.objects["Body"]; TAU = 2*math.pi
WING = [f"Wing_{s}_{k}" for s in ("L", "R") for k in (1, 2, 3)]

def set_action(a):
    arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
def wings(open_, fold=0.0, lift=0.0):
    """open_: 0 folded on the back .. 1 wide open; fold bends the outer parts; lift raises the wing (flap up)"""
    for S, sx in (("L", 1), ("R", -1)):
        K.rot_world(f"Wing_{S}_1", 'Y', -sx*(30*open_+18*lift)); K.rot_world(f"Wing_{S}_1", 'Z', sx*28*open_)
        K.rot_local(f"Wing_{S}_2", 'X', -22*open_+25*fold); K.rot_local(f"Wing_{S}_3", 'X', -16*open_+22*fold)
vgn = {g.index: g.name for g in body.vertex_groups}
SOFT = ("Wing_", "Thumb", "Index", "Middle", "Little", "CC_Base_L_ForearmTwist", "CC_Base_R_ForearmTwist", "LeftHand", "RightHand")
core = np.array([sum(g.weight for g in v.groups if vgn[g.group].startswith(SOFT) or any(k in vgn[g.group] for k in SOFT)) < 0.5
                 for v in body.data.vertices])          # wings, hands and forearm claws may touch / go into the floor
def body_zmin():
    dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh()
    c = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", c); z = c.reshape(-1, 3)[:, 2]; be.to_mesh_clear()
    return float(z[core].min())
def floor_lift(settle=0.0):
    """lift the hips if the body (not wings/claws) goes under the floor; settle (0..1) also brings a body that
    is above the floor down to it (lying on the ground after the fall)"""
    z = body_zmin(); dz = -z if z < 0 else -z*settle
    if abs(dz) > 1e-4:
        m = P["Hips"].matrix.copy(); m.translation.z += dz; P["Hips"].matrix = m; K.upd()
        P["Hips"].keyframe_insert("location", frame=sc.frame_current)

def narrow(deg):
    """the walk/run mocap keeps the creature's wide stance (toes ~40 cm apart): both thighs turn in by a
    constant angle about the forward axis at the hip. Constant (not aimed every frame), so the step keeps
    its own shape and the swinging leg never crosses the other one."""
    for S, sx in (("Left", 1), ("Right", -1)): K.rot_world(S+"UpperLeg", 'Y', sx*deg)
NARROW = {"Parado": 3, "Andar": 6, "Correr": 6}

def layer(name, wing_fn):
    a = bpy.data.actions[name]; set_action(a); f0, f1 = (int(x) for x in a.frame_range); n = f1-f0+1
    for f in range(f0, f1+1):
        sc.frame_set(f)
        for b in WING: P[b].matrix_basis = Matrix.Identity(4)
        K.upd()
        if name in NARROW:
            narrow(NARROW[name])
            for S in ("Left", "Right"): P[S+"UpperLeg"].keyframe_insert("rotation_quaternion", frame=f)
        wing_fn((f-f0)/max(1, n-1), f)
        for b in WING: P[b].keyframe_insert("rotation_quaternion", frame=f)
        floor_lift(smooth(((f-f0)/max(1, n-1)-0.35)/0.3) if name == "Morrer" else 0.0)
    print("ACTION", name, n, "quadros")

loop = lambda u, k=1: math.sin(TAU*u*k)
layer("Parado", lambda u, f: wings(0.08+0.05*loop(u), 0.05*loop(u, 1)))
layer("Andar", lambda u, f: wings(0.05+0.04*loop(u, 2), 0.06*loop(u, 2)))
layer("Correr", lambda u, f: wings(-0.05+0.06*loop(u, 2), 0.15, 0.1*loop(u, 2)))
layer("Levar_Golpe", lambda u, f: wings(0.25*math.sin(math.pi*u), 0.3*math.sin(math.pi*u)))
layer("Morrer", lambda u, f: wings(0.3*smooth(u/0.4), 0.5*smooth((u-0.3)/0.7)))
# ---------------------------------------------------------------- flight (made by hand)
# The wings now have a reason to flap: he flies. Lift comes with the down stroke, so the body rises a little
# right after each stroke (follows the wings with a delay); legs hang and trail, toes pointed.
HOVER = 0.6
def stroke(ph):
    """wing beat, ph 0..1: down stroke fast (40%), up stroke slower and folded. returns (lift -1..1, fold)"""
    if ph < 0.4: t = ph/0.4; return 1-2*smooth(t), 0.0
    t = (ph-0.4)/0.6; return -1+2*smooth(t), 0.35*math.sin(math.pi*t)
def new_action(name, n):
    a = bpy.data.actions.get(name)
    if a: bpy.data.actions.remove(a)
    a = bpy.data.actions.new(name); a.use_fake_user = True; set_action(a)
    root = P["CTRL_Root"]
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"):
        root[k] = 0.0; root.keyframe_insert(f'["{k}"]', frame=1)
    return a
UPPER = ["Spine", "Chest", "UpperChest", "Neck", "Head"]+[s+n for s in ("Left", "Right") for n in ("Shoulder", "UpperArm", "LowerArm", "Hand")]
def fly_pose(t, beat, height, fwd=0.0, heal=None):
    """t: time in frames (for the slower secondary motion), beat: wing phase 0..1, height above the ground"""
    K.reset(apose=True)
    lift, fold = stroke(beat % 1.0)
    rise = 0.05*stroke((beat-0.15) % 1.0)[0]                            # the body follows the stroke a bit later
    m = P["Hips"].matrix.copy(); m.translation.z += height-rise; P["Hips"].matrix = m; K.upd()
    K.rot_world("Hips", 'X', 6+16*fwd)                                   # leans into the flight
    K.rot_world("Spine", 'X', -4+3*rise*10+4*fwd); K.rot_world("Head", 'X', -6*fwd-2*rise*10)
    for S, sx in (("Left", 1), ("Right", -1)):                            # legs hang, trail back, toes pointed
        sw = 3*math.sin(TAU*(t/48.0)+(0 if S == "Left" else 1.3))
        K.rot_world(S+"UpperLeg", 'X', 4-14*fwd+sw); K.rot_world(S+"LowerLeg", 'X', 6)   # (digitigrade legs already slant back)
        K.rot_world(S+"Foot", 'X', 25+5*fwd); K.rot_world(S+"UpperLeg", 'Y', sx*4)
    if heal is not None:
        for b, mtx in heal.items():
            if b in UPPER: P[b].matrix_basis = mtx
        K.upd()
    else:                                                                 # arms relaxed, a little out, follow the beat
        for S, sx in (("Left", 1), ("Right", -1)):
            K.rot_world(S+"UpperArm", 'Y', -sx*(12+4*lift)); K.rot_world(S+"UpperArm", 'X', -10*fwd)
    wings(0.9, fold, 0.95*lift)
def fly_clip(name, n, cycles, fwd=0.0, heal=False):
    new_action(name, n)
    for f in range(1, n+1):
        u = (f-1)/(n-1); fly_pose(f, u*cycles, HOVER, fwd); K.key_all(f)
    print("ACTION", name, n, "quadros")
def takeoff(n=50):
    """crouch with the wings raised, jump with a strong down stroke, then keeps flapping up to the hover height"""
    new_action("Decolar", n)
    for f in range(1, n+1):
        u = (f-1)/(n-1)
        if u < 0.22:                                                      # crouch, wings go up (wind-up)
            t = smooth(u/0.22); K.reset(apose=True)
            m = P["Hips"].matrix.copy(); m.translation.z -= 0.10*t; P["Hips"].matrix = m; K.upd()
            K.rot_world("Spine", 'X', 10*t); wings(0.2+0.7*t, 0.2*t, 0.95*t)
            for S, _ in (("Left", 1), ("Right", -1)): K.rot_world(S+"UpperLeg", 'X', -12*t); K.rot_world(S+"LowerLeg", 'X', 14*t)
        else:                                                             # jump: beat 0 = start of the down stroke
            t = (u-0.22)/0.78; beat = t*2.2
            fly_pose(f, beat, HOVER*smooth(t/0.8)-0.10*(1-smooth(t/0.15)))
        K.key_all(f)
    print("ACTION Decolar", n, "quadros")
def landing(n=44):
    """comes down with the wings open wide as brakes, legs reach for the ground, knees absorb the touch"""
    new_action("Pousar", n)
    for f in range(1, n+1):
        u = (f-1)/(n-1)
        if u < 0.7:
            t = u/0.7; fly_pose(f, t*1.5, HOVER*(1-smooth(t)))
            for S, _ in (("Left", 1), ("Right", -1)): K.rot_world(S+"UpperLeg", 'X', -12*t)   # legs forward to land
        else:
            t = (u-0.7)/0.3; K.reset(apose=True)
            land = 0.08*math.sin(math.pi*t)
            m = P["Hips"].matrix.copy(); m.translation.z -= land; P["Hips"].matrix = m; K.upd()
            for S, _ in (("Left", 1), ("Right", -1)): K.rot_world(S+"UpperLeg", 'X', -10*math.sin(math.pi*t)); K.rot_world(S+"LowerLeg", 'X', 12*math.sin(math.pi*t))
            wings(0.9*(1-smooth(t)), 0.3*smooth(t))
        K.key_all(f)
    print("ACTION Pousar", n, "quadros")
fly_clip("Voar", 48, 2)                       # 2 beats in 1.6 s, loops
fly_clip("Voar_Frente", 40, 2, fwd=1.0)
takeoff(); landing()

# ---------------------------------------------------------------- shield skill (tank)
# Defensive stance on top of the idle mocap (keeps its breathing): lowers the hips, feet apart, leans into
# the hit, crosses the forearms in front of the chest and wraps the wings forward around the body like a
# shell. Blends with ease in/out; the hit is a short push back with a recoil and recovery.
def shield_pose(w, push=0.0):
    """w: 0 normal .. 1 full shield; push: 0..1 recoil of a hit on the shield"""
    m = P["Hips"].matrix.copy(); m.translation.z -= 0.09*w+0.05*push; m.translation.y += 0.14*push; P["Hips"].matrix = m; K.upd()
    K.rot_world("Hips", 'X', 8*w-10*push); K.rot_world("Spine", 'X', 6*w-6*push); K.rot_world("Chest", 'X', 4*w-10*push)
    K.rot_world("Head", 'X', -10*w+10*push)
    for S, sx in (("Left", 1), ("Right", -1)):
        K.rot_world(S+"UpperLeg", 'Y', -sx*7*w)                                  # feet apart, braced
        K.rot_world(S+"UpperLeg", 'X', -8*w)                                      # keep the legs under the leaning hips
        K.rot_world(S+"LowerLeg", 'X', 6*w)
    snap_base = {b: P[b].matrix_basis.copy() for b in ARMS}
    for S, sx in (("Left", 1), ("Right", -1)):                                    # forearms crossed in front
        K.aim(S+"UpperArm", (sx*0.35, -0.85, -0.25)); K.aim(S+"LowerArm", (-sx*0.75, -0.55, 0.30+0.05*sx))
        K.aim(S+"Hand", (-sx*0.6, -0.3, 0.75))
    braced = {b: P[b].matrix_basis.copy() for b in ARMS}
    for b in ARMS: P[b].matrix_basis = snap_base[b]
    K.upd()
    for b in ARMS:                                                                 # blend arms base -> braced
        qa, qb = snap_base[b].to_quaternion(), braced[b].to_quaternion()
        P[b].matrix_basis = qa.slerp(qb, min(max(w, 0.0), 1.0)).to_matrix().to_4x4()
    K.upd()
    for S, sx in (("L", 1), ("R", -1)):                                           # wings wrap forward
        # the wing hangs behind the shoulder: swing it round the vertical axis at its root to the front-side,
        # then bend the outer part inward so the two wings close like a shell in front of the body
        K.rot_world(f"Wing_{S}_1", 'Z', -sx*(WZ*w-10*push))
        K.rot_world(f"Wing_{S}_2", 'Z', -sx*WZ2*w); K.rot_world(f"Wing_{S}_3", 'Z', -sx*WZ3*w)
import os
WZ, WZ2, WZ3 = (float(x) for x in os.environ.get("WINGZ", "140,60,60").split(","))
ARMS = [s+n for s in ("Left", "Right") for n in ("UpperArm", "LowerArm", "Hand")]
IDLE = {}
def idle_base(i):
    if not IDLE:
        a = bpy.data.actions["Parado"]; set_action(a); f0, f1 = (int(x) for x in a.frame_range)
        for f in range(f0, f1+1):
            sc.frame_set(f); IDLE[f-f0] = {pb.name: pb.matrix_basis.copy() for pb in P}
    return IDLE[i % len(IDLE)]
def shield_clip(name, n, fn):
    idle_base(0)
    new_action(name, n)
    for f in range(1, n+1):
        for b, mtx in idle_base(f-1).items(): P[b].matrix_basis = mtx
        for b in WING: P[b].matrix_basis = Matrix.Identity(4)
        K.upd(); u = (f-1)/(n-1); fn(u); floor_lift(); K.key_all(f)
    print("ACTION", name, n, "quadros")
shield_clip("Escudo_Erguer", 18, lambda u: shield_pose(smooth(u)*(1+0.06*math.sin(math.pi*u))))   # small overshoot
shield_clip("Escudo_Segurar", 60, lambda u: shield_pose(1.0+0.03*math.sin(TAU*u)))
shield_clip("Escudo_Impacto", 20, lambda u: shield_pose(1.0, math.sin(math.pi*min(1, u/0.35))*(1-smooth((u-0.35)/0.65)) if u < 0.35 else 1-smooth((u-0.35)/0.65)))
shield_clip("Escudo_Baixar", 18, lambda u: shield_pose(1-smooth(u)))
print("eventos: AtivarEscudo no fim de Escudo_Erguer, ImpactoEscudo no quadro 4 de Escudo_Impacto, DesativarEscudo no inicio de Escudo_Baixar")
old = bpy.data.actions.get("Asas_Bater")
if old: bpy.data.actions.remove(old)
for x in bpy.data.actions:
    if "|" not in x.name: x.use_fake_user = True
set_action(bpy.data.actions["Parado"])
bpy.ops.wm.save_as_mainfile(filepath=OUT)
