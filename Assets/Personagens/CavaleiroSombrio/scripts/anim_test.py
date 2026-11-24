# End-to-end animation test, done the way an animator would:
#   1. animate a walk cycle in cavaleiro_sombrio_rig.blend using only the controls (IK feet, Hips, FK arms)
#   2. export the animation FBX with the settings from the LEIAME
# usage: blender -b cavaleiro_sombrio_rig.blend -P anim_test.py -- <out_dir>
import bpy, sys, math
from mathutils import Matrix, Vector
out = sys.argv[-1]; WITH_MESH = __import__("os").environ.get("WITH_MESH")
arm = bpy.data.objects["Armature"]; P = arm.pose.bones; A = arm.data
sc = bpy.context.scene; sc.frame_start, sc.frame_end = 1, 25; sc.render.fps = 30
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='POSE')
arm.animation_data_create(); arm.animation_data.action = bpy.data.actions.new("Teste_Caminhada")
root = P["CTRL_Root"]
root["ik_perna_Left"] = root["ik_perna_Right"] = 1.0; root["ik_braco_Left"] = root["ik_braco_Right"] = 0.0

def key(pb, *paths):
    for p in paths: pb.keyframe_insert(p, frame=sc.frame_current)

def aim_rot(n, d):
    """FK: rotate bone n to point along world direction d and return its basis rotation"""
    pb = P[n]; bpy.context.view_layer.update()
    h = pb.head.copy(); q = (pb.tail-pb.head).normalized().rotation_difference(Vector(d).normalized())
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix
    bpy.context.view_layer.update()

STEP, LIFT = 0.22, 0.10
# phase 0..1 for the left foot; right foot is shifted by half a cycle
def foot_at(ph):
    # stance (0..0.5): foot slides from front to back on the ground; swing (0.5..1): lifts and comes forward
    if ph < 0.5: y = STEP*(1-4*ph); z = 0.0
    else:
        s = (ph-0.5)*2; y = -STEP+2*STEP*s; z = LIFT*math.sin(math.pi*s)
    return y, z
for f in (1, 4, 7, 10, 13, 16, 19, 22, 25):
    sc.frame_set(f); ph = ((f-1)/24.0) % 1.0
    for S, off in (("Left", 0.0), ("Right", 0.5)):
        y, z = foot_at((ph+off) % 1.0)
        P["IK_Foot_"+S].location = (0, y, z)          # IK_Foot: local Y = forward, Z = up
        key(P["IK_Foot_"+S], "location")
    P["Hips"].location = (0, -0.05+0.025*abs(math.cos(2*math.pi*ph)) * 0 - 0.03*abs(math.cos(2*math.pi*ph)) , 0)
    key(P["Hips"], "location")
    # FK arms: hang down and swing opposite to the legs 
    sw = math.sin(2*math.pi*ph)
    for n in ("LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand"):
        P[n].rotation_quaternion = (1, 0, 0, 0)
    aim_rot("RightUpperArm", (-0.2, 0.35*sw, -0.92)); aim_rot("RightLowerArm", (-0.12, -0.35+0.25*sw, -0.9))
    aim_rot("LeftUpperArm", (0.35, -0.15*sw-0.05, -0.92)); aim_rot("LeftLowerArm", (0.15, -0.3-0.1*sw, -0.92))
    for n in ("LeftUpperArm", "LeftLowerArm", "RightUpperArm", "RightLowerArm"): key(P[n], "rotation_quaternion")
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=out+"/teste_caminhada.blend")

# export exactly as documented: armature only, deform bones only, no leaf bones, baked animation
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
if WITH_MESH:
    for n in ('Body','Sword'): bpy.data.objects[n].select_set(True)
bpy.ops.export_scene.fbx(filepath=out+("/teste_caminhada_com_malha.fbx" if WITH_MESH else "/teste_caminhada.fbx"), use_selection=True, object_types={'ARMATURE','MESH'} if WITH_MESH else {'ARMATURE'},
    apply_scale_options='FBX_SCALE_ALL', use_armature_deform_only=True, add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
print("ANIM exported")
