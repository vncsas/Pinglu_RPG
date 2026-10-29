# Test poses for the dark knight rig: feet planted with analytic IK, arms placed by targets,
# the greatsword driven through the right hand, left hand closing on the handle with the finger bones.
# usage: blender -b cavaleiro_sombrio_rig.blend -P poses.py -- <out_dir> <pose,pose,...> '[0,90]'
#   env CLOSE=1 -> close-up camera on the hands
import bpy, sys, os, math, json
from mathutils import Matrix, Vector, Quaternion
args = sys.argv[sys.argv.index("--")+1:]
outdir, which, views = args[0], args[1].split(','), json.loads(args[2])
arm = bpy.data.objects["Armature"]; P = arm.pose.bones; B = arm.data.bones
sword = bpy.data.objects["Sword"]
def upd(): bpy.context.view_layer.update()
ROOT = P["CTRL_Root"]
AD = arm["apose_dirs"]
FINGERS = ["Thumb", "Index", "Middle", "Ring", "Little"]; PH = ["Proximal", "Intermediate", "Distal"]
LIMBS = [b.name for b in B if b.use_deform and any(k in b.name for k in ("UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes"))
         and "Twist" not in b.name and "Weapon" not in b.name]

def socket(o, bone):
    o.parent_bone = bone; upd(); o.matrix_world = arm.matrix_world @ P[bone].matrix; upd()
def reset(apose=True):
    for k in list(ROOT.keys()):
        if k.startswith("ik_"): ROOT[k] = 0.0
    for pb in P: pb.matrix_basis = Matrix.Identity(4)
    socket(sword, "RightHandWeapon"); upd()
    if apose:
        for n in LIMBS: aim(n, AD[n])
def rot_world(n, ax, deg):
    pb = P[n]; h = pb.head.copy(); R = Matrix.Rotation(math.radians(deg), 4, ax)
    pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix; upd()
def rot_local(n, ax, deg):
    pb = P[n]; pb.matrix_basis = pb.matrix_basis @ Matrix.Rotation(math.radians(deg), 4, ax); upd()
def aim(n, d, twist=0):
    pb = P[n]; d = Vector(d).normalized(); h = pb.head.copy()
    q = (pb.tail-pb.head).normalized().rotation_difference(d); q = Quaternion(d, math.radians(twist)) @ q
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix; upd()
def hips(drop=0, fwd=0, side=0, yaw=0, lean=0):
    pb = P['Hips']; m = pb.matrix.copy(); m.translation += Vector((side, fwd, -drop)); pb.matrix = m; upd()
    if yaw: rot_world('Hips', 'Z', yaw)
    if lean: rot_world('Hips', 'X', lean)
def two_bone(upn, lon, target, pole):
    """aim upper/lower bones so the lower bone's tail lands on target, bending toward pole"""
    up, lo = P[upn], P[lon]
    l1 = (B[upn].tail_local-B[upn].head_local).length; l2 = (B[lon].tail_local-B[lon].head_local).length
    H = up.head.copy(); T = Vector(target)
    d = min((T-H).length, (l1+l2)*0.999); n = (T-H).normalized(); T = H+n*d
    a = (l1*l1-l2*l2+d*d)/(2*d); h = math.sqrt(max(l1*l1-a*a, 0))
    pole = Vector(pole); pole = (pole-n*pole.dot(n)).normalized()
    K = H+n*a+pole*h
    aim(upn, K-up.head); aim(lon, T-lo.head)
def foot(side, pos, pitch=0, yaw=0):
    """ankle to pos with the knee over the toes; pitch>0 lifts the heel (pivot on the ball),
    pitch<0 lifts the toes (pivot on the heel); never under the floor"""
    S = 'Left' if side == 'L' else 'Right'
    ft, to = P[S+'Foot'], P[S+'Toes']
    ank_r, ball_r = B[S+'Foot'].head_local, B[S+'Toes'].head_local
    heel_r = Vector((ank_r.x, ank_r.y+0.07, ball_r.z))
    Ry = Matrix.Rotation(math.radians(yaw), 3, 'Z'); Rrel = Ry @ Matrix.Rotation(math.radians(pitch), 3, 'X')
    zc = max(heel_r.z-(Rrel @ (heel_r-ank_r)).z, ball_r.z-(Rrel @ (ball_r-ank_r)).z)
    pos = Vector(pos); pos.z = max(pos.z, zc)
    fwd = P["Hips"].matrix.to_3x3() @ Vector((0, 0, 1)); fwd = Vector((fwd.x, fwd.y, 0)).normalized()
    fwd = (fwd+Ry @ Vector((0, -1, 0))).normalized()
    two_bone(S+'UpperLeg', S+'LowerLeg', pos, fwd)
    m = (Rrel @ B[S+'Foot'].matrix_local.to_3x3()).to_4x4(); m.translation = ft.head.copy(); ft.matrix = m; upd()
    Rt = Ry if pitch > 0 else Rrel
    m = (Rt @ B[S+'Toes'].matrix_local.to_3x3()).to_4x4(); m.translation = to.head.copy(); to.matrix = m; upd()
