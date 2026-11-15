# Corrige a pose do scan (trote, uma pata levantada) para "em pe": gira cada osso das pernas para a direcao alvo, aplica a deformacao
# na malha e redefine a pose de descanso do esqueleto.
import bpy, sys, math
sys.path.insert(0, "C:/Users/Thiago/Documentos/rig-cavalo/scripts")
from mathutils import Vector as V, Matrix
import horse_def as H
base = "C:/Users/Thiago/Documentos/rig-cavalo/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_trote.blend")
arm = bpy.data.objects["CavaloArmature"]; mesh = bpy.data.objects["Cavalo"]
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE')
alvo = H.em_pe(); pb = arm.pose.bones
def girar(nome, tgt_dir):
    b = pb[nome]; M = b.matrix.copy(); cur = (b.tail - b.head).normalized()
    R = cur.rotation_difference(tgt_dir.normalized()).to_matrix().to_4x4(); loc = M.translation.copy(); M.translation = V((0, 0, 0))
    b.matrix = Matrix.Translation(loc) @ R @ M; bpy.context.view_layer.update()
for nome, j in alvo.items():
    cor = j["cor"]; frente = nome.endswith("F"); print(nome, "fator de angulo", round(j["f"], 2))
    cadeia = ([(nome + "_Shoulder", j["top"] - V((H.XM + 0.55 * (j["top"].x - H.XM), 0.02, 1.36)))] if frente else []) + [
        (nome + ("_Arm" if frente else "_Thigh"), j["cot"] - j["top"]),
        (nome + ("_Forearm" if frente else "_Shin"), j["pul"] - j["cot"]),
        (nome + "_Cannon", j["bol"] - j["pul"]), (nome + "_Pastern", cor - j["bol"]), (nome + "_Hoof", j["casco"] - cor)]
    for bn, d in cadeia: girar(bn, d)
print("CASCOS (z, x, y):")
j = alvo["LH"]
for bn, key in (("LH_Thigh", "cot"), ("LH_Shin", "pul"), ("LH_Cannon", "bol"), ("LH_Pastern", "cor"), ("LH_Hoof", "casco")):
    t = pb[bn].tail; print("DBG", bn, "tail", tuple(round(c, 3) for c in t), "alvo", tuple(round(c, 3) for c in j[key]), "len", round((pb[bn].tail-pb[bn].head).length, 3))
for nome in alvo:
    t = arm.matrix_world @ pb[nome + "_Hoof"].tail; print(nome, round(t.z, 3), round(t.x, 3), round(t.y, 3))
# aplica na malha e redefine o descanso
bpy.ops.object.mode_set(mode='OBJECT'); bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
bpy.ops.object.modifier_apply(modifier="Armature")
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
mesh.modifiers.new("Armature", 'ARMATURE').object = arm
zs = [v.co.z for v in mesh.data.vertices]; print("malha zmin", min(zs), "zmax", max(zs))
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_empe.blend")
