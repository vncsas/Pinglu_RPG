# Animations for the stone golem (mini-boss) on the retargeted Quaternius mocap (anim_rt.blend from retarget.py):
#   mocap, slowed down for the weight: Parado, Andar, Correr, Levar_Golpe, Levar_Golpe_Forte, Morrer, Soco_Direto, Varrer
#   made by hand (fightkit key poses): Postura_Luta (loop), Esmagar (both fists overhead, slam), Pisada (stomp),
#   Rugir (roar + chest pounding), Soco_Pesado (wide haymaker), Atordoado (stunned, swaying)
# Hips are lifted wherever the body (not the hands) would go under the floor.
# usage: blender -b anim_rt.blend -P golem_anim.py -- <scripts_dir> <out.blend>      (env POSETEST=pose,pose POSEOUT=dir renders key poses)
import bpy, sys, math, os
import numpy as np
from mathutils import Matrix, Vector
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit, smooth
from fightkit import Fight, ease_out, ease_in, ease_io, back_out
arm = bpy.data.objects["Armature"]; K = Kit(arm); P = arm.pose.bones; sc = bpy.context.scene; body = bpy.data.objects["Body"]
vgn = {g.index: g.name for g in body.vertex_groups}
core = np.array([sum(g.weight for g in v.groups if vgn[g.group] in ("LeftHand", "RightHand")) < 0.5 for v in body.data.vertices])   # hands may touch the floor
def set_action(a):
    arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
def body_zmin():
    dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh()
    c = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", c); z = c.reshape(-1, 3)[:, 2]; be.to_mesh_clear()
    return float(z[core].min())
def floor_lift(settle=0.0):
    z = body_zmin(); dz = -z if z < 0 else -z*settle
    if abs(dz) > 1e-4:
        m = P["Hips"].matrix.copy(); m.translation.z += dz; P["Hips"].matrix = m; K.upd()
        P["Hips"].keyframe_insert("location", frame=sc.frame_current)
def new_action(name, n):
    a = bpy.data.actions.get(name)
    if a: bpy.data.actions.remove(a)
    a = bpy.data.actions.new(name); a.use_fake_user = True; set_action(a)
    root = P["CTRL_Root"]
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"):
        root[k] = 0.0; root.keyframe_insert(f'["{k}"]', frame=1)
    return a
def slow(name, k):
    """stretch the clip in time by k (heavier, slower)"""
    a = bpy.data.actions[name]
    for fc in a.fcurves if hasattr(a, "fcurves") else []:
        for kp in fc.keyframe_points:
            kp.co.x = 1+(kp.co.x-1)*k; kp.handle_left.x = 1+(kp.handle_left.x-1)*k; kp.handle_right.x = 1+(kp.handle_right.x-1)*k
        fc.update()
def action_fcurves(a):
    try: return list(a.fcurves)
    except Exception:
        return [fc for l in a.layers for st in l.strips for cb in st.channelbags for fc in cb.fcurves]
def stretch(name, k):
    a = bpy.data.actions[name]
    for fc in action_fcurves(a):
        for kp in fc.keyframe_points:
            kp.co.x = 1+(kp.co.x-1)*k; kp.handle_left.x = 1+(kp.handle_left.x-1)*k; kp.handle_right.x = 1+(kp.handle_right.x-1)*k
        fc.update()

# ---------------------------------------------------------------- mocap clips: slower, floor contact
if not os.environ.get("POSETEST"):
    for name, k in (("Parado", 1.25), ("Andar", 1.6), ("Correr", 1.35), ("Levar_Golpe", 1.4), ("Levar_Golpe_Forte", 1.4), ("Morrer", 1.5), ("Soco_Direto", 1.6), ("Varrer", 1.9)):
        stretch(name, k)
    for name in ("Parado", "Andar", "Correr", "Levar_Golpe", "Levar_Golpe_Forte", "Soco_Direto", "Varrer", "Morrer"):
        a = bpy.data.actions[name]; set_action(a); f0, f1 = (int(round(x)) for x in a.frame_range); n = f1-f0+1
        for f in range(f0, f1+1):
            sc.frame_set(f); K.upd(); u = (f-f0)/max(1, n-1)
            floor_lift(smooth((u-0.35)/0.3) if name == "Morrer" else 0.0)
        print("ACTION", name, n, "quadros")

# ---------------------------------------------------------------- hand-made key poses (hands measured from the chest centre)
K.reset(apose=True)
LF0, RF0 = P["LeftFoot"].head.copy(), P["RightFoot"].head.copy()
F = Fight(K, arm, sc, set_action, new_action, floor_lift, lambda: K.reset(apose=True))
def body_frame(yaw):
    f = Vector((math.sin(math.radians(yaw)), -math.cos(math.radians(yaw)), 0)); l = Vector((math.cos(math.radians(yaw)), math.sin(math.radians(yaw)), 0))
    return f, l, Vector((0, 0, 1))
