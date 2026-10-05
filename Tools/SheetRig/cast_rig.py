"""Mass-produced otters built from the four front rigs (front_rig: pajama / miner / fisher
/ chief). Every otter has its own role; it is a recipe:

  head        which drawing the head (hat, face, eyes) comes from
  body        which drawing the outfit (body, arms, legs + its prop) comes from
  prop        keep the outfit's prop (pickaxe / rod); False swaps in the mirrored free arm
  eyes        whose eyes + brows (default: the head's own). Eyes are always the drawings'
              own: another drawing's eyes are moved over as they are, never redrawn
  fur         fur colour (head fur, paws, feet); the pajama head keeps its cream fur
              (cream fur and white muzzle are too close to separate)
  hat         hat colour (pajama cap / hard hat / bucket hat)
  colors      outfit material colours (BASE[body]['materials'] keys)
  hair_back   long / bob / pigtails / ponytail / braids / mane   (drawn behind the head)
  hair_front  bangs / side_swoop / puff / bun / twin_buns / spiky / mohawk / pompadour /
              cowlick (hatless chief head only, kept above the brows)
  hair, tie   hair colour, hair tie colour
  items       head items (hair.item), body_items (hair.body_item), icol: their colours
  action      nod / mine / cast / wave / hop / dance

All parts are moved onto one common canvas (feet centre at ROOT_AT). A transplanted head
is scaled so its chin width matches the outfit's original head (the body's top edge was
cut along that chin) and its chin is put on the outfit's neck joint.
"""
import math
import numpy as np
from PIL import Image
from scipy import ndimage

import anim
import cut_parts as cp
import front_anim as fa
import front_rig as fr
import hair
import rig

CW, CH = 760, 600            # common canvas (working px)
PAW_TONE, FOOT_TONE = 0.86, 0.8   # paws / feet relative to the head fur colour
ROOT_AT = (380.0, 560.0)     # feet centre on it
INK_RGB = np.array((48, 8, 3), np.float32)

# --- the four source drawings --------------------------------------------------------------------

BASE = {
    'Pajama': dict(
        fur=None, fur_ref=(251, 234, 212),
        paws=[(384, 905), (864, 905)], feet=[(531, 1112), (717, 1112)],
        hat=dict(hue=(245, 330), s=(0.07, 1), v=(0.3, 1)),
        materials={'main': dict(hue=(245, 330), s=(0.07, 1), v=(0.3, 1),
                                parts=('Body', 'Arm_L', 'Arm_R', 'Leg_L', 'Leg_R'))},
        eyes=[(494, 552), (742, 552)], cheeks=[(430, 600), (817, 600)], nose=(620, 575),
        ears=[(335, 345), (905, 362)],
    ),
    'Miner': dict(
        fur=dict(fur=[(139, 80, 63), (104, 53, 42), (120, 68, 54)],
                 other=[(253, 242, 226), (239, 163, 138), (240, 220, 200)],
                 exclude=[fr.ellipse(622, 245, 105, 95)]),    # head lamp (brown ring = fur colour)
        fur_ref=(139, 80, 63),
        paws=[(332, 875), (883, 872)], feet=[(494, 1106), (748, 1105)],
        hat=dict(hue=(30, 62), s=(0.35, 1), v=(0.55, 1)),
        materials={'denim': dict(hue=(190, 245), s=(0.15, 1), v=(0.2, 1), parts=('Body', 'Leg_L', 'Leg_R'))},
        eyes=[(483, 516), (755, 516)], cheeks=[(400, 585), (838, 585)], nose=(620, 548),
        ears=[(250, 235), (985, 235)],
        prop='Arm_L',
    ),
    'Fisher': dict(
        fur=dict(fur=[(191, 168, 157), (162, 138, 128), (175, 152, 142)],
                 other=[(253, 247, 242), (253, 185, 180), (240, 235, 230), (93, 59, 51)], exclude=[]),
        fur_ref=(191, 168, 157),
        paws=[(317, 848), (900, 922)], feet=[(534, 1138), (737, 1138)],
        hat=dict(hue=(50, 120), s=(0.1, 1), v=(0.25, 1)),
        materials={'vest': dict(hue=(18, 48), s=(0.22, 0.75), v=(0.45, 1), parts=('Body',)),
                   'jeans': dict(hue=(195, 240), s=(0.1, 1), v=(0.2, 1), parts=('Body', 'Leg_L', 'Leg_R'))},
        eyes=[(499, 605), (758, 605)], cheeks=[(445, 640), (812, 640)], nose=(632, 612),
        ears=[(340, 322), (920, 322)],
        prop='Arm_L', prop_children=('Rod', 'Bobber'),
    ),
    'Chief': dict(
        fur=dict(fur=[(215, 174, 140), (200, 160, 128)],
                 other=[(252, 249, 243), (252, 188, 178), (251, 242, 235)], exclude=[]),
        fur_ref=(215, 174, 140),
        paws=[(381, 907), (857, 919)], feet=[(526, 1132), (712, 1132)],
        hat=None,
        materials={'vest': dict(hue=(150, 215), s=(0.06, 1), v=(0.25, 0.85), parts=('Body',)),
                   'pants': dict(hue=(0, 35), s=(0.2, 1), v=(0.15, 0.6), parts=('Body', 'Leg_L', 'Leg_R'),
                                 y=(930, 2000)),
                   'scarf': dict(hue=(350, 30), s=(0.4, 1), v=(0.55, 1), parts=('Scarf', 'Body'), y=(0, 800))},
        eyes=[(490, 554), (758, 554)], cheeks=[(405, 590), (843, 590)], nose=(622, 565),
        ears=[(330, 322), (890, 322)],
    ),
}

