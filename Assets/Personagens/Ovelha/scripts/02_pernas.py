import bpy, numpy as np
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-ovelha/saida/prep.blend")
o = bpy.data.objects["Ovelha"]; P = np.array([v.co[:] for v in o.data.vertices])
def clusters(pts, eps):
    n = len(pts); lab = -np.ones(n, int); c = 0
    for i in range(n):
        if lab[i] >= 0: continue
        stack = [i]; lab[i] = c
        while stack:
            a = stack.pop(); d = np.linalg.norm(pts - pts[a], axis=1); nb = np.where((d < eps) & (lab < 0))[0]; lab[nb] = c; stack.extend(nb.tolist())
        c += 1
    return lab, c
for z in [0.03, 0.08, 0.13, 0.18, 0.23, 0.28, 0.33, 0.38, 0.43, 0.48, 0.55, 0.65]:
    sl = P[(P[:, 2] > z-0.02) & (P[:, 2] < z+0.02)]
    if len(sl) == 0: print("z", z, "vazio"); continue
    lab, c = clusters(sl[:, :2], 0.06); out = []
    for k in range(c):
        pts = sl[lab == k]
        if len(pts) < 8: continue
        out.append((round(float(pts[:, 0].mean()), 3), round(float(pts[:, 1].mean()), 3), len(pts), round(float(pts[:, 0].max()-pts[:, 0].min()), 2), round(float(pts[:, 1].max()-pts[:, 1].min()), 2)))
    print("z", z, out)
