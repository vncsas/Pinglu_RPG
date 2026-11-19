import bpy, numpy as np
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-cavalo2/saida/prep.blend")
o = bpy.data.objects["Cavalo2"]; P = np.array([v.co[:] for v in o.data.vertices])
def clusters(pts, eps):
    n = len(pts); lab = -np.ones(n, int); c = 0
    for i in range(n):
        if lab[i] >= 0: continue
        stack = [i]; lab[i] = c
        while stack:
            a = stack.pop(); d = np.linalg.norm(pts - pts[a], axis=1); nb = np.where((d < eps) & (lab < 0))[0]; lab[nb] = c; stack.extend(nb.tolist())
        c += 1
    return lab, c
for z in [0.05, 0.15, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.85, 0.95, 1.05, 1.15]:
    sl = P[(P[:, 2] > z-0.03) & (P[:, 2] < z+0.03)]
    if len(sl) == 0: print("z", z, "vazio"); continue
    lab, c = clusters(sl[:, :2], 0.09); out = []
    for k in range(c):
        pts = sl[lab == k]
        if len(pts) < 8: continue
        out.append((round(float(pts[:, 0].mean()), 3), round(float(pts[:, 1].mean()), 3), len(pts), round(float(pts[:, 0].max()-pts[:, 0].min()), 2), round(float(pts[:, 1].max()-pts[:, 1].min()), 2)))
    print("z", z, out)
