# Dark knight humanoid skeleton guess (Blender coords: faces -Y, his left = +X, meters).
J = {
 'Hips':         ((0, 0.00, 0.95), (0, 0.01, 1.06)),
 'Spine':        ((0, 0.01, 1.06), (0, 0.03, 1.19)),
 'Chest':        ((0, 0.03, 1.19), (0, 0.05, 1.32)),
 'UpperChest':   ((0, 0.05, 1.32), (0, 0.05, 1.50)),
 'Neck':         ((0, 0.05, 1.50), (0, 0.02, 1.60)),
 'Head':         ((0, 0.02, 1.60), (0, 0.02, 1.82)),
 'LeftShoulder': ((0.04, 0.04, 1.46), (0.20, 0.06, 1.45)),
 'LeftUpperArm': ((0.20, 0.06, 1.45), (0.295, 0.085, 1.19)),
 'LeftLowerArm': ((0.295, 0.085, 1.19), (0.330, 0.025, 0.965)),
 'LeftHand':     ((0.330, 0.025, 0.965), (0.315, -0.005, 0.86)),
 'RightShoulder':((-0.04, 0.04, 1.46), (-0.20, 0.06, 1.45)),
 'RightUpperArm':((-0.20, 0.06, 1.45), (-0.287, 0.09, 1.19)),
 'RightLowerArm':((-0.287, 0.09, 1.19), (-0.312, 0.035, 0.97)),
 'RightHand':    ((-0.312, 0.035, 0.97), (-0.29, 0.03, 0.86)),
 'LeftUpperLeg': ((0.10, 0.0, 0.93), (0.128, -0.045, 0.50)),
 'LeftLowerLeg': ((0.128, -0.045, 0.50), (0.160, -0.045, 0.095)),
 'RightUpperLeg':((-0.10, 0.0, 0.93), (-0.125, 0.045, 0.50)),
 'RightLowerLeg':((-0.125, 0.045, 0.50), (-0.123, 0.06, 0.095)),
}

PARENT = {'Spine': 'Hips', 'Chest': 'Spine', 'UpperChest': 'Chest', 'Neck': 'UpperChest', 'Head': 'Neck',
 'LeftShoulder': 'UpperChest', 'LeftUpperArm': 'LeftShoulder', 'LeftLowerArm': 'LeftUpperArm', 'LeftHand': 'LeftLowerArm',
 'RightShoulder': 'UpperChest', 'RightUpperArm': 'RightShoulder', 'RightLowerArm': 'RightUpperArm', 'RightHand': 'RightLowerArm',
 'LeftUpperLeg': 'Hips', 'LeftLowerLeg': 'LeftUpperLeg', 'LeftFoot': 'LeftLowerLeg', 'LeftToes': 'LeftFoot',
 'RightUpperLeg': 'Hips', 'RightLowerLeg': 'RightUpperLeg', 'RightFoot': 'RightLowerLeg', 'RightToes': 'RightFoot'}
CONNECT = {'Spine', 'Chest', 'UpperChest', 'Neck', 'Head', 'LeftUpperArm', 'LeftLowerArm', 'LeftHand', 'RightUpperArm',
 'RightLowerArm', 'RightHand', 'LeftLowerLeg', 'LeftFoot', 'LeftToes', 'RightLowerLeg', 'RightFoot', 'RightToes'}
FINGERS = ["Thumb", "Index", "Middle", "Ring", "Little"]
PHALANX = ["Proximal", "Intermediate", "Distal"]
