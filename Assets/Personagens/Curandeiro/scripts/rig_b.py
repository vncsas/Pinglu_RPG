# Stage 2 of the healer (bone seraph) rig (runs on the output of build.py):
#   T-pose rest (arms out, legs straight - the sculpt was mid-step), healing sockets in the palms and chest,
#   Blender control rig (IK legs with reverse foot, IK/FK arms), materials with the PBR textures,
#   then FBX export (deform bones only, no leaf bones).
# usage: blender -b rig_a.blend -P rig_b.py -- <textures_dir> <out_dir>
import bpy, sys, os, math, time, numpy as np
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--")+1:]; TEX, outdir = args[0], args[1]
T0 = time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]", *a, flush=True)
def upd(): bpy.context.view_layer.update()

arm = bpy.data.objects["Armature"]; A = arm.data
body = bpy.data.objects["Body"]
SIDES = (("Left", 1), ("Right", -1))
FINGERS = ["Thumb", "Index", "Middle", "Ring", "Little"]

def mode(m, obj=arm):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = obj; obj.select_set(True)
    if m != 'OBJECT': bpy.ops.object.mode_set(mode=m)
def verts(o):
    c = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", c); return c.reshape(-1, 3)

# ---------------------------------------------------------------- 2. T-pose rest
APOSE_DIRS = {b.name: list((b.tail_local-b.head_local).normalized()) for b in A.bones}
mode('POSE'); P = arm.pose.bones
def aim(n, dvec):
    pb = P[n]; dvec = Vector(dvec).normalized(); h = pb.head.copy()
    q = (pb.tail-pb.head).normalized().rotation_difference(dvec)
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix; upd()
for S, sx in SIDES:
    # slight pre-bend (~7 deg) at elbows (backward) and knees (forward): a fully straight chain is
    # a singularity for the IK solver, which then refuses to bend at all
    aim(S+"UpperArm", (sx, 0.06, 0)); aim(S+"LowerArm", (sx, -0.06, 0)); aim(S+"Hand", (sx, 0, 0))
    aim(S+"UpperLeg", (sx*0.06, -0.06, -1)); aim(S+"LowerLeg", (sx*0.06, 0.06, -1))
    fd = Vector(APOSE_DIRS[S+"Foot"]); aim(S+"Foot", (0, -math.hypot(fd.x, fd.y), fd.z))      # toes straight ahead
    td = Vector(APOSE_DIRS[S+"Toes"]); aim(S+"Toes", (0, -math.hypot(td.x, td.y), td.z))
dz = 0.12-min(P["LeftFoot"].head.z, P["RightFoot"].head.z)        # ankles at their height (soles on the floor)
m = P["Hips"].matrix.copy(); m.translation.z += dz; P["Hips"].matrix = m; upd()
dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh()
zmin = min((be.matrix_world @ v.co).z for v in me.vertices); be.to_mesh_clear()
if zmin < 0:                                                      # soles exactly on the floor
    m = P["Hips"].matrix.copy(); m.translation.z -= zmin; P["Hips"].matrix = m; upd(); dz -= zmin
log("T-pose: hips moved", round(dz, 3))
mode('OBJECT', body)
for md in [md for md in body.modifiers if md.type == 'ARMATURE']: bpy.ops.object.modifier_apply(modifier=md.name)
mode('POSE'); bpy.ops.pose.select_all(action='SELECT'); bpy.ops.pose.armature_apply(selected=False); mode('OBJECT')
md = body.modifiers.new("Armature", 'ARMATURE'); md.object = arm
arm["apose_dirs"] = APOSE_DIRS

# ---------------------------------------------------------------- 3. healing sockets (no weights, exported):
# MagicHand_L / MagicHand_R in the palms (bone Y = out of the palm, down in the T-pose), HealCenter on the chest
# (bone Y = forward): where heal effects / projectiles start
mode('EDIT'); eb = A.edit_bones
for S, sx in SIDES:
    h = eb[S+"Hand"]; pc = h.head.lerp(h.tail, 0.55)
    b = eb.new("MagicHand_"+S[0]); b.head = pc+Vector((0, 0, -0.03)); b.tail = b.head+Vector((0, 0, -0.08))
    b.align_roll(Vector((sx, 0, 0))); b.parent = h