FUR = {
    'cream': (251, 234, 212), 'sand': (230, 202, 160), 'honey': (232, 196, 132), 'tan': (215, 174, 140),
    'caramel': (204, 142, 92), 'ginger': (226, 152, 100), 'rust': (182, 98, 66), 'chestnut': (150, 92, 62),
    'brown': (139, 80, 63), 'dark': (92, 62, 52), 'charcoal': (104, 96, 100), 'gray': (191, 168, 157),
    'silver': (182, 184, 194), 'slate': (128, 140, 162), 'snow': (226, 226, 232), 'rose': (228, 182, 176),
    'lilac': (188, 174, 206),
}

# --- the cast: one role each ---------------------------------------------------------------------

CAST = [
    dict(prefix='StargazerOtter', name='별지기', role='밤하늘 별을 세는 천문가',
         head='Pajama', body='Pajama', hat=(72, 82, 142), colors={'main': (100, 100, 170)},
         hair_back='long', hair=(198, 208, 242), items=['star'], action='wave'),
    dict(prefix='StorytellerOtter', name='동화 작가', role='마을 아이들의 동화 작가',
         head='Pajama', body='Pajama', hat=(150, 182, 140), colors={'main': (166, 198, 152)},
         hair_back='braids', hair=(142, 86, 56), tie=(244, 204, 90), items=['pencil'], action='nod'),
    dict(prefix='PillowMakerOtter', name='베개 장인', role='푹신한 베개 만드는 장인',
         head='Pajama', body='Pajama', eyes='Miner', hat=(246, 184, 150), colors={'main': (248, 196, 166)},
         hair_back='pigtails', hair=(244, 150, 182), tie=(255, 250, 240), action='hop'),
    dict(prefix='BlacksmithOtter', name='대장장이', role='마을 대장간 주인',
         head='Chief', body='Miner', prop=False, eyes='Miner', fur='dark', colors={'denim': (72, 72, 84)},
         hair_front='spiky', hair_back='mane', hair=(204, 70, 50), items=['soot'], action='wave'),
    dict(prefix='CarpenterOtter', name='목수', role='집 짓는 목수',
         head='Miner', body='Miner', prop=False, fur='caramel', hat=(242, 150, 62),
         colors={'denim': (192, 152, 104)}, hair_back='ponytail', hair=(52, 46, 54), tie=(206, 72, 62),
         items=['pencil'], action='hop'),
    dict(prefix='FossilHunterOtter', name='화석 발굴가', role='공룡 화석을 찾는 학자',
         head='Miner', body='Miner', fur='sand', hat=(198, 178, 122), colors={'denim': (114, 122, 84)},
         hair_back='bob', hair=(122, 82, 56), items=['bandage'], action='mine'),
    dict(prefix='MechanicOtter', name='정비공', role='수레 · 풍차 수리공',
         head='Chief', body='Miner', prop=False, eyes='Fisher', fur='slate', colors={'denim': (84, 96, 120)},
         hair_front='side_swoop', hair_back='mane', hair=(54, 58, 84), items=['soot'], action='dance'),
    dict(prefix='ExplorerOtter', name='탐험가', role='섬 구석구석 탐험가',
         head='Fisher', body='Fisher', prop=False, fur='ginger', hat=(192, 170, 118),
         colors={'vest': (150, 110, 72)}, hair_back='long', hair=(250, 222, 132), action='wave'),
    dict(prefix='PhotographerOtter', name='사진사', role='마을 사진관 주인',
         head='Fisher', body='Fisher', prop=False, fur='silver', hat=(66, 66, 74),
         colors={'vest': (132, 132, 142), 'jeans': (62, 72, 112)}, hair_back='pigtails', hair=(172, 122, 222),
         tie=(250, 220, 92), body_items=['camera'], action='hop'),
    dict(prefix='EntomologistOtter', name='곤충 박사', role='곤충 도감 연구가',
         head='Fisher', body='Fisher', prop=False, eyes='Pajama', fur='chestnut', hat=(112, 150, 92),
         colors={'vest': (172, 162, 112)}, hair_back='braids', hair=(96, 150, 128), tie=(244, 204, 90),
         items=['butterfly'], icol={'item': (150, 192, 242)}, action='dance'),
    dict(prefix='GardenerOtter', name='정원사', role='꽃밭 정원사',
         head='Chief', body='Fisher', prop=False, fur='honey',
         colors={'vest': (110, 162, 92), 'jeans': (122, 100, 82)},
         hair_front='puff', hair=(242, 152, 172), items=['flower'], icol={'item': (255, 250, 236)}, action='wave'),
    dict(prefix='BeekeeperOtter', name='양봉가', role='꿀벌 키우는 양봉가',
         head='Fisher', body='Fisher', prop=False, fur='dark', hat=(240, 240, 230), colors={'vest': (242, 200, 72)},
         hair_back='ponytail', hair=(252, 216, 122), tie=(92, 72, 62), items=['bee'], action='hop'),
    dict(prefix='PostmanOtter', name='우체부', role='편지 배달부',
         head='Chief', body='Chief', fur='brown',
         colors={'vest': (198, 66, 58), 'scarf': (62, 74, 132), 'pants': (62, 68, 98)},
         hair_front='side_swoop', hair=(92, 60, 44), action='hop'),
    dict(prefix='TeacherOtter', name='선생님', role='마을 학교 선생님',
         head='Chief', body='Chief', eyes='Fisher', fur='gray',
         colors={'vest': (152, 114, 84), 'scarf': (92, 122, 92)},
         hair_front='bun', hair=(214, 214, 220), action='nod'),
    dict(prefix='BakerOtter', name='제빵사', role='빵집 주인',
         head='Chief', body='Chief', fur='cream', colors={'vest': (236, 214, 180), 'scarf': (242, 150, 72)},
         hair_back='bob', hair=(204, 122, 72), items=['chef_hat'], body_items=['apron'], action='wave'),
    dict(prefix='PainterOtter', name='화가', role='풍경화 그리는 화가',
         head='Chief', body='Chief', fur='rose', colors={'vest': (132, 102, 172), 'scarf': (250, 202, 92)},
         hair_front='bangs', hair_back='long', hair=(242, 150, 182), items=['beret'],
         icol={'item': (64, 84, 142)}, action='dance'),
    dict(prefix='MusicianOtter', name='음악가', role='광장 거리 음악가',
         head='Chief', body='Chief', eyes='Miner', fur='charcoal',
         colors={'vest': (58, 58, 68), 'scarf': (202, 52, 62)},
         hair_front='mohawk', hair=(92, 152, 232), action='dance'),
    dict(prefix='LibrarianOtter', name='사서', role='도서관 사서',
         head='Chief', body='Chief', eyes='Fisher', fur='snow',
         colors={'vest': (62, 84, 126), 'scarf': (152, 62, 72)},
         hair_front='bangs', hair_back='bob', hair=(52, 46, 54), action='nod'),
    dict(prefix='ShopkeeperOtter', name='잡화점 주인', role='잡화점 주인',
         head='Chief', body='Chief', fur='tan', colors={'vest': (92, 142, 102), 'scarf': (242, 142, 62)},
         hair_front='pompadour', hair_back='mane', hair=(122, 72, 46), action='wave'),
    dict(prefix='TailorOtter', name='재봉사', role='옷 짓는 재봉사',
         head='Chief', body='Chief', eyes='Pajama', fur='lilac',
         colors={'vest': (186, 122, 146), 'scarf': (246, 232, 202)},
         hair_front='twin_buns', hair=(122, 92, 172), items=['bow'], icol={'item': (250, 214, 92)}, action='hop'),
]
for i, o in enumerate(CAST):
    o.setdefault('prop', True)
    o.setdefault('colors', {})
    o.setdefault('hat', None)
    o.setdefault('fur', None)          # None: the head drawing's own fur colour
    o.setdefault('eyes', o['head'])
    o.setdefault('hair_back', None)
    o.setdefault('hair_front', None)
    o.setdefault('hair', (120, 80, 60))
    o.setdefault('tie', None)
    o.setdefault('items', [])
    o.setdefault('body_items', [])
    o.setdefault('icol', {})
    if o['head'] == 'Pajama':
        o['fur'] = None
    if o['hair_front'] and o['head'] != 'Chief':
        raise ValueError(f"{o['prefix']}: front hair needs the hatless chief head")
    o['phase'] = (i * 0.37) % 2.9

