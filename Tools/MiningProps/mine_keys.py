"""Mining keys for the 광부 (QuarterRigView.MineKeys, rig-editor MINE_KEYS) from the 작업반장's sledge keys (QuarterRigView.SledgeKeys).

The 작업반장's near arm turns about its shoulder by `near`; the miner's arm sprite turns about its paw
(the pickaxe is drawn in it). So for each sledge key: the miner's paw is put where the 작업반장's paw would
be (nearX/nearY = shoulder + arm turned by `near` - the miner's paw joint), and the arm sprite turned so
the pickaxe points the way the sledge would (`near` - TOOL_ANGLE). The far paw holds the handle (grip).
Three blows a rock: the sledge's heave-and-blow repeated, then straighten up and a new rock.
"""
import json, math, pathlib, sys

SHOULDER = (-60.0, -53.0)   # a near shoulder like the other rigs' (body space, rig px, y down)
PAW = (-90.0, -42.0)        # the miner's near arm joint: its paw
ARM = 60.0                  # shoulder to paw
TOOL_ANGLE = 2.87           # the pickaxe's paw-to-head direction at rest, as an arm turn from hanging down

# The 작업반장's sledge keys: t, hips, body, head, near, far, tail, hit (QuarterRigView.SledgeKeys).
SLEDGE = [
    (.50, 10, -.06, -.10, -1.2, -.9, .06, 0),
    (1.10, 4, -.14, -.18, -3.7, -3.4, -.08, 0),
    (1.30, 2, -.16, -.20, -3.9, -3.6, -.10, 0),
    (1.46, 14, .16, .12, -1.45, -1.2, .16, 0),
    (1.50, 20, .22, .18, -1.20, -1.0, .22, 1),
    (1.62, 26, .18, .12, -1.15, -.95, .16, .6),
    (1.95, 22, .14, .10, -1.25, -1.05, .14, 0),
]
CYCLE = 1.90   # one heave and blow (.50 .. 2.40)


def miner(near):
    """(miner near, nearX, nearY) for a sledge near-arm angle."""
    c, s = math.cos(near), math.sin(near)
    # The arm hangs along +y; turned clockwise (y down) by `near`.
    px, py = SHOULDER[0] - s * ARM, SHOULDER[1] + c * ARM
    return near + TOOL_ANGLE, px - PAW[0], py - PAW[1]


def keys():
    out = [(0, 0, 0, 0, 0, 0, 0, 0, .06, 0, 0, "")]   # t hips body head near nearX nearY grip tail stone hit
    for blow in range(3):
        for t, hips, body, head, near, far, tail, hit in SLEDGE:
            n, nx, ny = miner(near)
            stone = blow + (1 if t >= 1.50 else 0)
            out.append((round(t + blow * CYCLE, 2), hips, body, head, round(n, 3), round(nx, 1), round(ny, 1), 1, tail, stone,
                        hit, "BANG" if hit == 1 else ""))
    end = 1.95 + 2 * CYCLE
    out.append((round(end + .55, 2), 0, -.04, -.10, 0, 0, 0, 0, .06, 3, 0, "straightens up, looks at the rubble"))
    n, nx, ny = miner(-1.2)
    out.append((round(end + .95, 2), 10, -.06, -.10, round(n, 3), round(nx, 1), round(ny, 1), 1, .06, 0, 0, "a new rock; = the .50 key"))
    return out


if __name__ == "__main__":
    for k in keys():
        print(k)
