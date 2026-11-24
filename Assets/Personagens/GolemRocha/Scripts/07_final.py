# UV em caixa nas pecas de pedra (olhos mantem as proprias), 2 materiais (Pedra, Olhos) e exporta o FBX Generic
import bpy, os
base = "C:/Users/Thiago/Documentos/rig-golem/"; out = base + "saida/Golem/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/golem_anim.blend")
arm = bpy.data.objects["Golem_Rig"]; ms = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode != 'OBJECT' else None
for m in ms:
    if m.name == "Golem_Eyes": continue
    bpy.ops.object.select_all(action='DESELECT'); m.select_set(True); bpy.context.view_layer.objects.active = m
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=2.4, correct_aspect=True, clip_to_bounds=False, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
def img(nome, cs='sRGB'):
    i = bpy.data.images.load(out + nome); i.colorspace_settings.name = cs; return i
def material(nome, cor=None, emi=None, nrm=None, suav=None, forca_emi=1.0):
    mt = bpy.data.materials.new(nome); mt.use_nodes = True; nt = mt.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    o = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]; bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); nt.links.new(bs.outputs[0], o.inputs[0])
    def tex(nm, cs, y):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(nm, cs); t.location = (-500, y); t.extension = 'REPEAT'; return t
    if cor: nt.links.new(tex(cor, 'sRGB', 300).outputs[0], bs.inputs['Base Color'])
    if emi: nt.links.new(tex(emi, 'sRGB', 0).outputs[0], bs.inputs['Emission Color']); bs.inputs['Emission Strength'].default_value = forca_emi
    if nrm:
        nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(tex(nrm, 'Non-Color', -300).outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    if suav:
        inv = nt.nodes.new('ShaderNodeInvert'); nt.links.new(tex(suav, 'Non-Color', -600).outputs[0], inv.inputs['Color']); nt.links.new(inv.outputs[0], bs.inputs['Roughness'])
    return mt
pedra = material("Golem_Pedra", "Golem_cor.jpg", "Golem_emissivo.jpg", "Golem_normal.png", "Golem_suavidade.png", 2.0)
olhos = material("Golem_Olhos", None, "Golem_olhos_emissivo.png", "Golem_olhos_normal.png", None, 3.0)
for m in ms:
    m.data.materials.clear(); m.data.materials.append(olhos if m.name == "Golem_Eyes" else pedra)
for a in bpy.data.actions: a.use_fake_user = True
arm.animation_data.action = None
for p in arm.pose.bones: p.rotation_quaternion = (1, 0, 0, 0); p.location = (0, 0, 0)
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/golem_final.blend")
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True)
for m in ms: m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out + "Golem.fbx", use_selection=True, object_types={'ARMATURE', 'MESH'}, apply_scale_options='FBX_SCALE_ALL',
    path_mode='STRIP', embed_textures=False, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=True)
print("exportado", sorted(os.listdir(out)))
