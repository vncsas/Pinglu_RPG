# Stage 2 of the knight rig (runs on the output of build.py):
#   twist + tabard bones and weights, T-pose rest, weapons as rigid props on socket bones,
#   extra sockets (sheath / back), Blender control rig (IK legs with reverse foot, IK/FK arms),
#   then FBX export (deform bones only, no leaf bones).
# usage: blender -b cavaleiro_rig.blend -P rig_v2.py -- <scripts_dir> <out_dir>
import bpy, sys, math, time, numpy as np
from mathutils import Vector, Matrix
d, outdir = sys.argv[-2:]; sys.path.insert(0, d)
import segment
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]", *a, flush=True)
def upd(): bpy.context.view_layer.update()

arm = bpy.data.objects["Armature"]; A = arm.data
body, sword, shield = (bpy.data.objects[n] for n in ("Body", "Sword", "Shield"))
SIDES = (("Left", 1), ("Right", -1))

def mode(m, obj=arm):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = obj; obj.select_set(True)
    if m != 'OBJECT': bpy.ops.object.mode_set(mode=m)

def verts(o):
    c = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", c); return c.reshape(-1, 3)

# ---------------------------------------------------------------- 1. twist + tabard bones (A-pose)
TABARD = [((0.025, -0.005, 0.92), (0.025, -0.008, 0.78)),
          ((0.025, -0.008, 0.78), (0.028, -0.010, 0.66)),
          ((0.028, -0.010, 0.66), (0.035, -0.010, 0.54))]
mode('EDIT'); eb = A.edit_bones
for S, _ in SIDES:
    lo = eb[S+"LowerArm"]; b = eb.new(S+"LowerArm_Twist")
    b.head = (lo.head+lo.tail)/2; b.tail = lo.tail.copy(); b.roll = lo.roll; b.parent = lo
for i, (h, t) in enumerate(TABARD):
    b = eb.new(f"Tabard_{i+1}"); b.head = h; b.tail = t; b.align_roll(Vector((0, -1, 0)))
    b.parent = eb["Hips"] if i == 0 else eb[f"Tabard_{i}"]; b.use_connect = i > 0
mode('OBJECT')

# ---------------------------------------------------------------- 2. weights for the new bones
vg = body.vertex_groups
for n in ["LeftLowerArm_Twist", "RightLowerArm_Twist"]+[f"Tabard_{i+1}" for i in range(3)]:
    if n not in vg: vg.new(name=n)
bv = body.data.vertices; co = verts(body)
# twist: wrist half of the forearm weight moves to the twist bone (smooth ramp along the forearm)
for S, _ in SIDES:
    bn = A.bones[S+"LowerArm"]; h, t = bn.head_local, bn.tail_local; ax = t-h; L2 = ax.length_squared
    gl, gt = vg[S+"LowerArm"], vg[S+"LowerArm_Twist"]
    moved = 0
    for vv in bv:
        w = next((g.weight for g in vv.groups if g.group == gl.index), 0.0)
        if w <= 0: continue
        s = (Vector(co[vv.index])-h).dot(ax)/L2
        f = min(max((s-0.3)/0.5, 0.0), 1.0); f = f*f*(3-2*f)
        if f > 0:
            gl.add([vv.index], w*(1-f), 'REPLACE'); gt.add([vv.index], w*f, 'REPLACE'); moved += 1
    log(S, "forearm twist verts:", moved)
