# Hand-made (procedural) animations for the dark knight, keyed on the deform bones (FK):
#   Parado            idle with the sword in hand (breathing)                      loop
#   Parado_Sem_Espada idle, sword on the back                                       loop
#   Andar             walk, sword in hand (right arm swings little, left arm + shoulders free)   loop
#   Andar_Sem_Espada  walk, sword on the back, both arms swinging                    loop
#   Correr            run, sword on the back, arms pumping, shoulders/torso counter-rotating     loop
#   Correr_Com_Espada run holding the greatsword low at the side, blade trailing                 loop
#   Guardar_Espada    sword goes over the right shoulder into the back socket       one-shot
#   Sacar_Espada      the reverse                                                    one-shot
# All in place (no root motion). The sword location is a custom property "espada_nas_costas" on
# CTRL_Root (0 = hand, 1 = back), keyed in every action and driving two Child Of constraints on Sword.
# usage: blender -b <rig or anim blend> -P locomotion.py -- <scripts_dir> <out.blend>
import bpy, sys, math
from mathutils import Matrix, Vector
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0]); OUT = args[1]
from posekit import Kit, smooth, lerp_matrix
arm = bpy.data.objects["Armature"]; K = Kit(arm); P, B = K.P, K.B
sword = bpy.data.objects["Sword"]; root = P["CTRL_Root"]; sc = bpy.context.scene; sc.render.fps = 30
SIDES = (("Left", 1), ("Right", -1))
L_LEG = sum((B[S+n].tail_local-B[S+n].head_local).length for S in ("Left",) for n in ("UpperLeg", "LowerLeg"))
ANK = {S: B[S+"Foot"].head_local.copy() for S, _ in SIDES}

# ---------------------------------------------------------------- sword: hand <-> back switch for Blender
if "espada_nas_costas" not in root:
    root["espada_nas_costas"] = 0.0
    root.id_properties_ui("espada_nas_costas").update(min=0.0, max=1.0, description="0 = espada na mao, 1 = nas costas")
    M = sword.matrix_world.copy(); sword.parent = None; sword.matrix_world = Matrix.Identity(4)
    for bone, expr in (("RightHandWeapon", "1-v"), ("SwordBack", "v")):
        c = sword.constraints.new('CHILD_OF'); c.name = "em_"+bone; c.target = arm; c.subtarget = bone
        c.inverse_matrix = Matrix.Identity(4)
        fc = c.driver_add("influence"); dr = fc.driver; dr.type = 'SCRIPTED'; dr.expression = expr
        var = dr.variables.new(); var.name = "v"; var.type = 'SINGLE_PROP'; t = var.targets[0]
        t.id_type = 'OBJECT'; t.id = arm; t.data_path = 'pose.bones["CTRL_Root"]["espada_nas_costas"]'
    bpy.context.view_layer.update()

def new_action(name, n_frames, back):
    arm.animation_data_create()
    a = bpy.data.actions.get(name)
    if a: bpy.data.actions.remove(a)
    a = bpy.data.actions.new(name); a.use_fake_user = True; arm.animation_data.action = a
    for k in ("ik_perna_Left", "ik_perna_Right", "ik_braco_Left", "ik_braco_Right"):
        root[k] = 0.0; root.keyframe_insert(f'["{k}"]', frame=1)
    root["espada_nas_costas"] = 1.0 if back else 0.0; root.keyframe_insert('["espada_nas_costas"]', frame=1)
    sc.frame_start, sc.frame_end = 1, n_frames
    return a

def place_hips(feet, drop=0.0, **kw):
    """hips lowered by `drop`, and further if needed so every foot target stays reachable (97% leg)"""
    extra = 0.0
    for _ in range(3):
        K.reset(apose=True)
        K.hips(drop=drop+extra, **kw)
        need = 0.0
        for S, T in feet.items():
            H = P[S+"UpperLeg"].head; dh = Vector((T.x-H.x, T.y-H.y, 0)).length
            v = math.sqrt(max((0.97*L_LEG)**2-dh*dh, 0.01))
            need = max(need, (H.z-T.z)-v)
        if need <= 1e-4: break
        extra += need
    return drop+extra

