# renderiza N quadros (vista lateral) de uma acao. uso: blender -b -P 08_preview.py -- pasta Acao n_quadros
import bpy, sys, os
from mathutils import Vector as V
args = sys.argv[sys.argv.index("--")+1:]; out, acao, n = args[0], args[1], int(args[2]); os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-cavalo2/saida/rig_anim.blend")
arm = bpy.data.objects["CavaloArmature"]; arm.animation_data.action = bpy.data.actions[acao]
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 700; sc.render.resolution_y = 520
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = 'TEXTURE'; arm.hide_render = True
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.0
c = V((0.0, 0.0, 0.9)); d = V((-1, 0, 0)); cam.location = c + d * 6; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
a = bpy.data.actions[acao]; f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
for i in range(n):
    f = f0 + (f1 - f0) * i / n; sc.frame_set(int(round(f))); sc.render.filepath = os.path.join(out, "%s_%02d.png" % (acao, i)); bpy.ops.render.render(write_still=True)
