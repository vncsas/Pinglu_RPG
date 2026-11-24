# Cria o esqueleto do cavalo na pose ORIGINAL do scan (trote) e calcula os pesos (proxy voxel -> transferencia).
import bpy, sys, math
sys.path.insert(0, "C:/Users/Thiago/Documentos/rig-cavalo2/scripts")
from mathutils import Vector as V
import horse_def as H
base = "C:/Users/Thiago/Documentos/rig-cavalo2/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/prep.blend")
mesh = bpy.data.objects["Cavalo2"]

def montar_ossos(ebones, pernas):
    def add(nome, head, tail, parent, deform=True, rollx=True):
        b = ebones.new(nome); b.head = head; b.tail = tail
        if parent: b.parent = ebones[parent]
        d = (tail - head).normalized(); x = V((1, 0, 0))
        if abs(d.dot(x)) < 0.95: b.align_roll(x.cross(d))
        b.use_deform = deform; return b
    for n, h, t, p in H.COLUNA:
        add(n, h, t, p, deform=(n not in ("Root", "Saddle")))
    for nome, j in pernas.items():
        frente = nome.endswith("F"); s = 1 if nome[0] == "L" else -1
        cor = j["cor"]
        if frente:
            add(nome + "_Shoulder", V((H.XM + 0.55 * (j["top"].x - H.XM), 0.02, 1.36)), j["top"], "Chest")
            add(nome + "_Arm", j["top"], j["cot"], nome + "_Shoulder")
            add(nome + "_Forearm", j["cot"], j["pul"], nome + "_Arm")
        else:
            add(nome + "_Thigh", j["top"], j["cot"], "Pelvis")
            add(nome + "_Shin", j["cot"], j["pul"], nome + "_Thigh")
        add(nome + "_Cannon", j["pul"], j["bol"], nome + ("_Forearm" if frente else "_Shin"))
        add(nome + "_Pastern", j["bol"], cor, nome + "_Cannon")
        add(nome + "_Hoof", cor, j["casco"], nome + "_Pastern")

bpy.ops.object.select_all(action='DESELECT')
arm_d = bpy.data.armatures.new("CavaloArmature"); arm = bpy.data.objects.new("CavaloArmature", arm_d)
bpy.context.scene.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); montar_ossos(arm_d.edit_bones, H.TROTE); bpy.ops.object.mode_set(mode='OBJECT')
arm_d.display_type = 'OCTAHEDRAL'

# ---- pesos: distancia GEODESICA (pela superficie) ate cada osso -> as pernas nao "vazam" umas para as outras
import heapq
me = mesh.data; n = len(me.vertices); co = [v.co.copy() for v in me.vertices]
adj = [[] for _ in range(n)]
for e in me.edges:
    a_, b_ = e.vertices; d = (co[a_] - co[b_]).length; adj[a_].append((b_, d)); adj[b_].append((a_, d))
def dist_seg(p, h, t):
    ab = t - h; l2 = ab.length_squared
    k = 0.0 if l2 == 0 else max(0.0, min(1.0, (p - h).dot(ab) / l2)); return (p - (h + ab * k)).length
raios = {"Spine": 0.70, "Pelvis": 0.62, "Chest": 0.64, "Neck": 0.22, "Head": 0.16, "Jaw": 0.10, "Ear": 0.06, "Tail": 0.14, "Shoulder": 0.18}
pesos = [dict() for _ in range(n)]
deform_bones = [b for b in arm_d.bones if b.use_deform]
for b in deform_bones:
    h = arm.matrix_world @ b.head_local; t = arm.matrix_world @ b.tail_local
    r = 0.10
    for k, vv in raios.items():
        if k in b.name: r = vv
    if "Hoof" in b.name or "Pastern" in b.name: r = 0.06
    dist = [1e9] * n; pq = []
    for i in range(n):
        d0 = dist_seg(co[i], h, t)
        if d0 < r: dist[i] = d0; heapq.heappush(pq, (d0, i))
    if not pq:   # osso sem vertice perto: usa o mais proximo
        i = min(range(n), key=lambda q: dist_seg(co[q], h, t)); dist[i] = dist_seg(co[i], h, t); heapq.heappush(pq, (dist[i], i))
    while pq:
        d, i = heapq.heappop(pq)
        if d > dist[i]: continue
        for j, w in adj[i]:
            nd = d + w
            if nd < dist[j] and nd < 0.9: dist[j] = nd; heapq.heappush(pq, (nd, j))
    # limite euclidiano: ossos das pernas nao puxam a barriga/tronco (so a pele perto do proprio osso)
    R = None
    for k, rv in (("Thigh", 0.50), ("Arm", 0.46), ("Shoulder", 0.40), ("Shin", 0.30), ("Forearm", 0.22)):
        if b.name.endswith(k): R = rv
    for i in range(n):
        if dist[i] < 0.9:
            w = 1.0 / (dist[i] + 0.03) ** 3
            if R is not None:
                e = dist_seg(co[i], h, t)
                if e >= R: continue
                w *= (1.0 - e / R) ** 1.0
                if b.name.endswith(("Arm", "Shoulder", "Thigh", "Forearm", "Shin")):      # peito/centro do corpo fica no tronco: so a lateral e a parte baixa acompanham a perna
                    lat = max(0.0, min(1.0, (abs(co[i].x) - 0.04) / 0.20)); lat = lat * lat * (3 - 2 * lat)
                    alt = max(0.0, min(1.0, (1.38 - co[i].z) / 0.62)); alt = alt * alt * (3 - 2 * alt)
                    w *= max(lat * alt, 0.0) + 0.01
            if b.name in ("Neck1", "Neck2"):      # a base do pescoco nao arrasta o peito/garganta: so acima do peito
                zz = max(0.0, min(1.0, (co[i].z - 1.18) / 0.22)); w *= zz * zz * (3 - 2 * zz) + 0.01
            pesos[i][b.name] = w
def norm(d):
    t_ = sum(d.values()); return {k: v / t_ for k, v in d.items()} if t_ > 0 else d
pesos = [norm(d) for d in pesos]
for it in range(40):
    novo = []
    for i in range(n):
        if not adj[i]: novo.append(pesos[i]); continue
        acc = {k: v * 0.5 for k, v in pesos[i].items()}; share = 0.5 / len(adj[i])
        for j, _w in adj[i]:
            for k, v in pesos[j].items(): acc[k] = acc.get(k, 0.0) + v * share
        novo.append(acc)
    pesos = novo
for b in deform_bones: mesh.vertex_groups.new(name=b.name)
gi = {g.name: g for g in mesh.vertex_groups}
sem = 0
for i in range(n):
    if not pesos[i]: sem += 1; continue
    top = sorted(pesos[i].items(), key=lambda kv: -kv[1])[:4]; tot = sum(v for _, v in top)
    for nme, v in top:
        if v / tot > 0.02: gi[nme].add([i], v / tot, 'REPLACE')
print("sem alcance:", sem)
sm=[co[i] for i in range(n) if not pesos[i]]
if sm: print("SEMPESO bbox", min(v.x for v in sm), max(v.x for v in sm), min(v.y for v in sm), max(v.y for v in sm), min(v.z for v in sm), max(v.z for v in sm))
bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
# ---- limpeza: ate 4 grupos, normaliza
bpy.ops.object.mode_set(mode='OBJECT')
mesh.vertex_groups.active_index = 0
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.01)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
sem = sum(1 for v in mesh.data.vertices if not any(g.weight > 0.01 for g in v.groups)); print("vertices sem peso", sem)
mesh.parent = arm; mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_base.blend")
