# Stage 1 of the dark knight rig (runs on norm.blend from step1_norm.py):
#   armature in the model's own pose (body + feet + twist + fingers from fingers.json),
#   every loose piece weighted on its own: cloth/armour pieces with bone heat restricted to the bones
#   of their body region, small rigid bits (plates, rings, bolts, crown, mask) copy one constant
#   weight set from the nearest weighted surface, then everything is joined into Body; Sword stays apart.
# usage: blender -b norm.blend -P build.py -- <scripts_dir> <fingers.json> <out.blend>
import bpy, sys, json, time, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0])
from skeleton import J, PARENT, CONNECT, FINGERS, PHALANX
FJ = json.load(open(args[1])); OUT = args[2]
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]", *a, flush=True)
SIDES = (("Left", 1), ("Right", -1))
K = 10.0     # bone heat fails at real scale on small pieces (precision): solve everything at 10x

def verts(o):
    c = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", c); return c.reshape(-1, 3)
def select_only(o):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o

# ---------------------------------------------------------------- 1. pieces: sword apart, the rest split in loose parts
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
sword_parts = [o for o in meshes if o.name.startswith("Merged_Sword")]
select_only(sword_parts[0])
for o in sword_parts: o.select_set(True)
bpy.ops.object.join(); sword = bpy.context.object; sword.name = sword.data.name = "Sword"
# the source has thousands of stray vertices/edges without faces: delete them everywhere first
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    select_only(o); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.delete_loose(); bpy.ops.object.mode_set(mode='OBJECT')
pieces = []
for o in [o for o in bpy.data.objects if o.type == 'MESH' and o != sword]:
    src = o.name
    select_only(o); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE'); bpy.ops.object.mode_set(mode='OBJECT')
    for p in bpy.context.selected_objects: p["src"] = src; pieces.append(p)
junk = [p for p in pieces if len(p.data.polygons) == 0]
for p in junk: bpy.data.objects.remove(p)
pieces = [p for p in pieces if p not in junk]
log("pieces:", len(pieces))

# ---------------------------------------------------------------- 2. joints
J = {k: (Vector(h), Vector(t)) for k, (h, t) in J.items()}
boots = {S: np.concatenate([verts(p) for p in pieces if p["src"].startswith("Boot_Toe") and verts(p)[:, 0].mean()*sx > 0])
         for S, sx in SIDES}
for S, sx in SIDES:
    ank = J[S+"LowerLeg"][1]; b = boots[S]; low = b[b[:, 2] < 0.05]
    dxy = low[:, :2]-np.array(ank[:2]); fwd = dxy[:, 1] < 0             # toes are forward (-Y)
    k = np.argmax(np.where(fwd, np.linalg.norm(dxy, axis=1), -1)); tip = low[k]
    tip = Vector((tip[0], tip[1], 0.035)); ball = Vector(ank).lerp(tip, 0.68); ball.z = 0.045
    J[S+"Foot"] = (ank.copy(), ball); J[S+"Toes"] = (ball.copy(), tip)
    log(S, "foot: ankle", tuple(round(x, 3) for x in ank), "ball", tuple(round(x, 3) for x in ball), "tip", tuple(round(x, 3) for x in tip))
for S, _ in SIDES:
    f = {n: [Vector(p) for p in FJ[S][n]] for n in FINGERS}
    wrist = J[S+"Hand"][0]
    f["Thumb"][0] = wrist.lerp(f["Thumb"][1], 0.5)                     # thumb CMC sits in the palm near the wrist
    J[S+"Hand"] = (wrist, f["Middle"][0].copy())
    for fn in FINGERS:
        for k, ph in enumerate(PHALANX):
            n = f"{S}{fn}{ph}"; J[n] = (f[fn][k], f[fn][k+1])
            PARENT[n] = S+"Hand" if k == 0 else f"{S}{fn}{PHALANX[k-1]}"
            if k > 0: CONNECT.add(n)

