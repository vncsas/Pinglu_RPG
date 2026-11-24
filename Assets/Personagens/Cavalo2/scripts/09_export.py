# Exporta Cavalo2.fbx (malha + esqueleto + 7 clipes) e a textura para saida/Cavalo2/
import bpy, os, shutil
base = "C:/Users/Thiago/Documentos/rig-cavalo2/"
out = base + "saida/Cavalo2/"; os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_anim.blend")
arm = bpy.data.objects["CavaloArmature"]; mesh = bpy.data.objects["Cavalo2"]
arm.name = "Cavalo2_Rig"; arm.data.name = "Cavalo2_Rig"; mesh.name = "Cavalo2_Corpo"
for a in bpy.data.actions: a.use_fake_user = True
arm.animation_data.action = None      # esqueleto em pose neutra (T = em pe) no FBX; as acoes sao exportadas pelo bake_anim_use_all_actions
for b in arm.pose.bones: b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0); b.scale = (1, 1, 1)
bpy.context.view_layer.update()
# textura ao lado do fbx
shutil.copy(base + "saida/Cavalo2_cor.jpg", out + "Cavalo2_cor.jpg"); shutil.copy(base + "saida/Cavalo2_normal.jpg", out + "Cavalo2_normal.jpg")
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out + "Cavalo2.fbx", use_selection=True, object_types={'ARMATURE', 'MESH'}, apply_scale_options='FBX_SCALE_ALL',
    path_mode='COPY', embed_textures=False, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=True)
print("exportado", os.listdir(out))