def hold_low(swing=0.0, breathe=0.0):
    """greatsword in the right hand, held low at the side: blade forward-down, pommel clear of the hip"""
    H = P["Hips"].head
    grip = H+Vector((-0.36, -0.13-0.06*swing, -0.06+0.02*abs(swing)+0.01*breathe))
    blade = Vector((-0.12, -0.75-0.06*swing, -0.55))
    K.hand_to("Right", K.hand_for_socket(K.sword_frame(grip, blade, (0, 0, 1))), Vector((-0.5, 0.6, -0.3)))

def free_arm(S, sx, swing, elbow, out=10, fist=0.0):
    """free arm: swing (deg, + = forward), elbow bend (deg), abduction out (deg)"""
    a, e, o = math.radians(swing), math.radians(swing+elbow), math.radians(out)
    K.aim(S+"UpperArm", (sx*math.sin(o), -math.sin(a)*math.cos(o), -math.cos(a)*math.cos(o)))
    K.aim(S+"LowerArm", (sx*math.sin(o)*0.6, -math.sin(e), -math.cos(e)))
    fd = P[S+"LowerArm"].tail-P[S+"LowerArm"].head
    K.aim(S+"Hand", fd)
    if fist: K.fist(S, fist)

def gait(name, frames, stride, stance, lift, sword_back, run=False, trail=False):
    drops = {}
    _gait(name, frames, stride, stance, lift, sword_back, run, trail, drops, None)          # 1: reach-based heights
    n = frames; raw = [drops[f] for f in range(1, n+1)]
    kern = [1, 2, 3, 2, 1]; sm_d = []
    for i in range(n):
        sm_d.append(sum(w*raw[(i+j-2) % n] for j, w in enumerate(kern))/sum(kern))
    sm_d = {f: sm_d[(f-1) % n] for f in range(1, n+2)}
    _gait(name, frames, stride, stance, lift, sword_back, run, trail, None, sm_d)          # 2: smoothed heights
    print("   altura do quadril: variacao %.1f cm (antes %.1f cm)" % ((max(sm_d.values())-min(sm_d.values()))*100, (max(raw)-min(raw))*100))

