# Step 1 (stone golem mini-boss): one Tripo sculpt FBX (1.9M tris). Bake transforms, weld, scale to H metres
# with the feet on z=0 and the pelvis on x=y=0 (he faces -Y, his left = +X), then pre-decimate so bone heat can run.
# usage: blender -b -P step1_norm.py -- <model.fbx> <norm.blend> [height] [tris]
import bpy, sys, numpy as np, bmesh
from mathutils import Matrix
a = sys.argv[sys.argv.index("--")+1:]
H = float(a[2]) if len(a) > 2 else 2.0; TRIS = int(a[3]) if len(a) > 3 else 400000
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in ms:
    o.data.transform(o.matrix_world); o.parent = None; o.matrix_world = Matrix.Identity(4)
for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
bpy.ops.object.select_all(action='DESELECT')
for o in ms: o.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.join()
body = bpy.context.object; body.name = body.data.name = "Body"
me = body.data
print("joined: %d verts, %d tris" % (len(me.vertices), sum(len(p.vertices)-2 for p in me.polygons)))
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=1e-5); bpy.ops.mesh.delete_loose(use_verts=True, use_edges=True, use_faces=False)
bpy.ops.object.mode_set(mode='OBJECT')
print("welded: %d verts, %d tris" % (len(me.vertices), sum(len(p.vertices)-2 for p in me.polygons)))
v = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
s = H/(v[:, 2].max()-v[:, 2].min())
me.transform(Matrix.Scale(s, 4))
v *= s
zmin = v[:, 2].min(); legs = v[v[:, 2] < zmin+0.5]
me.transform(Matrix.Translation((-0.0, 0.0, -zmin)))
tris = sum(len(p.vertices)-2 for p in me.polygons)
m = body.modifiers.new("Decimate", 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = TRIS/tris; m.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=m.name)
v = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
print("NORM scale %.3f, %d -> %d tris, %d verts, bbox %s %s" % (s, tris, sum(len(p.vertices)-2 for p in me.polygons), len(me.vertices), v.min(0).round(3), v.max(0).round(3)))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=a[1])
