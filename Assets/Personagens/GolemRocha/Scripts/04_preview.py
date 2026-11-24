# uso: blender -b -P 04_preview.py -- blend pasta [POSE|Acao n]
import bpy, sys, os
sys.path.insert(0, "C:/Users/Thiago/Documentos/rig-golem/scripts")
from mathutils import Vector as V
import gutil as G
a = sys.argv[sys.argv.index("--") + 1:]; blend, out = a[0], a[1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-golem/saida/" + blend)
arm = bpy.data.objects["Golem_Rig"]; sc = bpy.context.scene
T = "C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/textures/"
tex = os.environ.get("GOLEM_TEX") or (T + "CHARACTER_ROCK_BAJA_ZREMESHER_1_BaseColor..png")
img = bpy.data.images.load(tex)
for m in [o for o in bpy.data.objects if o.type == 'MESH']:
    mat = m.data.materials[0]; mat.use_nodes = True; nt = mat.node_tree
    if any(n.type == 'TEX_IMAGE' for n in nt.nodes): continue
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL': nt.nodes.remove(n)
    o = [n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'][0]; bs = nt.nodes.new('ShaderNodeBsdfPrincipled'); tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = img
    nt.links.new(tx.outputs[0], bs.inputs['Base Color']); nt.links.new(bs.outputs[0], o.inputs[0])
arm.hide_render = True
sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 560; sc.render.resolution_y = 560; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'RANDOM'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.75, 0.78, 0.85)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.6
C = V((0, -0.1, 1.2))
def shot(nome):
    for vn, d in (("frente", V((0, 1, 0))), ("lado", V((1, 0, 0)))):
        cam.location = C - d * 8; cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(out, "%s_%s.png" % (nome, vn)); bpy.ops.render.render(write_still=True)
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
if a[2] == "POSE":
    G.posar(arm, {"CC_Base_L_Upperarm": [("x", -50)], "CC_Base_R_Upperarm": [("y", 0), ("x", 35)], "CC_Base_L_Forearm": [("x", -40)],
                  "CC_Base_L_Thigh": [("x", -30)], "CC_Base_L_Calf": [("x", 45)], "CC_Base_R_Thigh": [("x", 25)],
                  "CC_Base_Spine01": [("z", 20)], "CC_Base_Spine02": [("z", 15)], "CC_Base_Head": [("z", 30)], "CC_Base_L_Clavicle": [("x", -10)]})
    bpy.context.view_layer.update(); shot("pose")
else:
    ac = bpy.data.actions[a[2]]; n = int(a[3]); arm.animation_data.action = ac; f0, f1 = int(ac.frame_range[0]), int(ac.frame_range[1])
    for i in range(n):
        sc.frame_set(int(round(f0 + (f1 - f0) * i / max(1, (n - 1) if len(a) > 4 else n)))); shot("%s_%02d" % (a[2], i))