def _gait(name, frames, stride, stance, lift, sword_back, run, trail, drops, fixed):
    """trail=True: runs holding the greatsword low in the right hand, blade trailing back"""
    new_action(name, frames+1, sword_back)
    for f in range(1, frames+2):
        p = ((f-1) % frames)/frames
        feet, pitch = {}, {}
        for S, sx in SIDES:
            u = (p+(0.0 if S == "Left" else 0.5)) % 1.0
            base = ANK[S].copy(); base.x *= 0.85 if not run else 0.75
            if u < stance:                           # on the ground: front -> back
                t = u/stance; y = -stride/2+stride*t; z = base.z
                if run: pt = -8*(1-smooth(t/0.25)) + 45*smooth((t-0.55)/0.45)
                else: pt = -18*(1-smooth(t/0.18)) + 32*smooth((t-0.7)/0.3)
            else:                                    # swing: lifts and comes forward
                s = (u-stance)/(1-stance)
                if run:                              # knee drives forward: foot highest when already ahead
                    y = stride/2-stride*smooth(s); z = base.z+lift*math.sin(math.pi*s**1.3)**0.85
                    pt = 40-32*smooth(s/0.45)-12*smooth((s-0.6)/0.4)
                else:
                    y = stride/2-stride*smooth(s); z = base.z+lift*math.sin(math.pi*s)
                    pt = 32*(1-smooth(s/0.4))-18*smooth((s-0.55)/0.45)
            feet[S] = Vector((base.x, base.y+y, z)); pitch[S] = pt
        c = math.cos(2*math.pi*p); s2 = math.sin(2*math.pi*p)
        if run:
            ground = {S: T for S, T in feet.items() if T.z < ANK[S].z+0.02}
            comp = max([0.0]+[math.sin(math.pi*min(1, ((p+(0 if S == "Left" else 0.5)) % 1)/stance))
                              for S in ground])                               # landing compression
            kw = dict(yaw=-9*c, side=0.012*s2, fwd=0.05, lean=8); d0 = 0.02+0.02*comp
        else:
            kw = dict(yaw=-6*c, side=0.02*s2); d0 = 0.01
        if fixed is None:
            d = place_hips(feet, drop=d0, **kw)
            if drops is not None: drops[f] = d
        else:
            K.reset(apose=True); K.hips(drop=fixed[f], **kw)
        for S, sx in SIDES:
            K.foot(S[0], feet[S], pitch=pitch[S], yaw=5*sx)
        # torso: lean, chest counter-rotation, shoulders, head stays on target
        if run:
            K.rot_world("Spine", 'X', 9); K.rot_world("Chest", 'X', 5); K.rot_world("Chest", 'Z', 14*c); K.rot_world("UpperChest", 'Z', 4*c)
        else:
            K.rot_world("Spine", 'X', 2); K.rot_world("Chest", 'Z', 9*c)
        K.rot_world("Spine", 'Y', -2*s2)
        for S, sx in SIDES:
            fwd_amt = (-c if S == "Left" else c)       # +1: this arm forward (opposite leg forward)
            K.rot_world(S+"Shoulder", 'Z', sx*(6 if run else 4)*fwd_amt)          # shoulder blade forward/back
            K.rot_world(S+"Shoulder", 'Y', -sx*(3 if run else 1.5)*abs(fwd_amt))
            if S == "Right" and trail:                 # greatsword held low at the side, blade trailing back
                H = P["Hips"].head
                grip = H+Vector((-0.31, -0.02-0.10*fwd_amt, 0.02+0.05*fwd_amt))   # close to the body: elbow bent
                blade = Vector((-0.22, 0.80+0.05*fwd_amt, -0.48))
                K.hand_to("Right", K.hand_for_socket(K.sword_frame(grip, blade, (0, 0, 1))), Vector((-0.5, 0.6, -0.2)))
            elif S == "Right" and not sword_back:      # holds the greatsword low at the side, small swing
                hold_low(swing=fwd_amt)
            elif run:
                free_arm(S, sx, 40*fwd_amt+10, 92+18*max(0, fwd_amt), out=14, fist=0.7 if S == "Left" else 0)
                if S == "Right": relax_right(0.25)                      # loose running fist
            else:
                free_arm(S, sx, 20*fwd_amt+2, 12+14*max(0, fwd_amt), out=9, fist=0.25 if S == "Left" else 0)
                if S == "Right": relax_right(0.85)                      # relaxed open hand
        K.rot_world("Head", 'Z', -(14 if run else 9)*c*0.6); K.rot_world("Neck", 'X', -14 if run else -1)
        K.key_all(f)
    print("ACTION", name, frames+1, "frames")

FOOT_L = lambda: Vector((ANK["Left"].x+0.01, ANK["Left"].y-0.03, ANK["Left"].z))    # relaxed stance: feet almost
FOOT_R = lambda: Vector((ANK["Right"].x-0.01, ANK["Right"].y+0.04, ANK["Right"].z))  # straight (the greaves are rigid:
                                                                                     # turned-out feet poke through the robe)
def relax_right(amount):
    """right hand modelled closed on the grip: open it to a relaxed hand (amount 0..1)"""
    for k, ph in enumerate(("Proximal", "Intermediate", "Distal")):
        for fn in ("Index", "Middle", "Ring", "Little"): K.rot_local(f"Right{fn}{ph}", 'X', -(42, 48, 28)[k]*amount)
        K.rot_local(f"RightThumb{ph}", 'X', -(12, 22, 20)[k]*amount)

