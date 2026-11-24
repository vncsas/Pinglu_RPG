# Hand-made animations for the sorceress (keyed FK on the deform bones, in place, 30 fps):
#   Parado           idle on the ground, her own pose (staff planted on the floor), breathing     loop
#   Comecar_Flutuar  lifts off: small crouch, rises ~30 cm, legs fold, arms open             one-shot
#   Flutuar          hovering: slow bob, legs and arms trail the body a little (overlap)       loop
#   Flutuar_Frente   gliding forward in the air: leaning in, legs trailing back                loop
#   Pousar           comes down and lands with a soft knee bend                                 one-shot
# The motion is slow and layered (every part follows the body with a delay) so it does not look
# mechanical; transitions are blends between real poses with ease in/out and a little overshoot.
# usage: blender -b maga_rig.blend -P maga_anim.py -- <scripts_dir> <out.blend>
import bpy, sys, math
from mathutils import Vector, Matrix, Quaternion
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit, smooth
arm = bpy.data.objects["Armature"]; K = Kit(arm); P, B = K.P, K.B
root = P["CTRL_Root"]; sc = bpy.context.scene; sc.render.fps = 30
SIDES = (("Left", 1), ("Right", -1))
L_LEG = sum((B["Left"+n].tail_local-B["Left"+n].head_local).length for n in ("UpperLeg", "LowerLeg"))
STAFF_UP = Vector((0.03, -0.06, 1)).normalized()            # staff held upright, crystal slightly forward
HOVER = 0.30                                                 # hover height (m)
TAU = 2*math.pi

def new_action(name, n):
    arm.animation_data_create(); a = bpy.data.actions.get(name)
    if a: bpy.data.actions.remove(a)
    a = bpy.data.actions.new(name); a.use_fake_user = True; arm.animation_data.action = a
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"):
        root[k] = 0.0; root.keyframe_insert(f'["{k}"]', frame=1)
    sc.frame_start, sc.frame_end = 1, n
    return a
def snap(): return {pb.name: pb.matrix_basis.copy() for pb in P}
def apply(s):
    for n, m in s.items(): P[n].matrix_basis = m
    K.upd()
def blend(a, b, t):
    out = {}
    for n in a:
        qa, qb = a[n].to_quaternion(), b[n].to_quaternion()
        M = qa.slerp(qb, t).to_matrix().to_4x4(); M.translation = a[n].translation.lerp(b[n].translation, t); out[n] = M
    return out
def key(f): K.key_all(f)

def staff_hand(grip, up=STAFF_UP, pole=(-0.6, 0.4, -0.5)):
    side = up.cross(Vector((0, 0, 1)))
    if side.length < 1e-3: side = Vector((1, 0, 0))
    K.hand_to("Right", K.hand_for_socket(K.sword_frame(grip, up, side.normalized())), Vector(pole))

# ---------------------------------------------------------------- poses
def ground_pose(b=0.0):
    """her own standing pose, staff planted on the floor; b = breathing (-1..1)"""
    K.reset(apose=True)
    for _ in range(3):                                      # hips down until both ankles reach the floor level
        need = 0.0
        for S, _ in SIDES:
            H = P[S+"UpperLeg"].head; T = P[S+"LowerLeg"].tail
            if T.z > 0.13: need = max(need, T.z-0.13)
        if need < 1e-4: break
        m = P["Hips"].matrix.copy(); m.translation.z -= need; P["Hips"].matrix = m; K.upd()
        for S, _ in SIDES:
            K.aim(S+"UpperLeg", K.AD[S+"UpperLeg"]); K.aim(S+"LowerLeg", K.AD[S+"LowerLeg"])
    m = P["Hips"].matrix.copy(); m.translation.z -= 0.004*b; P["Hips"].matrix = m; K.upd()
    K.rot_world("Chest", 'X', -1.0*b); K.rot_world("UpperChest", 'X', -0.8*b); K.rot_world("Head", 'X', 0.6*b)
    # keep the staff foot on the floor (the arm follows)
    Ms = P["RightHandWeapon"].matrix.copy(); up = Ms.to_3x3().col[1].normalized()
    foot = Ms.translation-up*1.159
    staff_hand(Ms.translation-Vector((0, 0, foot.z)), up, (-0.6, 0.5, -0.4))
    K.fist("Left", 0.15+0.05*b)

