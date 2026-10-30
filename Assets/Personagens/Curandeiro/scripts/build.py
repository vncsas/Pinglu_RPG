# Stage 1 of the healer (bone seraph) rig, on norm.blend (one fused Tripo sculpt):
#   armature in the model's own pose (body, feet, forearm twist, 4-bone wing chains),
#   bone heat on a clean voxel proxy and weights transferred back, then clean-ups:
#   - wings: a vertex either belongs to a wing (only wing bones + the shoulder blade) or not (no wing weight)
#   - halo: rigid on the Head
#   - robe below the waist: nearest leg close to it, hips farther out (as on the sorceress coat)
#   then decimate to a game mesh.
# usage: blender -b norm.blend -P build.py -- <scripts_dir> <out.blend> [tris]
import bpy, sys, time, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0])
from skeleton import J, PARENT, CONNECT, TOES
OUT = args[1]; TRIS = int(args[2]) if len(args) > 2 else 70000
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

body = bpy.data.objects["Maga"]; body.name = body.data.name = "Body"
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
    elif b.name.startswith("Wing"): b.align_roll(Vector((0, 1, 0)))
    else: b.align_roll(Vector((0, -1, 0)))
for S, _ in SIDES:
    lo = eb[S+"LowerArm"]; b = eb.new(S+"LowerArm_Twist"); b.head = (lo.head+lo.tail)/2; b.tail = lo.tail.copy(); b.roll = lo.roll; b.parent = lo
bpy.ops.object.mode_set(mode='OBJECT')
body.data.transform(Matrix.Scale(K, 4))
for b in A.bones: b.use_deform = not b.name.endswith("_Twist")
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "proxy"; bpy.context.scene.collection.objects.link(proxy)
select_only(proxy); proxy.data.remesh_voxel_size = 0.008*K; bpy.ops.object.voxel_remesh()
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

# ---------------------------------------------------------------- wings: hard split
WB = {S: [f"Wing_{S}_{k}" for k in (1, 2, 3, 4)] for S in ("L", "R")}
dw = {S: np.min([seg_dist(co, *B[n]) for n in WB[S]], axis=0) for S in WB}
BODYB = [n for n in B if not n.startswith("Wing") and not n.endswith("_Twist")]
dbody = np.min([seg_dist(co, *B[n]) for n in BODYB], axis=0)
nw = 0
for i, w in enumerate(W):
    ws = sum(x for k, x in w.items() if k.startswith("Wing"))
    S = "L" if co[i, 0] > 0 else "R"
    wing = (ws > 0.5 or dw[S][i] < dbody[i]*0.7) and co[i, 1] > 0.07 and co[i, 2] > 0.35 and abs(co[i, 0]) > 0.04
    if wing:
        ww = {k: x for k, x in w.items() if k.startswith(f"Wing_{S}")}
        if not ww: ww = {f"Wing_{S}_1": 1.0}
        t = sum(ww.values()); root = 1-sm(np.linalg.norm(co[i]-B[f"Wing_{S}_1"][0])/0.12)
        ww = {k: (1-root)*x/t for k, x in ww.items()}
        if root > 1e-3: ww["UpperChest"] = root
        W[i] = ww; nw += 1
    elif ws > 0:
        rest = {k: x for k, x in w.items() if not k.startswith("Wing")}
        W[i] = rest if rest else {"UpperChest": 1.0}
log("wing verts:", nw)
nh = 0                                                   # halo: everything above the skull near the centre line
for i in range(len(co)):
    if co[i, 2] > 1.73 and abs(co[i, 0]) < 0.22 and not any(k.startswith("Wing") for k in W[i]):
        W[i] = {"Head": 1.0}; nh += 1
log("halo verts:", nh)
dleg = {S: np.min([seg_dist(co, *B[S+n]) for n in ("UpperLeg", "LowerLeg", "Foot", "Toes")], axis=0) for S, _ in SIDES}
darm = np.min([seg_dist(co, *B[S+n]) for S, _ in SIDES for n in ("UpperArm", "LowerArm", "Hand")], axis=0)
nr = 0
for i in range(len(co)):                                 # robe below the waist
    x, y, z = co[i]
    if z > 1.02 or darm[i] < 0.06 or any(k.startswith("Wing") for k in W[i]): continue
    if min(dleg["Left"][i], dleg["Right"][i]) < 0.045: continue
    a = sm((1.02-z)/0.10); lo = 0.45*sm((0.55-z)/0.35); Wc = {}
    for S, _ in SIDES:
        f = 0.85*math.exp(-((dleg[S][i]-0.045)/0.16)**2); Wc[S+"UpperLeg"] = f*(1-lo); Wc[S+"LowerLeg"] = f*lo
    s = sum(Wc.values())
    if s > 0.85: Wc = {k: v*0.85/s for k, v in Wc.items()}
    Wc["Hips"] = 1-sum(Wc.values())
    old = W[i]; ot = sum(old.values()) or 1
    W[i] = {k: v for k, v in ((k, (1-a)*old.get(k, 0)/ot+a*Wc.get(k, 0)) for k in set(old) | set(Wc)) if v > 1e-3}; nr += 1
