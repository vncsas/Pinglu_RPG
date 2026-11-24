# Exports every animation (action with fake user) of animacoes.blend to its own FBX for Unity:
# Armature + Body, deform bones only, no leaf bones, baked, one take named after the action.
# Also saves the final animacoes.blend (with the active action = Parado) next to the FBX files, and
# optionally one .blend per clip for scripts/anim_verify.py.
# usage: blender -b animacoes_fix.blend -P export_anims.py -- <out_dir> [<verify_dir>]
import bpy, sys, os
args = sys.argv[sys.argv.index("--")+1:]; out = args[0]; vdir = args[1] if len(args) > 1 else None
os.makedirs(out, exist_ok=True)
arm = bpy.data.objects["Armature"]; body = bpy.data.objects["Body"]; sc = bpy.context.scene
acts = sorted([a for a in bpy.data.actions if a.use_fake_user], key=lambda a: a.name)
for a in acts:
    arm.animation_data.action = a
    if arm.animation_data.action_slot is None and a.slots: arm.animation_data.action_slot = a.slots[0]
    f0, f1 = (int(x) for x in a.frame_range); sc.frame_start, sc.frame_end = f0, f1
    sc.name = a.name                                              # FBX take name = clip name in Unity
    bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, f"curandeiro@{a.name}.fbx"), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        use_armature_deform_only=True, add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        armature_nodetype='NULL', mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    if vdir:
        os.makedirs(vdir, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(vdir, a.name+".blend"), copy=True)
    print("EXPORT", a.name, f1-f0+1, "quadros")
sc.name = "Scene"
arm.animation_data.action = bpy.data.actions["Parado"]; sc.frame_start, sc.frame_end = 1, int(bpy.data.actions["Parado"].frame_range[1])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "animacoes.blend"))
