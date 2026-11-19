# close das patas: uso blender -b -P 10_zoom.py -- pasta Acao frame
import bpy, sys, os
from mathutils import Vector as V
args = sys.argv[sys.argv.index("--")+1:]; out, acao, fr = args[0], args[1], int(args[2]); os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-cavalo2/saida/rig_anim.blend")
arm = bpy.data.objects["CavaloArmature"]; arm.animation_data.action = bpy.data.actions[acao]; arm.hide_render = True
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 800; sc.render.resolution_y = 700
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'; sc.frame_set(fr)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
for nome, c, esc in (("lado", V((0.0, -0.4, 0.85)), 1.8),):
    cam.data.ortho_scale = esc; d = V((-1, 0, 0)); cam.location = c + d * 6; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, f"{acao}_{fr}_{nome}.png"); bpy.ops.render.render(write_still=True)