def idle(name, frames, sword_back):
    new_action(name, frames+1, sword_back)
    for f in range(1, frames+2):
        p = ((f-1) % frames)/frames; b = math.sin(2*math.pi*p)
        place_hips({S: ANK[S] for S, _ in SIDES}, drop=0.02+0.006*b)
        K.foot('L', FOOT_L(), yaw=4); K.foot('R', FOOT_R(), yaw=-4)
        K.rot_world("Chest", 'X', -1.2*b); K.rot_world("UpperChest", 'X', -0.8*b)
        K.rot_world("Head", 'Z', -5+2*math.sin(2*math.pi*p*2))
        if sword_back:
            hang_arm("Right", -1); K.rot_world("RightUpperArm", 'X', 1.5*b); relax_right(0.85)
        else:
            hold_low(breathe=b)
        K.rot_world("LeftUpperArm", 'X', -1.5*b); K.fist("Left", 0.2)
        K.key_all(f)
    print("ACTION", name, frames+1, "frames")

def hang_arm(S, sx):
    K.aim(S+"UpperArm", (sx*0.17, 0.03, -1)); K.aim(S+"LowerArm", (sx*0.08, -0.17, -1))
    K.aim(S+"Hand", P[S+"LowerArm"].tail-P[S+"LowerArm"].head)

def cr(points, s):
    """Catmull-Rom through equally spaced points, s in [0, len-1] (smooth, no stops at the keys)"""
    n = len(points); i = min(int(s), n-2); t = s-i
    p0, p1, p2, p3 = points[max(i-1, 0)], points[i], points[i+1], points[min(i+2, n-1)]
    return 0.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)

def ease(u, lin=0.3):
    u = min(max(u, 0.0), 1.0); return (1-lin)*u*u*(3-2*u)+lin*u