def arm_to(S, off, elbow):
    f, l, u = body_frame(0.0)
    sh = P["UpperChest"].head.copy(); tgt = sh+f*off[0]+l*off[1]+u*off[2]; pole = f*elbow[0]+l*elbow[1]+u*elbow[2]       # K.two_bone wants a DIRECTION for the elbow, not a point
    K.two_bone(S+"UpperArm", S+"LowerArm", tgt, pole)
    d = tgt-P[S+"LowerArm"].head
    if d.length > 1e-4: K.aim(S+"Hand", d)
HANG = (0.0, 0.75, -1.25)
CURL_SIGN = float(os.environ.get("CURL_SIGN", "1"))
def pose(drop=0.06, hyaw=0, syaw=0, cyaw=0, lean=4, hfwd=0.0, head=0, chest_lean=0, lf=(0, 0, 0), rf=(0, 0, 0), lpitch=0, rpitch=0,
         L=(0.72, 0.34, 0.16), R=(0.72, -0.34, 0.16), Lel=(0.1, 0.55, -1), Rel=(0.1, -0.55, -1), curl=(30, 30)):          # default = high boxing guard; curl = wrist flexion (fist cocked)
    K.hips(drop=drop, fwd=hfwd, yaw=hyaw, lean=lean)
    K.rot_world("Spine", 'Z', syaw); K.rot_world("Chest", 'Z', cyaw); K.rot_world("Chest", 'X', chest_lean)
    K.rot_world("Head", 'Z', -(hyaw+syaw+cyaw)*0.7); K.rot_world("Head", 'X', -lean*0.6+head)
    K.foot('L', LF0+Vector(lf), pitch=lpitch); K.foot('R', RF0+Vector(rf), pitch=rpitch)
    arm_to("Left", L, Lel); arm_to("Right", R, Rel)
    for S, c_ in ("Left", curl[0]), ("Right", curl[1]): K.rot_local(S+"Hand", 'X', c_*CURL_SIGN)
cap = F.capture
cap("guard", pose)
cap("guardB", pose, drop=0.09, lean=7, L=(0.72, 0.34, 0.20), R=(0.72, -0.34, 0.20))
# slam: both fists overhead (leaning back), then down in front into the ground
cap("smash_up", pose, curl=(55, 55), drop=0.02, lean=-14, hfwd=-0.10, chest_lean=-12, head=8, L=(0.10, 0.52, 0.95), R=(0.10, -0.52, 0.95), Lel=(-0.4, 0.9, 0.2), Rel=(-0.4, -0.9, 0.2))
cap("smash_hit", pose, curl=(-10, -10), drop=0.34, lean=30, hfwd=0.22, chest_lean=16, head=-10, lf=(0, -0.22, 0), rf=(0, -0.05, 0),
    L=(1.00, 0.50, -1.12), R=(1.00, -0.50, -1.12), Lel=(0, 0.9, 0.3), Rel=(0, -0.9, 0.3))
cap("smash_dip", pose, curl=(60, 60), drop=0.08, lean=-6, hfwd=-0.04, chest_lean=-4, L=(0.20, 0.50, 0.70), R=(0.20, -0.50, 0.70), Lel=(-0.3, 0.9, 0.1), Rel=(-0.3, -0.9, 0.1))
cap("smash_follow", pose, curl=(0, 0), drop=0.30, lean=24, hfwd=0.18, chest_lean=12, head=-8, L=(0.95, 0.52, -1.05), R=(0.95, -0.52, -1.05), Lel=(0, 0.9, 0.3), Rel=(0, -0.9, 0.3))
# haymaker with the right arm: arm out and back, then across the front
cap("hay_coil", pose, curl=(30, 60), drop=0.14, hyaw=24, syaw=10, cyaw=6, lean=2, hfwd=-0.08, L=(0.72, 0.34, 0.16), R=(-0.35, -1.10, 0.05), Rel=(-0.6, -1.0, 0.5))
cap("hay_hit", pose, curl=(30, -15), drop=0.20, hyaw=-26, syaw=-12, cyaw=-8, lean=14, hfwd=0.14, rf=(0, -0.10, 0), L=(0.72, 0.34, 0.16), R=(1.05, 0.05, -0.05), Rel=(0, -0.7, -0.2))
# stomp: right knee up (foot high), then slam; arms out for balance
cap("stomp_up", pose, curl=(20, 20), drop=0.0, lean=-6, hyaw=-4, rf=(0, -0.18, 0.62), rpitch=-10, L=(0.30, 1.0, -0.45), R=(0.30, -1.0, -0.45), Lel=(-0.3, 1, -0.6), Rel=(-0.3, -1, -0.6))
cap("stomp_hit", pose, drop=0.20, lean=12, rf=(0, -0.28, 0.0), L=(0.55, 0.95, -0.55), R=(0.55, -0.95, -0.55), Lel=(-0.2, 1, -0.6), Rel=(-0.2, -1, -0.6))
# roar: chest out, head back, arms spread wide; then the fists pound the chest
cap("roar_open", pose, curl=(-25, -25), drop=0.0, lean=-16, chest_lean=-14, head=22, L=(0.15, 1.15, 0.45), R=(0.15, -1.15, 0.45), Lel=(-0.5, 1, 0.3), Rel=(-0.5, -1, 0.3))
cap("pound_in", pose, curl=(50, 50), drop=0.04, lean=-6, chest_lean=-8, head=10, L=(0.50, 0.30, -0.12), R=(0.50, -0.30, -0.12), Lel=(-0.2, 1, -0.2), Rel=(-0.2, -1, -0.2))
cap("pound_out", pose, curl=(-10, -10), drop=0.04, lean=-8, chest_lean=-10, head=14, L=(0.30, 0.80, 0.10), R=(0.30, -0.80, 0.10), Lel=(-0.3, 1, -0.1), Rel=(-0.3, -1, -0.1))
# stunned: head down, arms hanging, swaying
cap("stun_a", pose, drop=0.10, lean=14, head=-18, hyaw=-8, syaw=6, chest_lean=8, L=HANG, R=HANG, Lel=(-0.3, 1, -1), Rel=(-0.3, -1, -1))
cap("stun_b", pose, drop=0.12, lean=18, head=-22, hyaw=8, syaw=-6, chest_lean=10, L=(0.10, 0.80, -1.22), R=(0.10, -0.80, -1.22), Lel=(-0.3, 1, -1), Rel=(-0.3, -1, -1))

