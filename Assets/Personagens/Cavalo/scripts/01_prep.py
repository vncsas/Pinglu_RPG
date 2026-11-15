# Prepara o scan: orienta (cabeca para -Y, como os outros personagens), apoia no chao (z=0), centraliza, reduz a malha (~30k tris) e salva prep.blend
import bpy, sys, os, math
from mathutils import Vector, Matrix
base = "C:/Users/Thiago/Documentos/rig-cavalo/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.wm.obj_import(filepath=base + "fonte/Scaniverse/Scaniverse.obj")
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]; o.name = "Cavalo"; o.data.name = "Cavalo"
bpy.context.view_layer.objects.active = o; o.select_set(True)
# cabeca estava em -X; queremos -Y  -> gira +90 graus em Z
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # aplica a rotacao do importador
o.rotation_euler = (0, 0, math.radians(90)); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bb = [Vector(c) for c in o.bound_box]
mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))); mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
# centro em x e y (corpo, sem a cauda: usa a mediana dos vertices), z=0 no casco mais baixo
xs = sorted(v.co.x for v in o.data.vertices); ys = sorted(v.co.y for v in o.data.vertices)
cx = xs[len(xs)//2]; cy = ys[len(ys)//2]
for v in o.data.vertices: v.co.x -= cx; v.co.y -= cy; v.co.z -= mn.z
print("PRE tris", len(o.data.polygons))
# reduz a malha mantendo UV
dec = o.modifiers.new("dec", 'DECIMATE'); dec.decimate_type = 'COLLAPSE'; dec.ratio = 0.16
bpy.ops.object.modifier_apply(modifier="dec")
bpy.ops.object.shade_smooth()
me = o.data
print("POS tris", len(me.polygons), "verts", len(me.vertices))
bb = [Vector(c) for c in o.bound_box]
print("BBOX", Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))), Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb))))
# textura 2k
mat = bpy.data.materials.new("Cavalo"); mat.use_nodes = True
nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]; tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(base + "saida/Cavalo_cor.jpg")
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); bsdf.inputs["Roughness"].default_value = 0.85
o.data.materials.clear(); o.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/prep.blend")
