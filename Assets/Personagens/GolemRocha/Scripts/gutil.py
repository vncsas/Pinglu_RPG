import bpy, math
from mathutils import Quaternion, Vector as V
D = math.radians
AX = {"x": V((1, 0, 0)), "y": V((0, 1, 0)), "z": V((0, 0, 1))}
def q_mundo(pb, eixo, graus):
    """rotacao de 'graus' em torno de um eixo do MUNDO (x lado, y frente/tras, z cima), convertida para o espaco local do osso"""
    Rr = pb.bone.matrix_local.to_quaternion(); Rw = Quaternion(AX[eixo], D(graus))
    return Rr.inverted() @ Rw @ Rr
def posar(arm, d):
    """d: {osso: [(eixo, graus), ...]} aplicadas em ordem (mundo)"""
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = Quaternion((1, 0, 0, 0)); pb.location = (0, 0, 0)
    for n, lst in d.items():
        pb = arm.pose.bones[n]; q = Quaternion((1, 0, 0, 0))
        for (e, g) in lst: q = q_mundo(pb, e, g) @ q
        pb.rotation_quaternion = q
