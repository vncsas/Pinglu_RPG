# Step 1 (tank / alien creature): the source already has a Character Creator skeleton (95 bones) with good
# skin weights. Here: drop the sample animation and the empty shape keys, apply the 0.01 scale, rename the
# main bones to the Unity Humanoid names used by the other characters (vertex groups follow the rename),
# join the 12 pieces into one "Body" and move the UDIM tiles (u 0..3, v 0..4) into one atlas (u/3, v/4).
# usage: blender -b -P step1_norm.py -- <source.fbx> <norm.blend>
import bpy, sys, numpy as np
from mathutils import Matrix
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a[0])
for act in list(bpy.data.actions): bpy.data.actions.remove(act)
arm = bpy.data.objects["Armature"]
if arm.animation_data: arm.animation_data.action = None
for pb in arm.pose.bones: pb.matrix_basis.identity()        # the sample animation left the bones posed
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in ms:
    if o.data.shape_keys:
        bpy.context.view_layer.objects.active = o; o.shape_key_clear()
# apply the armature object scale (0.01) to bones and meshes
bpy.ops.object.select_all(action='DESELECT')
for o in [arm]+ms: o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
REN = {"CC_Base_Hip": "Hips", "CC_Base_Waist": "Spine", "CC_Base_Spine01": "Chest", "CC_Base_Spine02": "UpperChest",
       "CC_Base_NeckTwist01": "Neck", "CC_Base_Head": "Head"}
for s, S in (("L", "Left"), ("R", "Right")):
    REN.update({f"CC_Base_{s}_Clavicle": S+"Shoulder", f"CC_Base_{s}_Upperarm": S+"UpperArm", f"CC_Base_{s}_Forearm": S+"LowerArm",
                f"CC_Base_{s}_Hand": S+"Hand", f"CC_Base_{s}_Thigh": S+"UpperLeg", f"CC_Base_{s}_Calf": S+"LowerLeg",
                f"CC_Base_{s}_Foot": S+"Foot", f"CC_Base_{s}_ToeBase": S+"Toes"})
    for cc, hu in (("Thumb", "Thumb"), ("Index", "Index"), ("Mid", "Middle"), ("Pinky", "Little")):
        for k, ph in enumerate(("Proximal", "Intermediate", "Distal")): REN[f"CC_Base_{s}_{cc}{k+1}"] = f"{S}{hu}{ph}"
for old, new in REN.items(): arm.data.bones[old].name = new
# Character Creator limb bones stop half way (twist bones cover the rest): extend each one to the next joint,
# along the same line (same orientation, so the skinning is unchanged), so IK chains end where they should
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT'); eb = arm.data.edit_bones
for S in ("Left", "Right"):
    for a_, b_ in (("UpperLeg", "LowerLeg"), ("LowerLeg", "Foot"), ("UpperArm", "LowerArm"), ("LowerArm", "Hand")):
        bn, nx = eb[S+a_], eb[S+b_]
        d0 = (bn.tail-bn.head).normalized(); d1 = (nx.head-bn.head).normalized()
        roll = bn.roll; bn.tail = nx.head.copy(); bn.roll = roll
        print("EXT", S+a_, "angle change %.2f deg" % (d0.angle(d1)*57.3))
rb = eb.get("RL_BoneRoot")                                   # extra root above Hips (no weights): Hips is the root
if rb and not any(g.name == "RL_BoneRoot" for o in ms for g in o.vertex_groups):
    for c in rb.children: c.parent = None
    eb.remove(rb); print("removed RL_BoneRoot")
bpy.ops.object.mode_set(mode='OBJECT')
for o in ms:
    for g in o.vertex_groups:
        if g.name in REN: g.name = REN[g.name]          # (normally done by the rename already)
# one mesh, one atlas
bpy.ops.object.select_all(action='DESELECT')
for o in ms: o.select_set(True)
body = [o for o in ms if o.name == "CREATURE_10"][0]
bpy.context.view_layer.objects.active = body
wing_tag = body.vertex_groups.new(name="_asa"); wing_tag.add(range(len(body.data.vertices)), 1.0, 'REPLACE')   # the wings
bpy.ops.object.join(); body.name = body.data.name = "Body"
me = body.data
uv = me.uv_layers.active.data; u = np.empty(len(uv)*2); uv.foreach_get("uv", u); u = u.reshape(-1, 2)
tiles = np.floor(u).astype(int)
print("UDIM tiles used:", sorted(set(map(tuple, tiles.tolist()))))
u[:, 0] /= 3.0; u[:, 1] /= 4.0; uv.foreach_set("uv", u.ravel())
mat = bpy.data.materials.new("Tanque")
me.materials.clear(); me.materials.append(mat)
for p in me.polygons: p.material_index = 0
v = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", v); v = v.reshape(-1, 3)
print("NORM Body: %d verts %d tris, bbox %s %s, bones %d" % (len(me.vertices), sum(len(p.vertices)-2 for p in me.polygons), v.min(0).round(3), v.max(0).round(3), len(arm.data.bones)))
bpy.ops.wm.save_as_mainfile(filepath=a[1])
