# Analisa o scan: em fatias horizontais acha os "clusters" (pernas) para localizar cascos, joelhos e curvas das 4 pernas.
import bpy, numpy as np, math
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-cavalo/saida/prep.blend")
o = bpy.data.objects["Cavalo"]
P = np.array([v.co[:] for v in o.data.vertices])
print("N", len(P), "zmax", P[:, 2].max())
def clusters(pts, eps=0.07):
    n = len(pts); lab = -np.ones(n, int); c = 0
    for i in range(n):
        if lab[i] >= 0: continue
        stack = [i]; lab[i] = c
        while stack:
            a = stack.pop(); d = np.linalg.norm(pts - pts[a], axis=1); nb = np.where((d < eps) & (lab < 0))[0]
            lab[nb] = c; stack.extend(nb.tolist())
        c += 1
    return lab, c
for z in [0.05, 0.15, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.85, 0.95, 1.05, 1.15]:
    sl = P[(P[:, 2] > z-0.025) & (P[:, 2] < z+0.025)]
    if len(sl) == 0: print("z", z, "vazio"); continue
    lab, c = clusters(sl[:, :2], 0.06 if z < 0.9 else 0.08)
    out = []
    for k in range(c):
        pts = sl[lab == k]
        if len(pts) < 6: continue
        out.append((round(float(pts[:, 0].mean()), 3), round(float(pts[:, 1].mean()), 3), len(pts), round(float(pts[:, 0].max()-pts[:, 0].min()), 2), round(float(pts[:, 1].max()-pts[:, 1].min()), 2)))
    print("z", z, "clusters(x,y,n,dx,dy):", out)
