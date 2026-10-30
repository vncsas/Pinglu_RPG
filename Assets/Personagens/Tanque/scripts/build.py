# Stage 1 of the tank rig (runs on norm.blend from step1_norm.py):
#   wings: in the source they were skinned to the upper-arm twist bones, so every arm movement swung the
#   wings. Each wing gets its own chain of 3 bones (Wing_L_1..3 / Wing_R_1..3) on UpperChest, laid along
#   the wing's centre line (geodesic rings from where it attaches to the back), and is re-weighted to it
#   (rigid chitin: narrow blend bands at the joints, root blended with UpperChest). The other pieces of that
#   mesh (crest on the head, ridge on the back) keep their weights. Then decimate to a game mesh.
# usage: blender -b norm.blend -P build.py -- <out.blend> [tris]
import bpy, sys, heapq, time, numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
args = sys.argv[sys.argv.index("--")+1:]; OUT = args[0]; TRIS = int(args[1]) if len(args) > 1 else 80000
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]", *a, flush=True)
def sm(t): t = min(max(t, 0.0), 1.0); return t*t*(3-2*t)
arm = bpy.data.objects["Armature"]; A = arm.data; body = bpy.data.objects["Body"]; me = body.data
vg = body.vertex_groups; names = {g.index: g.name for g in vg}
co = np.array([v.co[:] for v in me.vertices]); n = len(co)
tag = vg["_asa"].index
W = [{names[g.group]: g.weight for g in v.groups if g.group != tag and g.weight > 1e-4} for v in me.vertices]
dom = np.array([max(w, key=w.get) if w else "" for w in W])
isw = np.array([any(g.group == tag for g in v.groups) for v in me.vertices])
E = np.array([e.vertices[:] for e in me.edges])
adj = [[] for _ in range(n)]
for x, y in E:
    d = float(np.linalg.norm(co[x]-co[y])); adj[x].append((y, d)); adj[y].append((x, d))
kd = KDTree(int((~isw).sum()))
for i in np.nonzero(~isw)[0]: kd.insert(co[i], int(i))
kd.balance()

def seg_dist(P, a, b):
    ab = b-a; t = np.clip(((P-a)@ab)/(ab@ab), 0, 1); return np.linalg.norm(P-(a+np.outer(t, ab)), axis=1)

chains = {}
for S, sx, cc in (("L", 1, "CC_Base_L_"), ("R", -1, "CC_Base_R_")):
    wing = isw & (co[:, 0]*sx > 0.0) & np.isin(dom, [cc+"UpperarmTwist01", cc+"UpperarmTwist02", cc+"ForearmTwist01", cc+"ForearmTwist02"])
    idx = np.nonzero(wing)[0]; inw = np.zeros(n, bool); inw[idx] = True
    near = np.array([kd.find(co[i])[2] for i in idx])
    roots = idx[near < 0.012]
    dist = np.full(n, np.inf); pq = []
    for r in roots: dist[r] = 0.0; heapq.heappush(pq, (0.0, int(r)))
    while pq:
        dd, k = heapq.heappop(pq)
        if dd > dist[k]: continue
        for j, w in adj[k]:
            if inw[j] and dd+w < dist[j]: dist[j] = dd+w; heapq.heappush(pq, (dist[j], j))
    ok = idx[np.isfinite(dist[idx])]; L = dist[ok].max()
    # loose bits of the wing not reached through the surface: nearest reached vertex
    if len(ok) < len(idx):
        k2 = KDTree(len(ok))
        for i in ok: k2.insert(co[i], int(i))
        k2.balance()
        for i in idx[~np.isfinite(dist[idx])]: dist[i] = dist[k2.find(co[i])[1]]
    s = dist[idx]/L
    joints = []
    for f in (0.0, 0.33, 0.66, 1.0):
        m = np.abs(s-f) < (0.04 if 0 < f < 1 else 0.06)
        if f == 0.0: m = s < 0.05
        if f == 1.0: m = s > 0.95
        joints.append(co[idx[m]].mean(0))
    chains[S] = (idx, s, joints)
    log(S, "wing:", len(idx), "verts, length %.2f m, joints" % L, [tuple(np.round(j, 2)) for j in joints])

# bones
bpy.context.view_layer.objects.active = arm; arm.select_set(True); bpy.ops.object.mode_set(mode='EDIT'); eb = A.edit_bones
for S, (idx, s, joints) in chains.items():
    prev = eb["UpperChest"]
    for k in range(3):
        b = eb.new(f"Wing_{S}_{k+1}"); b.head = Vector(joints[k]); b.tail = Vector(joints[k+1])
        b.align_roll(Vector((0, 1, 0))); b.parent = prev; b.use_connect = k > 0; prev = b
bpy.ops.object.mode_set(mode='OBJECT')

# weights: rigid segments, 6% blend bands at the joints, root blended into UpperChest
for S, (idx, s, joints) in chains.items():
    for i, si in zip(idx, s):
        x = si*3
        w = {f"Wing_{S}_{min(int(x), 2)+1}": 1.0}
        for bnd in (1, 2):                                       # blend band around each joint
            if abs(x-bnd) < 0.18:
                t = sm((x-(bnd-0.18))/0.36); w = {f"Wing_{S}_{bnd}": 1-t, f"Wing_{S}_{bnd+1}": t}
        root = 1-sm(si/0.06)
        tot = sum(w.values())
        w = {kk: (1-root)*v/tot for kk, v in w.items()}
        if root > 1e-3: w["UpperChest"] = root
        W[i] = w
for g in list(vg): vg.remove(g)
G = {}
for i, w in enumerate(W):
    for nme, x in w.items():
        if nme not in G: G[nme] = vg.new(name=nme)
        G[nme].add([int(i)], x, 'REPLACE')
log("wings re-weighted")

# game mesh
tris = sum(len(p.vertices)-2 for p in me.polygons)
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active = body
md = [m for m in body.modifiers if m.type == 'ARMATURE']
m = body.modifiers.new("Decimate", 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = min(1.0, TRIS/tris); m.use_collapse_triangulate = True
bpy.ops.object.modifier_move_to_index(modifier=m.name, index=0)
bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.005)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
log("Body decimated %d -> %d tris; unweighted verts: %d" % (tris, sum(len(p.vertices)-2 for p in me.polygons), sum(1 for v in me.vertices if not v.groups)))
bpy.ops.wm.save_as_mainfile(filepath=OUT); log("saved", OUT)