FUR_KOR = {'cream': '크림', 'sand': '모래색', 'honey': '꿀색', 'tan': '연갈색', 'caramel': '캐러멜', 'ginger': '주황',
           'rust': '적갈색', 'chestnut': '밤색', 'brown': '갈색', 'dark': '짙은 갈색', 'charcoal': '숯색', 'gray': '회색',
           'silver': '은회색', 'slate': '청회색', 'snow': '눈색', 'rose': '분홍빛', 'lilac': '라일락'}
HAIR_KOR = {'long': '긴 머리', 'bob': '단발', 'pigtails': '양갈래', 'ponytail': '포니테일', 'braids': '땋은 머리',
            'mane': '갈기', 'bangs': '앞머리', 'side_swoop': '옆으로 넘긴 머리', 'puff': '뽀글 파마', 'bun': '똥머리',
            'twin_buns': '양쪽 똥머리', 'spiky': '삐죽 머리', 'mohawk': '모히칸', 'pompadour': '올린 머리',
            'cowlick': '바보털'}
ITEM_KOR = {'pencil': '귀에 꽂은 연필', 'bee': '꿀벌', 'butterfly': '나비', 'star': '별 핀', 'flower': '꽃', 'bow': '리본',
            'beret': '베레모', 'chef_hat': '요리사 모자', 'headband': '머리띠', 'freckles': '주근깨', 'soot': '검댕',
            'bandage': '반창고', 'camera': '카메라', 'apron': '앞치마', 'bowtie': '나비넥타이'}