def sheath(name, reverse=False):
    """Guardar_Espada (46 frames): the sword comes up in front of the chest, swings back over the right
    shoulder and goes into SwordBack (frame 32: the sword changes socket); the hand lets go, opens and
    drops beside the body. One continuous curve (Catmull-Rom) for the sword path, the elbow and the body
    (chest twist, right shoulder lift, head, knees), so nothing stops at the key poses.
    Sacar_Espada = the same motion reversed."""
    N, REL = 46, 32
    poses = []
    arm.animation_data.action = None
    def body(f):
        reach = ease((f-3)/20, 0)*(1-ease((f-REL)/12, 0))               # 0 -> 1 -> 0
        lift = ease((f-12)/12, 0)*(1-ease((f-REL+2)/10, 0))             # hand above the shoulder
        place_hips({S: ANK[S] for S, _ in SIDES}, drop=0.02+0.025*reach, side=0.015*reach)
        K.foot('L', FOOT_L(), yaw=4); K.foot('R', FOOT_R(), yaw=-4)
        K.rot_world("Spine", 'Z', -6*reach); K.rot_world("Chest", 'Z', -10*reach); K.rot_world("UpperChest", 'Z', -5*reach)
        K.rot_world("Chest", 'Y', 4*lift)                                # lean toward the left, opens the right side
        K.rot_world("RightShoulder", 'Y', 14*lift); K.rot_world("RightShoulder", 'Z', 6*reach)
        K.rot_world("Head", 'Z', -5+12*reach); K.rot_world("Head", 'X', 5*lift)
        K.rot_world("LeftUpperArm", 'X', -8*reach); K.rot_world("LeftLowerArm", 'X', -10*reach); K.fist("Left", 0.2+0.2*reach)
    # key poses of the sword (grip position, blade direction, blade width) and of the elbow pole
    body(1); hold_low(); M0 = P["RightHandWeapon"].matrix.copy()
    keys = [(M0.translation, M0.to_3x3().col[1], M0.to_3x3().col[2], Vector((-0.5, 0.6, -0.3))),
            (Vector((-0.30, -0.30, 1.12)), Vector((-0.05, -0.45, 1)), Vector((1, 0, 0.1)), Vector((-0.7, 0.3, -0.5))),
            (Vector((-0.36, -0.14, 1.66)), Vector((0.0, 0.75, 0.66)), Vector((1, 0, 0)), Vector((-0.8, 0.0, -0.3))),
            (Vector((-0.24, 0.04, 1.86)), Vector((0.2, 0.65, -0.73)), Vector((1, 0, 0)), Vector((-0.5, -0.8, 0.3))),
            None]                                                       # last key = SwordBack, read per frame
    for f in range(1, N+1):
        body(f)
        M3 = P["SwordBack"].matrix.copy()
        ks = keys[:-1]+[(M3.translation, M3.to_3x3().col[1], M3.to_3x3().col[2], Vector((-0.5, -0.8, 0.3)))]
        if f <= REL:
            sp = ease((f-1)/(REL-1))*(len(ks)-1)
            pos = cr([k[0] for k in ks], sp); y = cr([k[1].normalized() for k in ks], sp); z = cr([k[2].normalized() for k in ks], sp)
            pole = cr([k[3] for k in ks], sp)
            miss = K.hand_to("Right", K.hand_for_socket(K.sword_frame(pos, y, z)), pole)
            if miss > 0.01: print(f"  quadro {f}: mao a {miss*100:.1f} cm do alvo")
        else:
            # let go: the hand leaves the grip, opens and drops beside the body with a soft follow-through
            Hb = K.hand_for_socket(M3)
            hang_arm("Right", -1); Hd = P["RightHand"].matrix.copy()
            u = (f-REL)/(N-REL); e = 1-(1-u)**3
            mid = Hb.translation.lerp(Hd.translation, 0.5)+Vector((-0.12, 0.05, 0.08))     # arc out to the side
            pos = cr([Hb.translation, mid, Hd.translation], e*2)
            R = Hb.to_quaternion().slerp(Hd.to_quaternion(), ease(u, 0.2)).to_matrix().to_4x4(); R.translation = pos
            pole = Vector((-0.5, -0.8, 0.3)).lerp(Vector((-0.4, 0.6, -0.5)), ease(u, 0))
            K.hand_to("Right", R, pole)
            relax_right(0.85*ease((f-REL)/8, 0))
        poses.append({pb.name: pb.matrix_basis.copy() for pb in P})
    new_action(name, N, back=reverse)
    for i, snap in enumerate(reversed(poses) if reverse else poses):
        for n, m in snap.items(): P[n].matrix_basis = m
        f = i+1; K.key_all(f)
        on_back = (f >= REL) if not reverse else (f <= N-REL+1)
        root["espada_nas_costas"] = 1.0 if on_back else 0.0
        root.keyframe_insert('["espada_nas_costas"]', frame=f)
    print("ACTION", name, N, "frames, troca de encaixe no quadro", REL if not reverse else N-REL+1)

idle("Parado", 60, sword_back=False)
idle("Parado_Sem_Espada", 60, sword_back=True)
SPEED = {}
def speed(name, frames, stride, stance): SPEED[name] = stride/(stance*frames/30.0)   # body speed for no foot sliding
for args in (("Andar", 32, 0.48, 0.62, 0.10, False), ("Andar_Sem_Espada", 32, 0.48, 0.62, 0.10, True)):
    gait(*args[:2], stride=args[2], stance=args[3], lift=args[4], sword_back=args[5]); speed(*args[:4])
gait("Correr", 20, stride=0.85, stance=0.40, lift=0.24, sword_back=True, run=True); speed("Correr", 20, 0.85, 0.40)
gait("Correr_Com_Espada", 20, stride=0.80, stance=0.40, lift=0.22, sword_back=False, run=True, trail=True)
speed("Correr_Com_Espada", 20, 0.80, 0.40)
print("SPEED", {k: round(v, 2) for k, v in SPEED.items()})
sheath("Guardar_Espada")
sheath("Sacar_Espada", reverse=True)
arm.animation_data.action = bpy.data.actions["Parado"]
bpy.ops.wm.save_as_mainfile(filepath=OUT)
