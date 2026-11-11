# Individually modelled irregular wall stones (dry-stone wall + paving slabs for the ramps).
# Each stone: subdivided box, rounded by pulling toward a sphere, then noise-displaced, chipped corners, random vertex jitter, slightly flattened.
# Flat shading (split normals) so the facets read as chiselled rock. Box-projected UVs (tileable rock texture in Unity).
# Blender axes: x = along wall, y = depth, z = UP (height). Unit stone ~ 1.0 x 0.9 x 0.62; Unity scales it.
# usage: blender -b -P make_pedras_muro.py -- <out_dir>
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, noise
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def pedra(name, seed, dims=(1.0, 0.62, 0.9), cuts=2, round_=0.55, noise_amp=0.05, jitter=0.02, chip=3, slab=False):
    rnd = random.Random(seed); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    sx, sy, sz = dims
    off = Vector((rnd.uniform(-50, 50), rnd.uniform(-50, 50), rnd.uniform(-50, 50)))
    chips = [Vector((rnd.choice((-1, 1)), rnd.choice((-1, 1)), rnd.choice((-1, 1)))).normalized()*0.5 for _ in range(chip)]
    for v in bm.verts:
        p = v.co.copy()
        sph = p.normalized()*0.5*1.18                                    # point on a sphere
        p = p.lerp(sph, round_)                                          # rounded corners
        n = (noise.noise(p*2.1+off)*0.9+noise.noise(p*5.3+off*1.7)*0.35)*noise_amp*2.0
        p += p.normalized()*n                                            # lumpy surface
        for c in chips:                                                  # chipped corners: pull vertices near a corner inward
            d = (p-c).length
            if d < 0.34: p -= p.normalized()*(0.34-d)*0.55
        p += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))*jitter
        v.co = Vector((p.x*sx, p.y*sy, p.z*sz))
    # sit on the ground: lowest point at z = 0
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    uvl = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            q = l.vert.co
            u, w = ((q.y, q.z) if ax == 0 else (q.x, q.z) if ax == 1 else (q.x, q.y))
            l[uvl].uv = (u*0.8+seed*0.137, w*0.8+seed*0.071)
    for f in bm.faces: f.smooth = False
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); return o

objs = []
for i in range(8):
    r = random.Random(100+i)
    objs.append(pedra(f"Pedra_M{i+1}", 100+i, dims=(r.uniform(0.85, 1.15), r.uniform(0.75, 1.0), r.uniform(0.5, 0.74)), noise_amp=r.uniform(0.04, 0.07), chip=r.randint(1, 2)))
for i in range(3):                                                   # flat paving slabs for the ramp path
    r = random.Random(200+i)
    objs.append(pedra(f"Laje_M{i+1}", 200+i, dims=(r.uniform(0.95, 1.2), r.uniform(0.85, 1.1), r.uniform(0.2, 0.26)), round_=0.65, noise_amp=0.04, chip=1, slab=True))
for o in objs:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=True)
    d = o.dimensions
    print("tris", o.name, sum(len(p.vertices)-2 for p in o.data.polygons), [round(x, 2) for x in d])
