# Fixes for retargeted / hand-made clips with the 1.7 m greatsword:
#   1. body below the floor -> raise the Hips on that frame (feet/armour touch z=0 at most; the long
#      robe may brush the floor in deep lunges instead of lifting the feet off the ground)
#   2. sword under the floor or through the body -> turn the right wrist by the smallest angle that
#      clears both (8 axes in the hand's frame, 5..60 deg), eased over the neighbouring frames so the
#      motion has no pops. The fist and the right forearm are allowed to touch the sword.
# usage: blender -b animacoes.blend -P fix_floor.py -- <out.blend> Action [Action ...]
#        Action:sem_pulso   only the floor lift (Guardar_Espada/Sacar_Espada: the hand must hit the socket)
#        Action:com_tunica  the robe counts for the floor lift too (walk/run cycles)
import bpy, sys, math, numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree
args = sys.argv[sys.argv.index("--")+1:]; OUT, ACTS = args[0], args[1:]
arm = bpy.data.objects["Armature"]; P = arm.pose.bones
sword = bpy.data.objects["Sword"]; body = bpy.data.objects["Body"]; sc = bpy.context.scene
SV = [v.co.copy() for v in sword.data.vertices]; SP = [tuple(p.vertices) for p in sword.data.polygons]
sv = np.array([v[:] for v in SV])
ENDS = [Vector(sv[np.argmax(sv[:, 1])]), Vector(sv[np.argmin(sv[:, 1])])]          # blade tip, pommel (local)
CLEAR, EASE, TOL = 0.03, 4, 5
vg = {g.index: g.name for g in body.vertex_groups}
HAND = ("RightHand", "RightLowerArm", "RightThumb", "RightIndex", "RightMiddle", "RightRing", "RightLittle")
handv = {v.index for v in body.data.vertices if sum(g.weight for g in v.groups if vg[g.group].startswith(HAND)) > 0.3}
handp = {p.index for p in body.data.polygons if any(i in handv for i in p.vertices)}

robe = {v.index for v in body.data.vertices if any(vg[g.group] == "_tunica" for g in v.groups)}   # cloth may brush the floor
WITH_ROBE = False
def body_eval():
    dg = bpy.context.evaluated_depsgraph_get(); be = body.evaluated_get(dg); me = be.to_mesh(); W = body.matrix_world
    co = [W @ v.co for v in me.vertices]; zmin = min(c.z for i, c in enumerate(co) if WITH_ROBE or i not in robe)
    t = BVHTree.FromPolygons(co, [tuple(p.vertices) for p in me.polygons]); be.to_mesh_clear(); return t, zmin

def sword_cost(tb, M):
    """(triangles of the body crossed, how far the lowest sword end is under the floor clearance)"""
    ts = BVHTree.FromPolygons([M @ v for v in SV], SP)
    hit = len({a for a, _ in tb.overlap(ts) if a not in handp})
    low = max(0.0, CLEAR-min((M @ e).z for e in ENDS))
    return hit, low

def candidates():
    pb = P["RightHand"]; R = (arm.matrix_world @ pb.matrix).to_3x3().normalized()
    axes = [R.col[0], -R.col[0], R.col[2], -R.col[2]]
    axes += [(R.col[0]+R.col[2]).normalized(), (R.col[0]-R.col[2]).normalized(), (-R.col[0]+R.col[2]).normalized(), (-R.col[0]-R.col[2]).normalized()]
    for deg in range(5, 65, 5):
        for ax in axes: yield Quaternion(ax, math.radians(deg))

for item in ACTS:
    name, _, opt = item.partition(":")
    WITH_ROBE = "com_tunica" in opt                              # walk/run: keep the hem above the floor too
    act = bpy.data.actions[name]; arm.animation_data.action = act
    if arm.animation_data.action_slot is None and act.slots: arm.animation_data.action_slot = act.slots[0]
    f0, f1 = (int(x) for x in act.frame_range)
    lift = {}
    for f in range(f0, f1+1):                                    # 1. body on the floor
        sc.frame_set(f); _, z = body_eval()
        if z < 0: lift[f] = -z
    E = 3                                                        # spread over the neighbours: no sudden hop
    lifted = 0.0
    for f in range(f0, f1+1):
        dz = max([v*(1-abs(f-k)/(E+1)) for k, v in lift.items() if abs(f-k) <= E] or [0.0])
        if f in lift: dz = max(dz, lift[f])
        if dz <= 0: continue
        sc.frame_set(f); m = P["Hips"].matrix.copy(); m.translation.z += dz; P["Hips"].matrix = m
        P["Hips"].keyframe_insert("location", frame=f); lifted = max(lifted, dz)
    need, bad_left = {}, []
    if "sem_pulso" not in opt:
        for f in range(f0, f1+1):                                # 2. wrist correction where needed
            sc.frame_set(f); tb, _ = body_eval(); M = sword.matrix_world.copy()
            hit, low = sword_cost(tb, M)
            if hit <= TOL and low <= 0: continue
            w = (arm.matrix_world @ P["RightHand"].matrix).translation
            best = None
            for q in candidates():
                Mq = Matrix.Translation(w) @ q.to_matrix().to_4x4() @ Matrix.Translation(-w) @ M
                h2, l2 = sword_cost(tb, Mq)
                if h2 <= TOL and l2 <= 0: best = q; break
            if best is None:                                     # nothing fully clean: least bad within the search
                best = min(candidates(), key=lambda q: sum(np.array(sword_cost(tb, Matrix.Translation(w) @ q.to_matrix().to_4x4() @ Matrix.Translation(-w) @ M))*(1, 200)))
                bad_left.append(f)
            R = (arm.matrix_world @ P["RightHand"].matrix).to_quaternion()
            need[f] = R.inverted() @ best @ R                    # stored in the hand's own frame
    def eased(f):
        if f in need: return need[f], 1.0                        # its own correction, always
        cands = [(1-abs(f-k)/(EASE+1), q) for k, q in need.items() if abs(f-k) <= EASE]
        if not cands: return None, 0
        wgt, q = max(cands, key=lambda c: c[0]*c[1].angle)
        return q, wgt
    n_keyed = 0
    for f in range(f0, f1+1):
        q, wgt = eased(f)
        if q is None or q.angle < 1e-4: continue
        sc.frame_set(f); pb = P["RightHand"]; base = pb.matrix.copy()
        tb, _ = body_eval(); h0, l0 = sword_cost(tb, sword.matrix_world.copy())
        for k in (1.0, 0.75, 0.5, 0.25, 0.0):                  # ease in the hand's frame; never make a frame worse
            qq = Quaternion().slerp(q, wgt*k)
            pb.matrix = base @ qq.to_matrix().to_4x4(); bpy.context.view_layer.update()
            h1, l1 = sword_cost(tb, sword.matrix_world.copy())
            if (h1 <= max(h0, TOL) and l1 <= l0+1e-4) or k == 0.0: break
        if qq.angle > 1e-4: pb.keyframe_insert("rotation_quaternion", frame=f); n_keyed += 1
    print(f"FIX {name}: quadril erguido ate {lifted*100:.1f} cm; pulso corrigido em {len(need)} quadros "
          f"(max {math.degrees(max((q.angle for q in need.values()), default=0)):.0f} graus), suavizado em {n_keyed}"
          f"{'; sem solucao limpa em ' + str(bad_left) if bad_left else ''}")
bpy.ops.wm.save_as_mainfile(filepath=OUT)
