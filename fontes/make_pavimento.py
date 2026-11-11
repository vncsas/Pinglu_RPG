# Pedras de calcamento (pavimento) para ruas e pracas: lajes baixas, arredondadas e quadradas-arredondadas, uma a uma.
# Superelipsoide achatado: expoente 2.1 = redonda, 4 = quadrada com cantos arredondados; ruido na superficie; flat shading.
# Blender: x,y = plano, z = ALTURA (0 = base). Unidade ~1 m de lado, ~0.2 m de espessura.
# usage: blender -b -P make_pavimento.py -- <out_dir>
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, noise
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)


def laje(name, seed, nxy, a, b, c, cuts=3, amp=0.035):
    rnd = random.Random(seed); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    off = Vector((rnd.uniform(-40, 40), rnd.uniform(-40, 40), rnd.uniform(-40, 40)))
    for v in bm.verts:
        d = v.co.normalized()
        # raio onde |x/a|^n + |y/b|^n + |z/c|^m = 1
        m = 3.0
        k = (abs(d.x/a)**nxy + abs(d.y/b)**nxy + abs(d.z/c)**m)
        r = k ** (-1.0/ nxy) if k > 0 else 1.0
        p = d * r
        n = noise.noise(p*3.1+off)*amp + noise.noise(p*7.7+off*1.3)*amp*0.4
        p.z += n * 1.6
        p.x += n*0.5; p.y += n*0.5
        p.x += rnd.uniform(-0.008, 0.008); p.y += rnd.uniform(-0.008, 0.008); p.z += rnd.uniform(-0.006, 0.006)
        v.co = p
    zmin = min(v.co.z for v in bm.verts); zmax = max(v.co.z for v in bm.verts); esc = (2*c*1.05) / max(zmax - zmin, 1e-4)
    for v in bm.verts: v.co.z = (v.co.z - zmin) * esc
    uvl = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for l in f.loops:
            q = l.vert.co
            l[uvl].uv = (q.x*0.9 + seed*0.173, q.y*0.9 + seed*0.091)
    for f in bm.faces: f.smooth = False
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); return o


objs = []
shapes = [  # nome, expoente (2.1 redonda .. 4.5 quadrada), meio-eixos a,b,c
    ("Calc_R1", 2.1, 0.50, 0.46, 0.10), ("Calc_R2", 2.3, 0.50, 0.40, 0.09), ("Calc_R3", 2.2, 0.44, 0.50, 0.10),
    ("Calc_Q1", 4.2, 0.48, 0.46, 0.10), ("Calc_Q2", 4.6, 0.50, 0.42, 0.09), ("Calc_Q3", 3.8, 0.45, 0.49, 0.10),
]
for i, (nm, n, a, b, c) in enumerate(shapes):
    objs.append(laje(nm, 300+i, n, a, b, c))
for o in objs:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=True)
    print("tris", o.name, sum(len(p.vertices)-2 for p in o.data.polygons), [round(x, 2) for x in o.dimensions])
