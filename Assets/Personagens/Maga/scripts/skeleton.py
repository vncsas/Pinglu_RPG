# Sorceress humanoid skeleton (Blender coords: faces -Y, her left = +X, meters). Measured on the
# normalised model: the body is ~9 cm to her left of x=0, left leg one step forward, high heels,
# left arm held out from the body, right hand on the staff in front.
J = {
 'Hips':         ((0.09, -0.02, 0.90), (0.09, -0.02, 1.00)),
 'Spine':        ((0.09, -0.02, 1.00), (0.09, -0.01, 1.10)),
 'Chest':        ((0.09, -0.01, 1.10), (0.09, 0.00, 1.20)),
 'UpperChest':   ((0.09, 0.00, 1.20), (0.095, 0.00, 1.33)),
 'Neck':         ((0.095, -0.01, 1.33), (0.105, -0.05, 1.43)),
 'Head':         ((0.105, -0.05, 1.43), (0.11, -0.07, 1.60)),
 'LeftShoulder': ((0.12, -0.01, 1.32), (0.24, 0.00, 1.33)),
 'LeftUpperArm': ((0.24, 0.00, 1.33), (0.34, 0.02, 1.08)),
 'LeftLowerArm': ((0.34, 0.02, 1.08), (0.51, -0.01, 0.88)),
 'LeftHand':     ((0.51, -0.01, 0.88), (0.56, -0.02, 0.80)),
 'RightShoulder':((0.06, -0.01, 1.32), (-0.06, 0.00, 1.33)),
 'RightUpperArm':((-0.06, 0.00, 1.33), (-0.16, -0.10, 1.10)),
 'RightLowerArm':((-0.16, -0.10, 1.10), (-0.19, -0.27, 1.12)),
 'RightHand':    ((-0.19, -0.27, 1.12), (-0.22, -0.33, 1.11)),
 'LeftUpperLeg': ((0.16, -0.02, 0.87), (0.205, -0.19, 0.52)),
 'LeftLowerLeg': ((0.205, -0.19, 0.52), (0.212, -0.25, 0.13)),
 'RightUpperLeg':((0.02, -0.02, 0.87), (0.045, 0.00, 0.49)),
 'RightLowerLeg':((0.045, 0.00, 0.49), (0.07, 0.05, 0.13)),
}
PARENT = {'Spine': 'Hips', 'Chest': 'Spine', 'UpperChest': 'Chest', 'Neck': 'UpperChest', 'Head': 'Neck',
 'LeftShoulder': 'UpperChest', 'LeftUpperArm': 'LeftShoulder', 'LeftLowerArm': 'LeftUpperArm', 'LeftHand': 'LeftLowerArm',
 'RightShoulder': 'UpperChest', 'RightUpperArm': 'RightShoulder', 'RightLowerArm': 'RightUpperArm', 'RightHand': 'RightLowerArm',
 'LeftUpperLeg': 'Hips', 'LeftLowerLeg': 'LeftUpperLeg', 'LeftFoot': 'LeftLowerLeg', 'LeftToes': 'LeftFoot',
 'RightUpperLeg': 'Hips', 'RightLowerLeg': 'RightUpperLeg', 'RightFoot': 'RightLowerLeg', 'RightToes': 'RightFoot'}
CONNECT = {'Spine', 'Chest', 'UpperChest', 'Neck', 'Head', 'LeftUpperArm', 'LeftLowerArm', 'LeftHand', 'RightUpperArm',
 'RightLowerArm', 'RightHand', 'LeftLowerLeg', 'LeftFoot', 'LeftToes', 'RightLowerLeg', 'RightFoot', 'RightToes'}
# staff axis (bottom point at z=0.08, direction toward the crystal), measured on norm.blend
STAFF_P0 = (-0.029, -0.124, 0.08); STAFF_D = (-0.181, -0.199, 0.963)
