# Stage 1 for Civilian 3 (comes with a Mixamo skeleton + weights): metres / Z-up, Unity-Humanoid bone names,
# end bones folded into their parents, max 4 influences, 'Body' / 'Armature' names.
# usage: blender -b -P prep.py -- <civilian.fbx> <out.blend>
import bpy, sys, numpy as np
from mathutils import Matrix, Vector
src, out = sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; me = [o for o in bpy.data.objects if o.type == 'MESH'][0]
def mode(m, o):
    if bpy.context.object and bpy.context.object.mode != 'OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = o; o.select_set(True)
    if m != 'OBJECT': bpy.ops.object.mode_set(mode=m)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
# --- units: cm + Y-up import rotation -> metres, Z-up, identity transforms
M = Matrix.Scale(0.01, 4) @ me.matrix_world
me.parent = None; me.data.transform(M); me.matrix_world = Matrix.Identity(4)
arm.scale = (0.01, 0.01, 0.01); mode('OBJECT', arm); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me.name = "Body"; me.data.name = "Body"; arm.name = "Armature"; arm.data.name = "Armature"
mode('POSE', arm)
for pb in arm.pose.bones: pb.matrix_basis = Matrix.Identity(4)
mode('OBJECT', arm)
# --- rename
REN = {"Spine1": "Chest", "Spine2": "UpperChest", "Arm": "UpperArm", "ForeArm": "LowerArm", "UpLeg": "UpperLeg", "Leg": "LowerLeg", "ToeBase": "Toes"}
FING = {"Thumb": "Thumb", "Index": "Index", "Middle": "Middle", "Ring": "Ring", "Pinky": "Little"}
PH = {"1": "Proximal", "2": "Intermediate", "3": "Distal"}
def new_name(n):
    n = n.replace("mixamorig:", "")
    if n.lower().endswith("_end"): return None
    for S in ("Left", "Right"):
        if n.startswith(S+"Hand") and n != S+"Hand":
            f = n[len(S+"Hand"):]; return S+FING[f[:-1]]+PH[f[-1]] if f[-1] in PH else None   # None = finger end bone
        if n.startswith(S):
            r = n[len(S):]
            if r in REN: return S+REN[r]
    if n in REN: return REN[n]
    if n.endswith("_End") or n == "HeadTop_End": return None
    return n
mapping = {b.name: new_name(b.name) for b in arm.data.bones}
ends = [n for n, v in mapping.items() if v is None]
print("END BONES", ends)
# fold the end bones' weights into the parent
vg = me.vertex_groups
for n in ends:
    par = arm.data.bones[n].parent.name; g = vg.get(n); gp = vg.get(par)
    if g is None: continue
    for v in me.data.vertices:
        for e in v.groups:
            if e.group == g.index and e.weight > 0: gp.add([v.index], e.weight, 'ADD')
    vg.remove(g)
mode('EDIT', arm)
for n in ends: arm.data.edit_bones.remove(arm.data.edit_bones[n])
mode('OBJECT', arm)
for b in arm.data.bones:
    nn = mapping[b.name]
    if nn is None: continue
    if b.name in vg: vg[b.name].name = nn
    b.name = nn
print("BONES", len(arm.data.bones), [b.name for b in arm.data.bones])
# --- 4 influences, normalised
mode('OBJECT', me)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
mx = max(len([g for g in v.groups if g.weight > 1e-4]) for v in me.data.vertices); print("max influences", mx)
me.parent = arm; me.matrix_parent_inverse = Matrix.Identity(4)
for md in me.modifiers: me.modifiers.remove(md)
md = me.modifiers.new("Armature", 'ARMATURE'); md.object = arm
c = np.array([v.co[:] for v in me.data.vertices]); print("mesh min", c.min(0), "max", c.max(0))
bpy.ops.wm.save_as_mainfile(filepath=out)