def hand_to(S, M_hand, pole):
    """put the hand bone at matrix M_hand (armature space) with the arm solved to reach it"""
    two_bone(S+"UpperArm", S+"LowerArm", M_hand.translation, pole)
    m = M_hand.copy(); m.translation = P[S+"Hand"].head.copy(); P[S+"Hand"].matrix = m; upd()
def sword_to(grip, blade_dir, flat=(0, 0, 1), pole=(-0.3, 0.6, -0.5)):
    """right hand placed so the sword grip is at `grip` and the blade points along blade_dir"""
    y = Vector(blade_dir).normalized(); z = Vector(flat); z = (z-y*z.dot(y)).normalized(); x = y.cross(z)
    Msock = Matrix((x, y, z)).transposed().to_4x4(); Msock.translation = Vector(grip)
    rel = B["RightHand"].matrix_local.inverted() @ B["RightHandWeapon"].matrix_local
    hand_to("Right", Msock @ rel.inverted(), pole)
def left_on_handle(offset, pole=(0.3, 0.6, -0.5)):
    """left hand closes on the sword handle `offset` metres below the right hand: the right hand's
    frame mirrored across the plane holding the sword axis and the back of the right hand"""
    Ms = arm.matrix_world.inverted() @ sword.matrix_world
    ax = (Ms.to_3x3() @ Vector((0, 1, 0))).normalized(); grip = Ms.translation
    knuckles = sum((P[f"Right{f}Proximal"].head for f in FINGERS[1:]), Vector())/4
    n = knuckles-grip; n = (n-ax*n.dot(ax)).normalized(); b = ax.cross(n).normalized()
    Refl = Matrix.Identity(3)-2*Matrix((b*b.x, b*b.y, b*b.z))
    Mr = P["RightHand"].matrix
    R = Refl @ Mr.to_3x3() @ Matrix.Diagonal((-1, 1, 1))
    M = R.to_4x4(); M.translation = grip-ax*offset+Refl @ (Mr.translation-grip)
    hand_to("Left", M, pole); fist("Left", 1.0)
def fist(S, amount):
    """curl the fingers (rotation about each bone's local X)"""
    for fn in FINGERS:
        for k, ph in enumerate(PH):
            deg = (55 if fn == "Thumb" and k == 0 else 70)*amount if fn != "Thumb" else (25, 35, 35)[k]*amount
            rot_local(f"{S}{fn}{ph}", 'X', deg)
def open_hand(S):
    for fn in FINGERS:
        for k, ph in enumerate(PH):
            pb = P[f"{S}{fn}{ph}"]; par = pb.parent
            aim(pb.name, (par.tail-par.head) if k > 0 else (P[S+"Hand"].tail-P[S+"Hand"].head)) if fn != "Thumb" else None

def pose_tpose(): reset(apose=False)
def pose_parado():
    hips(drop=0.02)
    foot('L', (0.17, 0.0, 0.095), yaw=12); foot('R', (-0.15, 0.05, 0.095), yaw=-12)
    rot_world('Head', 'Z', -6)
def pose_caminhada():
    hips(drop=0.05, fwd=-0.05, yaw=-6)
    rot_world('Chest', 'Z', 8)
    foot('L', (0.10, -0.25, 0.10), pitch=-15)          # front foot, heel strike
    foot('R', (-0.10, 0.42, 0.12), pitch=30)           # back foot, pushing off the toes
    aim('LeftUpperArm', (0.2, 0.35, -0.92)); aim('LeftLowerArm', (0.12, 0.05, -1))
    aim('RightUpperArm', (-0.2, -0.3, -0.93)); aim('RightLowerArm', (-0.15, -0.55, -0.82))
def pose_guarda():
    hips(drop=0.12, yaw=-15); rot_world('Chest', 'Z', 10)
    foot('L', (0.22, -0.25, 0.10), yaw=5); foot('R', (-0.20, 0.35, 0.10), yaw=-35)
    sword_to((0.0, -0.40, 1.05), (0.15, -0.55, 0.82))
    left_on_handle(0.18)
    rot_world('Head', 'Z', 10)
def pose_ataque():
    hips(drop=0.16, fwd=-0.08, yaw=20); rot_world('Spine', 'X', -10); rot_world('Chest', 'Z', 15)
    foot('L', (0.20, -0.40, 0.10), yaw=10); foot('R', (-0.20, 0.45, 0.12), pitch=25, yaw=-25)
    sword_to((0.05, -0.15, 1.85), (0.10, 0.75, 0.65), flat=(1, 0, 0), pole=(-0.6, 0.3, -0.4))
    left_on_handle(0.18, pole=(0.6, 0.3, -0.4))
