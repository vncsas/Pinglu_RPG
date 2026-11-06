# Keyframed fight animation for Auric Nemesis: named key poses (guard, jab, cross, hook, kick, smash, block...) blended
# with eased curves (anticipation -> fast strike with overshoot -> follow-through -> settle back into the stance),
# so the strikes flow instead of looking mechanical. Everything is FK on the deform bones (posekit.Kit).
# Used by auric_anim.py:   from fightkit import Fight
import math
from mathutils import Matrix, Quaternion, Vector
from posekit import smooth

def ease_out(t):  return 1-(1-t)**3                        # fast start, soft stop (strike)
def ease_in(t):   return t**3                               # slow start (anticipation)
def ease_io(t):   return smooth(t)
def back_out(t, s=1.5):                                     # overshoots a little and settles (follow-through)
    t -= 1; return 1+(s+1)*t**3+s*t**2

def qslerp(qa, qb, t):
    """slerp that also accepts t outside 0..1 (overshoot): qa * (qa^-1 qb)^t"""
    d = qa.inverted() @ qb
    if d.w < 0: d = Quaternion((-d.w, -d.x, -d.y, -d.z))
    ax, ang = d.to_axis_angle()
    return qa @ Quaternion(ax, ang*t) if ang > 1e-7 else qa.copy()

class Fight:
    def __init__(self, K, arm, sc, set_action, new_action, floor_lift, idle_pose):
        self.K, self.arm, self.P, self.sc = K, arm, arm.pose.bones, sc
        self.set_action, self.new_action, self.floor_lift, self.idle_pose = set_action, new_action, floor_lift, idle_pose
        self.snaps = {}

    # ------------------------------------------------------------------ pose capture / blend
    def capture(self, name, fn, **kw):
        """pose = idle base + fn(K) ; stored as {bone: (rotation quaternion, location)}"""
        self.idle_pose()
        fn(**kw); self.K.upd()
        self.snaps[name] = {pb.name: (pb.matrix_basis.to_quaternion(), pb.matrix_basis.translation.copy()) for pb in self.P}
    GROUPS = {"arms": ("Shoulder", "UpperArm", "LowerArm", "Hand"), "head": ("Neck", "Head")}
    def group_of(self, n):
        for g, keys in self.GROUPS.items():
            if any(k in n for k in keys): return g
        return "body"
    def apply(self, a, b, t, names=None):
        A, B = self.snaps[a], self.snaps[b]
        for n, pb in ((pb.name, pb) for pb in self.P):
            if names is not None and n not in names: continue
            qa, la = A[n]; qb, lb = B[n]
            pb.matrix_basis = Matrix.LocRotScale(la.lerp(lb, t), qslerp(qa, qb, t), None)
        self.K.upd()
    def clip(self, name, frames, keys, loop=False, lag=None):
        """keys: [(frame, pose_name, easing)] ; each segment blends from the previous pose with 'easing' (fn of 0..1).
        lag: {"arms": frames, "head": frames} = overlapping action: the arms / head follow the body a few frames late
        (the torso and hips lead, the arms whip after them), which is what makes strikes flow instead of looking mechanical"""
        lag = lag or {}
        self.new_action(name, frames)
        keys = sorted(keys, key=lambda k: k[0])
        groups = {g: [pb.name for pb in self.P if self.group_of(pb.name) == g] for g in ("body", "arms", "head")}
        def pose_at(ff):
            ff = max(ff, 1.0)
            seg = max(i for i, k in enumerate(keys) if k[0] <= ff) if ff >= keys[0][0] else 0
            if seg >= len(keys)-1: return keys[-1][1], keys[-1][1], 0.0
            f0, pa, _ = keys[seg]; f1, pb_, ez = keys[seg+1]
            t = 0.0 if f1 == f0 else (ff-f0)/(f1-f0)
            return pa, pb_, ez(min(max(t, 0.0), 1.0))
        for f in range(1, frames+1):
            self.sc.frame_set(f)
            for g, names in groups.items():
                a, b, t = pose_at(f-lag.get(g, 0))
                self.apply(a, b, t, set(names))
            self.floor_lift(); self.K.key_all(f)
        print("ACTION", name, frames, "quadros")