# tabard: the flap hangs free below the belt -> connected sheet under z=0.885 grown from a seed on it
E = np.empty(len(body.data.edges)*2, np.int64); body.data.edges.foreach_get("vertices", E); E = E.reshape(-1, 2)
allowed = co[:, 2] < 0.885
lab = segment.components(E, allowed)
seed = np.argmin(np.where(allowed, np.linalg.norm(co-np.array([0.03, -0.005, 0.70]), axis=1), 9))
tab = allowed & (lab == lab[seed])
log("tabard verts:", int(tab.sum()), "bbox", np.round(co[tab].min(0), 3), np.round(co[tab].max(0), 3))
nodes = [(0.90, "Hips"), (0.84, "Tabard_1"), (0.72, "Tabard_2"), (0.60, "Tabard_3")]
for i in np.nonzero(tab)[0]:
    i = int(i); z = co[i, 2]
    for g in list(bv[i].groups): vg[g.group].remove([i])
    if z >= nodes[0][0]: ws = {nodes[0][1]: 1.0}
    elif z <= nodes[-1][0]: ws = {nodes[-1][1]: 1.0}
    else:
        for (z0, n0), (z1, n1) in zip(nodes, nodes[1:]):
            if z1 <= z <= z0:
                f = (z0-z)/(z0-z1); ws = {n0: 1-f, n1: f}; break
    for n, w in ws.items():
        if w > 1e-4: vg[n].add([i], w, 'REPLACE')
mode('OBJECT', body)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)

# ---------------------------------------------------------------- 3. weapon frames measured in A-pose
def socket_local(o, bone):
    M = arm.matrix_world @ A.bones[bone].matrix_local
    w = np.array([(o.matrix_world @ Vector(c))[:] for c in verts(o)])
    R = np.array(M.to_3x3()); t = np.array(M.translation)
    return (w-t) @ R, R            # local coords (rows), socket rotation
sh_loc, R_sh = socket_local(shield, "LeftForearmShield")
c_sh = (sh_loc.min(0)+sh_loc.max(0))/2
ev, evec = np.linalg.eigh(np.cov((sh_loc-sh_loc.mean(0)).T))
n_loc, u_loc = evec[:, 0], evec[:, 2]                      # thinnest axis = plate normal, longest = up
if (R_sh @ n_loc)[0] < 0: n_loc = -n_loc                   # outward = +X world in the A-pose
if (R_sh @ u_loc)[2] < 0: u_loc = -u_loc                   # up = +Z world
u_loc = u_loc-n_loc*n_loc.dot(u_loc); u_loc /= np.linalg.norm(u_loc)
sw_loc, _ = socket_local(sword, "RightHandWeapon")
blade = sw_loc[sw_loc[:, 1] > 0.15][:, [0, 2]]
ev2, evec2 = np.linalg.eigh(np.cov((blade-blade.mean(0)).T))
w_loc = np.array([evec2[0, 1], 0.0, evec2[1, 1]])          # blade width direction (in local XZ)
log("shield normal/up (local)", np.round(n_loc, 2), np.round(u_loc, 2), "sword width", np.round(w_loc, 2))

# ---------------------------------------------------------------- 4. T-pose rest
APOSE_DIRS = {b.name: list((b.tail_local-b.head_local).normalized()) for b in A.bones}
mode('POSE')
P = arm.pose.bones
def aim(n, dvec):
    pb = P[n]; dvec = Vector(dvec).normalized(); h = pb.head.copy()
    q = (pb.tail-pb.head).normalized().rotation_difference(dvec)
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix; upd()
for S, sx in SIDES:
    # slight pre-bend (~7 deg) at elbows (backward) and knees (forward): a fully straight chain is
    # a singularity for the IK solver, which then refuses to bend at all
    aim(S+"UpperArm", (sx, 0.06, 0)); aim(S+"LowerArm", (sx, -0.06, 0)); aim(S+"Hand", (sx, 0, 0))
    aim(S+"UpperLeg", (sx*0.06, -0.06, -1)); aim(S+"LowerLeg", (sx*0.06, 0.06, -1))
    fd = Vector(APOSE_DIRS[S+"Foot"]); aim(S+"Foot", (0, fd.y, fd.z)); aim(S+"Toes", (0, -1, 0))
dz = 0.12-min(P["LeftFoot"].head.z, P["RightFoot"].head.z)   # keep the soles on the floor
m = P["Hips"].matrix.copy(); m.translation.z += dz; P["Hips"].matrix = m; upd()
log("T-pose: hips raised", round(dz, 3))
mode('OBJECT')
for o in (body, sword, shield):                              # bake the posed meshes
    mode('OBJECT', o)
    for md in [md for md in o.modifiers if md.type == 'ARMATURE']: bpy.ops.object.modifier_apply(modifier=md.name)
