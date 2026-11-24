# Stage 1 of the sorceress rig (runs on norm.blend from step1_norm.py, one fused mesh):
#   staff cut out of the mesh (radial profile around its axis), armature in the model's own pose,
#   bone heat at 10x scale, holes / loose bits filled from the nearest weighted surface, the long open
#   coat re-weighted below the waist (follows the nearest leg close to it, the hips farther out, so the
#   wide flaring panels do not whip around), then decimated to a game mesh (weights interpolated).
# usage: blender -b norm.blend -P build.py -- <scripts_dir> <out.blend> [body_tris] [staff_tris]
import bpy, sys, time, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0])
from skeleton import J, PARENT, CONNECT, STAFF_P0, STAFF_D
import json, os
FJ = json.load(open(os.path.join(args[0], "fingers.json")))["Left"] if os.path.exists(os.path.join(args[0], "fingers.json")) else None
FINGERS = ["Thumb", "Index", "Middle", "Ring", "Little"]; PHALANX = ["Proximal", "Intermediate", "Distal"]
OUT = args[1]; BODY_TRIS = int(args[2]) if len(args) > 2 else 60000; STAFF_TRIS = int(args[3]) if len(args) > 3 else 6000
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]", *a, flush=True)
SIDES = (("Left", 1), ("Right", -1))
K = 10.0
def sm(t): t = min(max(t, 0.0), 1.0); return t*t*(3-2*t)
def select_only(o):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
def verts(o):
    c = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", c); return c.reshape(-1, 3)
def components(E, allowed):
    lab = np.arange(len(allowed)); e = E[allowed[E[:, 0]] & allowed[E[:, 1]]]
    while True:
        m = np.minimum(lab[e[:, 0]], lab[e[:, 1]]); new = lab.copy()
        np.minimum.at(new, e[:, 0], m); np.minimum.at(new, e[:, 1], m); new = new[new][new]
        if np.array_equal(new, lab): return lab
        lab = new

# ---------------------------------------------------------------- 1. cut the staff out
body = bpy.data.objects["Maga"]; me = body.data
v = verts(body); E = np.empty(len(me.edges)*2, np.int64); me.edges.foreach_get("vertices", E); E = E.reshape(-1, 2)
p0 = np.array(STAFF_P0); d = np.array(STAFF_D); d /= np.linalg.norm(d)
D = v-p0; t = D@d; r = np.linalg.norm(D-np.outer(t, d), axis=1)
cand = (((t >= -0.12) & (t < 0.05) & (r < 0.045)) |            # metal foot of the staff
        ((t >= 0.05) & (t < 1.28) & (r < 0.0155)) |            # shaft (the fist around it is at r > 2 cm)
        ((t >= 1.28) & (t < 1.80) & (r < 0.16) & (v[:, 2] > 1.28)))   # crystal head
lab = components(E, cand)
shaft = np.nonzero(cand & (t > 0.3) & (t < 0.9))[0]
ids, cnt = np.unique(lab[shaft], return_counts=True)
keep = set(ids[cnt > 20]); staff = cand & np.isin(lab, list(keep))
# pieces of the head/foot not connected through the shaft (separate crystal shards): keep if inside the region
loose = cand & ~staff & (((t >= 1.28) & (r < 0.16)) | (t < 0.05))
staff |= loose
log("staff verts:", int(staff.sum()), "t range", round(float(t[staff].min()), 3), round(float(t[staff].max()), 3))
loops = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", loops)
ls = np.empty(len(me.polygons), np.int64); me.polygons.foreach_get("loop_start", ls)
fl = np.add.reduceat(staff[loops].astype(int), ls); lt = np.empty(len(me.polygons), np.int64); me.polygons.foreach_get("loop_total", lt)
sel = fl*2 > lt
select_only(body); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_mode(type='FACE'); bpy.ops.mesh.select_all(action='DESELECT')
bpy.ops.object.mode_set(mode='OBJECT'); me.polygons.foreach_set("select", sel)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
staff_o = [o for o in bpy.context.selected_objects if o != body][0]
staff_o.name = staff_o.data.name = "Staff"; body.name = body.data.name = "Body"
for o in (body, staff_o):
    select_only(o); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.delete_loose(); bpy.ops.object.mode_set(mode='OBJECT')
    log(o.name, len(o.data.vertices), "verts", sum(len(p.vertices)-2 for p in o.data.polygons), "tris")