ACTION_KOR = {'nod': '꾸벅꾸벅 졸기', 'mine': '곡괭이질', 'cast': '낚싯대 던지기', 'wave': '손 흔들기',
              'hop': '폴짝 뛰기', 'dance': '흔들흔들 춤'}
HEAD_KOR = {'Pajama': '잠옷 해달', 'Miner': '광부 해달', 'Fisher': '낚시 해달', 'Chief': '촌장 해달'}


# --- colour helpers ------------------------------------------------------------------------------

def rgb_to_hsv(rgb):
    r, g, b = [rgb[..., i] / 255 for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rr = m & (mx == r)
    gg = m & (mx == g) & ~rr
    bb = m & ~rr & ~gg
    h[rr] = (60 * ((g - b)[rr] / d[rr])) % 360
    h[gg] = 60 * ((b - r)[gg] / d[gg]) + 120
    h[bb] = 60 * ((r - g)[bb] / d[bb]) + 240
    return h, np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0), mx


def hsv_mask(a, hue, s, v):
    h, sat, val = rgb_to_hsv(a[..., :3])
    lo, hi = hue
    hm = (h >= lo) & (h <= hi) if lo <= hi else (h >= lo) | (h <= hi)
    return hm & (sat >= s[0]) & (sat <= s[1]) & (val >= v[0]) & (val <= v[1]) & (a[..., 3] > 100)


def soft(mask, sigma=0.7):
    """Recolour weight from a hard mask: 1 inside, spilling ~1 px into the anti-aliased rim."""
    return np.clip(ndimage.gaussian_filter(mask.astype(np.float32), sigma) * 1.6, 0, 1)


def recolor(a, w, target, v_ref):
    """Paint weight w of `a` with `target`, keeping the shading (brightness relative to v_ref)."""
    v = a[..., :3].max(-1)
    new = np.clip(np.asarray(target, np.float32) * (v / v_ref)[..., None], 0, 255)
    out = a.copy()
    out[..., :3] = a[..., :3] * (1 - w[..., None]) + new * w[..., None]
    return out


def fur_weight(a, refs, sigma=26.0):
    rgb = a[..., :3]
    g = lambda c: np.exp(-((rgb - np.asarray(c, np.float32)) ** 2).sum(-1) / (2 * sigma ** 2))
    f = sum(g(c) for c in refs['fur'])
    o = sum(g(c) for c in refs['other']) + g(INK_RGB) + g((90, 30, 20))
    return f / (f + o + 1e-9)


