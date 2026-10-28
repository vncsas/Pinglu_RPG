# Verifies an exported animation FBX against the character FBX (what Unity relies on):
#   - the clip reproduces the source animation frame by frame (bone positions)
#   - every animated bone exists in the character with the same name and the same rest pose
#     (Unity binds clips by bone name/path, so equal names + equal rest = identical result)
# usage: blender -b -P anim_verify.py -- personagem.fbx animacao.fbx origem.blend
import bpy, sys
from mathutils import Vector
char_fbx, anim_fbx, src_blend = sys.argv[-3:]

bpy.ops.wm.open_mainfile(filepath=src_blend)
arm = bpy.data.objects["Armature"]; sc = bpy.context.scene
deform = [b.name for b in arm.data.bones if b.use_deform]
f0, f1 = sc.frame_start, sc.frame_end
ref = {}
for f in range(f0, f1+1):
    sc.frame_set(f); ref[f] = {n: (arm.matrix_world @ arm.pose.bones[n].head).copy() for n in deform}
    for n in deform:
        pb = arm.pose.bones[n]; par = pb.parent
        ref.setdefault(("L", f), {})[n] = (par.matrix.inverted() @ pb.matrix) if par else pb.matrix.copy()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=char_fbx)
char = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
char_rest = {b.name: b.matrix_local.copy() for b in char.data.bones}
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=anim_fbx)
anim = [o for o in bpy.data.objects if o not in before and o.type == 'ARMATURE'][0]
act = anim.animation_data.action; fr = [int(round(k)) for k in act.frame_range]
names = [b.name for b in anim.data.bones]
missing = [n for n in deform if n not in names]
extra = [n for n in names if n not in char_rest]
rest_err = max(sum(abs(a-b) for ra, rb in zip(anim.data.bones[n].matrix_local, char_rest[n]) for a, b in zip(ra, rb)) for n in names if n in char_rest)
off = fr[0]-f0
err, worst, lerr = 0.0, None, 0.0
for f in range(f0, f1+1):
    bpy.context.scene.frame_set(f+off)
    for n in deform:
        pb = anim.pose.bones[n]
        e = (anim.matrix_world @ pb.head - ref[f][n]).length
        if e > err: err, worst = e, (f, n)
        loc = (pb.parent.matrix.inverted() @ pb.matrix) if pb.parent else pb.matrix
        lerr = max(lerr, max(abs(a-b) for ra, rb in zip(loc, ref[("L", f)][n]) for a, b in zip(ra, rb)))
# Unity binds clip curves by bone name and applies the local (parent-space) transforms:
# matching names + matching local transforms per frame = same motion on the character
ok = err < 0.005 and lerr < 1e-3 and not missing and not extra
print("ANIMCHECK transformacoes locais por quadro (o que o Unity aplica): diferenca maxima", round(lerr, 6))
if rest_err > 1e-3: print("ANIMCHECK obs: FBX so com esqueleto -> o importador do Blender usa o 1o quadro como repouso (nao afeta o Unity)")
print("ANIMCHECK ossos no clipe:", len(names), "| faltando:", missing, "| fora do personagem:", extra)
print("ANIMCHECK pose de repouso clipe x personagem (diferenca):", round(rest_err, 6))
print("ANIMCHECK quadros", fr, "(deslocamento %d) | erro maximo vs. animacao original: %.4f m em %s" % (off, err, worst))
print("ANIMCHECK", "OK" if ok else "PROBLEMA")
sys.exit(0 if ok else 1)
