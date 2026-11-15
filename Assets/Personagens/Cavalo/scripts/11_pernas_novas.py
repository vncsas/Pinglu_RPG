# Refaz as 4 patas (abaixo do meio do antebraco/tibia): apaga a geometria do scan e cria tubos limpos com anatomia de cavalo
# (joelho/jarrete, boleto, quartela, casco), raios medidos no proprio scan (levemente engrossados), UV copiada do scan (mesma cor/textura).
import bpy, bmesh, math, sys
from mathutils import Vector as V, kdtree
base = "C:/Users/Thiago/Documentos/rig-cavalo/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_empe.blend")
arm = bpy.data.objects["CavaloArmature"]; mesh = bpy.data.objects["Cavalo"]
bones = arm.data.bones
def jp(leg):
    F = leg.endswith("F")
    nomes = [("Arm" if F else "Thigh"), ("Forearm" if F else "Shin"), "Cannon", "Pastern", "Hoof"]
    p = {"top": bones[leg + "_" + nomes[0]].head_local.copy(), "cot": bones[leg + "_" + nomes[1]].head_local.copy(), "pul": bones[leg + "_Cannon"].head_local.copy(),
         "bol": bones[leg + "_Pastern"].head_local.copy(), "cor": bones[leg + "_Hoof"].head_local.copy(), "casco": bones[leg + "_Hoof"].tail_local.copy()}
    return p
LEGS = ["LF", "RF", "LH", "RH"]
orig_me = mesh.data.copy()
# ---- referencias do scan (posicao, UV, raio)
ob = bmesh.new(); ob.from_mesh(orig_me); ob.verts.ensure_lookup_table(); uvl = ob.loops.layers.uv.active
comUV = [v for v in ob.verts if v.link_loops]
kd = kdtree.KDTree(len(comUV)); vuv = {}
for v in comUV:
    kd.insert(v.co, v.index); vuv[v.index] = v.link_loops[0][uvl].uv.copy()
kd.balance()
P = [v.co.copy() for v in ob.verts]
def raio_em(pt, d, lo=0.015, hi=0.16):
    """raio medio do scan numa fatia perpendicular ao eixo d em pt"""
    ds = []
    for q in P:
        r = q - pt
        a = r.dot(d)
        if abs(a) > 0.03: continue
        rad = (r - d * a).length
        if rad < hi: ds.append(rad)
    if len(ds) < 5: return lo * 2
    ds.sort(); return ds[int(len(ds) * 0.8)]            # percentil 80: contorno externo
# ---- perfil de cor das patas (do topo ao casco) amostrado do scan
import numpy as np
img = bpy.data.images.load(base + "saida/Cavalo_cor_orig.jpg"); W0, H0 = img.size
pix = np.array(img.pixels[:], dtype=np.float32).reshape(H0, W0, 4)
def cor_em(uv):
    x = min(W0 - 1, max(0, int(uv[0] * W0))); y = min(H0 - 1, max(0, int(uv[1] * H0))); return pix[y, x, :3]
NB = 32; soma = np.zeros((NB, 3)); cont = np.zeros(NB)
for leg in LEGS:
    j0 = jp(leg); zs = j0["cot"].z + (j0["pul"].z - j0["cot"].z) * 0.45 + 0.09
    for v in comUV:
        q = v.co
        if q.z < zs and q.z > -0.01 and (V((q.x, q.y, 0)) - V((j0["bol"].x, j0["bol"].y, 0))).length < 0.12 + 0.25 * max(0, q.z - 0.3):
            b = int(min(NB - 1, max(0, (zs - q.z) / zs * NB))); soma[b] += cor_em(vuv[v.index]); cont[b] += 1
perfil = np.zeros((NB, 3))
for b in range(NB):
    perfil[b] = soma[b] / cont[b] if cont[b] > 0 else (perfil[b - 1] if b > 0 else np.array([0.75, 0.72, 0.68]))
for _ in range(3): perfil = np.vstack([perfil[0], (perfil[:-2] + 2 * perfil[1:-1] + perfil[2:]) / 4, perfil[-1]])
casaco = np.array([0.66, 0.63, 0.60]); perfil = np.array([casaco * (1 - 0.35 * max(0, (b / (NB - 1) - 0.55)) / 0.45) for b in range(NB)])     # pelo claro; escurece para o boleto/quartela
perfil[-3:] = np.array([0.16, 0.14, 0.13])                    # casco escuro
print("perfil cor topo:", perfil[0], "casco:", perfil[-1])
# nova imagem: 64 linhas extras no fim com o gradiente; UVs antigas reescaladas
EXTRA = 64; H1 = H0 + EXTRA
novo = np.ones((H1, W0, 4), dtype=np.float32); novo[EXTRA:, :, :] = pix
for x in range(W0):
    b = x / (W0 - 1) * (NB - 1); i0 = int(b); i1 = min(NB - 1, i0 + 1); f = b - i0; c = perfil[i0] * (1 - f) + perfil[i1] * f
    novo[:EXTRA, x, :3] = c
