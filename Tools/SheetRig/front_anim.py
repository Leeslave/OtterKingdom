"""Poses for the front-view rigs (front_rig): idle, walk and one signature action per
character. Units like anim.py: rig px (1x sheet px), degrees, CCW positive.

Signature actions (period ACTION_PERIOD[name], loopable):
  Pajama  nod off   head droops with closed eyes, jerks awake, pompom swings
  Miner   mining    pickaxe raised behind the shoulder, struck down, little hop
  Fisher  cast      rod drawn back and flicked forward, bobber swings out
  Chief   wave      right paw raised and waved
"""
import math

import anim

PI = math.pi
smooth = anim.smooth
keyed = anim.keyed


def blink(t, every=3.1, offset=0.0, dur=0.14):
    """True while the eyes are closed (a quick blink every `every` s, sometimes double)."""
    u = (t + offset) % every
    return u < dur or (int((t + offset) / every) % 3 == 1 and dur * 1.6 < u < dur * 2.6)


def merge(base, extra):
    """Add rotation / offset fields of `extra` onto `base` (scales multiply)."""
    out = {k: dict(v) for k, v in base.items()}
    for bone, fields in extra.items():
        d = out.setdefault(bone, {})
        for f, v in fields.items():
            if f in ('alt', 'layer'):
                d[f] = v
            elif f in ('sx', 'sy'):
                d[f] = d.get(f, 1.0) * v
            else:
                d[f] = d.get(f, 0.0) + v
    return out


# --- secondary motion -------------------------------------------------------------------------

def pendulum(drive, t, dt=1 / 120, damping=2.4, stiffness=55.0, gain=1.0):
    """Angle (deg) of a hanging part whose pivot rotates with drive(t) (deg): the part
    lags behind the drive and keeps swinging. Integrated from t-3s so it settles."""
    t0 = max(0.0, t - 3.0)
    n = int((t - t0) / dt)
    ang = vel = 0.0
    prev = drive(t0)
    pv = 0.0
    for i in range(1, n + 1):
        tt = t0 + i * dt
        d = drive(tt)
        dv = (d - prev) / dt
        acc = (dv - pv) / dt
        pv, prev = dv, d
        # the pivot's angular acceleration kicks the bob the other way
        a = -stiffness * ang - damping * vel - gain * acc * 0.02
        vel += a * dt
        ang += vel * dt
    return ang


# --- idle / walk -------------------------------------------------------------------------------

def idle(name, t):
    p = 2 * PI * t / anim.IDLE_PERIOD
    b = math.sin(p)
    pose = anim.idle('Front', t)
    if name == 'Pajama':   # sleepy: slower, deeper breath, head lolls a little
        pose = merge(pose, {'Head': dict(rot=2.5 * math.sin(p * 0.5)), 'Body': dict(sy=1 + 0.008 * b)})
        pose['Pompom'] = dict(rot=pendulum(lambda x: 2.5 * math.sin(2 * PI * x / anim.IDLE_PERIOD * 0.5), t) * 3)
    if name == 'Fisher':
        pose['Bobber'] = dict(rot=4 * math.sin(2 * PI * t / 1.7))
    if name == 'Chief':
        pose['Scarf'] = dict(rot=3 * math.sin(p + 1.0))
    if blink(t, offset={'Pajama': 0.3, 'Miner': 1.1, 'Fisher': 2.0, 'Chief': 0.7}[name],
             dur=0.3 if name == 'Pajama' else 0.14):
        pose = merge(pose, {'Head': dict(alt='blink')})
    return pose


def walk(name, t):
    pose = anim.walk('Front', t)
    arm = 2 * PI * t / anim.WALK_PERIOD
    if name == 'Miner':
        # the pickaxe is heavy: that arm swings less
        pose['Arm_L'] = dict(rot=5 * math.sin(arm))
    if name == 'Fisher':
        # the rod is held steady: it counter-rotates against the arm swing
        rod = -0.85 * pose['Arm_L']['rot']
        pose['Rod'] = dict(rot=rod)
        drive = lambda x: anim.walk('Front', x)['Arm_L']['rot'] * 0.15 + anim.walk('Front', x)['Body']['rot']
        pose['Bobber'] = dict(rot=pendulum(drive, t, gain=3.0) * 4)
    if name == 'Pajama':
        pose['Pompom'] = dict(rot=12 * math.sin(arm * 2 - 1.2))
    if name == 'Chief':
        pose['Scarf'] = dict(rot=6 * math.sin(arm * 2 - 0.8))
    if blink(t, offset=1.7):
        pose = merge(pose, {'Head': dict(alt='blink')})
    return pose


# --- signature actions ----------------------------------------------------------------------------

