# Procedural trees (light, game-ready): tapered trunk + branches + leaf CARDS (alpha-cut leaves) -> real-looking canopy for few triangles.
# Meshes in metres, origin at the base of the trunk. Slots: Casca (bark), Folha (leaf card).
# usage: blender -b -P make_trees.py -- <out_dir>
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix, Quaternion
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
MATS = {}
def mat(n):
    if n not in MATS: MATS[n] = bpy.data.materials.new(n)
    return MATS[n]

class T:
    def __init__(self, name, seed): self.name = name; self.rnd = random.Random(seed); self.bm = bmesh.new(); self.uvl = self.bm.loops.layers.uv.verify(); self.bark = 0; self.leaf = 1; self.slots = ["Casca", "Folha"]
    def cyl(self, p0, p1, r0, r1, sides=7, vscale=1.0):
        bm = self.bm; d = (p1-p0); L = d.length
        if L < 1e-4: return
        q = Vector((0, 0, 1)).rotation_difference(d.normalized()); rings = []
        for p, r in ((p0, r0), (p1, r1)):
            rings.append([bm.verts.new(p + q @ Vector((math.cos(i*math.tau/sides)*r, math.sin(i*math.tau/sides)*r, 0))) for i in range(sides)])
        for i in range(sides):
            j = (i+1) % sides
            f = bm.faces.new((rings[0][i], rings[0][j], rings[1][j], rings[1][i])); f.material_index = self.bark
            for lp in f.loops:
                u = (0 if lp.vert in (rings[0][i], rings[1][i]) else 1) * (math.tau*max(r0, r1)/1.2)/sides*sides*0.5 + 0
                lp[self.uvl].uv = ((i if lp.vert in (rings[0][i], rings[1][i]) else i+1)/sides*max(r0, 0.05)*5.5, (0 if lp.vert in rings[0] else L*vscale/1.5))
    def card(self, c, size, up_bias=0.5):
        bm = self.bm; r = self.rnd
        n = Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(-0.2, 1.0)*up_bias+0.3)).normalized()
        a = n.cross(Vector((0, 0, 1)));
        if a.length < 0.1: a = Vector((1, 0, 0))
        a.normalize(); b = n.cross(a); s = size*r.uniform(0.8, 1.3)
        vs = [bm.verts.new(c + a*x*s + b*y*s) for x, y in ((-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5))]
        f = bm.faces.new(vs); f.material_index = self.leaf
        for lp, uv in zip(f.loops, ((0, 0), (1, 0), (1, 1), (0, 1))): lp[self.uvl].uv = uv
    def finish(self):
        me = bpy.data.meshes.new(self.name); self.bm.to_mesh(me); self.bm.free()
        for s in self.slots: me.materials.append(mat(s))
        o = bpy.data.objects.new(self.name, me); bpy.context.scene.collection.objects.link(o); return o

def branch(t, p, d, length, r, depth, leaves, spread, cards):
    r_ = t.rnd; segs = 3; pts = [p]; cur = Vector(p); dirv = Vector(d).normalized()
    for i in range(segs):
        dirv = (dirv + Vector((r_.uniform(-0.25, 0.25), r_.uniform(-0.25, 0.25), r_.uniform(-0.05, 0.12)))).normalized()
        cur = cur + dirv*length/segs; pts.append(cur.copy())
    for i in range(segs): t.cyl(pts[i], pts[i+1], r*(1-i/segs), r*(1-(i+1)/segs)*0.85, sides=6)
    if depth > 0:
        for k in range(r_.randint(2, 3)):
            tt = r_.uniform(0.45, 0.95); base = pts[0].lerp(pts[-1], tt)
            nd = (dirv + Vector((r_.uniform(-1, 1), r_.uniform(-1, 1), r_.uniform(0.0, 0.6)))*spread).normalized()
            branch(t, base, nd, length*r_.uniform(0.45, 0.7), r*0.5, depth-1, leaves, spread, cards)
    if leaves:
        for k in range(cards):
            c = pts[-1].lerp(pts[max(1, segs-1)], r_.random()*0.8) + Vector((r_.uniform(-1, 1), r_.uniform(-1, 1), r_.uniform(-0.3, 0.8)))*leaves*0.55
            t.card(c, leaves*r_.uniform(0.9, 1.3))

def tree(name, seed, height, trunk_r, n_main, branch_len, depth, leaf_size, cards, spread, lean=0.04, dead=False):
    t = T(name, seed); r = t.rnd
    # trunk (slightly bent)
    pts = [Vector((0, 0, 0))]; cur = Vector((0, 0, 0)); dv = Vector((r.uniform(-lean, lean), r.uniform(-lean, lean), 1)).normalized()
    segs = 5
    for i in range(segs):
        dv = (dv + Vector((r.uniform(-lean, lean), r.uniform(-lean, lean), 0))).normalized(); cur = cur + dv*height*0.62/segs; pts.append(cur.copy())
    for i in range(segs): t.cyl(pts[i], pts[i+1], trunk_r*(1-i/segs*0.55)*(1.35 if i == 0 else 1), trunk_r*(1-(i+1)/segs*0.55), sides=8)
    # root flare
    for k in range(4):
        a = r.uniform(0, math.tau); t.cyl(Vector((math.cos(a)*trunk_r*0.5, math.sin(a)*trunk_r*0.5, 0.25)), Vector((math.cos(a)*trunk_r*1.9, math.sin(a)*trunk_r*1.9, -0.1)), trunk_r*0.35, trunk_r*0.12, sides=5)
    top = pts[-1]
    for k in range(n_main):
        a = k/n_main*math.tau + r.uniform(-0.4, 0.4); tt = r.uniform(0.62, 1.0); base = pts[0].lerp(top, tt) if tt < 1 else top
        base = pts[min(segs, int(tt*segs))].lerp(pts[min(segs, int(tt*segs)+1)] if int(tt*segs)+1 <= segs else top, (tt*segs) % 1.0)
        d = Vector((math.cos(a)*1.0, math.sin(a)*1.0, r.uniform(0.35, 1.0)))
        branch(t, base, d, branch_len*r.uniform(0.8, 1.2), trunk_r*0.55, depth, 0 if dead else leaf_size, spread, cards)
    if not dead:                                           # crown on top
        for k in range(int(cards*1.5)):
            c = top + Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(0.0, 1.2)))*leaf_size*1.4
            t.card(c, leaf_size*r.uniform(0.9, 1.3))
    return t.finish()

objs = [tree("Arvore_Carvalho", 1, 6.5, 0.42, 6, 3.4, 2, 0.95, 26, 0.9),
        tree("Arvore_Carvalho2", 5, 5.6, 0.36, 5, 3.0, 2, 0.9, 24, 0.9, lean=0.06),
        tree("Arvore_Esguia", 2, 8.5, 0.28, 5, 2.4, 2, 0.8, 22, 0.55),
        tree("Arvore_Seca", 3, 5.5, 0.34, 6, 2.8, 3, 0.8, 0, 1.0, lean=0.1, dead=True),
        tree("Arvore_Seca2", 4, 4.2, 0.30, 5, 2.4, 3, 0.8, 0, 1.1, lean=0.12, dead=True)]
for o in objs:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False)
    print("tris", o.name, sum(len(p.vertices)-2 for p in o.data.polygons))
