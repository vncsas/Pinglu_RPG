import bpy
from mathutils import Vector as V
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-pantano/saida/prep0.blend")
m=bpy.data.objects["Monstro"]; S=12.0
co=[v.co*S for v in m.data.vertices]
def cen(pts): 
    n=len(pts); return V((sum(p.x for p in pts)/n,sum(p.y for p in pts)/n,sum(p.z for p in pts)/n)), n
print("== perna +x (z)")
z=0.0
while z<1.0:
    pts=[c for c in co if z<=c.z<z+0.05 and c.x>0.05 and c.y<0.3]
    if pts: c,n=cen(pts); print("z%.2f c=(%.2f,%.2f,%.2f) n=%d ext x[%.2f,%.2f] y[%.2f,%.2f]"%(z,c.x,c.y,c.z,n,min(p.x for p in pts),max(p.x for p in pts),min(p.y for p in pts),max(p.y for p in pts)))
    z+=0.05
print("== perna -x (z)")
z=0.0
while z<1.0:
    pts=[c for c in co if z<=c.z<z+0.05 and c.x<-0.05]
    if pts: c,n=cen(pts); print("z%.2f c=(%.2f,%.2f,%.2f) n=%d ext y[%.2f,%.2f]"%(z,c.x,c.y,c.z,n,min(p.y for p in pts),max(p.y for p in pts)))
    z+=0.05
print("== braco pendente +x (z)")
z=0.6
while z<1.6:
    pts=[c for c in co if z<=c.z<z+0.05 and c.x>0.38]
    if pts: c,n=cen(pts); print("z%.2f c=(%.2f,%.2f,%.2f) n=%d"%(z,c.x,c.y,c.z,n))
    z+=0.05
print("== braco frontal -x (y)")
y=-1.3
while y<-0.3:
    pts=[c for c in co if y<=c.y<y+0.05 and c.z>1.0 and c.x<0.2 and c.z<1.9 and c.y<-0.3]
    if pts: c,n=cen(pts); print("y%.2f c=(%.2f,%.2f,%.2f) n=%d ext x[%.2f,%.2f] z[%.2f,%.2f]"%(y,c.x,c.y,c.z,n,min(p.x for p in pts),max(p.x for p in pts),min(p.z for p in pts),max(p.z for p in pts)))
    y+=0.05
print("== tronco/cabeca (z) centro")
z=0.8
while z<2.25:
    pts=[c for c in co if z<=c.z<z+0.1 and -0.35<c.x<0.35 and c.y>-0.8 and not (c.x>0.4)]
    if pts: c,n=cen(pts); print("z%.2f c=(%.2f,%.2f,%.2f) n=%d ext x[%.2f,%.2f] y[%.2f,%.2f]"%(z,c.x,c.y,c.z,n,min(p.x for p in pts),max(p.x for p in pts),min(p.y for p in pts),max(p.y for p in pts)))
    z+=0.1