tex_nova = bpy.data.images.new("Cavalo_cor_nova", W0, H1); tex_nova.pixels = novo.ravel().tolist(); tex_nova.filepath_raw = base + "saida/Cavalo_cor.jpg"; tex_nova.file_format = 'JPEG'; tex_nova.save()
S0 = H0 / H1

# ---- decide o corte e apaga as patas do scan
bm = bmesh.new(); bm.from_mesh(mesh.data); bm.verts.ensure_lookup_table()
cortes = {}; cadeias = {}
for leg in LEGS:
    j = jp(leg); cadeias[leg] = j
    zc = j["cot"].z + (j["pul"].z - j["cot"].z) * 0.45; cortes[leg] = zc
    chain = [j["cot"], j["pul"], j["bol"], j["cor"], j["casco"]]
    def dist_chain(q):
        best = 9
        for a, b in zip(chain[:-1], chain[1:]):
            ab = b - a; t = max(0, min(1, (q - a).dot(ab) / ab.length_squared)); best = min(best, (q - (a + ab * t)).length)
        return best
    kill = [v for v in bm.verts if v.co.z < zc and dist_chain(v.co) < 0.17]
    print(leg, "corte z=%.3f" % zc, "apagando", len(kill))
    bmesh.ops.delete(bm, geom=kill, context='VERTS'); bm.verts.ensure_lookup_table()
# ---- cria tubos
def curva(pts, n_por_seg=6):
    """Catmull-Rom"""
    out = []
    P_ = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    for i in range(1, len(P_) - 2):
        p0, p1, p2, p3 = P_[i - 1], P_[i], P_[i + 1], P_[i + 2]
        for k in range(n_por_seg):
            t = k / n_por_seg
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-1]); return out
SEG = 14
novos = {}
for leg in LEGS:
    j = cadeias[leg]; zc = cortes[leg]; F = leg.endswith("F")
    # ponto de partida: no eixo cot->pul na altura do corte, um pouco acima (fica escondido dentro do volume do scan)
    a, b = j["cot"], j["pul"]; t0 = (a.z - (zc + 0.09)) / (a.z - b.z); start = a + (b - a) * t0
    eixo = [start, j["pul"], j["bol"], j["cor"], j["casco"]]
    pts = curva(eixo, 5)
    # raios por ponto (do scan); suaviza
    rs = []
    for k, q in enumerate(pts):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        rs.append(raio_em(q, d))
    for _ in range(4): rs = [rs[0]] + [(rs[i - 1] + 2 * rs[i] + rs[i + 1]) / 4 for i in range(1, len(rs) - 1)] + [rs[-1]]
    # perfil anatomico por articulacao (m): musculo -> joelho/jarrete -> canela fina -> boleto -> quartela -> casco
    n = len(pts); L = sum((pts[i + 1] - pts[i]).length for i in range(n - 1))
    def arco_de(e):
        d = min(range(n), key=lambda i: (pts[i] - e).length); return sum((pts[i + 1] - pts[i]).length for i in range(d)) / L
    f0, f1, f2 = arco_de(j["pul"]), arco_de(j["bol"]), arco_de(j["cor"])
    rt, rm = (0.074, 0.062) if F else (0.082, 0.066)
    knee = 0.054 if F else 0.058
    keys = [(0.0, rt), (f0 * 0.55, rm), (f0 - 0.035, knee * 0.95), (f0, knee), (f0 + 0.05, 0.036), ((f0 + f1) / 2, 0.033), (f1 - 0.04, 0.036), (f1 - 0.005, 0.050), (f1 + 0.04, 0.045), ((f1 + f2) / 2, 0.034), (f2 - 0.01, 0.036), (0.985, 0.047), (1.0, 0.050)]
    keys.sort()
    def r_em(u):
        for (ua, ra), (ub, rb) in zip(keys[:-1], keys[1:]):
            if ua <= u <= ub and ub > ua: t = (u - ua) / (ub - ua); t = t * t * (3 - 2 * t); return ra + (rb - ra) * t
        return keys[-1][1]
    acc = 0.0; final = []
    for i in range(n):
        if i > 0: acc += (pts[i] - pts[i - 1]).length
        u = acc / L; final.append((u, r_em(u)))
    # aneis com transporte paralelo
    verts = []; t_prev = (pts[1] - pts[0]).normalized(); ref = V((1, 0, 0)) if abs(t_prev.x) < 0.9 else V((0, 1, 0)); nrm = (ref - t_prev * ref.dot(t_prev)).normalized()
    aneis = []
    for k, q in enumerate(pts):
        tg = (pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)]).normalized()
        nrm = (nrm - tg * nrm.dot(tg)).normalized(); bn = tg.cross(nrm)
        ring = []
        for s in range(SEG):
            ang = 2 * math.pi * s / SEG; e = 1.0 if not F else 1.0
            ring.append(q + (nrm * math.cos(ang) * 1.0 + bn * math.sin(ang) * 0.86) * final[k][1])   # secao levemente achatada (lateral um pouco mais fina)
        aneis.append(ring)
    novos[leg] = (aneis, final, pts)
