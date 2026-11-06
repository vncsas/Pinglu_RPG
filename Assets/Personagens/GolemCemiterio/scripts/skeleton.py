# Gravestone golem (mini-boss) skeleton, Blender coords: faces -Y, its left = +X, metres, measured on norm.blend (2.4 m tall).
# Huge shoulders and hanging arms, short thick legs, slab "head". Symmetric: the right side is mirrored from the left.
J = {
 'Hips':          ((0, 0.00, 1.15), (0, 0.02, 1.32)),
 'Spine':         ((0, 0.02, 1.32), (0, 0.04, 1.50)),
 'Chest':         ((0, 0.04, 1.50), (0, 0.05, 1.68)),
 'UpperChest':    ((0, 0.05, 1.68), (0, 0.05, 1.86)),
 'Neck':          ((0, 0.05, 1.86), (0, 0.04, 2.02)),
 'Head':          ((0, 0.04, 2.02), (0, 0.03, 2.38)),
 'LeftShoulder':  ((0.10, 0.02, 1.82), (0.58, -0.05, 1.80)),
 'LeftUpperArm':  ((0.58, -0.05, 1.80), (0.78, -0.25, 1.25)),
 'LeftLowerArm':  ((0.78, -0.25, 1.25), (0.92, -0.30, 0.85)),
 'LeftHand':      ((0.92, -0.30, 0.85), (0.92, -0.30, 0.62)),
 'LeftUpperLeg':  ((0.30, 0.00, 1.15), (0.38, -0.20, 0.66)),
 'LeftLowerLeg':  ((0.38, -0.20, 0.66), (0.38, -0.03, 0.22)),
}
for k in [k for k in J if k.startswith("Left")]:
    (h, t) = J[k]; J[k.replace("Left", "Right")] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]))
TOES = {"Left": (0.38, -0.40, 0.04), "Right": (-0.38, -0.40, 0.04)}     # toe tips
PARENT = {'Spine': 'Hips', 'Chest': 'Spine', 'UpperChest': 'Chest', 'Neck': 'UpperChest', 'Head': 'Neck',
 'LeftShoulder': 'UpperChest', 'LeftUpperArm': 'LeftShoulder', 'LeftLowerArm': 'LeftUpperArm', 'LeftHand': 'LeftLowerArm',
 'RightShoulder': 'UpperChest', 'RightUpperArm': 'RightShoulder', 'RightLowerArm': 'RightUpperArm', 'RightHand': 'RightLowerArm',
 'LeftUpperLeg': 'Hips', 'LeftLowerLeg': 'LeftUpperLeg', 'LeftFoot': 'LeftLowerLeg', 'LeftToes': 'LeftFoot',
 'RightUpperLeg': 'Hips', 'RightLowerLeg': 'RightUpperLeg', 'RightFoot': 'RightLowerLeg', 'RightToes': 'RightFoot'}
CONNECT = {'Spine', 'Chest', 'UpperChest', 'Neck', 'Head', 'LeftUpperArm', 'LeftLowerArm', 'LeftHand', 'RightUpperArm',
 'RightLowerArm', 'RightHand', 'LeftLowerLeg', 'LeftFoot', 'LeftToes', 'RightLowerLeg', 'RightFoot', 'RightToes'}