log("robe verts:", nr)
# the limbs are bare bones (2-3 cm thick): anything with arm weight that is farther than that from the arm
# bones (robe, belt rope, shawl hanging next to the arm) belongs to the body, not to the arm
ARMB = ("Shoulder", "UpperArm", "LowerArm", "Hand")
TORSO = [n for n in ("Hips", "Spine", "Chest", "UpperChest", "Neck", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg")]
dtor = np.stack([seg_dist(co, *B[n]) for n in TORSO], 1)
na = 0
for S, _ in SIDES:
    dlimb = np.minimum(seg_dist(co, *B[S+"UpperArm"]), seg_dist(co, *B[S+"LowerArm"]))
    hh, ht = B[S+"Hand"]; dhand = seg_dist(co, hh, ht+(ht-hh)/np.linalg.norm(ht-hh)*0.03)   # bone + fingertips
    for i, w in enumerate(W):
        aw = sum(x for k, x in w.items() if k.startswith(S) and k.endswith(ARMB) and "Shoulder" not in k)
        if aw <= 0 or any(k.startswith("Wing") for k in w): continue
        if dlimb[i] < 0.04 or dhand[i] < 0.055: continue
        # hard below the elbow (cloth fused to the hanging forearm/hand is not arm); soft around the upper arm
        # (the shawl drapes over the shoulder and should partly follow the arm)
        lower = seg_dist(co[i:i+1], *B[S+"LowerArm"])[0] < seg_dist(co[i:i+1], *B[S+"UpperArm"])[0]
        keep = 0.0 if lower or dhand[i] < 0.12 else 1-sm((dlimb[i]-0.04)/0.03)
        tb = TORSO[int(np.argmin(dtor[i]))]
        for k in [k for k in w if k.startswith(S) and k.endswith(ARMB) and "Shoulder" not in k]:
            w[tb] = w.get(tb, 0)+w[k]*(1-keep); w[k] *= keep
        na += 1
log("cloth taken off the arms:", na)
nf = 0                                                    # finger tips the sculpt fused into the side of the robe
for S, sx in SIDES:
    hh, ht = B[S+"Hand"]; dh = seg_dist(co, hh, ht+(ht-hh)*0.3)
    for i in np.nonzero((dh < 0.07) & (co[:, 0]*sx > 0.25) & (co[:, 2] < 0.95))[0]:
        if not any(k.startswith(S) and k.endswith(("Hand", "LowerArm", "LowerArm_Twist")) and x > 0.5 for k, x in W[int(i)].items()):
            W[int(i)] = {S+"Hand": 1.0}; nf += 1
log("fused finger tips given to the hands:", nf)
# the sculpt fused the hanging hands/forearms to the side of the robe: faces joining arm vertices to
# non-arm vertices below the elbow are cut, so lifting the arm does not drag sheets of robe along
import bmesh
isarm = np.array([sum(x for k, x in w.items() if k.endswith(("LowerArm", "Hand"))) > 0.5 for w in W])
bm = bmesh.new(); bm.from_mesh(body.data); bm.faces.ensure_lookup_table()
cut = [f for f in bm.faces if co[f.verts[0].index, 2] < 1.08 and len({isarm[v.index] for v in f.verts}) == 2]
bmesh.ops.delete(bm, geom=cut, context='FACES_ONLY'); bm.to_mesh(body.data); bm.free()
log("faces cut between arm and robe:", len(cut))
# loose bits left after the cut (finger tips, bracelet beads) next to a hand: they belong to that hand
bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
seen = np.zeros(len(bm.verts), bool); nb = 0
for v0 in bm.verts:
    if seen[v0.index]: continue
    comp = [v0]; seen[v0.index] = True; k = 0
    while k < len(comp):
        for e in comp[k].link_edges:
            o = e.other_vert(comp[k])
            if not seen[o.index]: seen[o.index] = True; comp.append(o)
        k += 1
    if len(comp) > 4000: continue
    idx = np.array([v.index for v in comp])
    for S, _ in SIDES:
        hh, ht = B[S+"Hand"]
        dd = seg_dist(co[idx], hh, ht+(ht-hh)*0.3)
        if len(idx) < 1500: print("  island", len(idx), "near", S, "min %.3f mean %.3f" % (dd.min(), dd.mean()), "arm share %.2f" % isarm[idx].mean()) if dd.min() < 0.08 else None
        if dd.min() < 0.06 and dd.mean() < 0.09 and not isarm[idx].all():
            for i in idx: W[int(i)] = {S+"Hand": 1.0}
            nb += len(idx)
bm.free()
log("loose bits given to the hands:", nb)
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