# ---------------------------------------------------------------- 3. armature (built at K scale for bone heat)
A = bpy.data.armatures.new("Armature"); arm = bpy.data.objects.new("Armature", A)
bpy.context.scene.collection.objects.link(arm)
select_only(arm); bpy.ops.object.mode_set(mode='EDIT'); eb = A.edit_bones
for n, (h, t) in J.items():
    b = eb.new(n); b.head = h*K; b.tail = t*K
for n, p in PARENT.items():
    eb[n].parent = eb[p]; eb[n].use_connect = n in CONNECT
for b in eb:
    if any(x in b.name for x in ("Arm", "Hand", "Shoulder")) or any(f in b.name for f in FINGERS): b.align_roll(Vector((0, 0, 1)))
    else: b.align_roll(Vector((0, -1, 0)))
for S, sx in SIDES:   # fingers + hand: Z toward the palm, so a positive rotation about local X closes the hand
    palm = FJ[S]["palmar"]
    for fn in FINGERS:
        for k, ph in enumerate(PHALANX): eb[f"{S}{fn}{ph}"].align_roll(Vector(palm[fn][k]))
    eb[S+"Hand"].align_roll(sum((Vector(palm[fn][0]) for fn in FINGERS[1:]), Vector()))
    lo = eb[S+"LowerArm"]; b = eb.new(S+"LowerArm_Twist")
    b.head = (lo.head+lo.tail)/2; b.tail = lo.tail.copy(); b.roll = lo.roll; b.parent = lo
bpy.ops.object.mode_set(mode='OBJECT')
for p in pieces: p.data.transform(Matrix.Scale(K, 4))
sword.data.transform(Matrix.Scale(K, 4))

# ---------------------------------------------------------------- 4. region of every piece -> allowed bones
def fingers(S): return [f"{S}{fn}{ph}" for fn in FINGERS for ph in PHALANX]
SPINE = ["Hips", "Spine", "Chest", "UpperChest"]
LEGS = ["LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg"]
def region(src, S):
    if src.startswith("Gloves"): return [S+"Hand", S+"LowerArm"]+fingers(S)
    if src.startswith("Glove_MainPlate"): return "rigid"
    if src.startswith("Bracer"): return [S+"LowerArm"] if not src.startswith("Bracer_Ring") else "rigid"
    if src.startswith("Shoulder_Elbow"): return [S+"UpperArm", S+"LowerArm"]
    if src.startswith(("Shoulder_Plate", "Shoulder_Left", "Shoulder_Right")): return [S+"Shoulder", S+"UpperArm"]
    if src.startswith("ArmStrap"): return [S+"UpperArm"]
    if src.startswith("UnderShoulder"): return ["Chest", "UpperChest", "LeftShoulder", "RightShoulder", "LeftUpperArm", "RightUpperArm"]
    if src.startswith("Padded_Shirt"):
        return SPINE+["Neck"]+[s+b for s, _ in SIDES for b in ("Shoulder", "UpperArm", "LowerArm")]
    if src.startswith("Chest_TopStraps"): return ["Chest", "UpperChest", "LeftShoulder", "RightShoulder"]
    if src.startswith("Chest_"): return SPINE
    if src.startswith("Belt"): return ["Hips", "Spine"]
    if src.startswith(("Padded_Pants", "Dress")): return ["Hips", "Spine"]+LEGS
    if src.startswith("Boot_Toe"): return [S+"LowerLeg", S+"Foot", S+"Toes"]
    if src.startswith("Hood"): return ["Head", "Neck", "UpperChest"]
    if src.startswith(("Padded_Cover", "Padded_NeckStrap")): return ["Head", "Neck"]
    if src.startswith(("Head_Mask", "Crown")): return ["Head"]
    raise ValueError(src)
SMALL = 0.07*K
heat, rigid, single = [], [], []
for p in pieces:
    v = verts(p); S = "Left" if v[:, 0].mean() > 0 else "Right"
    r = region(p["src"], S); diag = np.linalg.norm(v.max(0)-v.min(0))
    if r == "rigid" or diag < SMALL: rigid.append(p)
    elif len(r) == 1: single.append((p, r[0]))
    else: heat.append((p, r))
