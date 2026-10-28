# Checks every action of an animation .blend frame by frame:
#   - sword tip / pommel under the floor (the greatsword is 1.7 m long)
#   - lowest body point (feet sinking into the floor or floating)
#   - sword going through the body (the fist that holds it and the right forearm are ignored)
# usage: blender -b animacoes.blend -P check_anim.py -- [Action ...]
import bpy, sys, numpy as np
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
arm = bpy.data.objects["Armature"]; sword = bpy.data.objects["Sword"]; body = bpy.data.objects["Body"]
sv = np.array([v.co[:] for v in sword.data.vertices])
tip, pommel = Vector(sv[np.argmax(sv[:, 1])]), Vector(sv[np.argmin(sv[:, 1])])   # sword local Y = blade
from mathutils.bvhtree import BVHTree
vg = {g.index: g.name for g in body.vertex_groups}
HAND = ("RightHand", "RightLowerArm", "RightLowerArm_Twist", "RightThumb", "RightIndex", "RightMiddle", "RightRing", "RightLittle")
handv = {v.index for v in body.data.vertices if sum(g.weight for g in v.groups if vg[g.group].startswith(HAND)) > 0.3}
handp = {p.index for p in body.data.polygons if any(i in handv for i in p.vertices)}
def tree(o):
    dg = bpy.context.evaluated_depsgraph_get(); oe = o.evaluated_get(dg); me = oe.to_mesh(); W = o.matrix_world
    t = BVHTree.FromPolygons([W @ v.co for v in me.vertices], [tuple(p.vertices) for p in me.polygons]); oe.to_mesh_clear(); return t
sc = bpy.context.scene
acts = [bpy.data.actions[a] for a in args] if args else [a for a in bpy.data.actions if a.use_fake_user]
for act in acts:
    arm.animation_data.action = act
    if arm.animation_data.action_slot is None and act.slots: arm.animation_data.action_slot = act.slots[0]
    f0, f1 = (int(x) for x in act.frame_range)
    worst_tip, under, body_min, body_max_low = 9, [], 9, -9
    hits = []
    for f in range(f0, f1+1):
        sc.frame_set(f)
        z = min((sword.matrix_world @ tip).z, (sword.matrix_world @ pommel).z)
        worst_tip = min(worst_tip, z)
        if z < -0.02: under.append(f)
        dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh()
        c = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", c); zmin = c.reshape(-1, 3)[:, 2].min()
        be.to_mesh_clear(); body_min = min(body_min, zmin); body_max_low = max(body_max_low, zmin)
        n = len({a for a, _ in tree(body).overlap(tree(sword)) if a not in handp})
        if n > 5: hits.append((f, n))
    rng = f"{under[0]}-{under[-1]}" if under else ""
    print(f"CHECK {act.name:18s} quadros {f0:3d}-{f1:3d} | espada min z {worst_tip:+.2f}"
          f"{'  NO CHAO em ' + str(len(under)) + ' quadros (' + rng + ')' if under else ''}"
          f" | corpo z min {body_min:+.3f} (pior quadro sem pe no chao: {body_max_low:+.3f})"
          f" | espada no corpo: {('%d quadros, pior %d tri (quadro %d)' % (len(hits), max(h[1] for h in hits), max(hits, key=lambda h: h[1])[0])) if hits else 'nao'}")