uc = eb["UpperChest"]; c = (uc.head+uc.tail)/2
b = eb.new("HealCenter"); b.head = Vector((0, -0.14, c.z)); b.tail = b.head+Vector((0, -0.1, 0)); b.align_roll(Vector((0, 0, 1))); b.parent = uc
mode('OBJECT')

# ---------------------------------------------------------------- 4. control rig (non-deform; not exported)
mode('EDIT'); eb = A.edit_bones
def nb(name, head, tail, parent=None, roll_up=None, M=None, deform=False):
    b = eb.new(name); b.head = head; b.tail = tail
    if M is not None: b.matrix = M
    elif roll_up is not None: b.align_roll(Vector(roll_up))
    b.parent = eb[parent] if parent else None; b.use_deform = deform; return b
for n in ("MagicHand_L", "MagicHand_R", "HealCenter"): eb[n].use_deform = True
nb("CTRL_Root", (0, 0, 0), (0, 0.45, 0))
for S, sx in SIDES:
    ft, to = eb[S+"Foot"], eb[S+"Toes"]
    ank, ball, tipt = ft.head.copy(), to.head.copy(), to.tail.copy()
    heel = Vector((ank.x, ank.y+0.06, 0.0))
    nb("IK_Foot_"+S, heel, Vector((tipt.x, tipt.y, 0.0)), "CTRL_Root", roll_up=(0, 0, 1))
    nb("RF_Toe_"+S, tipt, tipt+Vector((0, 0.06, 0)), "IK_Foot_"+S, roll_up=(0, 0, 1))
    nb("RF_Ball_"+S, ball, ball+Vector((0, 0.06, 0)), "RF_Toe_"+S, roll_up=(0, 0, 1))
    b = nb("MCH_Foot_"+S, ank, ball, "RF_Ball_"+S); b.roll = ft.roll
    b = nb("MCH_Toes_"+S, ball, tipt, "RF_Toe_"+S); b.roll = to.roll
    nb("MCH_AnkleTarget_"+S, ank, ank+Vector((0, 0, 0.01)), "RF_Ball_"+S)
    knee = eb[S+"UpperLeg"].tail.copy()
    nb("POLE_Knee_"+S, knee+Vector((0, -0.5, 0)), knee+Vector((0, -0.58, 0)), "IK_Foot_"+S)
    hd = eb[S+"Hand"]; b = nb("IK_Hand_"+S, hd.head.copy(), hd.tail.copy(), "CTRL_Root"); b.roll = hd.roll
    elbow = eb[S+"UpperArm"].tail.copy()
    nb("POLE_Elbow_"+S, elbow+Vector((0, 0.45, 0)), elbow+Vector((0, 0.53, 0)), "CTRL_Root")
mode('OBJECT')

cols = {n: A.collections.new(n) for n in ("Deform", "Asas", "Controles", "Mecanismo", "Encaixes")}
for b in A.bones:
    n = b.name
    c = "Mecanismo" if n.startswith("MCH_") else "Controles" if n.startswith(("CTRL_", "IK_", "RF_", "POLE_")) else \
        "Encaixes" if n in ("MagicHand_L", "MagicHand_R", "HealCenter") else "Asas" if n.startswith("Wing_") else "Deform"
    cols[c].assign(b)
    b.color.palette = 'THEME04' if ("Left" in n or n.endswith("_Left")) else 'THEME01' if ("Right" in n or n.endswith("_Right")) else 'THEME09'