# --- per-base data (cached) ----------------------------------------------------------------------

_BASES = {}


def base(name):
    """Parts of one drawing as straight-alpha arrays on its native working canvas, plus
    joints / anchors / masks in working px."""
    if name in _BASES:
        return _BASES[name]
    spec = fr.CHARS[name]
    info = BASE[name]
    prem, s = fr.cut(name)
    rgba, _ = fr.load(spec)
    with fr.working(spec, s):
        interior, lab, _ = cp.segment(rgba)
        region = lambda pts: ndimage.binary_dilation(np.isin(lab, [cp.region_at(lab, p) for p in pts]),
                                                     iterations=2)
        paws, feet = region(info['paws']), region(info['feet'])
        bare = fr.erase_eyes(prem['Head'], spec)
        excl = np.zeros(lab.shape, np.float32)
        for poly in (info['fur'] or {}).get('exclude', []):
            excl = np.maximum(excl, cp.coverage(poly, lab.shape, smooth=True))
    joints = {}
    for bname, parent, (x, y), part in spec['bones']:
        joints[bname] = (parent, np.array((x * s, (spec['feet'] if y is None else y) * s)), part)
    parts = {k: fr.unpremul(v) for k, v in prem.items()}
    w = lambda p: (p[0] * s, p[1] * s)
    d = dict(spec=spec, info=info, s=s, parts=parts, bare=bare, paws=paws, feet=feet, excl=excl, joints=joints,
             anchors=dict(eyes=[w(p) for p in info['eyes']], cheeks=[w(p) for p in info['cheeks']],
                          nose=w(info['nose']), ears=[w(p) for p in info['ears']]),
             offset=np.array(ROOT_AT) - joints['Root'][1])
    # chin width 20 px above the neck joint: a transplanted head is scaled to cover the same
    neck = joints['Head'][1]
    row = parts['Head'][int(neck[1] - 20), :, 3] > 128
    xs = np.nonzero(row)[0]
    d['chin_w'] = float(xs.max() - xs.min()) if len(xs) else 1.0
    # reference brightness of every recolourable material
    vref = {}
    head = parts['Head']
    if info['hat']:
        m = hsv_mask(head, **info['hat'])
        vref['hat'] = float(np.median(head[..., :3].max(-1)[m]))
    for key, mat in info['materials'].items():
        vals = []
        for p in mat['parts']:
            a = parts[p]
            m = material_mask(d, p, a, mat)
            vals.append(a[..., :3].max(-1)[m])
        vref[key] = float(np.median(np.concatenate(vals)))
    vref['fur'] = float(max(info['fur_ref']))
    # paws / feet: their own brightness, so they come out as the fur colour (a bit darker)
    for key, region, part_prefix in (('paw', d['paws'], 'Arm'), ('foot', d['feet'], 'Leg')):
        vals = []
        for p, a in parts.items():
            if p.startswith(part_prefix):
                m = region & (a[..., 3] > 200) & (a[..., :3].mean(-1) > 90)
                vals.append(a[..., :3].max(-1)[m])
        vref[key] = float(np.median(np.concatenate(vals)))
    d['vref'] = vref
    _BASES[name] = d
    return d


def material_mask(d, part, a, mat):
    m = hsv_mask(a, mat['hue'], mat['s'], mat['v'])
    m &= ~(d['paws'] | d['feet'])
    if 'y' in mat:
        ys = np.arange(m.shape[0])[:, None] / d['s']
        m &= (ys >= mat['y'][0]) & (ys <= mat['y'][1])
    return m


# --- eyes --------------------------------------------------------------------------------------------

def eye_alpha(S):
    """Coverage of S's eyes + brows on its native canvas: how far each pixel was pulled from
    the painted-out fur toward the ink (so the anti-aliased rims carry over)."""
    if 'eye_alpha' not in S:
        a, bare = S['parts']['Head'], S['bare']
        ink = np.asarray(S['spec']['ink'], np.float32)
        num = (bare[..., :3] - a[..., :3]).mean(-1)
        den = np.maximum((bare[..., :3] - ink).mean(-1), 20)
        changed = np.abs(bare[..., :3] - a[..., :3]).max(-1) > 2
        S['eye_alpha'] = np.clip(num / den, 0, 1) * changed
    return S['eye_alpha']


def eye_map(S, T):
    """Scale + offset taking S's eyes onto T's (working px): p_T = (p_S - mS) * k + mT."""
    eS, eT = np.array(S['anchors']['eyes']), np.array(T['anchors']['eyes'])
    k = np.linalg.norm(eT[1] - eT[0]) / np.linalg.norm(eS[1] - eS[0])
    return eS.mean(0), eT.mean(0), k


