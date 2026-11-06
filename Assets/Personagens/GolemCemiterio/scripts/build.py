# Stage 1 of the stone golem (mini-boss) rig, on norm.blend (one fused Tripo sculpt):
#   armature in the model's own pose, bone heat on a clean voxel proxy, weights transferred back, then decimate.
# usage: blender -b norm.blend -P build.py -- <scripts_dir> <out.blend> [tris]
import bpy, sys, time, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0])
from skeleton import J, PARENT, CONNECT, TOES
OUT = args[1]; TRIS = int(args[2]) if len(args) > 2 else 150000
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]", *a, flush=True)
SIDES = (("Left", 1), ("Right", -1)); K = 10.0
def sm(t): t = min(max(t, 0.0), 1.0); return t*t*(3-2*t)
def select_only(o):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
def verts(o):
    c = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", c); return c.reshape(-1, 3)
def seg_dist(P, a, b):
    a, b = np.array(a), np.array(b); ab = b-a; t = np.clip(((P-a)@ab)/(ab@ab), 0, 1); return np.linalg.norm(P-(a+np.outer(t, ab)), axis=1)

body = bpy.data.objects["Body"]
select_only(body); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.delete_loose(); bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- joints: feet
J = {k: (Vector(h), Vector(t)) for k, (h, t) in J.items()}
for S, _ in SIDES:
    ank = J[S+"LowerLeg"][1]; tip = Vector(TOES[S]); ball = ank.lerp(tip, 0.68); ball.z = 0.03
    J[S+"Foot"] = (ank.copy(), ball); J[S+"Toes"] = (ball.copy(), tip)

# ---------------------------------------------------------------- armature (K scale for bone heat)
A = bpy.data.armatures.new("Armature"); arm = bpy.data.objects.new("Armature", A); bpy.context.scene.collection.objects.link(arm)
select_only(arm); bpy.ops.object.mode_set(mode='EDIT'); eb = A.edit_bones
for n, (h, t) in J.items():
    b = eb.new(n); b.head = h*K; b.tail = t*K
for n, p in PARENT.items():
    eb[n].parent = eb[p]; eb[n].use_connect = n in CONNECT
for b in eb:
    if any(x in b.name for x in ("Arm", "Hand", "Shoulder")): b.align_roll(Vector((0, 0, 1)))
    else: b.align_roll(Vector((0, -1, 0)))
for S, _ in SIDES:
    lo = eb[S+"LowerArm"]; b = eb.new(S+"LowerArm_Twist"); b.head = (lo.head+lo.tail)/2; b.tail = lo.tail.copy(); b.roll = lo.roll; b.parent = lo
bpy.ops.object.mode_set(mode='OBJECT')
body.data.transform(Matrix.Scale(K, 4))
for b in A.bones: b.use_deform = not b.name.endswith("_Twist")
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "proxy"; bpy.context.scene.collection.objects.link(proxy)
select_only(proxy); proxy.data.remesh_voxel_size = 0.016*K; bpy.ops.object.voxel_remesh()
select_only(proxy); arm.select_set(True); bpy.context.view_layer.objects.active = arm
log("bone heat on proxy:", len(proxy.data.vertices), "verts ...")
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
log("bone heat: %d/%d proxy verts weighted" % (sum(1 for x in proxy.data.vertices if x.groups), len(proxy.data.vertices)))
for b in A.bones: b.use_deform = True
for g in proxy.vertex_groups: body.vertex_groups.new(name=g.name)
dt = body.modifiers.new("dt", 'DATA_TRANSFER'); dt.object = proxy; dt.use_vert_data = True
dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'; dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
select_only(body); bpy.ops.object.modifier_apply(modifier=dt.name); bpy.data.objects.remove(proxy)
body.data.transform(Matrix.Scale(1/K, 4))
select_only(arm); bpy.ops.object.mode_set(mode='EDIT')
real = {b.name: (b.head/K, b.tail/K, b.roll) for b in A.edit_bones}
for b in A.edit_bones: b.head, b.tail, b.roll = real[b.name]
bpy.ops.object.mode_set(mode='OBJECT')

co = verts(body); vg = body.vertex_groups; names = {g.index: g.name for g in vg}
W = [{names[g.group]: g.weight for g in x.groups if g.weight > 1e-4} for x in body.data.vertices]
src = np.array([bool(w) for w in W]); kd = KDTree(int(src.sum()))
for i in np.nonzero(src)[0]: kd.insert(co[i], int(i))
kd.balance()
for i in np.nonzero(~src)[0]: W[int(i)] = dict(W[kd.find(co[i])[1]])
log("filled", int((~src).sum()), "verts")
B = {b.name: (np.array(b.head_local), np.array(b.tail_local)) for b in A.bones}

# the shoulder pads carry rusty spikes: half their vertices got arm weight and half chest weight, so a raised arm
# stretches them into needles. Pad + spikes (outside the torso, above the shoulder line) follow the shoulder bone as one.
nps = 0
for i, w in enumerate(W):
    x, y, z = co[i]
    a_ = sm((abs(x)-0.36)/0.14)*sm((z-1.78)/0.12)
    if a_ < 0.01: continue
    S = "Left" if x > 0 else "Right"
    tot = sum(w.values()) or 1
    nw = {k: v/tot*(1-a_) for k, v in w.items()}
    nw[S+"Shoulder"] = nw.get(S+"Shoulder", 0)+a_
    W[i] = {k: v for k, v in nw.items() if v > 1e-3}; nps += 1
log("shoulder pad / spike vertices rigidified:", nps)
for S, _ in SIDES:                                       # forearm twist
    h, tl = B[S+"LowerArm"]; ax = tl-h; L2 = ax@ax
    for i, w in enumerate(W):
        x = w.get(S+"LowerArm", 0.0)
        if x <= 0: continue
        f = sm((((co[i]-h)@ax)/L2-0.3)/0.5)
        if f > 0: w[S+"LowerArm"] = x*(1-f); w[S+"LowerArm_Twist"] = x*f
for g in list(vg): vg.remove(g)
G = {}
for i, w in enumerate(W):
    for n, x in w.items():
        if n not in G: G[n] = vg.new(name=n)
        G[n].add([i], x, 'REPLACE')
tris = sum(len(p.vertices)-2 for p in body.data.polygons)
m = body.modifiers.new("Decimate", 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = min(1.0, TRIS/tris); m.use_collapse_triangulate = True
select_only(body); bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.01)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
log("Body: %d -> %d tris, %d verts, unweighted %d" % (tris, sum(len(p.vertices)-2 for p in body.data.polygons), len(body.data.vertices), sum(1 for x in body.data.vertices if not x.groups)))
body.parent = arm; md = body.modifiers.new("Armature", 'ARMATURE'); md.object = arm
bpy.ops.wm.save_as_mainfile(filepath=OUT); log("saved", OUT)
