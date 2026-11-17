import bpy
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-pantano/saida/prep0.blend")
m=bpy.data.objects["Monstro"]; S=12.0
co=[v.co*S for v in m.data.vertices]
def clusters(vals,gap):
    vals=sorted(vals); out=[[vals[0],vals[0]]]
    for v in vals[1:]:
        if v-out[-1][1]>gap: out.append([v,v])
        else: out[-1][1]=v
    return out
zmax=max(c.z for c in co)
print("H",zmax)
z=0.0
while z<zmax:
    pts=[c for c in co if z<=c.z<z+0.1]
    if pts:
        cl=clusters([p.x for p in pts],0.12)
        s=" | ".join("x[%.2f,%.2f] y[%.2f,%.2f]"%(a,b,min(p.y for p in pts if a<=p.x<=b),max(p.y for p in pts if a<=p.x<=b)) for a,b in cl)
        print("z%.2f: %s"%(z,s))
    z+=0.1