ACTION_PERIOD = {'Pajama': 4.0, 'Miner': 1.4, 'Fisher': 3.2, 'Chief': 1.8}
ACTION_KOR = {'Pajama': '꾸벅꾸벅 졸기', 'Miner': '곡괭이질', 'Fisher': '낚싯대 던지기', 'Chief': '손 흔들어 인사'}


def action(name, t):
    return {'Pajama': nod_off, 'Miner': mining, 'Fisher': cast, 'Chief': wave}[name](t)


def nod_off(t):
    v = (t % ACTION_PERIOD['Pajama']) / ACTION_PERIOD['Pajama']
    # 0..0.6 slowly droop (eyes shut at 0.15), 0.62 jerk awake, then settle
    droop = keyed(v, [(0, 0), (0.6, 1), (0.64, -0.25), (0.75, 0.08), (0.85, 0), (1, 0)])
    pose = idle('Pajama', t)
    head_rot = -9 * droop
    pose = merge(pose, {
        'Head': dict(rot=head_rot, dy=7 * max(droop, 0)),
        'Body': dict(rot=-1.5 * droop, dy=1.5 * max(droop, 0)),
        'Arm_L': dict(rot=3 * droop), 'Arm_R': dict(rot=-3 * droop),
    })
    period = ACTION_PERIOD['Pajama']
    drive = lambda x: -9 * keyed((x % period) / period,
                                 [(0, 0), (0.6, 1), (0.64, -0.25), (0.75, 0.08), (0.85, 0), (1, 0)])
    pose['Pompom'] = dict(rot=pendulum(drive, t, gain=2.5) * 2.5 + 0.6 * head_rot)
    closed = 0.12 < v < 0.62 or (0.66 < v < 0.72)
    pose['Head'].pop('alt', None)
    if closed:
        pose['Head']['alt'] = 'blink'
    return pose


def mining(t):
    period = ACTION_PERIOD['Miner']
    v = (t % period) / period
    # raise beside the head, hold, strike down past rest, recoil. The arm is drawn in
    # front of the head throughout so the raised pickaxe stays visible.
    arm = keyed(v, [(0, 0), (0.42, -48), (0.5, -52), (0.6, 22), (0.7, 14), (0.85, 5), (1, 0)])
    lean = keyed(v, [(0, 0), (0.45, 3), (0.6, -4), (0.75, -1), (1, 0)])
    hop = keyed(v, [(0, 0), (0.45, -4), (0.6, 2), (0.7, 0), (1, 0)])
    pose = idle('Miner', t)
    return merge(pose, {
        'Arm_L': dict(rot=arm, layer=35),
        'Arm_R': dict(rot=-0.08 * arm),
        'Body': dict(rot=lean, dy=hop, sy=1 - 0.02 * max(0, -hop + 1) * (0.55 < v < 0.75)),
        'Head': dict(rot=-0.5 * lean + 2 * math.sin(2 * PI * v)),
        'Leg_L': dict(dy=min(0, hop) * 0.5), 'Leg_R': dict(dy=min(0, hop) * 0.5),
    })


def cast(t):
    period = ACTION_PERIOD['Fisher']
    rod_keys = [(0, 0), (0.3, 24), (0.38, 26), (0.48, -22), (0.56, -14), (0.75, -16), (0.9, -4), (1, 0)]
    arm_keys = [(0, 0), (0.3, -10), (0.38, -12), (0.48, 14), (0.56, 8), (0.75, 9), (0.9, 2), (1, 0)]
    v = (t % period) / period
    rod, arm = keyed(v, rod_keys), keyed(v, arm_keys)
    lean = keyed(v, [(0, 0), (0.35, 3), (0.5, -3), (0.8, -1.5), (1, 0)])
    pose = idle('Fisher', t)
    drive = lambda x: keyed((x % period) / period, rod_keys) + keyed((x % period) / period, arm_keys)
    pose = merge(pose, {
        'Arm_L': dict(rot=arm), 'Rod': dict(rot=rod),
        'Body': dict(rot=lean), 'Head': dict(rot=-0.6 * lean),
    })
    pose['Bobber'] = dict(rot=pendulum(drive, t, damping=1.6, stiffness=40.0, gain=4.0) * 1.0)
    return pose


def wave(t):
    period = ACTION_PERIOD['Chief']
    v = (t % period) / period
    up = keyed(v, [(0, 0), (0.18, 1), (0.85, 1), (1, 0)])
    wag = math.sin(2 * PI * v * 3) * up
    pose = idle('Chief', t)
    return merge(pose, {
        'Arm_R': dict(rot=100 * up + 15 * wag),   # stays behind the head: the paw peeks out beside it
        'Body': dict(rot=-2.5 * up + 0.6 * wag, dy=-1 * up),
        'Head': dict(rot=3 * up - 0.8 * wag),
        'Scarf': dict(rot=4 * wag),
        'Arm_L': dict(rot=-3 * up),
    })
