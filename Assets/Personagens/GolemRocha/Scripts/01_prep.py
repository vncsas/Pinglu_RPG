import bpy, os
from mathutils import Vector as V
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/source/Character Free Golem Rock by Oscar Creativo.fbx")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
ms=[o for o in bpy.data.objects if o.type=='MESH']
# UV bounds por malha
for m in ms:
    uv=m.data.uv_layers[0].data; us=[d.uv[0] for d in uv]; vs=[d.uv[1] for d in uv]
    print("UV", m.name, "u[%.2f,%.2f] v[%.2f,%.2f]"%(min(us),max(us),min(vs),max(vs)), "mat", m.data.materials[0].name)
# bbox mundo e pose
bpy.context.view_layer.update()
zs=[];xs=[];ys=[]
for m in ms:
    for v in m.data.vertices:
        w=m.matrix_world@v.co; zs.append(w.z); xs.append(w.x); ys.append(w.y)
print("BBOX x[%.2f,%.2f] y[%.2f,%.2f] z[%.2f,%.2f]"%(min(xs),max(xs),min(ys),max(ys),min(zs),max(zs)))
print("armature matrix_world scale", arm.matrix_world.to_scale(), "rot", [round(a,1) for a in arm.matrix_world.to_euler()])
# posicoes de ossos principais (mundo)
for n in ("CC_Base_Hip","CC_Base_Pelvis","CC_Base_Waist","CC_Base_Spine01","CC_Base_Spine02","CC_Base_Head","CC_Base_L_Clavicle","CC_Base_L_Upperarm","CC_Base_L_Forearm","CC_Base_L_Hand","CC_Base_L_Thigh","CC_Base_L_Calf","CC_Base_L_Foot","CC_Base_L_ToeBase"):
    b=arm.data.bones[n]; h=arm.matrix_world@b.head_local; t=arm.matrix_world@b.tail_local
    print("BONE", n, tuple(round(x,3) for x in h), "->", tuple(round(x,3) for x in t))
print("nomes dos ossos restantes:", [b.name for b in arm.data.bones if "Finger" in b.name or "Pinky" in b.name or "Thumb" in b.name or "Index" in b.name][:12])