# ---- injeta no bmesh (com UV do scan e pesos)
uv_new = bm.loops.layers.uv.active
grupo_idx = {g.name: g.index for g in mesh.vertex_groups}
dl = bm.verts.layers.deform.verify()
def pesos_ponto(leg, u_arco, pts_arco):
    F = leg.endswith("F")
    nomes = [leg + ("_Forearm" if F else "_Shin"), leg + "_Cannon", leg + "_Pastern", leg + "_Hoof"]
    # limites em arco (fracao) dos ossos: forearm/shin: [0, joint pul], cannon: [pul, bol], pastern [bol, cor], hoof [cor, fim]
    j = cadeias[leg]; ejs = [j["pul"], j["bol"], j["cor"]]
    tot = sum((pts_arco[i + 1] - pts_arco[i]).length for i in range(len(pts_arco) - 1)); fr = []
    for e in ejs:
        d = min(range(len(pts_arco)), key=lambda i: (pts_arco[i] - e).length); fr.append(sum((pts_arco[i + 1] - pts_arco[i]).length for i in range(d)) / tot)
    bw = 0.05                                      # largura da mistura nas articulacoes
    ws = [0, 0, 0, 0]
    def ss(x): x = max(0, min(1, x)); return x * x * (3 - 2 * x)
    # bandas
    b1 = ss((u_arco - (fr[0] - bw)) / (2 * bw)); b2 = ss((u_arco - (fr[1] - bw)) / (2 * bw)); b3 = ss((u_arco - (fr[2] - bw)) / (2 * bw))
    ws[0] = 1 - b1; ws[1] = b1 * (1 - b2); ws[2] = b2 * (1 - b3); ws[3] = b3
    return {nomes[i]: ws[i] for i in range(4) if ws[i] > 0.001}
# reescala as UVs do scan (a textura ganhou linhas extras embaixo)
for f_ in bm.faces:
    for l_ in f_.loops: l_[uv_new].uv = (l_[uv_new].uv[0], l_[uv_new].uv[1] * S0 + EXTRA / H1)
for leg in LEGS:
    aneis, final, pts = novos[leg]; n = len(aneis); idx = []
    tot = sum((pts[i + 1] - pts[i]).length for i in range(n - 1)); acc = 0.0
    for k, ring in enumerate(aneis):
        if k > 0: acc += (pts[k] - pts[k - 1]).length
        w = pesos_ponto(leg, acc / tot, pts); row = []
        for p in ring:
            v = bm.verts.new(p)
            for nm, val in w.items(): v[dl][grupo_idx[nm]] = val
            row.append(v)
        idx.append(row)
    # faces
    for k in range(n - 1):
        for s in range(SEG):
            f = bm.faces.new((idx[k][s], idx[k][(s + 1) % SEG], idx[k + 1][(s + 1) % SEG], idx[k + 1][s]))
    # tampa do casco (fundo plano) e do topo (escondido)
    cen = bm.verts.new(sum((v.co for v in idx[-1]), V()) / SEG); cen[dl][grupo_idx[leg + "_Hoof"]] = 1.0
    for s in range(SEG): bm.faces.new((idx[-1][s], idx[-1][(s + 1) % SEG], cen))
    ct = bm.verts.new(sum((v.co for v in idx[0]), V()) / SEG); ct[dl][grupo_idx[leg + ("_Forearm" if leg.endswith("F") else "_Shin")]] = 1.0
    for s in range(SEG): bm.faces.new((idx[0][(s + 1) % SEG], idx[0][s], ct))
    # UV: faixa de cor (gradiente topo->casco) na parte nova da textura
    for k, row in enumerate(idx):
        for v in row:
            for l in v.link_loops: l[uv_new].uv = ((k / (len(idx) - 1)) * 0.98 + 0.01, 0.5 * EXTRA / H1)
    for v in (cen, ct):
        for l in v.link_loops: l[uv_new].uv = ((1.0 if v is cen else 0.0) * 0.98 + 0.01, 0.5 * EXTRA / H1)
    for v in bm.verts:
        pass
bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh.data); bm.free(); ob.free()
for p_ in mesh.data.polygons: p_.use_smooth = True
print("verts finais", len(mesh.data.vertices))
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_empe_final.blend")