mode('POSE'); bpy.ops.pose.select_all(action='SELECT'); bpy.ops.pose.armature_apply(selected=False); mode('OBJECT')
md = body.modifiers.new("Armature", 'ARMATURE'); md.object = arm
arm["apose_dirs"] = APOSE_DIRS; arm["apose_hips_dz"] = dz

# ---------------------------------------------------------------- 5. weapons -> rigid props parented to socket bones
def to_prop(o, bone):
    M = arm.matrix_world @ A.bones[bone].matrix_local
    o.data.transform(M.inverted() @ o.matrix_world)
    o.vertex_groups.clear()
    for md in list(o.modifiers): o.modifiers.remove(md)
    o.parent = arm; o.parent_type = 'BONE'; o.parent_bone = bone; o.matrix_parent_inverse = Matrix.Identity(4)
    upd(); o.matrix_world = M; upd()
to_prop(sword, "RightHandWeapon"); to_prop(shield, "LeftForearmShield")

# ---------------------------------------------------------------- 6. sockets: sword sheath (left hip), shield on the back
def frame(src_axes, dst_axes, head):
    a = np.column_stack([src_axes[0], src_axes[1], np.cross(src_axes[0], src_axes[1])])
    b = np.column_stack([dst_axes[0], dst_axes[1], np.cross(dst_axes[0], dst_axes[1])])
    R = Matrix((b @ a.T).tolist()); M = R.to_4x4(); M.translation = Vector(head); return M
sheath_dir = Vector((0.12, 0.62, -0.78)).normalized()
sheath_w = sheath_dir.cross(Vector((1, 0, 0))).normalized()
M_sheath = frame([np.array([0, 1, 0]), w_loc], [np.array(sheath_dir), np.array(sheath_w)], (0.27, 0.02, 1.0+dz))
back_c = np.array([0.01, 0.47, 1.10+dz])
Mb = frame([n_loc, u_loc], [np.array([0, 1, 0]), np.array([0, 0, 1])], (0, 0, 0))
M_back = Mb.copy(); M_back.translation = Vector(back_c-np.array(Mb.to_3x3()) @ c_sh)

# ---------------------------------------------------------------- 7. control rig (non-deform; not exported)
mode('EDIT'); eb = A.edit_bones
def nb(name, head, tail, parent=None, roll_up=None, M=None, deform=False):
    b = eb.new(name); b.head = head; b.tail = tail
    if M is not None: b.matrix = M
    elif roll_up is not None: b.align_roll(Vector(roll_up))
    b.parent = eb[parent] if parent else None; b.use_deform = deform; return b
for name, M, par in (("SwordSheath", M_sheath, "Hips"), ("ShieldBack", M_back, "UpperChest")):
    nb(name, (0, 0, 0), (0, 0.12, 0), par, M=M, deform=True)    # deform=True so the FBX keeps them (no weights)
nb("CTRL_Root", (0, 0, 0), (0, 0.45, 0))
for S, sx in SIDES:
    ft, to = eb[S+"Foot"], eb[S+"Toes"]
    ank, ball, tip = ft.head.copy(), to.head.copy(), to.tail.copy()
    heel = Vector((ank.x, ank.y+0.05, 0.0))
    nb("IK_Foot_"+S, heel, Vector((tip.x, tip.y, 0.0)), "CTRL_Root", roll_up=(0, 0, 1))
    nb("RF_Toe_"+S, tip, tip+Vector((0, 0.06, 0)), "IK_Foot_"+S, roll_up=(0, 0, 1))
    nb("RF_Ball_"+S, ball, ball+Vector((0, 0.06, 0)), "RF_Toe_"+S, roll_up=(0, 0, 1))
    b = nb("MCH_Foot_"+S, ank, ball, "RF_Ball_"+S); b.roll = ft.roll
    b = nb("MCH_Toes_"+S, ball, tip, "RF_Toe_"+S); b.roll = to.roll
    nb("MCH_AnkleTarget_"+S, ank, ank+Vector((0, 0, 0.01)), "RF_Ball_"+S)
    knee = eb[S+"UpperLeg"].tail.copy()
    nb("POLE_Knee_"+S, knee+Vector((0, -0.5, 0)), knee+Vector((0, -0.58, 0)), "IK_Foot_"+S)
    hd = eb[S+"Hand"]; b = nb("IK_Hand_"+S, hd.head.copy(), hd.tail.copy(), "CTRL_Root"); b.roll = hd.roll
    elbow = eb[S+"UpperArm"].tail.copy()
    nb("POLE_Elbow_"+S, elbow+Vector((0, 0.45, 0)), elbow+Vector((0, 0.53, 0)), "CTRL_Root")
