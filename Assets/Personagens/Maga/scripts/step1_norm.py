# Step 1 (sorceress): import the Tripo FBX (one fused ~2M-triangle mesh), scale to 1.70 m with the
# feet on z=0 (she already faces -Y, her left = +X), and pre-decimate to ~300k triangles so bone heat
# can run (the final game mesh is decimated again after weighting). Textures packed for the renders.
# usage: blender -b -P step1_norm.py -- <tripo.fbx> <norm.blend> [height] [tris]
import bpy, sys, numpy as np
from mathutils import Matrix
a = sys.argv[sys.argv.index("--")+1:]
H = float(a[2]) if len(a) > 2 else 1.70; TRIS = int(a[3]) if len(a) > 3 else 300000
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
o = [o for o in bpy.data.objects if o.type == 'MESH'][0]; o.name = o.data.name = "Maga"
o.data.transform(o.matrix_world); o.parent = None; o.matrix_world = Matrix.Identity(4)
for x in [x for x in bpy.data.objects if x != o]: bpy.data.objects.remove(x)
v = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
s = H/(v[:, 2].max()-v[:, 2].min())
o.data.transform(Matrix.Translation((0, 0, -v[:, 2].min()*s)) @ Matrix.Scale(s, 4))
tris = sum(len(p.vertices)-2 for p in o.data.polygons)
bpy.context.view_layer.objects.active = o; o.select_set(True)
m = o.modifiers.new("Decimate", 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = TRIS/tris; m.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=m.name)
v = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
print("NORM scale %.3f, %d -> %d tris, bbox %s %s" % (s, tris, sum(len(p.vertices)-2 for p in o.data.polygons), v.min(0).round(3), v.max(0).round(3)))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=a[1])
