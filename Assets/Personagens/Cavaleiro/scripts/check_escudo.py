# Checks, frame by frame, whether the shield goes through the knight's body in an animation.
# Contact with the left arm (where the shield is strapped) is ignored.
# usage: blender -b minha_animacao.blend -P check_escudo.py -- [inicio fim] [--limite 20]
#   (the .blend must be based on cavaleiro_rig.blend, with the animation on the Armature)
import bpy, sys
from mathutils.bvhtree import BVHTree
args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
sc = bpy.context.scene
nums = [a for a in args if a.lstrip("-").isdigit() and args[args.index(a)-1] != "--limite"]
f0, f1 = (int(nums[0]), int(nums[1])) if len(nums) >= 2 else (sc.frame_start, sc.frame_end)
LIMIT = int(args[args.index("--limite")+1]) if "--limite" in args else 20   # tolerated touching triangles

body = bpy.data.objects["Body"]; shield = bpy.data.objects["Shield"]
vg = {g.index: g.name for g in body.vertex_groups}
ARM = ("LeftUpperArm", "LeftLowerArm", "LeftLowerArm_Twist", "LeftHand")
armv = {v.index for v in body.data.vertices if sum(g.weight for g in v.groups if vg[g.group] in ARM) > 0.3}
armp = {p.index for p in body.data.polygons if any(i in armv for i in p.vertices)}

def tree(o):
    dg = bpy.context.evaluated_depsgraph_get(); oe = o.evaluated_get(dg); me = oe.to_mesh()
    W = o.matrix_world; t = BVHTree.FromPolygons([W @ v.co for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
    oe.to_mesh_clear(); return t

bad = []
for f in range(f0, f1+1):
    sc.frame_set(f)
    n = sum(1 for a, _ in tree(body).overlap(tree(shield)) if a not in armp)
    if n > LIMIT: bad.append((f, n))
print("\n=== ESCUDO x CORPO, quadros %d-%d (tolerancia %d triangulos)" % (f0, f1, LIMIT))
if not bad:
    print("OK: o escudo nao atravessa o corpo em nenhum quadro")
else:
    for f, n in bad: print(f"  quadro {f:4d}: {n} triangulos do corpo atravessados")
    print(f"ATENCAO: {len(bad)} quadro(s) com o escudo atravessando. Abra um pouco o braco esquerdo nesses quadros.")
