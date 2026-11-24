import bpy, os
from mathutils import Vector as V
T="C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/textures/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/source/Character Free Golem Rock by Oscar Creativo.fbx")
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; arm.hide_render=True
base=bpy.data.images.load(T+"CHARACTER_ROCK_BAJA_ZREMESHER_1_BaseColor..png"); olho=bpy.data.images.load(T+"CHARACTER_ROCK_BAJA_EYES_Emissive.1023.png")
for m in [o for o in bpy.data.objects if o.type=='MESH']:
    mat=m.data.materials[0]; mat.use_nodes=True; nt=mat.node_tree
    for n in list(nt.nodes):
        if n.type!='OUTPUT_MATERIAL': nt.nodes.remove(n)
    o=[n for n in nt.nodes if n.type=='OUTPUT_MATERIAL'][0]; bs=nt.nodes.new('ShaderNodeBsdfPrincipled'); tx=nt.nodes.new('ShaderNodeTexImage'); tx.image=olho if m.name=="EYES" else base; tx.extension='REPEAT'
    nt.links.new(tx.outputs[0],bs.inputs['Base Color']); nt.links.new(bs.outputs[0],o.inputs[0])
sc=bpy.context.scene; sc.render.engine='BLENDER_WORKBENCH'; sc.render.resolution_x=700; sc.render.resolution_y=700
sc.display.shading.light='FLAT'; sc.display.shading.color_type='TEXTURE'
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'; cam.data.ortho_scale=3.4
sc.world=bpy.data.worlds.new("w"); sc.world.color=(0.75,0.78,0.85)
c=V((0,0,1.25))
os.makedirs("C:/Users/Thiago/Documentos/rig-golem/saida/v0",exist_ok=True)
for nome,d in (("frente",V((0,1,0))),("lado",V((1,0,0)))):
    cam.location=c-d*8; cam.rotation_euler=d.to_track_quat('-Z','Y').to_euler()
    sc.render.filepath="C:/Users/Thiago/Documentos/rig-golem/saida/v0/%s.png"%nome; bpy.ops.render.render(write_still=True)
