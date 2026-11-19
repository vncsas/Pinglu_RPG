import bpy, sys, os, math
from mathutils import Vector as V
args = sys.argv[sys.argv.index("--")+1:]; out, acao = args[0], args[1]; frames = [int(x) for x in args[2:]]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-cavalo2/saida/rig_anim.blend")
arm = bpy.data.objects["CavaloArmature"]; arm.animation_data.action = bpy.data.actions[acao]; arm.hide_render = True
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 700; sc.render.resolution_y = 700; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.9
c = V((-0.1, -0.5, 0.9)); d = V((-1.0, -0.9, 0.35)).normalized(); cam.location = c + d * 6; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
for f in frames:
    sc.frame_set(f); sc.render.filepath = os.path.join(out, f"{acao}_{f}.png"); bpy.ops.render.render(write_still=True)
