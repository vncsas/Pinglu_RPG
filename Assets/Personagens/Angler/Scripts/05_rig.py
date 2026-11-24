import bpy, sys, heapq
sys.path.insert(0, "C:/Users/Thiago/Documentos/rig-angler/scripts")
from mathutils import Vector as V
import adef as M
base = "C:/Users/Thiago/Documentos/rig-angler/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/prep.blend")
mesh = bpy.data.objects["Angler"]
bpy.context.view_layer.objects.active = mesh; mesh.select_set(True)
arm_d = bpy.data.armatures.new("AnglerArmature"); arm = bpy.data.objects.new("AnglerArmature", arm_d)
bpy.context.scene.collection.objects.link(arm); bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for n, h, t, p in M.ossos():
    b = arm_d.edit_bones.new(n); b.head = h; b.tail = t
    if p: b.parent = arm_d.edit_bones[p]
    d = (t - h).normalized(); x = V((1, 0, 0))
    if abs(d.dot(x)) < 0.95: b.align_roll(x.cross(d))
    b.use_deform = (n != "Root")
bpy.ops.object.mode_set(mode='OBJECT')
me = mesh.data; n = len(me.vertices); co = [v.co.copy() for v in me.vertices]
adj = [[] for _ in range(n)]
for e in me.edges:
    a, b = e.vertices; d = (co[a] - co[b]).length; adj[a].append((b, d)); adj[b].append((a, d))
def dseg(p, h, t):
    ab = t - h; l2 = ab.length_squared; k = 0.0 if l2 == 0 else max(0.0, min(1.0, (p - h).dot(ab) / l2)); return (p - (h + ab * k)).length
def sufijo(d, nome):
    for k in d:
        if nome.endswith(k): return d[k]
    return None
pesos = [dict() for _ in range(n)]
for b in arm_d.bones:
    if not b.use_deform: continue
    h = b.head_local; t = b.tail_local; r = sufijo(M.RAIO, b.name) or 0.15; R = sufijo(M.LIM, b.name)
    dist = [1e9] * n; pq = []
    for i in range(n):
        d0 = dseg(co[i], h, t)
        if d0 < r: dist[i] = d0; heapq.heappush(pq, (d0, i))
    if not pq:
        i = min(range(n), key=lambda q: dseg(co[q], h, t)); dist[i] = dseg(co[i], h, t); heapq.heappush(pq, (dist[i], i))
    while pq:
        d, i = heapq.heappop(pq)
        if d > dist[i]: continue
        for j, w in adj[i]:
            nd = d + w
            if nd < dist[j] and nd < 0.9: dist[j] = nd; heapq.heappush(pq, (nd, j))
    for i in range(n):
        if dist[i] < 0.9:
            w = 1.0 / (dist[i] + 0.03) ** 3
            if R is not None:
                e = dseg(co[i], h, t)
                if e >= R: continue
                w *= (1.0 - e / R) ** 1.5
            pesos[i][b.name] = w
def norm(d):
    s = sum(d.values()); return {k: v / s for k, v in d.items()} if s > 0 else d
pesos = [norm(d) for d in pesos]
for it in range(4):
    novo = []
    for i in range(n):
        if not adj[i]: novo.append(pesos[i]); continue
        acc = {k: v * 0.5 for k, v in pesos[i].items()}; sh = 0.5 / len(adj[i])
        for j, _ in adj[i]:
            for k, v in pesos[j].items(): acc[k] = acc.get(k, 0.0) + v * sh
        novo.append(acc)
    pesos = novo
for b in arm_d.bones:
    if b.use_deform: mesh.vertex_groups.new(name=b.name)
gi = {g.name: g for g in mesh.vertex_groups}; sem = 0
for i in range(n):
    if not pesos[i]:
        sem += 1; bn = min((b for b in arm_d.bones if b.use_deform), key=lambda b: dseg(co[i], b.head_local, b.tail_local)); gi[bn.name].add([i], 1.0, 'REPLACE'); continue
    top = sorted(pesos[i].items(), key=lambda kv: -kv[1])[:4]; tot = sum(v for _, v in top)
    for nm, v in top:
        if v / tot > 0.02: gi[nm].add([i], v / tot, 'REPLACE')
print("sem alcance:", sem)
bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
mesh.parent = arm; mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_scan.blend")
