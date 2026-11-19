import bpy
from mathutils import Vector as V
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-angler/saida/prep.blend")
m=bpy.data.objects["Angler"]; co=[v.co.copy() for v in m.data.vertices]
def cl(vals,gap):
    vals=sorted(vals); out=[[vals[0],vals[0]]]
    for v in vals[1:]:
        if v-out[-1][1]>gap: out.append([v,v])
        else: out[-1][1]=v
    return out
print("BB", [round(min(c[i] for c in co),2) for i in range(3)], [round(max(c[i] for c in co),2) for i in range(3)])
print("== x>0 por z: clusters em y(real) e centro x")
z=0.0
while z<2.6:
    pts=[c for c in co if z<=c.z<z+0.1 and c.x>0.05]
    if pts:
        s=[]
        for a,b in cl([p.y for p in pts],0.10):
            q=[p for p in pts if a<=p.y<=b]; s.append("y[%.2f,%.2f] x[%.2f,%.2f] n%d"%(a,b,min(p.x for p in q),max(p.x for p in q),len(q)))
        print("z%.1f: %s"%(z," | ".join(s)))
    z+=0.1
print("== braco x>0.7 por z: centro")
z=0.0
while z<2.0:
    pts=[c for c in co if z<=c.z<z+0.1 and c.x>0.7]
    if pts: n=len(pts); print("z%.1f c=(%.2f,%.2f) n%d x[%.2f,%.2f] y[%.2f,%.2f]"%(z,sum(p.x for p in pts)/n,sum(p.y for p in pts)/n,n,min(p.x for p in pts),max(p.x for p in pts),min(p.y for p in pts),max(p.y for p in pts)))
    z+=0.1
print("== cauda y>0.2 (real) por y: centro z,x")
y=0.2
while y<1.8:
    pts=[c for c in co if y<=c.y<y+0.1]
    if pts: n=len(pts); print("y%.1f c=(x%.2f,z%.2f) z[%.2f,%.2f] x[%.2f,%.2f]"%(y,sum(p.x for p in pts)/n,sum(p.z for p in pts)/n,min(p.z for p in pts),max(p.z for p in pts),min(p.x for p in pts),max(p.x for p in pts)))
    y+=0.1
print("== cabeca y<-0.5 por y: z range")
y=-1.4
while y<-0.2:
    pts=[c for c in co if y<=c.y<y+0.1 and abs(c.x)<0.5]
    if pts: print("y%.1f z[%.2f,%.2f] x[%.2f,%.2f] n%d"%(y,min(p.z for p in pts),max(p.z for p in pts),min(p.x for p in pts),max(p.x for p in pts),len(pts)))
    y+=0.1
