# Hand-made NPC animations for Civilian 3 (on top of the retargeted mocap clips):
#   Correr_Desesperado  panicked run: the sprint with arms thrown up and flailing, head whipping back to look behind,
#                       torso leaning back, shorter/faster (loop)
#   Assustado           frightened standing loop: knees bent, hands up protecting the face, trembling, quick breathing
# usage: blender -b anim_rt.blend -P civil_anim.py -- <scripts_dir> <out.blend>
import bpy, sys, math
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit, smooth
arm = bpy.data.objects["Armature"]; K = Kit(arm); P, B = K.P, K.B
root = P["CTRL_Root"]; sc = bpy.context.scene; sc.render.fps = 30
TAU = 2*math.pi
def snap(): return {pb.name: pb.matrix_basis.copy() for pb in P}
def set_action(a):
    arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
def new_action(name):
    a = bpy.data.actions.get(name)
    if a: bpy.data.actions.remove(a)
    a = bpy.data.actions.new(name); a.use_fake_user = True; set_action(a)
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"):
        root[k] = 0.0; root.keyframe_insert(f'["{k}"]', frame=1)
    return a
def write(name, frames):
    a = new_action(name)
    for i, s in enumerate(frames):
        for n, m in s.items(): P[n].matrix_basis = m
        K.upd(); K.key_all(i+1)
    sc.frame_start, sc.frame_end = 1, len(frames)
    print("ACTION", name, len(frames), "quadros")

# ------------------------------------------------------------------ panicked run
src = bpy.data.actions["Correr_Base"]; set_action(src); n = int(src.frame_range[1]-src.frame_range[0]+1); f0 = int(src.frame_range[0])
frames = []
for i in range(n):
    sc.frame_set(f0+i); u = i/n
    # arms: thrown up and out, flailing; opposite phase left/right
    for S, sx in (("Left", 1), ("Right", -1)):
        ph = 0.0 if sx > 0 else 0.5; w = math.sin(TAU*(u+ph))
        K.rot_world(S+"UpperArm", 'Y', sx*(-32+14*w))        # + out/up (sign checked in the renders)
        K.rot_world(S+"UpperArm", 'X', -(45+30*w))           # + forward/up
        K.rot_world(S+"LowerArm", 'X', -15)
        K.fist(S, -0.3)
    K.rot_world("Spine", 'X', -9); K.rot_world("Chest", 'X', -5)
    K.rot_world("Head", 'X', -8)
    K.rot_world("Head", 'Z', 38*math.sin(TAU*u))              # whips round to look behind
    K.rot_world("Chest", 'Z', -8*math.sin(TAU*u))
    frames.append(snap())
write("Correr_Desesperado", frames)

# ------------------------------------------------------------------ frightened loop
N = 60; frames = []
ANK = {S: B[S+"Foot"].head_local.copy() for S in ("Left", "Right")}
for i in range(N):
    u = i/N; tr = math.sin(TAU*u*7)                           # tremble (7 cycles per loop)
    K.reset(apose=True)
    br = math.sin(TAU*u*2)                                    # quick breathing
    K.hips(drop=0.05+0.006*br, fwd=0.0, lean=-5+0.8*br)
    K.rot_world("Spine", 'Z', 1.2*tr); K.rot_world("Chest", 'X', 2.5*br)
    for S, sx in (("L", 1), ("R", -1)):
        p = ANK["Left" if sx > 0 else "Right"].copy(); p.z = max(p.z, 0.13); p.y -= 0.02; p.x += sx*0.03
        K.foot(S, p)
    for S, sx in (("Left", 1), ("Right", -1)):
        sh = P[S+"UpperArm"].head.copy()
        tgt = sh+Vector((sx*0.10+0.012*tr*sx, -0.33, 0.12+0.012*br)); pole = Vector((sx*0.5, 0.35, -1))
        K.two_bone(S+"UpperArm", S+"LowerArm", tgt, pole)
        d = (tgt-P[S+"LowerArm"].head)
        K.aim(S+"Hand", Vector((0, -0.4, 1)))                  # hands open, fingers up
        K.fist(S, 0.25)
    K.rot_world("Head", 'X', -7+1.2*br); K.rot_world("Head", 'Z', 4*math.sin(TAU*u*2)+1.0*tr)
    frames.append(snap())
write("Assustado", frames)


