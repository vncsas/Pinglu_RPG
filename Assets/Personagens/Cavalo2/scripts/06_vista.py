# renderiza vistas (lado, frente, tras) do cavalo com o esqueleto por cima. uso: blender -b -P 06_vista.py -- arquivo.blend pasta [pose]
import bpy, sys, os, math
from mathutils import Vector as V
args = sys.argv[sys.argv.index("--")+1:]; blend, out = args[0], args[1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 1000; sc.render.resolution_y = 800
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'; sc.render.film_transparent = False
for ob in bpy.data.objects:
    if ob.type == 'ARMATURE': ob.show_in_front = True; ob.data.display_type = 'STICK'
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.6
c = V((0.0, 0.0, 0.95))
for name, d in {"lado": (-1, 0, 0), "frente": (0, -1, 0), "tras": (0, 1, 0), "cima": (0, 0, 1)}.items():
    d = V(d).normalized(); cam.location = c + d * 6; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, name + ".png"); bpy.ops.render.render(write_still=True)
