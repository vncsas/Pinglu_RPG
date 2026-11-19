# renderiza vistas com enquadramento fixo (centro C, escala 3.2) para sobrepor grade de coordenadas
import bpy, sys
from mathutils import Vector as V
blend = sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-angler/saida/" + blend)
m = bpy.data.objects["Angler"]
img = bpy.data.images.load("C:/Users/Thiago/Documentos/rig-angler/src/tripo_convert_4e8af36c-5356-44e1-a8a8-c2916d77b7a2.fbm/Zyklus8_Zeile7_basecolor.JPEG")
mat = m.data.materials[0]; mat.use_nodes = True; nt = mat.node_tree
if not any(n.type == 'TEX_IMAGE' for n in nt.nodes):
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    o = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]; bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = img
    nt.links.new(tx.outputs[0], bs.inputs['Base Color']); nt.links.new(bs.outputs[0], o.inputs[0])
for o in bpy.data.objects:
    if o.type == 'ARMATURE': o.hide_render = True
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 800; sc.render.resolution_y = 800
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.2
C = V((0, 0, 1.2))
for nome, d, up in (("lado", V((1, 0, 0)), 'Y'), ("frente", V((0, 1, 0)), 'Y')):
    cam.location = C - d * 8; cam.rotation_euler = d.to_track_quat('-Z', up).to_euler()
    sc.render.filepath = "C:/Users/Thiago/Documentos/rig-angler/saida/g_%s.png" % nome; bpy.ops.render.render(write_still=True)
