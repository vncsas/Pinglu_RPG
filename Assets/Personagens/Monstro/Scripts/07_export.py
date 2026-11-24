# Exporta Monstro.fbx (malha + esqueleto + 5 clipes) para saida/Monstro/
import bpy, os
base = "C:/Users/Thiago/Documentos/rig-pantano/"; out = base + "saida/Monstro/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_anim.blend")
arm = bpy.data.objects["MonstroArmature"]; mesh = bpy.data.objects["Monstro"]
arm.name = "Monstro_Rig"; arm.data.name = "Monstro_Rig"; mesh.name = "Monstro_Corpo"
for a in bpy.data.actions: a.use_fake_user = True
arm.animation_data.action = None
for b in arm.pose.bones: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0); b.scale = (1, 1, 1)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out + "Monstro.fbx", use_selection=True, object_types={'ARMATURE', 'MESH'}, apply_scale_options='FBX_SCALE_ALL',
    path_mode='COPY', embed_textures=False, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=True)
print("exportado", os.listdir(out))
