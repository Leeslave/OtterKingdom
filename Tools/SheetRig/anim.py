"""Procedural Idle / Walk poses for the otter rig (1x px, degrees, CCW positive)."""
import math

WALK_PERIOD = 0.64
IDLE_PERIOD = 2.4
PI = math.pi

# --- side view: stubby legs, feet that roll heel -> toe (New Super Mario Bros style) ---
# Geometry (1x px) must match rig.SIDE_*: trouser = hip -> ankle, sole contact points
# relative to the ankle (x forward, y down).
SIDE_TROUSER = 21.0
SIDE_HEEL = (-13.0, 19.0)
SIDE_TOE = (14.0, 19.0)
SIDE_PERIOD = 0.56      # one full cycle = two steps
SIDE_STEP = 16.0        # the planted foot travels from +STEP to -STEP under the hip
SIDE_LIFT = 9.0         # swing foot height
SIDE_KICK = 5.0         # extra backward flick of the swing foot right after toe-off
HEEL_STRIKE = 12.0      # toe-up angle when the foot lands (deg)
TOE_OFF = -26.0         # heel-up angle when the foot leaves the ground (deg)
SIDE_BOB = 2.2          # hip dip just after each landing (1x px)
SIDE_ARM = 32.0
# Ground speed at which the planted foot doesn't slide (1x px / s).
SIDE_SPEED = 4 * SIDE_STEP / SIDE_PERIOD

def lift(x):
    return max(0.0, x)


def idle(view, t):
    p = 2 * PI * t / IDLE_PERIOD
    b = math.sin(p)
    pose = {
        'Body': dict(sy=1 + 0.018 * b, sx=1 - 0.008 * b),
        'Head': dict(rot=1.6 * math.sin(p + PI / 2), dy=-0.4 * b),
    }
    if view == 'Side':
        pose.update(side_legs(((3.0, 0.0), 0.0), ((-3.0, 0.0), 0.0), 0.4 + 0.4 * b)[0])
        pose['Arm_Near'] = dict(rot=3 * math.sin(p + 0.6))
        pose['Arm_Far'] = dict(rot=3 * math.sin(p + 0.9))
        pose['Tail'] = dict(rot=4 * math.sin(p))
        pose['Tail_2'] = dict(rot=6 * math.sin(p - 0.7))
        pose['Tail_3'] = dict(rot=8 * math.sin(p - 1.4))
    else:
        # arms drift out a little as the chest rises
        pose['Arm_L'] = dict(rot=-2.5 * b)
        pose['Arm_R'] = dict(rot=2.5 * b)
        if view == 'Back':
            pose['Tail'] = dict(rot=5 * math.sin(p * 0.5))
    return pose


def walk(view, t):
    p = 2 * PI * t / WALK_PERIOD
    s, c = math.sin(p), math.cos(p)
    if view == 'Side':
        return side_walk(t / SIDE_PERIOD)
    # front / back: rotation barely reads, so feet step up and the hips sway onto the planted foot
    mirror = 1 if view == 'Front' else -1
    pose = {
        'Leg_L': dict(dy=-7 * lift(s), sy=1 - 0.04 * lift(s)),
        'Leg_R': dict(dy=-7 * lift(-s), sy=1 - 0.04 * lift(-s)),
        'Body': dict(dy=-2.6 * abs(s) + 0.8, rot=3.0 * s * mirror, dx=1.5 * s * mirror),
        'Head': dict(rot=-1.8 * s * mirror, dy=0.8 * math.sin(2 * p + 1.2)),
        'Arm_L': dict(rot=12 * s * mirror, dy=-0.8 * lift(-s)),
        'Arm_R': dict(rot=12 * s * mirror, dy=-0.8 * lift(s)),
    }
    if view == 'Back':
        pose['Tail'] = dict(rot=16 * math.sin(p - 0.7))
    return pose


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def keyed(v, keys):
    """Smoothstep through (v, value) keys."""
    for (v0, a), (v1, b) in zip(keys, keys[1:]):
        if v <= v1:
            return a + (b - a) * smooth((v - v0) / (v1 - v0))
    return keys[-1][1]


def rot(p, deg):
    """Rotate (x, y-down) by deg, positive = counter-clockwise on screen (rig.R)."""
    r = math.radians(deg)
    c, s = math.cos(r), math.sin(r)
    return c * p[0] + s * p[1], -s * p[0] + c * p[1]


def rolled(contact, phi):
    """Ankle offset when the foot is rotated by phi around a sole point resting on the ground."""
    rx, ry = rot(contact, phi)
    return contact[0] - rx, contact[1] - ry