def transplant_eyes(a, S, T, blink):
    """Put S's eyes (or, when blinking, S's closed-eye lids + brows) on T's painted-out face."""
    mS, mT, k = eye_map(S, T)
    ink = np.asarray(T['spec']['ink'], np.float32)
    pen = hair.Pen(a, T['s'], ink)
    if blink:
        b = S['spec']['blink']
        strokes = [(p, b['width']) for p in b['lids']] + [(p, b.get('brow_width', 10)) for p in b.get('brows', [])]
        for pts, wd in strokes:
            q = [((np.array(p) * S['s'] - mS) * k + mT) / T['s'] for p in pts]
            pen.stroke([tuple(x) for x in q], wd * S['s'] * k / T['s'])
        return pen.a
    al = Image.fromarray(eye_alpha(S).astype(np.float32), 'F')
    # output -> input: p_S = (p_T - mT) / k + mS
    data = (1 / k, 0, mS[0] - mT[0] / k, 0, 1 / k, mS[1] - mT[1] / k)
    al = np.asarray(al.transform(a.shape[1::-1], Image.Transform.AFFINE, data=data,
                                 resample=Image.Resampling.BILINEAR))
    pen.over(np.clip(al, 0, 1) * (a[..., 3] > 0), ink)
    return pen.a


# --- assembly --------------------------------------------------------------------------------------

def place(a, M):
    """Warp a straight-alpha array onto the common canvas. M maps native -> common:
    (scale, mirror_axis_or_None, tx, ty) as x' = scale * x + tx (then mirrored)."""
    sc, mirror, tx, ty = M
    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA').convert('RGBa')
    if mirror is None:
        data = (1 / sc, 0, -tx / sc, 0, 1 / sc, -ty / sc)
    else:   # x' = 2*axis - (sc*x + tx)  ->  x = (2*axis - x' - tx) / sc
        data = (-1 / sc, 0, (2 * mirror - tx) / sc, 0, 1 / sc, -ty / sc)
    out = im.transform((CW, CH), Image.Transform.AFFINE, data=data,
                       resample=Image.Resampling.BICUBIC).convert('RGBA')
    return np.asarray(out).astype(np.float32)


def place_pt(p, M):
    sc, mirror, tx, ty = M
    x, y = sc * p[0] + tx, sc * p[1] + ty
    if mirror is not None:
        x = 2 * mirror - x
    return np.array((x, y))


def darker(c, k):
    return tuple(v * k for v in c)


def paint_head(o, H, blink):
    """The otter's head on H's native canvas: fur / hat colour, eyes, hair, items."""
    own_eyes = o['eyes'] == o['head']
    a0 = (H['parts']['Head_Blink'] if blink else H['parts']['Head']) if own_eyes else H['bare']
    a = a0.copy()
    hat_w = soft(hsv_mask(a0, **H['info']['hat'])) if H['info']['hat'] else 0
    if o['fur'] and H['info']['fur']:
        w = fur_weight(a0, H['info']['fur']) * (1 - H['excl']) * (1 - hat_w)
        a = recolor(a, w * (a0[..., 3] > 0), FUR[o['fur']], H['vref']['fur'])
    if o['hat'] and H['info']['hat']:
        a = recolor(a, hat_w, o['hat'], H['vref']['hat'])
    if not own_eyes:
        a = transplant_eyes(a, base(o['eyes']), H, blink)
    g = hair.GEOM[o['head']]
    ink = H['spec']['ink']
    seed = sum(map(ord, o['prefix']))
    if o['hair_back']:
        layer = np.zeros_like(a)
        pen = hair.Pen(layer, H['s'], ink, seed)
        hair.back(pen, o['hair_back'], g, darker(o['hair'], 0.9) if o['hair_front'] else o['hair'], o['tie'])
        a = hair.under(a, layer)
    pen = hair.Pen(a, H['s'], ink, seed + 1)
    if o['hair_front']:
        hair.front(pen, o['hair_front'], g, o['hair'])
    for it in o['items']:
        hair.item(pen, it, g, o['icol'])
    return pen.a


