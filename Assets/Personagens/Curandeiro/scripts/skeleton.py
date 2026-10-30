# Bone seraph (healer) skeleton (Blender coords: faces -Y, its left = +X, meters), measured on norm.blend.
# The sculpt stands mid-step: right leg forward, left leg back. Wings: 4-bone chains along the leading edge
# (root on the shoulder blade > up to the wrist at the top > down the primaries to the tip).
J = {
 'Hips':          ((0, -0.02, 0.95), (0, 0.00, 1.06)),
 'Spine':         ((0, 0.00, 1.06), (0, 0.02, 1.18)),
 'Chest':         ((0, 0.02, 1.18), (0, 0.04, 1.30)),
 'UpperChest':    ((0, 0.04, 1.30), (0, 0.04, 1.43)),
 'Neck':          ((0, 0.04, 1.43), (0, -0.02, 1.52)),
 'Head':          ((0, -0.02, 1.52), (0, -0.07, 1.72)),
 'LeftShoulder':  ((0.03, 0.03, 1.41), (0.175, 0.0, 1.40)),
 'LeftUpperArm':  ((0.175, 0.0, 1.40), (0.225, 0.02, 1.10)),
 'LeftLowerArm':  ((0.225, 0.02, 1.10), (0.285, -0.07, 0.89)),
 'LeftHand':      ((0.285, -0.07, 0.89), (0.30, -0.13, 0.71)),
 'LeftUpperLeg':  ((0.085, -0.05, 0.93), (0.115, 0.02, 0.49)),
 'LeftLowerLeg':  ((0.115, 0.02, 0.49), (0.095, 0.12, 0.12)),
 'RightUpperLeg': ((-0.085, -0.05, 0.93), (-0.115, -0.27, 0.57)),
 'RightLowerLeg': ((-0.115, -0.27, 0.57), (-0.085, -0.17, 0.12)),
 'Wing_L_1':      ((0.10, 0.14, 1.42), (0.30, 0.22, 1.62)),
 'Wing_L_2':      ((0.30, 0.22, 1.62), (0.42, 0.27, 1.77)),
 'Wing_L_3':      ((0.42, 0.27, 1.77), (0.70, 0.28, 1.15)),
 'Wing_L_4':      ((0.70, 0.28, 1.15), (0.84, 0.25, 0.44)),
}
for k in [k for k in J if k.startswith("Wing_L") or k.startswith("Left") and any(a in k for a in ("Shoulder", "Arm", "Hand"))]:
    (h, t) = J[k]; J[k.replace("_L_", "_R_").replace("Left", "Right")] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]))
TOES = {"Left": (0.11, -0.08, 0.02), "Right": (-0.12, -0.37, 0.02)}     # toe tips
PARENT = {'Spine': 'Hips', 'Chest': 'Spine', 'UpperChest': 'Chest', 'Neck': 'UpperChest', 'Head': 'Neck',
 'LeftShoulder': 'UpperChest', 'LeftUpperArm': 'LeftShoulder', 'LeftLowerArm': 'LeftUpperArm', 'LeftHand': 'LeftLowerArm',
 'RightShoulder': 'UpperChest', 'RightUpperArm': 'RightShoulder', 'RightLowerArm': 'RightUpperArm', 'RightHand': 'RightLowerArm',
 'LeftUpperLeg': 'Hips', 'LeftLowerLeg': 'LeftUpperLeg', 'LeftFoot': 'LeftLowerLeg', 'LeftToes': 'LeftFoot',
 'RightUpperLeg': 'Hips', 'RightLowerLeg': 'RightUpperLeg', 'RightFoot': 'RightLowerLeg', 'RightToes': 'RightFoot',
 'Wing_L_1': 'UpperChest', 'Wing_L_2': 'Wing_L_1', 'Wing_L_3': 'Wing_L_2', 'Wing_L_4': 'Wing_L_3',
 'Wing_R_1': 'UpperChest', 'Wing_R_2': 'Wing_R_1', 'Wing_R_3': 'Wing_R_2', 'Wing_R_4': 'Wing_R_3'}
CONNECT = {'Spine', 'Chest', 'UpperChest', 'Neck', 'Head', 'LeftUpperArm', 'LeftLowerArm', 'LeftHand', 'RightUpperArm',
 'RightLowerArm', 'RightHand', 'LeftLowerLeg', 'LeftFoot', 'LeftToes', 'RightLowerLeg', 'RightFoot', 'RightToes',
 'Wing_L_2', 'Wing_L_3', 'Wing_L_4', 'Wing_R_2', 'Wing_R_3', 'Wing_R_4'}
