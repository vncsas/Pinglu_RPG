# uso: blender -b -P 07_preview.py -- blend pasta_abs Acao n   |   blend pasta_abs POSE   (pose de teste)
import bpy, sys, os, math
from mathutils import Vector as V
a = sys.argv[sys.argv.index("--")+1:]; blend, out = a[0], a[1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documents/rig-angler/saida/".replace("Documents","Documentos") + blend)
arm = bpy.data.objects["AnglerArmature"]; mesh = bpy.data.objects["Angler"]
img = bpy.data.images.load("C:/Users/Thiago/Documentos/rig-angler/src/tripo_convert_4e8af36c-5356-44e1-a8a8-c2916d77b7a2.fbm/Zyklus8_Zeile7_basecolor.JPEG")
mat = mesh.data.materials[0]; mat.use_nodes = True; nt = mat.node_tree
if not any(n.type == 'TEX_IMAGE' for n in nt.nodes):
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    o = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]; bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = img
    nt.links.new(tx.outputs[0], bs.inputs['Base Color']); nt.links.new(bs.outputs[0], o.inputs[0])
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 560; sc.render.resolution_y = 560
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'; arm.hide_render = True
sc.render.film_transparent = False; sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.75, 0.78, 0.85)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 6.0
vistas = (("lado", V((1, 0, 0))), ("frente", V((0, 1, 0))))
c = V((0, -0.5, 1.5))
def shot(nome):
    for vn, d in vistas:
        cam.location = c - d * 8; cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (nome, vn)); bpy.ops.render.render(write_still=True)
if a[2] == "POSE":
    bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE'); pb = arm.pose.bones; D = math.radians
    for b in pb: b.rotation_mode = 'XYZ'
    for n, r in (("L_Arm", (-50, 0, 0)), ("L_Forearm", (-40, 0, 0)), ("R_Arm", (0, 0, 50)), ("Jaw", (-35, 0, 0)), ("Neck", (15, 0, 20)), ("L_Thigh", (-35, 0, 0)), ("R_Thigh", (30, 0, 0)), ("Tail3", (0, 0, 40)), ("Tail5", (0, 0, 40)), ("L_Hand", (-30, 0, 0)), ("Spine2", (0, 0, 15))):
        pb[n].rotation_euler = tuple(D(x) for x in r)
    bpy.context.view_layer.update(); shot("pose")
else:
    ac = bpy.data.actions[a[2]]; n = int(a[3]); arm.animation_data.action = ac; f0, f1 = int(ac.frame_range[0]), int(ac.frame_range[1])
    for i in range(n):
        sc.frame_set(int(round(f0 + (f1 - f0) * i / max(1, n - 1 if a[4:5] == ["end"] else n)))); shot("%s_%02d" % (a[2], i))
