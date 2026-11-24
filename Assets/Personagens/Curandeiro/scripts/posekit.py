# Pose helpers for the dark knight rig (FK posing in armature space), shared by the animation scripts.
import bpy, math
from mathutils import Matrix, Vector, Quaternion

FINGERS = ["Thumb", "Index", "Middle", "Ring", "Little"]; PH = ["Proximal", "Intermediate", "Distal"]

class Kit:
    def __init__(self, arm):
        self.arm = arm; self.P = arm.pose.bones; self.B = arm.data.bones
        self.AD = arm["apose_dirs"]
        self.LIMBS = [b.name for b in self.B if b.use_deform and any(k in b.name for k in
                      ("UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes"))
                      and "Twist" not in b.name and "Weapon" not in b.name and b.name in self.AD]
        self.rel_sword = (self.B["RightHand"].matrix_local.inverted() @ self.B["RightHandWeapon"].matrix_local) if "RightHandWeapon" in self.B else None

    def upd(self): bpy.context.view_layer.update()

    def reset(self, apose=True, arms=True):
        """identity pose; apose=True puts the limbs back in the model's original pose (arms down,
        sword hold); arms=False keeps the arms in T-pose"""
        for pb in self.P: pb.matrix_basis = Matrix.Identity(4)
        self.upd()
        if apose:
            for n in self.LIMBS:
                if arms or not any(k in n for k in ("Arm", "Hand")): self.aim(n, self.AD[n])

    def rot_world(self, n, ax, deg):
        pb = self.P[n]; h = pb.head.copy(); R = Matrix.Rotation(math.radians(deg), 4, ax)
        pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix; self.upd()

    def rot_local(self, n, ax, deg):
        pb = self.P[n]; pb.matrix_basis = pb.matrix_basis @ Matrix.Rotation(math.radians(deg), 4, ax); self.upd()

    def aim(self, n, d, twist=0):
        pb = self.P[n]; d = Vector(d).normalized(); h = pb.head.copy()
        q = (pb.tail-pb.head).normalized().rotation_difference(d); q = Quaternion(d, math.radians(twist)) @ q
        pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix; self.upd()

    def hips(self, drop=0, fwd=0, side=0, yaw=0, lean=0, roll=0):
        pb = self.P['Hips']; m = pb.matrix.copy(); m.translation += Vector((side, -fwd, -drop)); pb.matrix = m; self.upd()
        if yaw: self.rot_world('Hips', 'Z', yaw)
        if lean: self.rot_world('Hips', 'X', lean)          # + = forward
        if roll: self.rot_world('Hips', 'Y', roll)

    def two_bone(self, upn, lon, target, pole):
        P, B = self.P, self.B; up, lo = P[upn], P[lon]
        l1 = (B[upn].tail_local-B[upn].head_local).length; l2 = (B[lon].tail_local-B[lon].head_local).length
        H = up.head.copy(); T = Vector(target)
        d = min((T-H).length, (l1+l2)*0.999); n = (T-H).normalized(); T = H+n*d
        a = (l1*l1-l2*l2+d*d)/(2*d); h = math.sqrt(max(l1*l1-a*a, 0))
        pole = Vector(pole); pole = (pole-n*pole.dot(n)).normalized()
        K = H+n*a+pole*h
        self.aim(upn, K-up.head); self.aim(lon, T-lo.head)

    def foot(self, side, pos, pitch=0, yaw=0):
        """ankle to pos (knee over the toes); pitch>0 lifts the heel (pivot on the ball), pitch<0
        lifts the toes (pivot on the heel); heel/ball never under the floor"""
        P, B = self.P, self.B
        S = 'Left' if side == 'L' else 'Right'
        ft, to = P[S+'Foot'], P[S+'Toes']
        ank_r, ball_r = B[S+'Foot'].head_local, B[S+'Toes'].head_local
        heel_r = Vector((ank_r.x, ank_r.y+0.07, ball_r.z-0.01))
        Ry = Matrix.Rotation(math.radians(yaw), 3, 'Z'); Rrel = Ry @ Matrix.Rotation(math.radians(pitch), 3, 'X')
        zc = max(heel_r.z-(Rrel @ (heel_r-ank_r)).z, ball_r.z-(Rrel @ (ball_r-ank_r)).z)
        pos = Vector(pos); pos.z = max(pos.z, zc)
        fwd = P["Hips"].matrix.to_3x3() @ Vector((0, 0, 1)); fwd = Vector((fwd.x, fwd.y, 0)).normalized()
        fwd = (fwd+Ry @ Vector((0, -1, 0))).normalized()
        self.two_bone(S+'UpperLeg', S+'LowerLeg', pos, fwd)
        m = (Rrel @ B[S+'Foot'].matrix_local.to_3x3()).to_4x4(); m.translation = ft.head.copy(); ft.matrix = m; self.upd()
        Rt = Ry if pitch > 0 else Rrel
        m = (Rt @ B[S+'Toes'].matrix_local.to_3x3()).to_4x4(); m.translation = to.head.copy(); to.matrix = m; self.upd()
        return pos

    def hand_to(self, S, M_hand, pole):
        """hand bone to matrix M_hand (armature space), arm solved to reach it; returns the wrist miss (m)"""
        self.two_bone(S+"UpperArm", S+"LowerArm", M_hand.translation, pole)
        miss = (self.P[S+"Hand"].head-M_hand.translation).length
        m = M_hand.copy(); m.translation = self.P[S+"Hand"].head.copy(); self.P[S+"Hand"].matrix = m; self.upd()
        return miss

    def sword_frame(self, grip, blade_dir, flat):
        y = Vector(blade_dir).normalized(); z = Vector(flat); z = (z-y*z.dot(y)).normalized(); x = y.cross(z)
        M = Matrix((x, y, z)).transposed().to_4x4(); M.translation = Vector(grip); return M

    def hand_for_socket(self, M_socket):
        """right-hand matrix that puts the RightHandWeapon socket at M_socket"""
        return M_socket @ self.rel_sword.inverted()

    def fist(self, S, amount):
        for fn in FINGERS:
            for k, ph in enumerate(PH):
                deg = ((25, 35, 35)[k] if fn == "Thumb" else 70)*amount
                if f"{S}{fn}{ph}" in self.P: self.rot_local(f"{S}{fn}{ph}", 'X', deg)   # (some rigs have 4 fingers)

    def key_all(self, frame):
        for pb in self.P:
            b = self.B[pb.name]
            if b.use_deform and "Weapon" not in pb.name and pb.name != "SwordBack":
                pb.keyframe_insert("rotation_quaternion", frame=frame)
        self.P["Hips"].keyframe_insert("location", frame=frame)

def smooth(t): t = min(max(t, 0.0), 1.0); return t*t*(3-2*t)

def lerp_matrix(A, B, t):
    q = A.to_quaternion().slerp(B.to_quaternion(), t); M = q.to_matrix().to_4x4()
    M.translation = A.translation.lerp(B.translation, t); return M
