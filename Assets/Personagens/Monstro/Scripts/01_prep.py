# importa, aplica transformacoes, escala p/ altura alvo, centra; imprime bbox e salva; renderiza 4 vistas
import bpy, sys, os
from mathutils import Vector as V
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/Thiago/Documentos/swamp-monster/source/SWAMP_MONSTER.fbx")
m = [o for o in bpy.data.objects if o.type=='MESH'][0]; m.name="Monstro"
bpy.context.view_layer.objects.active=m; m.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
co=[v.co for v in m.data.vertices]
mn=V((min(c.x for c in co),min(c.y for c in co),min(c.z for c in co))); mx=V((max(c.x for c in co),max(c.y for c in co),max(c.z for c in co)))
print("BBOX", tuple(round(x,3) for x in mn), tuple(round(x,3) for x in mx))
bpy.ops.wm.save_as_mainfile(filepath="C:/Users/Thiago/Documentos/rig-pantano/saida/prep0.blend")
# texturas
img = bpy.data.images.load("C:/Users/Thiago/Documentos/swamp-monster/textures/FOREST_MONSTER_V1_mat_Flattened_Diffuse.png")
mat = m.data.materials[0]; mat.use_nodes=True; nt=mat.node_tree
for n in list(nt.nodes):
    if n.type!='OUTPUT_MATERIAL': nt.nodes.remove(n)
out=[n for n in nt.nodes if n.type=='OUTPUT_MATERIAL'][0]
bs=nt.nodes.new('ShaderNodeBsdfPrincipled'); tx=nt.nodes.new('ShaderNodeTexImage'); tx.image=img
nt.links.new(tx.outputs[0], bs.inputs['Base Color']); nt.links.new(bs.outputs[0], out.inputs[0])
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=600; sc.render.resolution_y=700
sc.display.shading.light='FLAT'; sc.display.shading.color_type='TEXTURE'
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'
c=(mn+mx)/2; sz=max((mx-mn).x,(mx-mn).y,(mx-mn).z); cam.data.ortho_scale=sz*1.15
os.makedirs("C:/Users/Thiago/Documentos/rig-pantano/saida/prev",exist_ok=True)
for nome,d in (("frente",V((0,-1,0))),("lado",V((1,0,0))),("costas",V((0,1,0))),("topo",V((0,0,1)))):
    cam.location=c-d*(sz*3); up='Y' if nome!="topo" else 'Y'
    cam.rotation_euler=(d).to_track_quat('-Z','Y').to_euler() if False else (-(-d)).to_track_quat('Z','Y').to_euler() if False else d.to_track_quat('-Z','Y').to_euler()
    sc.render.filepath="C:/Users/Thiago/Documentos/rig-pantano/saida/prev/%s.png"%nome; bpy.ops.render.render(write_still=True)
