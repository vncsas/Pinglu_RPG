# Importa o scan do cavalo, mostra tamanho/orientacao e renderiza 4 vistas (workbench com textura).
import bpy, sys, os, math
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath="C:/Users/Thiago/Documentos/rig-cavalo/fonte/Scaniverse/Scaniverse.obj")
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print("MESHES", [(o.name, len(o.data.vertices), len(o.data.polygons)) for o in objs])
o = objs[0]
bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))); mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
print("BBOX", mn, mx, "SIZE", mx-mn)
# loose parts
import bmesh
bm = bmesh.new(); bm.from_mesh(o.data)
seen = set(); comps = []
for v in bm.verts:
    if v.index in seen: continue
    stack = [v]; seen.add(v.index); n = 0
    while stack:
        a = stack.pop(); n += 1
        for e in a.link_edges:
            b = e.other_vert(a)
            if b.index not in seen: seen.add(b.index); stack.append(b)
    comps.append(n)
print("COMPONENTES", sorted(comps, reverse=True)[:10], "total", len(comps))
bm.free()
# render
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 900; sc.render.resolution_y = 700
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'
c = (mn+mx)/2; size = max((mx-mn).x, (mx-mn).y, (mx-mn).z)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = size*1.15
views = {"frente": (0, -1, 0), "lado": (1, 0, 0), "cima": (0, 0, 1), "tras3q": (1, -1, 0.5)}
for name, d in views.items():
    d = Vector(d).normalized(); cam.location = c + d*size*3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, name + ".png"); bpy.ops.render.render(write_still=True)
