import bpy, sys, os
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/Thiago/Documentos/rig-ovelha/fonte/tripo_convert_b707ba7b-33c7-4ec5-a908-2a9f49337a52.fbx")
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print("OBJS", [(o.name, len(o.data.vertices), len(o.data.polygons), [m.name for m in o.data.materials], o.matrix_world.to_euler(), o.scale[:]) for o in objs])
print("ARM", [o.name for o in bpy.context.scene.objects if o.type == 'ARMATURE'])
for o in objs:
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))); mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    print("BBOX", o.name, mn, mx, "SIZE", mx-mn, "UV", len(o.data.uv_layers))
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 900; sc.render.resolution_y = 700
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
o = objs[0]; bb = [o.matrix_world @ Vector(c) for c in o.bound_box]; mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))); mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
c = (mn+mx)/2; size = max((mx-mn).x, (mx-mn).y, (mx-mn).z)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = size*1.15
for name, d in {"frente": (0, -1, 0), "lado": (1, 0, 0), "cima": (0, 0, 1), "tras3q": (1, -1, 0.5)}.items():
    d = Vector(d).normalized(); cam.location = c + d*size*3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, name + ".png"); bpy.ops.render.render(write_still=True)
