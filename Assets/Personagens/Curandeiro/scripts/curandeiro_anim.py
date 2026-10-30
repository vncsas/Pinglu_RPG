# Test animations for the healer, on the retargeted Quaternius mocap (anim_rt.blend from retarget.py):
#   every clip gets wing motion on top (the mocap has no wings): slow "breathing" when idle, a sway that
#   follows the steps when walking/running, wings opening wide on the heal; feet brought closer in walk/run.
#   Flight clips made by hand: Decolar, Voar, Voar_Frente, Voar_Curar, Pousar.
#   Hips lifted on frames where the body would go under the floor.
# usage: blender -b anim_rt.blend -P curandeiro_anim.py -- <scripts_dir> <out.blend>
import bpy, sys, math, numpy as np
from mathutils import Matrix
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit, smooth
arm = bpy.data.objects["Armature"]; K = Kit(arm); P = arm.pose.bones; sc = bpy.context.scene
body = bpy.data.objects["Body"]; TAU = 2*math.pi
WING = [f"Wing_{s}_{k}" for s in ("L", "R") for k in (1, 2, 3, 4)]

def set_action(a):
    arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
def wings(open_, fold=0.0, lift=0.0):
    """open_: 0 folded on the back .. 1 wide open; fold bends the outer parts; lift raises the wing (flap up)"""
    for S, sx in (("L", 1), ("R", -1)):
        # 4-bone feathered wings: _1 spreads/lifts the whole wing, _3/_4 (primaries) bend for the fold/stroke
        K.rot_world(f"Wing_{S}_1", 'Y', -sx*(26*open_+20*lift)); K.rot_world(f"Wing_{S}_1", 'Z', sx*22*open_)
        K.rot_local(f"Wing_{S}_3", 'X', -18*open_+22*fold); K.rot_local(f"Wing_{S}_4", 'X', -12*open_+18*fold)
vgn = {g.index: g.name for g in body.vertex_groups}
SOFT = ("LeftHand", "RightHand")                        # the hands may touch the floor; the wings may not
core = np.array([sum(g.weight for g in v.groups if vgn[g.group].startswith(SOFT) or any(k in vgn[g.group] for k in SOFT)) < 0.5
                 for v in body.data.vertices])          # wings, hands and forearm claws may touch / go into the floor
def body_zmin():
    dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh()
    c = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", c); z = c.reshape(-1, 3)[:, 2]; be.to_mesh_clear()
    return float(z[core].min())
WINGV = np.array([sum(g.weight for g in v.groups if vgn[g.group].startswith("Wing_")) > 0.5 for v in body.data.vertices])
def floor_lift(settle=0.0, frame=None):
    """lift the hips if the body (not wings/claws) goes under the floor; settle (0..1) also brings a body that
    is above the floor down to it (lying on the ground after the fall)"""
    z = body_zmin(); dz = -z if z < 0 else -z*settle
    if abs(dz) > 1e-4:
        m = P["Hips"].matrix.copy(); m.translation.z += dz; P["Hips"].matrix = m; K.upd()
        P["Hips"].keyframe_insert("location", frame=sc.frame_current if frame is None else frame)

def narrow(deg):
    """the walk/run mocap keeps the creature's wide stance (toes ~40 cm apart): both thighs turn in by a
    constant angle about the forward axis at the hip. Constant (not aimed every frame), so the step keeps
    its own shape and the swinging leg never crosses the other one."""
    for S, sx in (("Left", 1), ("Right", -1)): K.rot_world(S+"UpperLeg", 'Y', sx*deg)
NARROW = {}

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
layer("Curar_Entrar", lambda u, f: wings(0.7*smooth(u)))
layer("Curar_Pronta", lambda u, f: wings(0.7+0.06*loop(u), 0.05*loop(u)))
layer("Curar_Lancar", lambda u, f: wings(0.7+0.3*math.sin(math.pi*min(1, u/0.5)), -0.1*math.sin(math.pi*min(1, u/0.5))))
layer("Curar_Sair", lambda u, f: wings(0.7*(1-smooth(u))))
layer("Levar_Golpe", lambda u, f: wings(0.25*math.sin(math.pi*u), 0.3*math.sin(math.pi*u)))
layer("Morrer", lambda u, f: wings(1.3*smooth((u-0.15)/0.45), -0.2*smooth((u-0.3)/0.5)))
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
CURA = {}
def cura_upper(i, src="Curar_Pronta"):
    """upper body of a heal mocap clip at frame i (to heal while flying)"""
    if src not in CURA:
        a = bpy.data.actions[src]; set_action(a); f0, f1 = (int(x) for x in a.frame_range); CURA[src] = {}
        for f in range(f0, f1+1):
            sc.frame_set(f); CURA[src][f-f0] = {pb.name: pb.matrix_basis.copy() for pb in P}
    return CURA[src][i % len(CURA[src])]
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
    src = heal if isinstance(heal, str) else "Curar_Pronta"
    if heal: cura_upper(0, src)                  # read the heal pose BEFORE creating the new action
    new_action(name, n)
    for f in range(1, n+1):
        u = (f-1)/(n-1); fly_pose(f, u*cycles, HOVER, fwd, cura_upper(f-1, src) if heal else None); K.key_all(f)
    print("ACTION", name, n, "quadros")
def takeoff(n=50):
    """crouch with the wings raised, jump with a strong down stroke, then keeps flapping up to the hover height"""
    new_action("Decolar", n)
    for f in range(1, n+1):
        u = (f-1)/(n-1)
        if u < 0.22:                                                      # crouch, wings go up (wind-up)
            t = smooth(u/0.22); K.reset(apose=True); floor_lift(1.0, f)
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
            wings(0.9*(1-smooth(t)), 0.3*smooth(t)); floor_lift(1.0, f)
        K.key_all(f)
    print("ACTION Pousar", n, "quadros")
fly_clip("Voar", 48, 2)                       # 2 beats in 1.6 s, loops
fly_clip("Voar_Frente", 40, 2, fwd=1.0)
fly_clip("Voar_Curar", 64, 2, heal=True)
fly_clip("Voar_Curar_Lancar", 16, 16/24.0, heal="Curar_Lancar")   # the shot in the air (event LancarCura, frame 7)
takeoff(); landing()
old = bpy.data.actions.get("Asas_Bater")
if old: bpy.data.actions.remove(old)
for x in bpy.data.actions:
    if "|" not in x.name: x.use_fake_user = True
set_action(bpy.data.actions["Parado"])
bpy.ops.wm.save_as_mainfile(filepath=OUT)
