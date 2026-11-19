# perfil do corpo: por faixa de Y mostra z minimo/maximo e centro (para posicionar coluna, pescoco, cabeca e cauda)
import bpy, numpy as np
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-ovelha/saida/prep.blend")
o = bpy.data.objects["Ovelha"]; P = np.array([v.co[:] for v in o.data.vertices])
print("range y", P[:, 1].min(), P[:, 1].max())
for y0 in np.arange(-0.55, 0.56, 0.05):
    s = P[(P[:, 1] >= y0) & (P[:, 1] < y0+0.05)]
    if len(s) == 0: continue
    top = s[:, 2].max(); body = s[s[:, 2] > 0.8] if len(s[s[:, 2] > 0.8]) else s
    print("y %.2f..%.2f n=%d zmin(all)=%.2f zmax=%.2f | z>0.8: zmin=%.2f zmean=%.2f xmean=%.3f xw=%.2f" % (y0, y0+0.1, len(s), s[:, 2].min(), top, body[:, 2].min(), body[:, 2].mean(), body[:, 0].mean(), body[:, 0].max()-body[:, 0].min()))