def build_otter(o):
    """{'ims': {part: PIL}, 'bones': [rig.Bone], 'alts', 's'} for one cast entry."""
    H, B = base(o['head']), base(o['body'])
    fur = FUR[o['fur']] if o['fur'] else BASE[o['head']]['fur_ref']
    head = paint_head(o, H, blink=False)
    blink = paint_head(o, H, blink=True)

    # body (native canvas of B): outfit colours, paws / feet in the fur colour, body items
    bparts = {}
    for p, a in B['parts'].items():
        if p in ('Head', 'Head_Blink', 'Pompom'):
            continue
        masks = {key: soft(material_mask(B, p, a, B['info']['materials'][key])) for key in o['colors']
                 if p in B['info']['materials'][key]['parts']}
        for key, w in masks.items():
            a = recolor(a, w, o['colors'][key], B['vref'][key])
        if p.startswith(('Arm', 'Leg')) and (o['fur'] or o['head'] != o['body']):
            arm = p.startswith('Arm')
            region = B['paws'] if arm else B['feet']
            lum = a[..., :3].mean(-1)
            w = region * np.clip((lum - 60) / 25, 0, 1)
            tone = np.asarray(fur, np.float32) * (PAW_TONE if arm else FOOT_TONE)
            a = recolor(a, w, tone, B['vref']['paw' if arm else 'foot'])
        if p == 'Body' and o['body_items']:
            pen = hair.Pen(a.copy(), B['s'], B['spec']['ink'], 7)
            for it in o['body_items']:
                hair.body_item(pen, it, hair.BODY[o['body']], o['icol'])
            a = pen.a
        bparts[p] = a

    # common canvas
    Mb = (1.0, None, *B['offset'])
    joints = {}
    for name, (parent, pos, part) in B['joints'].items():
        if name in ('Head', 'Pompom'):
            continue
        joints[name] = (parent, place_pt(pos, Mb), part)
    neck_b = place_pt(B['joints']['Head'][1], Mb)
    ims = {p: place(a, Mb) for p, a in bparts.items()}

    if not o['prop'] and B['info'].get('prop'):
        # free left arm: the right arm mirrored about the neck
        Mm = (1.0, neck_b[0], *B['offset'])
        ims['Arm_L'] = place(bparts['Arm_R'], Mm)
        joints['Arm_L'] = ('Body', place_pt(B['joints']['Arm_R'][1], Mm), 'Arm_L')
        for c in B['info'].get('prop_children', ()):
            ims.pop(c, None)
            joints.pop(c, None)

    sc = 1.0 if o['head'] == o['body'] else float(np.clip(B['chin_w'] / H['chin_w'], 0.88, 1.15))
    neck_h = H['joints']['Head'][1]
    Mh = (sc, None, neck_b[0] - sc * neck_h[0], neck_b[1] - sc * neck_h[1])
    ims['Head'] = place(head, Mh)
    ims['Head_Blink'] = place(blink, Mh)
    joints['Head'] = ('Body', neck_b, 'Head')
    if 'Pompom' in H['joints']:
        ims['Pompom'] = place(H['parts']['Pompom'], Mh)
        joints['Pompom'] = ('Head', place_pt(H['joints']['Pompom'][1], Mh), 'Pompom')

    # bones (rig units = working px / rig.S), parents first
    layers = dict(fr.CHARS[o['body']]['layers'])
    layers.update(Head=30, Pompom=32)
    order = ['Root', 'Leg_L', 'Leg_R', 'Body', 'Scarf', 'Arm_L', 'Rod', 'Bobber', 'Arm_R', 'Head', 'Pompom']
    bones = []
    for name in order:
        if name not in joints:
            continue
        parent, pos, part = joints[name]
        bones.append(rig.Bone(name, parent, (pos[0] / rig.S, pos[1] / rig.S), part, layers.get(part, 0)))
    pil = {p: Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), 'RGBA') for p, a in ims.items()}
    return dict(ims=pil, bones=bones, alts={'Head': {'blink': pil['Head_Blink']}}, s=B['s'])


def make_rig(built):
    ims = {k: v for k, v in built['ims'].items() if k != 'Head_Blink'}
    return rig.Rig(built['bones'], ims, alts=built['alts'])


# --- animation ---------------------------------------------------------------------------------------

def _extras(o, pose, t, moving):
    p = 2 * math.pi * t
    if o['head'] == 'Pajama':
        pose.setdefault('Pompom', {})['rot'] = (12 * math.sin(p * 2 / anim.WALK_PERIOD - 1.2) if moving
                                                else 6 * math.sin(p / 2.6))
    if o['body'] == 'Fisher' and o['prop']:
        if moving:
            pose['Rod'] = dict(rot=-0.85 * pose.get('Arm_L', {}).get('rot', 0))
        pose['Bobber'] = dict(rot=(10 if moving else 4) * math.sin(p / (anim.WALK_PERIOD if moving else 1.7) - 0.8))
    if o['body'] == 'Chief':
        pose['Scarf'] = dict(rot=(6 if moving else 3) * math.sin(p * (2 / anim.WALK_PERIOD if moving else
                                                                        1 / anim.IDLE_PERIOD) - 0.8))
    if o['body'] == 'Miner' and o['prop'] and moving:
        pose['Arm_L'] = dict(rot=5 * math.sin(p / anim.WALK_PERIOD))
    return pose