def hover_pose(t, fwd=0.0):
    """in the air; t = cycle phase 0..1; fwd = 0 hovering, 1 gliding forward"""
    K.reset(apose=False)
    bob = math.sin(TAU*t); lag = lambda d: math.sin(TAU*(t-d))
    lift = HOVER+0.035*bob
    m = P["Hips"].matrix.copy(); m.translation.z += lift-0.02; m.translation.y -= 0.04*fwd; P["Hips"].matrix = m; K.upd()
    K.rot_world("Hips", 'X', 2*lag(0.1)+12*fwd)
    K.rot_world("Spine", 'X', -3+1.5*lag(0.15)+4*fwd); K.rot_world("Chest", 'X', -2+1.2*lag(0.2)); K.rot_world("Head", 'X', 3-1.5*lag(0.25)-10*fwd)
    K.rot_world("Chest", 'Z', 2*math.sin(TAU*(t*0.5)))     # slow turn, half speed (not locked to the bob)
    # legs hang and trail: knees bent, toes pointed (high heels), the left a little ahead
    for S, sx in SIDES:
        ph = 0.12 if S == "Left" else 0.22
        th = (14 if S == "Left" else 4)-26*fwd+4*lag(ph)     # thigh forward (+) / back (-) in degrees
        kn = (24 if S == "Left" else 38)+18*fwd+5*lag(ph+0.1)
        a, k = math.radians(th), math.radians(th-kn)
        K.aim(S+"UpperLeg", (sx*0.07, -math.sin(a), -math.cos(a))); K.aim(S+"LowerLeg", (sx*0.04, -math.sin(k), -math.cos(k)))
        fd = P[S+"LowerLeg"].tail-P[S+"LowerLeg"].head
        K.aim(S+"Foot", fd.normalized()*0.55+Vector((0, -0.45, 0))); K.aim(S+"Toes", fd.normalized()*0.6+Vector((0, -0.4, 0)))
    # left arm floats out, palm down, fingers relaxed and a bit open; right arm holds the staff upright
    a = math.radians(38+4*lag(0.3)-10*fwd)
    K.aim("LeftUpperArm", (math.sin(a), 0.05+0.25*fwd, -math.cos(a))); K.aim("LeftLowerArm", (math.sin(a)*1.25, -0.25, -math.cos(a)*0.8))
    K.aim("LeftHand", (0.55, -0.2, -0.45+0.08*lag(0.4)))
    K.fist("Left", -0.25+0.08*lag(0.45))
    H = P["Hips"].head
    staff_hand(H+Vector((-0.27, -0.20-0.10*fwd, 0.22+0.015*lag(0.2))), (STAFF_UP+Vector((0, -0.35*fwd, 0))).normalized())

# ---------------------------------------------------------------- clips
def clip_parado(n=90):
    new_action("Parado", n+1)
    for f in range(1, n+2):
        ground_pose(math.sin(TAU*(f-1)/n)); key(f)
def clip_flutuar(name, n, fwd):
    new_action(name, n+1)
    for f in range(1, n+2):
        hover_pose(((f-1) % n)/n, fwd); key(f)
def clip_transition(name, n, up):
    """Comecar_Flutuar (up=True) / Pousar: blend ground <-> hover with anticipation and overshoot"""
    ground_pose(0.0); G = snap(); ANKG = {S: P[S+"LowerLeg"].tail.copy() for S, _ in SIDES}; hover_pose(0.0); Hp = snap()
    new_action(name, n)
    for f in range(1, n+1):
        u = (f-1)/(n-1)
        if up:
            t = smooth((u-0.15)/0.75)                              # waits a moment, then rises
            dip = 0.05*math.sin(math.pi*min(1, u/0.25))*(u < 0.25) # crouches before take-off
            over = 0.03*math.sin(math.pi*max(0, (u-0.7)/0.3))      # small overshoot at the top
            apply(blend(G, Hp, t))
            m = P["Hips"].matrix.copy(); m.translation.z += -dip+over; P["Hips"].matrix = m; K.upd()
            if u < 0.3:                                             # feet stay on the floor while crouching
                for S, _ in SIDES: K.foot(S[0], ANKG[S])
        else:
            t = 1-smooth(u/0.8)                                     # comes down
            land = 0.06*math.sin(math.pi*max(0, min(1, (u-0.7)/0.3)))   # knees give when touching down
            apply(blend(G, Hp, t))
            m = P["Hips"].matrix.copy(); m.translation.z -= land; P["Hips"].matrix = m; K.upd()
            if u > 0.75:                                            # feet planted once she has landed
                for S, _ in SIDES: K.foot(S[0], ANKG[S])
        key(f)
    print("ACTION", name, n, "quadros")

if __name__ == "__main__":
    clip_parado()
    clip_flutuar("Flutuar", 96, 0.0)
    clip_flutuar("Flutuar_Frente", 64, 1.0)
    clip_transition("Comecar_Flutuar", 45, True)
    clip_transition("Pousar", 40, False)
    for a in ("Parado", "Flutuar", "Flutuar_Frente"): print("ACTION", a, int(bpy.data.actions[a].frame_range[1]), "quadros")
    arm.animation_data.action = bpy.data.actions["Flutuar"]
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
