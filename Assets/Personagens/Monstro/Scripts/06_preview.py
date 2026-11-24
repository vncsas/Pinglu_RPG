# uso: blender -b -P 06_preview.py -- blend pasta [Acao n_quadros]   (sem Acao: so poses estaticas de 3 vistas)
import bpy, sys, os
from mathutils import Vector as V
a = sys.argv[sys.argv.index("--")+1:]; blend, out = a[0], a[1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-pantano/saida/" + blend)
arm = bpy.data.objects["MonstroArmature"]; mesh = bpy.data.objects["Monstro"]
if mesh.data.materials: pass
img = bpy.data.images.load("C:/Users/Thiago/Documentos/swamp-monster/textures/FOREST_MONSTER_V1_mat_Flattened_Diffuse.png")
mat = mesh.data.materials[0]; mat.use_nodes = True; nt = mat.node_tree
if not any(n.type == 'TEX_IMAGE' for n in nt.nodes):
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    o = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]; bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = img
    nt.links.new(tx.outputs[0], bs.inputs['Base Color']); nt.links.new(bs.outputs[0], o.inputs[0])
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 520; sc.render.resolution_y = 620
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'; arm.hide_render = True
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.0
vistas = (("lado", V((1, 0, 0))), ("frente", V((0, 1, 0))))
c = V((0, 0, 1.15))
def shot(nome):
    for vn, d in vistas:
        cam.location = c - d * 6; cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (nome, vn)); bpy.ops.render.render(write_still=True)
if len(a) > 2:
    ac = bpy.data.actions[a[2]]; n = int(a[3]); arm.animation_data.action = ac; f0, f1 = int(ac.frame_range[0]), int(ac.frame_range[1])
    for i in range(n):
        sc.frame_set(int(round(f0 + (f1 - f0) * i / n))); shot("%s_%02d" % (a[2], i))
else: shot("pose")