mode('OBJECT')

# bone collections + colours
cols = {n: A.collections.new(n) for n in ("Deform", "Controles", "Mecanismo", "Encaixes")}
for b in A.bones:
    n = b.name
    c = "Mecanismo" if n.startswith("MCH_") else "Controles" if n.startswith(("CTRL_", "IK_", "RF_", "POLE_")) else \
        "Encaixes" if n in ("RightHandWeapon", "LeftForearmShield", "SwordSheath", "ShieldBack") else "Deform"
    cols[c].assign(b)
    b.color.palette = 'THEME04' if ("Left" in n or n.endswith("_Left")) else 'THEME01' if ("Right" in n or n.endswith("_Right")) else 'THEME09'
cols["Mecanismo"].is_visible = False

# widgets (custom shapes)
wgt = bpy.data.collections.new("WGT"); bpy.context.scene.collection.children.link(wgt)
def widget(name, pts, edges):
    me = bpy.data.meshes.new(name); me.from_pydata(pts, edges, []); o = bpy.data.objects.new(name, me); wgt.objects.link(o); return o
ring = [(math.cos(a)*0.5, 0.0, math.sin(a)*0.5) for a in np.linspace(0, 2*math.pi, 33)[:-1]]
W_CIRCLE = widget("WGT_circle", ring, [(i, (i+1) % 32) for i in range(32)])
cube = [(x, y, z) for x in (-.5, .5) for y in (0, 1) for z in (-.5, .5)]
W_BOX = widget("WGT_box", cube, [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)])
sph = ring+[(p[0], p[2], 0) for p in ring]+[(0, p[0], p[2]) for p in ring]
W_SPHERE = widget("WGT_sphere", sph, [(k*32+i, k*32+(i+1) % 32) for k in range(3) for i in range(32)])
bpy.context.view_layer.layer_collection.children["WGT"].exclude = True

P = arm.pose.bones
def shape(n, w, s):
    P[n].custom_shape = w; P[n].custom_shape_scale_xyz = s; A.bones[n].show_wire = True
shape("CTRL_Root", W_CIRCLE, (3, 3, 3))
for S, _ in SIDES:
    shape("IK_Foot_"+S, W_BOX, (0.6, 1, 0.3)); shape("RF_Toe_"+S, W_CIRCLE, (1.2, 1, 1.2)); shape("RF_Ball_"+S, W_CIRCLE, (1.6, 1, 1.6))
    shape("POLE_Knee_"+S, W_SPHERE, (0.6, 0.6, 0.6)); shape("POLE_Elbow_"+S, W_SPHERE, (0.6, 0.6, 0.6))
    shape("IK_Hand_"+S, W_BOX, (0.9, 1, 0.9))

# constraints + IK/FK switches (custom props on CTRL_Root, drivers on the influences)
root = P["CTRL_Root"]
def prop(name, val):
    root[name] = val; root.id_properties_ui(name).update(min=0.0, max=1.0, soft_min=0.0, soft_max=1.0, description="0 = FK, 1 = IK")