def pose_guardado():
    pose_parado(); socket(sword, "SwordBack")
def pose_dedos():
    reset(apose=False)
    socket(sword, "SwordBack")
    aim('LeftUpperArm', (0.7, -0.5, 0.1)); aim('LeftLowerArm', (0.1, -1, 0.3))
    aim('RightUpperArm', (-0.7, -0.5, 0.1)); aim('RightLowerArm', (-0.1, -1, 0.3))
    fist("Left", 1.0)                      # left: closed fist
    for k, ph in enumerate(PH):            # right: opens the sword grip
        for fn in FINGERS[1:]: rot_local(f"Right{fn}{ph}", "X", -(40, 50, 30)[k])
    for k, ph in enumerate(PH): rot_local(f"RightThumb{ph}", 'X', -(15, 25, 25)[k])
def pose_ik():
    reset(apose=False)
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"): ROOT[k] = 1.0
    P["Hips"].location = (0, -0.12, 0)
    P["IK_Foot_Left"].location = (0, -0.25, 0.0); P["IK_Foot_Right"].location = (0, 0.25, 0.0)
    P["RF_Ball_Right"].rotation_euler = (math.radians(35), 0, 0)
    P["IK_Hand_Left"].location = (0.25, -0.35, -0.35); P["IK_Hand_Right"].location = (-0.45, -0.25, -0.35)
    upd()
def pose_torcao():
    reset(apose=False)
    for S in ("Left", "Right"):
        pb = P[S+"Hand"]; ax = (pb.tail-pb.head).normalized(); h = pb.head.copy()
        R = Matrix.Rotation(math.radians(90), 4, ax); pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix; upd()
POSES = dict(tpose=pose_tpose, parado=pose_parado, caminhada=pose_caminhada, guarda=pose_guarda, ataque=pose_ataque,
             guardado=pose_guardado, dedos=pose_dedos, ik=pose_ik, torcao=pose_torcao)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 800; sc.render.resolution_y = 1000
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = -0.3
w = bpy.data.worlds.new("w"); sc.world = w
bg = w.node_tree.nodes.get("Background") if w.node_tree else None
if bg: bg.inputs[0].default_value = (0.35, 0.37, 0.42, 1)
else: w.color = (0.35, 0.37, 0.42)
def light(name, loc, energy, size):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = energy; l.size = size
    o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.location = loc
    o.rotation_euler = (Vector((0, 0, 1)).rotation_difference(Vector(loc)-Vector((0, 0, 1.0)))).to_euler()
light("key", (-2.5, -3, 3), 600, 3); light("fill", (3, -2.5, 1.5), 200, 3); light("rim", (1, 3.5, 3), 600, 2)
bpy.ops.mesh.primitive_plane_add(size=10); fl = bpy.context.object; m = bpy.data.materials.new("floor")
m.diffuse_color = (0.08, 0.08, 0.09, 1); fl.data.materials.append(m)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 50
CLOSE = os.environ.get('CLOSE')
def shoot(name, ang, dist=4.6, h=1.2, tz=1.0):
    if CLOSE: dist, h, tz = 1.8, 1.4, 1.2
    a = math.radians(ang); cam.location = (dist*math.sin(a), -dist*math.cos(a), h)
    cam.rotation_euler = (Vector((0, 0, tz))-cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = f"{outdir}/{name}.png"; bpy.ops.render.render(write_still=True)
def check(name):
    import numpy as np
    dg = bpy.context.evaluated_depsgraph_get(); b = bpy.data.objects["Body"].evaluated_get(dg); me = b.to_mesh()
    z = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", z); z = z.reshape(-1, 3)
    zmin = (b.matrix_world @ Vector(z[np.argmin(z[:, 2])])).z; b.to_mesh_clear()
    out = [f"floor_min={zmin:+.3f}"]
    for S in ("Left", "Right"):
        up, lo, ft, to = P[S+"UpperLeg"], P[S+"LowerLeg"], P[S+"Foot"], P[S+"Toes"]
        knee = lo.head; mid = (up.head+lo.tail)/2; kd = knee-mid; kd.z = 0; fd = to.tail-ft.head; fd.z = 0
        ang = math.degrees(kd.angle(fd)) if kd.length > 0.01 and fd.length > 0.01 else 0.0
        out.append(f"{S}: knee-vs-toes {ang:4.0f}deg, toe z {to.tail.z:+.3f}")
    print("CHECK", name, " | ".join(out), flush=True)
os.makedirs(outdir, exist_ok=True)
for p in which:
    reset(); POSES[p](); check(p)
    for i, a in enumerate(views): shoot(f"{p}_{i}", a)