def idle(o, t):
    t = t + o['phase']
    pose = _extras(o, anim.idle('Front', t), t, False)
    if fa.blink(t, offset=o['phase'] * 1.7):
        pose = fa.merge(pose, {'Head': dict(alt='blink')})
    return pose


def walk(o, t):
    t = t + o['phase']
    pose = _extras(o, anim.walk('Front', t), t, True)
    if fa.blink(t, offset=o['phase'] * 1.3):
        pose = fa.merge(pose, {'Head': dict(alt='blink')})
    return pose


HOP_PERIOD, DANCE_PERIOD = 1.1, 1.2
ACTION_PERIOD = {'nod': fa.ACTION_PERIOD['Pajama'], 'mine': fa.ACTION_PERIOD['Miner'],
                 'cast': fa.ACTION_PERIOD['Fisher'], 'wave': fa.ACTION_PERIOD['Chief'],
                 'hop': HOP_PERIOD, 'dance': DANCE_PERIOD}


def hop(o, t):
    v = (t % HOP_PERIOD) / HOP_PERIOD
    crouch = fa.keyed(v, [(0, 0), (0.16, 1), (0.26, 0), (0.66, 0), (0.74, 0.7), (0.88, 0), (1, 0)])
    air = math.sin(math.pi * min(1.0, max(0.0, (v - 0.24) / 0.44)))
    pose = idle(o, t - o['phase'])
    return fa.merge(pose, {
        'Root': dict(dy=-17 * air),
        'Body': dict(sy=1 - 0.06 * crouch, sx=1 + 0.03 * crouch, dy=3 * crouch),
        'Head': dict(dy=2.5 * crouch - 1.5 * air, rot=3 * math.sin(2 * math.pi * v)),
        'Arm_L': dict(rot=-78 * air - 8 * crouch), 'Arm_R': dict(rot=78 * air + 8 * crouch),
        'Leg_L': dict(dy=-3 * air), 'Leg_R': dict(dy=-3 * air),
    })


def dance(o, t):
    p = 2 * math.pi * t / DANCE_PERIOD
    sway = math.sin(p)
    pose = idle(o, t - o['phase'])
    return fa.merge(pose, {
        'Root': dict(dy=-3 * abs(math.sin(p))),
        'Body': dict(rot=6 * sway, dx=2.5 * sway),
        'Head': dict(rot=-4 * sway, dy=0.8 * math.cos(2 * p)),
        'Arm_L': dict(rot=-45 - 22 * sway), 'Arm_R': dict(rot=45 - 22 * sway),
        'Leg_L': dict(dy=-4 * max(0.0, sway)), 'Leg_R': dict(dy=-4 * max(0.0, -sway)),
    })


def action(o, t):
    tt = t + o['phase']
    a = o['action']
    if a == 'nod':
        return fa.nod_off(tt)
    if a == 'mine':
        return fa.mining(tt)
    if a == 'cast':
        return fa.cast(tt)
    if a == 'wave':
        return fa.wave(tt)
    if a == 'hop':
        return hop(o, tt)
    return dance(o, tt)


def recipe_kor(o):
    """Human-readable recipe lines (Korean)."""
    fur = FUR_KOR[o['fur']] if o['fur'] else '원본'
    outfit = HEAD_KOR[o['body']] + ('' if o['prop'] or o['body'] not in ('Miner', 'Fisher') else ' (빈손)')
    lines = [f"머리: {HEAD_KOR[o['head']]} · 옷: {outfit}", f"털색: {fur}",
             f"눈 · 눈썹: {HEAD_KOR[o['eyes']]} 원본"]
    hair_ = [HAIR_KOR[h] for h in (o['hair_front'], o['hair_back']) if h]
    if hair_:
        lines.append('머리 모양: ' + ' + '.join(hair_))
    items = [ITEM_KOR[i] for i in o['items'] + o['body_items']]
    if items:
        lines.append('소품: ' + ', '.join(items))
    lines.append(f"동작: {ACTION_KOR[o['action']]}")
    return lines