def drive(con, propname):
    fc = con.driver_add("influence"); dr = fc.driver; dr.type = 'AVERAGE'
    var = dr.variables.new(); var.type = 'SINGLE_PROP'; t = var.targets[0]
    t.id_type = 'OBJECT'; t.id = arm; t.data_path = f'pose.bones["CTRL_Root"]["{propname}"]'
c = P["Hips"].constraints.new('CHILD_OF'); c.target = arm; c.subtarget = "CTRL_Root"; c.inverse_matrix = Matrix.Identity(4)
for S, _ in SIDES:
    prop("ik_perna_"+S, 1.0); prop("ik_braco_"+S, 0.0)
    for limb, tgt, pole, chain_end, end_ctrl, pp in (("Leg", "MCH_AnkleTarget_"+S, "POLE_Knee_"+S, S+"LowerLeg", None, "ik_perna_"+S),
                                                      ("Arm", "IK_Hand_"+S, "POLE_Elbow_"+S, S+"LowerArm", None, "ik_braco_"+S)):
        ik = P[chain_end].constraints.new('IK'); ik.target = arm; ik.subtarget = tgt
        ik.pole_target = arm; ik.pole_subtarget = pole; ik.chain_count = 2
        # pole angle that leaves the rest pose untouched: numeric search (coarse 5 deg, then 0.1 deg)
        base = S+("UpperLeg" if limb == "Leg" else "UpperArm")
        ik.influence = 1.0
        def err(deg):
            ik.pole_angle = math.radians(deg); upd()
            e = 0.0
            for n in (base, chain_end):
                pb, bb = P[n], A.bones[n]
                e += (pb.tail-bb.tail_local).length
                e += sum(abs(x-y) for ra, rb in zip(pb.matrix.to_3x3(), bb.matrix_local.to_3x3()) for x, y in zip(ra, rb))*0.05
            return e
        best = min(range(-180, 180, 5), key=err)
        for step in (1.0, 0.1):
            best = min((best+k*step for k in range(-5, 6)), key=err)
        ik.pole_angle = math.radians(best); e = err(best)
        log(S, limb, "pole angle", round(best, 1), "rest error", round(e, 5))
        drive(ik, pp)
    for own, tgt, pp in ((S+"Foot", "MCH_Foot_"+S, "ik_perna_"+S), (S+"Toes", "MCH_Toes_"+S, "ik_perna_"+S),
                         (S+"Hand", "IK_Hand_"+S, "ik_braco_"+S)):
        cr = P[own].constraints.new('COPY_ROTATION'); cr.target = arm; cr.subtarget = tgt; drive(cr, pp)
    tw = P[S+"LowerArm_Twist"].constraints.new('COPY_ROTATION'); tw.target = arm; tw.subtarget = S+"Hand"
    tw.use_x = False; tw.use_z = False; tw.owner_space = tw.target_space = 'LOCAL'; tw.influence = 0.5
for pb in P:
    if not A.bones[pb.name].use_deform and pb.name.startswith(("CTRL_", "IK_", "RF_", "POLE_")): pb.rotation_mode = 'XYZ'
upd()
# sanity: with IK on legs and FK arms the rest pose must be unchanged
dev = max((P[b.name].matrix.translation-b.head_local).length for b in A.bones if b.use_deform)
log("max deform-bone drift at rest with controls active:", round(dev, 5))

# ---------------------------------------------------------------- 8. save + export
arm.show_in_front = True; A.display_type = 'OCTAHEDRAL'
bpy.ops.wm.save_as_mainfile(filepath=outdir+"/cavaleiro_rig.blend"); log("saved blend")
A.pose_position = 'REST'; upd()
bpy.ops.object.select_all(action='DESELECT')
for o in (arm, body, sword, shield): o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=outdir+"/cavaleiro_rigged.fbx", use_selection=True,
    object_types={'ARMATURE', 'MESH'}, apply_scale_options='FBX_SCALE_ALL',
    use_armature_deform_only=True, add_leaf_bones=False, bake_anim=False,
    primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
A.pose_position = 'POSE'
log("exported fbx")