# ---------------------------------------------------------------- 2. joints (feet from the shoe soles)
J = {k: (Vector(h), Vector(tl)) for k, (h, tl) in J.items()}
bv = verts(body)
for S, _ in SIDES:
    ank = J[S+"LowerLeg"][1]
    low = bv[(bv[:, 2] < 0.035) & (np.linalg.norm(bv[:, :2]-np.array(ank[:2]), axis=1) < 0.22)]
    fwd = low[low[:, 1] < ank.y]                                     # toes are forward (-Y) of the ankle
    tip = fwd[np.argmin(fwd[:, 1])]; tip = Vector((tip[0], tip[1], 0.02))
    ball = tip.lerp(Vector((ank.x, ank.y, 0.02)), 0.33); ball.z = 0.025          # high heels: the ball is on the floor
    J[S+"Foot"] = (ank.copy(), ball); J[S+"Toes"] = (ball.copy(), tip)
    log(S, "foot: ankle", tuple(round(x, 3) for x in ank), "ball", tuple(round(x, 3) for x in ball), "tip", tuple(round(x, 3) for x in tip))

if FJ:                                                             # left hand fingers (fingers.py)
    J["LeftHand"] = (Vector(FJ["wrist"]), Vector(FJ["Middle"][0]))
    J["LeftLowerArm"] = (J["LeftLowerArm"][0], Vector(FJ["wrist"]))
    for fn in FINGERS:
        for k, ph in enumerate(PHALANX):
            n = f"Left{fn}{ph}"; J[n] = (Vector(FJ[fn][k]), Vector(FJ[fn][k+1]))
            PARENT[n] = "LeftHand" if k == 0 else f"Left{fn}{PHALANX[k-1]}"
            if k > 0: CONNECT.add(n)

# ---------------------------------------------------------------- 3. armature (at K scale for bone heat)
A = bpy.data.armatures.new("Armature"); arm = bpy.data.objects.new("Armature", A); bpy.context.scene.collection.objects.link(arm)
select_only(arm); bpy.ops.object.mode_set(mode='EDIT'); eb = A.edit_bones
for n, (h, tl) in J.items():
    b = eb.new(n); b.head = h*K; b.tail = tl*K
for n, p in PARENT.items():
    eb[n].parent = eb[p]; eb[n].use_connect = n in CONNECT
for b in eb:
    b.align_roll(Vector((0, 0, 1)) if any(x in b.name for x in ("Arm", "Hand", "Shoulder")) else Vector((0, -1, 0)))
if FJ:                                                             # Z toward the palm: +X rotation closes a finger
    for fn in FINGERS:
        for k, ph in enumerate(PHALANX): eb[f"Left{fn}{ph}"].align_roll(Vector(FJ["palmar"][fn][k]))
    eb["LeftHand"].align_roll(sum((Vector(FJ["palmar"][f][0]) for f in FINGERS[1:]), Vector()))
for S, _ in SIDES:
    lo = eb[S+"LowerArm"]; b = eb.new(S+"LowerArm_Twist")
    b.head = (lo.head+lo.tail)/2; b.tail = lo.tail.copy(); b.roll = lo.roll; b.parent = lo
