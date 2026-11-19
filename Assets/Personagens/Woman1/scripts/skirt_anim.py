# Bakes the skirt bones (Skirt_FL/FR/SL/SR/BL/BR) into every clip from the swing of the thighs:
#   front panel lifts forward when its leg goes forward, back panel lifts backward when its leg goes back,
#   side panel opens with the leg (abduction) and a little with the forward swing.
# usage: blender -b anim_final.blend -P skirt_anim.py -- <scripts_dir> <out.blend>
import bpy, sys, math
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit
arm = bpy.data.objects["Armature"]; K = Kit(arm); P = arm.pose.bones; sc = bpy.context.scene
SK = ["Skirt_FL", "Skirt_FR", "Skirt_SFL", "Skirt_SFR", "Skirt_SBL", "Skirt_SBR", "Skirt_BL", "Skirt_BR"]
def clamp(x, a, b): return max(a, min(b, x))
def leg_angles(S, sx):
    t = P[S+"UpperLeg"].tail-P[S+"UpperLeg"].head
    pitch = math.degrees(math.atan2(-t.y, -t.z))                    # + = thigh forward (front is -Y)
    abd = math.degrees(math.atan2(sx*t.x, -t.z))                   # + = thigh outward
    return pitch, abd
def pose_skirt():
    for n in SK: P[n].matrix_basis = Matrix.Identity(4)
    K.upd()
    for S, sx, f, sf, sb, b in (("Left", 1, "Skirt_FL", "Skirt_SFL", "Skirt_SBL", "Skirt_BL"), ("Right", -1, "Skirt_FR", "Skirt_SFR", "Skirt_SBR", "Skirt_BR")):
        pitch, abd = leg_angles(S, sx)
        fw = clamp(pitch*1.25-1.0, 0.0, 85.0); bk = clamp(-pitch*1.2-1.0, 0.0, 55.0); op = clamp(abd*1.25-1.0, 0.0, 45.0)
        if fw > 0.01: K.rot_world(f, 'X', -fw); K.rot_world(sf, 'X', -fw*0.6)         # hem forward/up
        if bk > 0.01: K.rot_world(b, 'X', bk); K.rot_world(sb, 'X', bk*0.6)            # hem backward/up
        ang = -sx*op
        if abs(ang) > 0.01: K.rot_world(sf, 'Y', ang*0.9); K.rot_world(sb, 'Y', ang*0.9)   # hem outward
    return {n: P[n].matrix_basis.to_quaternion() for n in SK}
acts = [a for a in bpy.data.actions if a.use_fake_user]
for a in acts:
    arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
    f0, f1 = (int(x) for x in a.frame_range); keys = {}
    for f in range(f0, f1+1):
        sc.frame_set(f); keys[f] = pose_skirt()
    for f in range(f0, f1+1):
        for n in SK:
            P[n].rotation_mode = 'QUATERNION'; P[n].rotation_quaternion = keys[f][n]; P[n].keyframe_insert("rotation_quaternion", frame=f)
    print("SKIRT", a.name, f1-f0+1)
arm.animation_data.action = bpy.data.actions["Parado"]
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("DONE")