cols["Mecanismo"].is_visible = False

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
    for limb, tgt, pole, chain_end, pp in (("Leg", "MCH_AnkleTarget_"+S, "POLE_Knee_"+S, S+"LowerLeg", "ik_perna_"+S),
                                            ("Arm", "IK_Hand_"+S, "POLE_Elbow_"+S, S+"LowerArm", "ik_braco_"+S)):
        ik = P[chain_end].constraints.new('IK'); ik.target = arm; ik.subtarget = tgt
        ik.pole_target = arm; ik.pole_subtarget = pole; ik.chain_count = 2
        base = S+("UpperLeg" if limb == "Leg" else "UpperArm"); ik.influence = 1.0
        def err(deg):
            ik.pole_angle = math.radians(deg); upd(); e = 0.0
            for n in (base, chain_end):
                pb, bb = P[n], A.bones[n]
                e += (pb.tail-bb.tail_local).length
                e += sum(abs(x-y) for ra, rb in zip(pb.matrix.to_3x3(), bb.matrix_local.to_3x3()) for x, y in zip(ra, rb))*0.05
            return e
        best = min(range(-180, 180, 5), key=err)
        for step in (1.0, 0.1): best = min((best+k*step for k in range(-5, 6)), key=err)
        ik.pole_angle = math.radians(best); e = err(best)
        log(S, limb, "pole angle", round(best, 1), "rest error", round(e, 5))
        drive(ik, pp)
    for own, tgt, pp in ((S+"Foot", "MCH_Foot_"+S, "ik_perna_"+S), (S+"Toes", "MCH_Toes_"+S, "ik_perna_"+S),
                         (S+"Hand", "IK_Hand_"+S, "ik_braco_"+S)):
        cr = P[own].constraints.new('COPY_ROTATION'); cr.target = arm; cr.subtarget = tgt; drive(cr, pp)
    tw = P[S+"LowerArm_Twist"].constraints.new('COPY_ROTATION'); tw.target = arm; tw.subtarget = S+"Hand"
    tw.use_x = False; tw.use_z = False; tw.owner_space = tw.target_space = 'LOCAL'; tw.influence = 0.5
for pb in P:
    if pb.name.startswith(("CTRL_", "IK_", "RF_", "POLE_")): pb.rotation_mode = 'XYZ'
upd()
dev = max((P[b.name].matrix.translation-b.head_local).length for b in A.bones if b.use_deform)
log("max deform-bone drift at rest with controls active:", round(dev, 5))

# ---------------------------------------------------------------- 5. materials: the same Unity texture set
# (BaseColor, Normal, MetallicSmoothness = metallic in RGB + smoothness in alpha), referenced, not packed
def img(name, noncolor):
    im = bpy.data.images.load(os.path.join(TEX, name), check_existing=True)
    if noncolor: im.colorspace_settings.name = 'Non-Color'
    return im
for m in [m for m in bpy.data.materials if m.users]:            # one material for body + staff
    key = "curandeiro"; m.name = "Curandeiro"
    nt = m.node_tree; nt.nodes.clear(); L = nt.links.new
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled"); L(bs.outputs[0], out.inputs[0])
    def tex(suffix, noncolor, y):
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = img(f"{key}_{suffix}.png", noncolor); n.location = (-700, y); return n
    L(tex("BaseColor", False, 300).outputs["Color"], bs.inputs["Base Color"])
    ms = tex("MetallicSmoothness", True, 0)
    sep = nt.nodes.new("ShaderNodeSeparateColor"); sep.location = (-400, 50); L(ms.outputs["Color"], sep.inputs[0])
    L(sep.outputs[0], bs.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0; inv.location = (-400, -150)
    L(ms.outputs["Alpha"], inv.inputs[1]); L(inv.outputs[0], bs.inputs["Roughness"])
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.location = (-300, -400)
    L(tex("Normal", True, -400).outputs["Color"], nm.inputs["Color"]); L(nm.outputs[0], bs.inputs["Normal"])
for im in [im for im in bpy.data.images if im.users == 0]: bpy.data.images.remove(im)   # the packed Tripo JPEGs
bpy.ops.file.make_paths_relative()

# ---------------------------------------------------------------- 6. save + export
arm.show_in_front = True; A.display_type = 'OCTAHEDRAL'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(outdir, "curandeiro_rig.blend"), relative_remap=True); log("saved blend")
A.pose_position = 'REST'; upd()
bpy.ops.object.select_all(action='DESELECT')
for o in (arm, body): o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, "curandeiro_rigged.fbx"), use_selection=True,
    object_types={'ARMATURE', 'MESH'}, apply_scale_options='FBX_SCALE_ALL',
    use_armature_deform_only=True, add_leaf_bones=False, bake_anim=False,
    primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    mesh_smooth_type='FACE', use_custom_props=False, path_mode='STRIP', embed_textures=False)
A.pose_position = 'POSE'
log("exported fbx")