bpy.ops.object.mode_set(mode='OBJECT')
body.data.transform(Matrix.Scale(K, 4)); staff_o.data.transform(Matrix.Scale(K, 4))
for b in A.bones: b.use_deform = not b.name.endswith("_Twist")              # twist gets its share later
# Bone heat fails on the fused, self-intersecting sculpt: solve it on a clean closed proxy (voxel remesh,
# ~1 cm, within a few mm of the real surface) and transfer the weights back (nearest face, interpolated).
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "proxy"; bpy.context.scene.collection.objects.link(proxy)
select_only(proxy); proxy.data.remesh_voxel_size = 0.010*K; bpy.ops.object.voxel_remesh()
select_only(proxy); arm.select_set(True); bpy.context.view_layer.objects.active = arm
log("bone heat on proxy:", len(proxy.data.vertices), "verts ...")
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
got = sum(1 for x in proxy.data.vertices if x.groups)
log("bone heat done: %d/%d proxy verts weighted" % (got, len(proxy.data.vertices)))
for b in A.bones: b.use_deform = True
for g in proxy.vertex_groups: body.vertex_groups.new(name=g.name)
dt = body.modifiers.new("dt", 'DATA_TRANSFER'); dt.object = proxy; dt.use_vert_data = True
dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
select_only(body); bpy.ops.object.modifier_apply(modifier=dt.name)
bpy.data.objects.remove(proxy)
body.parent = None; body.matrix_world = Matrix.Identity(4)
for md in list(body.modifiers): body.modifiers.remove(md)
body.data.transform(Matrix.Scale(1/K, 4)); staff_o.data.transform(Matrix.Scale(1/K, 4))
select_only(arm); bpy.ops.object.mode_set(mode='EDIT')
real = {b.name: (b.head/K, b.tail/K, b.roll) for b in A.edit_bones}
for b in A.edit_bones: b.head, b.tail, b.roll = real[b.name]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- 4. holes + loose islands from the main surface
bv = body.data.vertices; co = verts(body); vg = body.vertex_groups
names = {g.index: g.name for g in vg}
W = [{names[g.group]: g.weight for g in x.groups if g.weight > 1e-4} for x in bv]
bE = np.empty(len(body.data.edges)*2, np.int64); body.data.edges.foreach_get("vertices", bE); bE = bE.reshape(-1, 2)
blab = components(bE, np.ones(len(bv), bool)); ids, cnt = np.unique(blab, return_counts=True)
src = (blab == ids[np.argmax(cnt)]) & np.array([bool(w) for w in W])
fix = np.nonzero(~src)[0]
kd = KDTree(int(src.sum()))
for i in np.nonzero(src)[0]: kd.insert(co[i], int(i))
kd.balance()
for i in fix: W[int(i)] = dict(W[kd.find(co[i])[1]])
log("filled", len(fix), "verts (no weight or loose islands)")

# ---------------------------------------------------------------- 4b. left hand: fingers weighted on the real mesh
# (the 1 cm proxy fuses the fingers): what the heat gave to the hand is shared between the palm (Hand bone)
# and the finger bones by inverse distance to each bone segment (sharp: 4th power, best 3)
if FJ:
    HB = ["LeftHand"]+[f"Left{fn}{ph}" for fn in FINGERS for ph in PHALANX]
    Bh = {b.name: (np.array(b.head_local), np.array(b.tail_local)) for b in A.bones if b.name in HB}
    def sd(P_, a, b):
        ab = b-a; tt = np.clip(((P_-a)@ab)/(ab@ab), 0, 1); return np.linalg.norm(P_-(a+np.outer(tt, ab)), axis=1)
    Dh = np.stack([sd(co, *Bh[n]) for n in HB], 1)
    n_h = 0
    for i, w in enumerate(W):
        tot = sum(x for k, x in w.items() if k in HB)
        if tot < 0.3 or Dh[i].min() > 0.05: continue
        inv = 1.0/np.maximum(Dh[i], 0.002)**4; top = np.argsort(-inv)[:3]; s_ = inv[top].sum()
        for k in HB: w.pop(k, None)
        for j in top:
            if inv[j]/s_ > 0.01: w[HB[j]] = tot*inv[j]/s_
        n_h += 1
    log("left hand: fingers weighted on", n_h, "verts")

# ---------------------------------------------------------------- 5. the coat below the waist
def seg_dist(P, a, b):
    a, b = np.array(a), np.array(b); ab = b-a; tt = np.clip(((P-a)@ab)/(ab@ab), 0, 1); return np.linalg.norm(P-(a+np.outer(tt, ab)), axis=1)
B = {b.name: (np.array(b.head_local), np.array(b.tail_local)) for b in A.bones}
dleg = {S: np.min([seg_dist(co, *B[S+n]) for n in ("UpperLeg", "LowerLeg", "Foot", "Toes")], axis=0) for S, _ in SIDES}
darm = np.min([seg_dist(co, *B[S+n]) for S, _ in SIDES for n in ("UpperArm", "LowerArm", "Hand")], axis=0)
# sleeves and coat are one surface: cloth closer to the torso than to the arm must not follow the arm
# (it made "wings" from the ribs to the sleeves when the arms go up / into the T-pose)
spine_chain = [B[n] for n in ("Hips", "Spine", "Chest", "UpperChest", "Neck")]
dtorso = np.min([seg_dist(co, *sg) for sg in spine_chain], axis=0)
ARMB = ("UpperArm", "LowerArm", "LowerArm_Twist", "Hand")
def torso_bone(z): return "Hips" if z < 1.0 else "Spine" if z < 1.1 else "Chest" if z < 1.2 else "UpperChest"
n_arm = 0
for i, w in enumerate(W):
    if not any(k.endswith(ARMB) for k in w): continue
    f = sm((dtorso[i]-darm[i]+0.04)/0.10)                         # 0: belongs to the torso, 1: to the arm
    if f >= 0.999: continue
    moved = 0.0
    for k in [k for k in w if k.endswith(ARMB)]:
        moved += w[k]*(1-f); w[k] *= f
    tb = torso_bone(co[i][2]); w[tb] = w.get(tb, 0)+moved; n_arm += 1