if os.environ.get("POSETEST"):
    from mathutils import Vector as V
    names = os.environ["POSETEST"].split(","); outd = os.environ["POSEOUT"]
    sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 520; sc.render.resolution_y = 700
    sc.view_settings.view_transform = 'AgX'; w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.35, 0.37, 0.42)
    for nm, loc, e in (("key", (-2.5, -3, 3), 900), ("fill", (3, -2.5, 1.5), 400), ("rim", (1, 3.5, 3), 900)):
        l = bpy.data.lights.new(nm, 'AREA'); l.energy = e; l.size = 3; o = bpy.data.objects.new(nm, l); sc.collection.objects.link(o)
        o.location = loc; o.rotation_euler = V((0, 0, 1)).rotation_difference(V(loc)-V((0, 0, 1))).to_euler()
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 50
    os.makedirs(outd, exist_ok=True); arm.animation_data.action = None
    for nme in names:
        F.apply(nme, nme, 0.0)
        for i, ang in enumerate((0, 50, 90)):
            a_ = math.radians(ang); d = 7.0; cam.location = (d*math.sin(a_), -d*math.cos(a_), 1.5)
            cam.rotation_euler = (V((0, 0, 1.2))-cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = f"{outd}/{nme}_{i}.png"; bpy.ops.render.render(write_still=True)
    sys.exit(0)

io, eo, ei, bo = ease_io, ease_out, ease_in, back_out
F.clip("Postura_Luta", 70, [(1, "guard", io), (35, "guardB", io), (70, "guard", io)], lag={"arms": 3, "head": 4})
F.clip("Esmagar", 70, [(1, "guard", io), (14, "smash_dip", ei), (30, "smash_up", io), (35, "smash_up", io), (40, "smash_hit", bo), (44, "smash_follow", io), (54, "smash_follow", io), (68, "guard", io), (70, "guard", io)], lag={"arms": 3, "head": 5})
F.clip("Soco_Pesado", 58, [(1, "guard", io), (10, "guard", io), (20, "hay_coil", ei), (26, "hay_hit", bo), (33, "hay_hit", io), (54, "guard", io), (58, "guard", io)], lag={"arms": 4, "head": 4})
F.clip("Pisada", 60, [(1, "guard", io), (20, "stomp_up", io), (27, "stomp_up", io), (31, "stomp_hit", bo), (40, "stomp_hit", io), (58, "guard", io), (60, "guard", io)], lag={"arms": 3, "head": 3})
F.clip("Rugir", 104, [(1, "guard", io), (16, "roar_open", io), (30, "roar_open", io), (38, "pound_in", eo), (46, "pound_out", io), (54, "pound_in", eo), (62, "pound_out", io),
                     (70, "pound_in", eo), (80, "roar_open", io), (88, "roar_open", io), (100, "guard", io), (104, "guard", io)], lag={"arms": 2, "head": 4})
F.clip("Atordoado", 90, [(1, "stun_a", io), (30, "stun_b", io), (60, "stun_a", io), (90, "stun_a", io)], lag={"arms": 5, "head": 8})
for x in bpy.data.actions:
    if "|" not in x.name: x.use_fake_user = True
set_action(bpy.data.actions["Parado"])
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("DONE")
