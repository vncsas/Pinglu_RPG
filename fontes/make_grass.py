# Grass tufts for Unity: upright blades (no "lying" tufts), tapered, slightly curved at the tip only, vertex colour:
#   R = height along the blade (0 base -> 1 tip), G = per-blade random (colour variation in the shader).
# LOD0 tufts (4 variants, 2 segments per blade) for near; LOD1 (1 segment per blade, few triangles) for mid/far distance.
# usage: blender -b -P make_grass.py -- <out_dir>
import bpy, bmesh, sys, math, random, os
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def tuft(name, blades, height, spread, width, seed, segs=2, tip=0.10, lean=0.10, var=0.35):
    rnd = random.Random(seed)
    me = bpy.data.meshes.new(name); bm = bmesh.new(); col = bm.loops.layers.color.new("Col"); uv = bm.loops.layers.uv.new("UVMap")
    for b in range(blades):
        a = rnd.uniform(0, math.tau); r = spread*math.sqrt(rnd.random())
        base = Vector((math.cos(a)*r, math.sin(a)*r, 0))
        h = height*rnd.uniform(1.0-var, 1.0+var*0.6); w = width*rnd.uniform(0.75, 1.25)
        # blades grow nearly straight up; the lean grows with the distance to the tuft centre but stays small
        out_dir = Vector((math.cos(a), math.sin(a), 0))*(r/max(spread, 1e-6))*lean + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0))*lean*0.6
        face = rnd.uniform(0, math.tau); side = Vector((math.cos(face), math.sin(face), 0))
        rv = rnd.random(); prev = None
        for s in range(segs+1):
            t = s/segs
            p = base+Vector((0, 0, h*t))+out_dir*h*t*t*1.0 + Vector((math.cos(face+1.6), math.sin(face+1.6), 0))*tip*h*(t**3)    # tip bend
            ww = w*(1.0-t)**0.7 if s < segs else 0.0
            L = bm.verts.new(p-side*ww); R = bm.verts.new(p+side*ww)
            if prev:
                f = bm.faces.new((prev[0], prev[1], R, L)) if s < segs else bm.faces.new((prev[0], prev[1], R))
                for lp in f.loops:
                    lp[col] = (t if lp.vert in (L, R) else (t-1/segs), rv, 0, 1)
                    lp[uv].uv = (0.5, lp.vert.co.z/h if h else 0)
            prev = (L, R)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o
objs = [tuft("GrassTuft_A", 34, 0.34, 0.11, 0.011, 1), tuft("GrassTuft_B", 40, 0.50, 0.13, 0.012, 2),
        tuft("GrassTuft_C", 26, 0.22, 0.09, 0.010, 3), tuft("GrassTuft_Tall", 28, 0.80, 0.12, 0.013, 4, tip=0.16),
        # LOD1: single-segment blades, wider, fewer
        tuft("GrassLod_A", 14, 0.34, 0.12, 0.020, 11, segs=1), tuft("GrassLod_B", 16, 0.50, 0.14, 0.022, 12, segs=1),
        tuft("GrassLod_C", 12, 0.22, 0.10, 0.018, 13, segs=1), tuft("GrassLod_Tall", 12, 0.80, 0.13, 0.024, 14, segs=1)]
for o in objs:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=True)
    print("tris", o.name, sum(len(p.vertices)-2 for p in o.data.polygons))
