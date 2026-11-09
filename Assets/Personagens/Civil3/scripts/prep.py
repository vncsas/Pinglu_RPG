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
MOUTH_BELOW_TIP = 0.038
# --- Jaw: Humanoid optional bone. Pivot behind the lips, weights on the lower lip / chin / beard (skin + beard only; the
# upper lip, moustache, cheeks and hair stay on the Head). Geometry is found from the face itself.
def sm(t): t = min(max(t, 0.0), 1.0); return t*t*(3-2*t)
def add_jaw():
    A = arm.data; hb = A.bones["Head"]; hz = hb.head_local.z
    gi = {g.index: g.name for g in me.vertex_groups}; hg = me.vertex_groups["Head"]
    wh = np.zeros(len(me.data.vertices))
    for v in me.data.vertices:
        for e in v.groups:
            if e.group == hg.index: wh[v.index] = e.weight
    co = np.array([v.co[:] for v in me.data.vertices])
    mats = [m.name for m in me.data.materials]; skin = set()
    for pl in me.data.polygons:
        if mats[pl.material_index] == "Body": skin.update(pl.vertices)
    skin = np.array(sorted(skin))
    head_skin = skin[(wh[skin] > 0.5) & (co[skin, 2] > hz+0.02)]
    band = head_skin[(co[head_skin, 2] > hz+0.05) & (co[head_skin, 2] < hz+0.24)]
    nose_y = co[band, 1].min()
    tip_i = band[np.argmin(co[band, 1])]; tip_z = co[tip_i, 2]
    strip = skin[(abs(co[skin, 0]) < 0.04) & (co[skin, 2] > hz-0.12) & (co[skin, 2] < tip_z+0.01)]
    # front profile (most forward y per 4 mm slice) walking down from the nose tip: the chin ends where it falls back to the neck
    zs = np.arange(tip_z, hz-0.12, -0.004); prof = []
    for z0 in zs:
        sel = strip[(co[strip, 2] <= z0) & (co[strip, 2] > z0-0.004)]
        prof.append(co[sel, 1].min() if len(sel) else 9.0)
    chin_z = zs[-1]
    for z0, y0 in zip(zs, prof):
        if z0 < tip_z-0.04 and y0 > nose_y+0.065: chin_z = z0+0.004; break
    mouth_z = tip_z-MOUTH_BELOW_TIP
    piv = Vector((0.0, nose_y+0.115, mouth_z+0.045))
    print("JAW nose_y %.3f chin_z %.3f mouth_z %.3f tip_z %.3f pivot" % (nose_y, chin_z, mouth_z, tip_z), tuple(round(x, 3) for x in piv))
    jg = me.vertex_groups.new(name="Jaw"); n = 0
    for i, c in enumerate(co):
        if wh[i] < 0.05: continue
        w = sm((mouth_z+0.012-c[2])/0.02)*sm((piv.y-0.02-c[1])/0.05)*sm((0.075-abs(c[0]))/0.035)
        w *= sm((c[2]-(chin_z-0.035))/0.03)
        if w > 0.02:
            me.vertex_groups["Head"].add([i], wh[i]*(1-w), 'REPLACE'); jg.add([i], wh[i]*w, 'REPLACE'); n += 1
    print("JAW vertices:", n)
    mode('EDIT', arm); eb = arm.data.edit_bones
    b = eb.new("Jaw"); b.head = piv; b.tail = piv+Vector((0, -(piv.y-(nose_y+0.0)), -0.045)); b.parent = eb["Head"]; b.use_connect = False
    b.align_roll(Vector((0, 0, 1)))
    mode('OBJECT', arm)
mode('OBJECT', me)
add_jaw()
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
