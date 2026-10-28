# Retargets animations from the Universal Animation Library 2 (Quaternius, CC0; UE-style skeleton)
# onto the dark knight rig. Both rests are T-poses facing the same way (in armature space), so every
# mapped bone gets the source's armature-space rotation change from rest:
#     R_target(f) = R_source(f) @ R_source_rest^-1 @ R_target_rest
# Hips translation is scaled by the hip-height ratio. Right-hand fingers are left out (the right hand is
# modelled closed on the sword grip). Results are FK keys on the deform bones (IK switches keyed to 0).
# usage: blender -b cavaleiro_sombrio_rig.blend -P retarget.py -- <UAL2_Standard.fbx> <out.blend> Src=Dst [Src=Dst ...]
import bpy, sys
from mathutils import Matrix
args = sys.argv[sys.argv.index("--")+1:]
src_fbx, out = args[0], args[1]
JOBS = [a.split("=") for a in args[2:]]

MAP = {"pelvis": "Hips", "spine_01": "Spine", "spine_02": "Chest", "spine_03": "UpperChest",
       "neck_01": "Neck", "Head": "Head"}
for s, S in (("l", "Left"), ("r", "Right")):
    MAP.update({f"clavicle_{s}": S+"Shoulder", f"upperarm_{s}": S+"UpperArm", f"lowerarm_{s}": S+"LowerArm",
                f"hand_{s}": S+"Hand", f"thigh_{s}": S+"UpperLeg", f"calf_{s}": S+"LowerLeg",
                f"foot_{s}": S+"Foot", f"ball_{s}": S+"Toes"})
    if s == "l":
        for src, dst in (("thumb", "Thumb"), ("index", "Index"), ("middle", "Middle"), ("ring", "Ring"), ("pinky", "Little")):
            for k, ph in enumerate(("Proximal", "Intermediate", "Distal")):
                MAP[f"{src}_0{k+1}_{s}"] = f"{S}{dst}{ph}"

tgt = bpy.data.objects["Armature"]
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=src_fbx)
new = [o for o in bpy.data.objects if o not in before]
src = [o for o in new if o.type == 'ARMATURE'][0]
TP, SP = tgt.pose.bones, src.pose.bones
order = [b.name for b in tgt.data.bones if b.name in MAP.values()]           # parents before children
inv = {v: k for k, v in MAP.items()}
def arm_rot(arm, M): return (arm.matrix_world.to_3x3() @ M.to_3x3()).normalized()   # world-aligned bone rotation
src_rest = {s: arm_rot(src, src.data.bones[s].matrix_local) for s in MAP}
tgt_rest = {t: arm_rot(tgt, tgt.data.bones[t].matrix_local) for t in MAP.values()}
s_hip0 = src.matrix_world @ src.data.bones["pelvis"].head_local
t_hip0 = tgt.matrix_world @ tgt.data.bones["Hips"].head_local
scale = t_hip0.z/s_hip0.z
# source faces the same way as the target once its object rotation is removed: work in "source
# armature space" re-expressed with the target's world axes
align = tgt.matrix_world.to_3x3() @ src.matrix_world.to_3x3().inverted()
root = TP["CTRL_Root"]
sc = bpy.context.scene
for src_name, dst_name in JOBS:
    act = next(a for a in bpy.data.actions if a.name.split("|")[-1] == src_name and a.name != src_name)
    src.animation_data_create(); src.animation_data.action = act
    if src.animation_data.action_slot is None and act.slots: src.animation_data.action_slot = act.slots[0]
    f0, f1 = (int(x) for x in act.frame_range)
    tgt.animation_data_create()
    a = bpy.data.actions.new(dst_name); a.use_fake_user = True; tgt.animation_data.action = a
    for pb in TP: pb.matrix_basis = Matrix.Identity(4)
    for pb in TP:                         # bones the source does not drive (right fingers, twist...) keyed at rest,
        b = tgt.data.bones[pb.name]       # so no pose leaks in from another action when clips are baked
        if b.use_deform and pb.name not in MAP.values() and "Weapon" not in pb.name and pb.name != "SwordBack":
            pb.keyframe_insert("rotation_quaternion", frame=1)
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"):
        root[k] = 0.0; root.keyframe_insert(f'["{k}"]', frame=1)
    if "espada_nas_costas" in root:                                       # sword in hand for these clips
        root["espada_nas_costas"] = 0.0; root.keyframe_insert('["espada_nas_costas"]', frame=1)
    for f in range(f0, f1+1):
        sc.frame_set(f)
        for t in order:
            s = inv[t]
            Rs = (align @ src.matrix_world.to_3x3() @ SP[s].matrix.to_3x3()).normalized()
            Rs_rest = (align @ src_rest[s]).normalized()
            R = (Rs @ Rs_rest.inverted() @ tgt_rest[t]).normalized()
            Rl = (tgt.matrix_world.to_3x3().inverted() @ R).to_4x4()
            pb = TP[t]
            if t == "Hips":
                hs = align @ (src.matrix_world @ SP["pelvis"].head - s_hip0)
                Rl.translation = tgt.matrix_world.inverted() @ (t_hip0+hs*scale)
            else:
                Rl.translation = pb.head.copy() if pb.parent is None else pb.matrix.translation
            pb.matrix = Rl
            bpy.context.view_layer.update()
        for t in order:
            TP[t].keyframe_insert("rotation_quaternion", frame=f-f0+1)
        TP["Hips"].keyframe_insert("location", frame=f-f0+1)
    print("RETARGET", src_name, "->", dst_name, f1-f0+1, "frames")
for o in new:
    bpy.data.objects.remove(o)
for a in [a for a in bpy.data.actions if "|" in a.name]: bpy.data.actions.remove(a)
bpy.ops.wm.save_as_mainfile(filepath=out)