# ------------------------------------------------------------------ arms closer to the body in the mocap clips
# (the library's slim character holds its arms ~20 deg off the torso; this one is broader, with thick sleeves)
ADDUCT = {"Parado": 2, "Andar": 2, "Andar_Calmo": 2, "Correr": 1, "Falar": 8, "Negar": 4,
          "Interagir": 4, "Levar_Golpe": 4, "Levar_Golpe_Cabeca": 4, "Levar_Golpe_Forte": 4}
for name, deg in ADDUCT.items():
    a = bpy.data.actions[name]; set_action(a); n = int(a.frame_range[1]-a.frame_range[0]+1); f0 = int(a.frame_range[0]); fr = []
    for i in range(n):
        sc.frame_set(f0+i)
        for S, sx in (("Left", 1), ("Right", -1)): K.rot_world(S+"UpperArm", 'Y', sx*deg)
        fr.append(snap())
    write(name, fr)

# ------------------------------------------------------------------ Concordar: nods twice (on the idle), Ouvir: listening, hands clasped
base = bpy.data.actions["Parado"]; set_action(base); n = int(base.frame_range[1]-base.frame_range[0]+1); f0 = int(base.frame_range[0]); fr = []
for i in range(n):
    sc.frame_set(f0+i); u = i/n
    env = smooth((u-0.08)/0.12)*smooth((0.72-u)/0.12)            # nod envelope
    nod = env*max(0.0, -math.cos(TAU*2.0*(u-0.08)/0.64+0)) if False else env*(0.5-0.5*math.cos(TAU*2.0*(u-0.08)/0.64))
    K.rot_world("Head", 'X', 11*nod); K.rot_world("Neck", 'X', 4*nod); K.rot_world("Chest", 'X', 2.5*nod)
    fr.append(snap())
write("Concordar", fr)
N = 76; fr = []
for i in range(N):
    u = i/N; br = math.sin(TAU*u); K.reset(apose=True)
    K.hips(drop=0.0+0.004*br, lean=1.0)
    for S, sx in (("L", 1), ("R", -1)):
        K.foot(S, Vector((sx*0.13+sx*0.02, -0.085, 0.13)))
    for S, sx in (("Left", 1), ("Right", -1)):
        sh = P[S+"UpperArm"].head.copy()
        tgt = sh+Vector((-sx*0.17, -0.27+0.004*br, -0.40)); pole = Vector((sx*0.6, 0.5, -1))
        K.two_bone(S+"UpperArm", S+"LowerArm", tgt, pole); K.aim(S+"Hand", Vector((-sx*0.3, -1, -0.3))); K.fist(S, 0.15)
    sway = math.sin(TAU*u)
    K.rot_world("Spine", 'Z', 2.0*sway); K.rot_world("Head", 'Z', 4*math.sin(TAU*u+0.8)); K.rot_world("Head", 'Y', 6)
    K.rot_world("Head", 'X', 3+2*max(0, math.sin(TAU*2*u)))
    fr.append(snap())
write("Ouvir", fr)

# ------------------------------------------------------------------ Varrer: sweeping with a broom (the broom is a prop the game attaches)
N = 56; fr = []
ANKS = {S: B[S+"Foot"].head_local.copy() for S in ("Left", "Right")}
for i in range(N):
    u = i/N; sw = math.sin(TAU*u); K.reset(apose=True)
    K.hips(drop=0.06, lean=18+1.5*math.sin(TAU*2*u), side=0.03*sw)
    K.rot_world("Spine", 'Z', 7*sw); K.rot_world("Chest", 'Z', 4*sw); K.rot_world("Head", 'X', 6)
    for S, sx in (("L", 1), ("R", -1)):
        p_ = ANKS["Left" if sx > 0 else "Right"].copy(); p_.z = max(p_.z, 0.13); p_.y -= 0.03 if sx < 0 else -0.05
        K.foot(S, p_)
    for S, sx, cx, dz in (("Left", 1, 0.06, -0.36), ("Right", -1, -0.02, -0.50)):
        sh = P[S+"UpperArm"].head.copy()
        tgt = Vector((cx+0.17*sw, sh.y-0.30-0.05*math.cos(TAU*u), sh.z+dz+0.02*math.cos(TAU*2*u)))
        K.two_bone(S+"UpperArm", S+"LowerArm", tgt, Vector((sx*0.6, 0.5, -1)))
        K.aim(S+"Hand", Vector((0, -0.6, -0.8))); K.fist(S, 0.7)
    fr.append(snap())
write("Varrer", fr)

for x in bpy.data.actions:
    if "|" not in x.name: x.use_fake_user = True
bpy.data.actions.remove(bpy.data.actions["Correr_Base"])
set_action(bpy.data.actions["Parado"])
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("DONE")
