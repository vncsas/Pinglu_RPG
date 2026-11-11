# Stepping-stone slabs, swamp reeds (cattails) and lily pads, as FBX for Unity.
# usage: blender -b -P make_trilha.py -- <out_dir>
import bpy, bmesh, sys, os, math, random
from mathutils import Vector
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def finish(bm, name, mats):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(bpy.data.materials.new(m))
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); return o

def laje(name, seed, r=0.55, thick=0.16, quad=False):
    rnd = random.Random(seed); bm = bmesh.new(); uvl = bm.loops.layers.uv.verify()
    n = 20 if quad else 16; ang0 = 0.0 if quad else rnd.uniform(0, math.tau); sx = rnd.uniform(0.95, 1.12)
    pw = 4.5 if quad else 2.0                                  # superellipse: 2 = round, 4.5 = square with rounded corners
    def pt(i):
        a = ang0+i*math.tau/n; c, s_ = math.cos(a), math.sin(a); k = (abs(c)**pw+abs(s_)**pw)**(-1/pw); j = rnd.uniform(0.96, 1.04)
        return (c*k*r*j*sx, s_*k*r*j)
    ring = [pt(i) for i in range(n)]
    bot = [bm.verts.new((x, y, -0.12)) for x, y in ring]                         # buried part
    mid = [bm.verts.new((x*0.985, y*0.985, thick*0.8)) for x, y in ring]
    top = [bm.verts.new((x*0.88, y*0.88, thick+rnd.uniform(-0.01, 0.015))) for x, y in ring]
    ctr = bm.verts.new((rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), thick+0.012))
    fs = []
    for i in range(n):
        j = (i+1) % n
        fs += [bm.faces.new((bot[i], bot[j], mid[j], mid[i])), bm.faces.new((mid[i], mid[j], top[j], top[i])), bm.faces.new((top[i], top[j], ctr))]
    for f in fs:
        f.material_index = 0
        for lp in f.loops: lp[uvl].uv = (lp.vert.co.x/1.1+0.5, lp.vert.co.y/1.1+0.5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(bm, name, ["Laje"])

def junco(name, seed, blades=14, h=1.5):
    rnd = random.Random(seed); bm = bmesh.new(); col = bm.loops.layers.color.new("Col"); uvl = bm.loops.layers.uv.verify()
    for b in range(blades):
        a = rnd.uniform(0, math.tau); r = 0.18*math.sqrt(rnd.random()); base = Vector((math.cos(a)*r, math.sin(a)*r, 0))
        hh = h*rnd.uniform(0.7, 1.15); w = 0.028*rnd.uniform(0.8, 1.3); face = rnd.uniform(0, math.tau); side = Vector((math.cos(face), math.sin(face), 0))
        lean = Vector((math.cos(a), math.sin(a), 0))*rnd.uniform(0.05, 0.22); prev = None; rv = rnd.random()
        for s in range(4):
            t = s/3; p = base+Vector((0, 0, hh*t))+lean*hh*t*t; ww = w*(1-t)**0.6 if s < 3 else 0
            L = bm.verts.new(p-side*ww); R = bm.verts.new(p+side*ww)
            if prev:
                f = bm.faces.new((prev[0], prev[1], R, L)) if s < 3 else bm.faces.new((prev[0], prev[1], R))
                for lp in f.loops: lp[col] = (t if lp.vert in (L, R) else t-1/3, rv, 0, 1); lp[uvl].uv = (0.5, lp.vert.co.z/hh)
            prev = (L, R)
        if b % 4 == 0:                                                      # cattail head: small dark cylinder near the tip
            tip = base+Vector((0, 0, hh*0.78))+lean*hh*0.6; sides = 6
            rings = [[bm.verts.new(tip+Vector((math.cos(i*math.tau/sides)*rr, math.sin(i*math.tau/sides)*rr, zz))) for i in range(sides)] for zz, rr in ((0, 0.018), (0.07, 0.022), (0.14, 0.012))]
            for k in range(2):
                for i in range(sides):
                    j = (i+1) % sides; f = bm.faces.new((rings[k][i], rings[k][j], rings[k+1][j], rings[k+1][i]))
                    for lp in f.loops: lp[col] = (0.0, 0.95, 1, 1); lp[uvl].uv = (0.5, 0.5)       # b=1 marks the brown head in the shader
    return finish(bm, name, ["Junco"])

def vitoria(name, seed):
    rnd = random.Random(seed); bm = bmesh.new(); uvl = bm.loops.layers.uv.verify(); segs = 14
    r = rnd.uniform(0.28, 0.42); c = bm.verts.new((0, 0, 0.01)); ring = [bm.verts.new((math.cos(i*math.tau/segs)*r*(1 if not (i == 0) else 0.2), math.sin(i*math.tau/segs)*r*(1 if not (i == 0) else 0.2), 0.0)) for i in range(segs)]
    for i in range(segs):
        f = bm.faces.new((c, ring[i], ring[(i+1) % segs]))
        for lp in f.loops: lp[uvl].uv = (lp.vert.co.x/r*0.5+0.5, lp.vert.co.y/r*0.5+0.5)
    return finish(bm, name, ["Vitoria"])

objs = [laje("Laje_A", 1), laje("Laje_B", 2, 0.5), laje("Laje_C", 3, 0.62, quad=True), laje("Laje_D", 4, 0.5, quad=True), junco("Junco_A", 5), junco("Junco_B", 6, 18, 1.8), vitoria("Vitoria_A", 7), vitoria("Vitoria_B", 8)]
for o in objs:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False, bake_space_transform=True)
    print("tris", o.name, sum(len(p.vertices)-2 for p in o.data.polygons))
