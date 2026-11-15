# escala (cavalo de tracao ~1,75 m na cernelha), orienta (cabeca para -Y), reduz a malha e prepara materiais (basecolor/normal/rugosidade)
import bpy, sys, os, math
from mathutils import Vector as V
base = "C:/Users/Thiago/Documentos/rig-cavalo2/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=base + "fonte/tripo_convert_8c94b723-53db-4186-8eff-1959588fa67b.fbx")
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]; o.name = "Cavalo2"; o.data.name = "Cavalo2"
bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
P = [v.co.copy() for v in o.data.vertices]
# cabeca: lado com a orelha/ponto mais alto na frente do centro. O ponto mais alto da cabeca fica perto de y extremo; o pescoco e mais alto que a garupa perto da cabeca
ymin = min(p.y for p in P); ymax = max(p.y for p in P)
alto_neg = max(p.z for p in P if p.y < ymin * 0.5); alto_pos = max(p.z for p in P if p.y > ymax * 0.5)
print("topo lado -Y", alto_neg, "topo lado +Y", alto_pos, "ymin", ymin, "ymax", ymax)
ESC = 1.76 / (max(p.z for p in P))
for v in o.data.vertices: v.co *= ESC
print("ESCALA", ESC)
dec = o.modifiers.new("dec", 'DECIMATE'); dec.decimate_type = 'COLLAPSE'; dec.ratio = 60000.0 / len(o.data.polygons)
bpy.ops.object.modifier_apply(modifier="dec")
bpy.ops.object.shade_smooth()
print("TRIS", len(o.data.polygons), "VERTS", len(o.data.vertices))
mat = bpy.data.materials.new("Cavalo2"); mat.use_nodes = True; nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(base + "saida/Cavalo2_cor.jpg"); nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); bsdf.inputs["Roughness"].default_value = 0.6
o.data.materials.clear(); o.data.materials.append(mat)
bb = [V(c) for c in o.bound_box]; print("BBOX", V((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))), V((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb))))
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/prep.blend")