log("cloth taken off the arms (closer to the torso):", n_arm, "verts")
n_coat = 0
for i in range(len(co)):
    x, y, z = co[i]
    if z > 0.92 or darm[i] < 0.14: continue                      # torso, sleeves stay with the heat weights
    near = min(dleg["Left"][i], dleg["Right"][i])
    if near < 0.075: continue                                      # the legs / boots themselves
    a = sm((0.92-z)/0.10)                                          # blend band at the waist
    lo = 0.45*sm((0.55-z)/0.35)
    Wc = {}
    tot = 0.0
    for S, _ in SIDES:
        f = 0.85*math.exp(-((dleg[S][i]-0.075)/0.16)**2)
        Wc[S+"UpperLeg"] = f*(1-lo); Wc[S+"LowerLeg"] = f*lo; tot += f
    s = sum(Wc.values())
    if s > 0.85: Wc = {k: w*0.85/s for k, w in Wc.items()}
    Wc["Hips"] = 1-sum(Wc.values())
    old = W[i]; ot = sum(old.values()) or 1
    new = {k: (1-a)*old.get(k, 0)/ot+a*Wc.get(k, 0) for k in set(old) | set(Wc)}
    W[i] = {k: w for k, w in new.items() if w > 1e-3}; n_coat += 1
log("coat verts re-weighted:", n_coat)

# boots: explicit, clean weights (heat on the proxy + the coat rule had left hips/thigh weight in the feet,
# which tore the heel into a spike when walking): shin above the ankle, Foot from the heel to the ball,
# Toes past the ball, a short blend at each joint. The heel spike is behind the ankle -> Foot.
n_boot = 0
for S, sx in SIDES:
    ank, ball = B[S+"Foot"]; tip = B[S+"Toes"][1]; fd = ball-ank; fl = np.linalg.norm(fd)
    dfoot = np.minimum(seg_dist(co, ank, ball), seg_dist(co, ball, tip))
    dother = dleg["Left" if S == "Right" else "Right"]
    for i in np.nonzero((dfoot < 0.075) & (co[:, 2] < ank[2]+0.06) & (dfoot < dother))[0]:
        p = co[i]; t = ((p-ank)@fd)/(fl*fl)                        # 0 at the ankle, 1 at the ball
        toes = sm((t-0.92)/0.16)
        shin = sm((p[2]-ank[2]-0.005)/0.05)*(1-sm(t/0.3))           # boot shaft right above the ankle
        w = {S+"LowerLeg": shin}; rest = 1-shin
        w[S+"Foot"] = rest*(1-toes); w[S+"Toes"] = rest*toes
        W[i] = {k: x for k, x in w.items() if x > 1e-3}; n_boot += 1
log("boots re-weighted:", n_boot, "verts")

# twist: wrist half of the forearm weight moves to the twist bone (smooth ramp)
for S, _ in SIDES:
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

# ---------------------------------------------------------------- 6. game mesh: decimate (weights interpolated), clean
for o, target in ((body, BODY_TRIS), (staff_o, STAFF_TRIS)):
    tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    m = o.modifiers.new("Decimate", 'DECIMATE'); m.decimate_type = 'COLLAPSE'; m.ratio = min(1.0, target/tris); m.use_collapse_triangulate = True
    select_only(o); bpy.ops.object.modifier_apply(modifier=m.name)
    log(o.name, "decimated", tris, "->", sum(len(p.vertices)-2 for p in o.data.polygons), "tris")
select_only(body)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.01)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
empty = sum(1 for x in body.data.vertices if not x.groups)
log("Body:", len(body.data.vertices), "verts; unweighted:", empty)
body.parent = arm; md = body.modifiers.new("Armature", 'ARMATURE'); md.object = arm
staff_o.parent = arm
bpy.ops.wm.save_as_mainfile(filepath=OUT); log("saved", OUT)