log("heat:", len(heat), "single-bone:", len(single), "rigid:", len(rigid))

# ---------------------------------------------------------------- 5. weights
deform_all = [b.name for b in A.bones]
for p, r in heat:
    for b in A.bones: b.use_deform = b.name in r
    select_only(p); arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    empty = sum(1 for vv in p.data.vertices if not any(g.weight > 1e-4 for g in vv.groups))
    if empty: log("  heat left", empty, "/", len(p.data.vertices), "verts empty on", p.name, p["src"])
    p["empty"] = empty
for b in A.bones: b.use_deform = True
# The long robe: bone heat tied it mostly to the hips, so the legs (with rigid greaves) came out through
# the cloth when stepping. Explicit weights instead: hips at the belt, legs below; each side follows its
# own leg (soft split at the centre), and the hem also follows the shin.
def sm(t): t = min(max(t, 0.0), 1.0); return t*t*(3-2*t)
for p in [p for p, _ in heat if p["src"].startswith("Dress")]:
    for g in list(p.vertex_groups): p.vertex_groups.remove(g)
    G = {n: p.vertex_groups.new(name=n) for n in ("Hips", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg")}
    for v in p.data.vertices:
        x, y, z = (v.co/K)[:]
        h = 0.12+0.88*sm((z-0.80)/0.22)                    # hips: 1 at the belt -> 0.12 from mid-thigh down
        lo = 0.5*sm((0.58-z)/0.38)                          # share of the leg weight that follows the shin
        b = sm((x+0.05)/0.10)                               # left/right split, 10 cm soft band at the centre
        back = sm((y-0.0)/0.08)                             # back panel: hangs more from the hips (no "sail"
        h = h+(1-h)*0.4*back                                #  when the leg goes back); the front keeps up with the leg
        W = {"Hips": h}
        for S, share in (("Left", b), ("Right", 1-b)):
            W[S+"UpperLeg"] = (1-h)*share*(1-lo); W[S+"LowerLeg"] = (1-h)*share*lo
        for n, w in W.items():
            if w > 1e-3: G[n].add([v.index], w, 'REPLACE')
    p["tunica"] = True
    log("robe reweighted:", p.name, len(p.data.vertices), "verts")
for p, bn in single:
    g = p.vertex_groups.new(name=bn); g.add(range(len(p.data.vertices)), 1.0, 'REPLACE')

# reference field: all weighted verts of heat/single pieces
ref_co, ref_w = [], []
for p in [p for p, _ in heat]+[p for p, _ in single]:
    names = {g.index: g.name for g in p.vertex_groups}
    for vv in p.data.vertices:
        w = {names[g.group]: g.weight for g in vv.groups if g.weight > 1e-4}
        if w: ref_co.append(vv.co.copy()); ref_w.append(w)
kd = KDTree(len(ref_co))
for i, c in enumerate(ref_co): kd.insert(c, i)
kd.balance()
def blend(ws):
    acc = {}
    for w in ws:
        t = sum(w.values())
        for n, x in w.items(): acc[n] = acc.get(n, 0)+x/t/len(ws)
    return acc
def apply(p, i, w):
    for n, x in w.items():
        g = p.vertex_groups.get(n) or p.vertex_groups.new(name=n); g.add([i], x, 'REPLACE')
# heat holes: per-vertex copy from the nearest weighted vertex (own piece first)
for p, _ in heat:
    if not p["empty"]: continue
    names = {g.index: g.name for g in p.vertex_groups}
    own = [(vv.co.copy(), {names[g.group]: g.weight for g in vv.groups if g.weight > 1e-4}) for vv in p.data.vertices]
    own = [(c, w) for c, w in own if w]
    k2 = KDTree(max(1, len(own)))
    for i, (c, _) in enumerate(own): k2.insert(c, i)
    k2.balance()
    for vv in p.data.vertices:
        if any(g.weight > 1e-4 for g in vv.groups): continue
        w = own[k2.find(vv.co)[1]][1] if own else ref_w[kd.find(vv.co)[1]]
        apply(p, vv.index, w)
# rigid: one constant weight set per piece = blend of the nearest reference weights of its verts
for p in rigid:
    for g in list(p.vertex_groups): p.vertex_groups.remove(g)
    ws = [ref_w[kd.find(vv.co)[1]] for vv in p.data.vertices]
    w = blend(ws); w = {n: x for n, x in w.items() if x > 0.05}; t = sum(w.values()); w = {n: x/t for n, x in w.items()}
    for n, x in w.items():
        g = p.vertex_groups.new(name=n); g.add(range(len(p.data.vertices)), x, 'REPLACE')
log("weights done")

# ---------------------------------------------------------------- 6. join, back to real scale
for p in pieces:
    for md in list(p.modifiers): p.modifiers.remove(md)
    p.parent = None; p.matrix_world = Matrix.Identity(4)
select_only(pieces[0])
for p in pieces: p.select_set(True)
robe_pieces = [p for p in pieces if p.get("tunica")]
for p in robe_pieces: p.vertex_groups.new(name="_tunica_tmp").add(range(len(p.data.vertices)), 1.0, 'REPLACE')
bpy.ops.object.join(); body = bpy.context.object; body.name = body.data.name = "Body"
body.data.transform(Matrix.Scale(1/K, 4)); sword.data.transform(Matrix.Scale(1/K, 4))
select_only(arm); bpy.ops.object.mode_set(mode='EDIT')
real = {b.name: (b.head/K, b.tail/K, b.roll) for b in A.edit_bones}   # (connected bones share points:
for b in A.edit_bones: b.head, b.tail, b.roll = real[b.name]           #  read all first, then write)
bpy.ops.object.mode_set(mode='OBJECT')
# merge coincident duplicated verts between pieces? no: pieces stay separate surfaces (keeps UVs/normals intact)

# twist: wrist half of the forearm weight moves to the twist bone (smooth ramp along the forearm)
vg = body.vertex_groups; co = verts(body); bv = body.data.vertices
for S, _ in SIDES:
    if S+"LowerArm_Twist" not in vg: vg.new(name=S+"LowerArm_Twist")
    bn = A.bones[S+"LowerArm"]; h, t = bn.head_local, bn.tail_local; ax = t-h; L2 = ax.length_squared
    gl, gt = vg[S+"LowerArm"], vg[S+"LowerArm_Twist"]
    for vv in bv:
        w = next((g.weight for g in vv.groups if g.group == gl.index), 0.0)
        if w <= 0: continue
        s = (Vector(co[vv.index])-h).dot(ax)/L2
        f = min(max((s-0.3)/0.5, 0.0), 1.0); f = f*f*(3-2*f)
        if f > 0: gl.add([vv.index], w*(1-f), 'REPLACE'); gt.add([vv.index], w*f, 'REPLACE')
tg = body.vertex_groups["_tunica_tmp"]                                  # robe tag: kept out of the normalisation
robe_idx = [v.index for v in body.data.vertices if any(g.group == tg.index for g in v.groups)]
body.vertex_groups.remove(tg)
select_only(body)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.001)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
body.vertex_groups.new(name="_tunica").add(robe_idx, 1.0, 'REPLACE')   # tag for the floor fixes (no bone: not exported)
empty = sum(1 for vv in body.data.vertices if not vv.groups)
log("Body:", len(body.data.vertices), "verts", sum(len(p.vertices)-2 for p in body.data.polygons), "tris; unweighted verts:", empty)
body.parent = arm; md = body.modifiers.new("Armature", 'ARMATURE'); md.object = arm
sword.parent = arm
arm.show_in_front = True
bpy.ops.wm.save_as_mainfile(filepath=OUT); log("saved", OUT)