def side_foot(u):
    """Ankle offset from its standing position (x forward, y down) and foot angle
    (deg, + = toe up) at cycle phase u. 0..0.5 stance, 0.5..1 swing."""
    u %= 1.0
    if u < 0.5:
        x = SIDE_STEP * (1 - 4 * u)            # planted: moves back at ground speed
        if u < 0.12:                             # heel strike -> foot slaps flat
            phi = HEEL_STRIKE * (1 - smooth(u / 0.12))
            dx, dy = rolled(SIDE_HEEL, phi)
        elif u > 0.3:                            # heel comes up, pushing off the toe
            phi = TOE_OFF * smooth((u - 0.3) / 0.2)
            dx, dy = rolled(SIDE_TOE, phi)
        else:
            phi, dx, dy = 0.0, 0.0, 0.0
        return (x + dx, dy), phi
    v = (u - 0.5) * 2
    (x0, y0), _ = side_foot(0.4999)
    x1 = SIDE_STEP + rolled(SIDE_HEEL, HEEL_STRIKE)[0]
    y1 = rolled(SIDE_HEEL, HEEL_STRIKE)[1]
    x = x0 + (x1 - x0) * smooth(v) - SIDE_KICK * math.sin(PI * min(1, v * 1.6)) * (1 - v)
    # lifts quickly behind the body, then reaches forward and drops onto the heel
    y = y0 + (y1 - y0) * v - SIDE_LIFT * math.sin(PI * v ** 0.75)
    phi = keyed(v, [(0, TOE_OFF), (0.3, TOE_OFF - 16), (0.8, HEEL_STRIKE + 4), (1, HEEL_STRIKE)])
    return (x, y), phi


def side_legs(near, far, drop=0.0):
    """Pose for Hip / legs / feet. near, far = (ankle offset, foot angle); drop = wanted
    hip dip. The hip goes lower if an ankle would be out of reach of a straight trouser
    leg; legs that don't need the full length tuck up into the body (reads as a knee bend)."""
    L = SIDE_TROUSER
    need = max(L + ay - math.sqrt(max(L * L - ax * ax, 1.0)) for (ax, ay), _ in (near, far))
    drop = max(drop, need)
    pose = {'Hip': dict(dy=drop)}
    for side, ((ax, ay), phi) in (('Near', near), ('Far', far)):
        vx, vy = ax, L + ay - drop
        d = math.hypot(vx, vy)
        theta = math.degrees(math.atan2(vx, vy))
        pose[f'Leg_{side}'] = dict(rot=theta, dx=(d - L) * vx / d, dy=(d - L) * vy / d)
        pose[f'Foot_{side}'] = dict(rot=phi - theta)
    return pose, drop


def side_bob(u):
    """Wanted hip dip: lowest just after each landing, highest as the legs pass."""
    return SIDE_BOB * (0.5 + 0.5 * math.cos(4 * PI * (u - 0.07)))


def side_walk(u):
    near, far = side_foot(u), side_foot(u + 0.5)
    pose, drop = side_legs(near, far, side_bob(u))
    squash = side_bob(u) / SIDE_BOB
    # follow-through: the head and tail answer the bob a little later
    bob = lambda d: side_legs(side_foot(u - d), side_foot(u - d + 0.5), side_bob(u - d))[1]
    arm = math.cos(2 * PI * (u - 0.05))
    pose.update({
        # each arm swings against the leg on its side, the forward arm rising a bit more
        'Arm_Near': dict(rot=-SIDE_ARM * arm + 4),
        'Arm_Far': dict(rot=SIDE_ARM * arm + 4),
        'Body': dict(rot=-4 + 0.8 * math.sin(4 * PI * (u - 0.07)), sy=1.01 - 0.025 * squash, sx=1 + 0.012 * squash),
        'Head': dict(rot=-1.0 * bob(0.08) + 2.0, dy=0.5 * bob(0.08)),
        'Tail': dict(rot=-2.0 * bob(0.06) + 3),
        'Tail_2': dict(rot=-3.0 * bob(0.12)),
        'Tail_3': dict(rot=-4.0 * bob(0.18)),
    })
    return pose


def blend(a, b, w):
    """Linear blend of two poses (w=0 -> a)."""
    out = {}
    for k in set(a) | set(b):
        pa, pb = a.get(k, {}), b.get(k, {})
        d = {}
        for f in set(pa) | set(pb):
            if f in ('alt', 'layer'):   # sprite swaps / draw order don't blend: take the nearer pose's
                d[f] = (pb if w >= 0.5 else pa).get(f)
                continue
            base = 1.0 if f in ('sx', 'sy') else 0.0
            d[f] = pa.get(f, base) * (1 - w) + pb.get(f, base) * w
        out[k] = d
    return out
