"""Author an original Sunspire Roc against rocA binding landmarks only.

The generator deliberately reads only bone names and inverse bind matrices from
the extracted native reference.  Every visible surface below is procedural,
new geometry authored for this experiment; no native positions, triangles,
normals, UVs, or texture pixels are read.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent;ROOT=OUT.parent.parent
sys.path.insert(0, str(ROOT / 'tools/ai-model-pipeline'))
from export_ftk_glb import write_glb
from validate_glb import validate


# One deliberately small, nearest-sampled palette makes the model legible at
# FTK combat distance without borrowing a game texture.
PALETTE = [
    '101720',  # midnight wing shadow
    '1C2B3B',  # deep blue charcoal
    '30455B',  # blue slate
    '526B80',  # pale flight feather edge
    '87401F',  # warm rust
    'BA6927',  # copper
    'E49A3A',  # sun gold
    'F5CD72',  # bright crest / eye ring
    'F2E7CA',  # ivory beak
    '12100D',  # pupil
    '513522',  # talon
    'BFCED3',  # cool highlight
]


def write_palette() -> None:
    image = Image.new('RGB', (len(PALETTE) * 32, 32))
    for i, hex_color in enumerate(PALETTE):
        rgb = tuple(int(hex_color[k:k + 2], 16) for k in (0, 2, 4))
        image.paste(rgb, (i * 32, 0, (i + 1) * 32, 32))
    image.save(OUT / 'sunspire-roc_basecolor.png')


class Surface:
    """Small, explicit low-poly modelling vocabulary in FTK mesh space."""

    def __init__(self, renderer_id: int):
        self.ref = np.load(
            ROOT / f'scratch/skeleton-audit/{renderer_id}/reference.npz',
            allow_pickle=False,
        )
        self.names = self.ref['bone_names'].tolist()
        self.centers = np.linalg.inv(self.ref['bindposes'])[:, :3, 3]
        self.B = dict(zip(self.names, self.centers))
        self.data = {key: [] for key in ('positions', 'normals', 'uvs', 'triangles', 'joints', 'weights')}
        self.data['bone_names'] = self.names
        self.pieces = []

    def weights(self, bone):
        return {bone: 1.0} if isinstance(bone, str) else bone

    def triangle(self, points, skins, color):
        vertices = np.asarray(points, dtype=float)
        normal = np.cross(vertices[1] - vertices[0], vertices[2] - vertices[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-10:
            raise ValueError('Degenerate authored triangle')
        normal /= length
        start = len(self.data['positions'])
        self.data['triangles'].append([start, start + 1, start + 2])
        for point, skin in zip(vertices, skins):
            skin = self.weights(skin)
            joints = [self.names.index(name) for name in skin]
            values = list(skin.values())
            total = sum(values)
            if not (0 < len(joints) <= 4 and total > 0):
                raise ValueError('Invalid authored skin weight')
            self.data['positions'].append(point.tolist())
            self.data['normals'].append(normal.tolist())
            self.data['uvs'].append([(color + .5) / len(PALETTE), .5])
            self.data['joints'].append(joints + [0] * (4 - len(joints)))
            self.data['weights'].append([value / total for value in values] + [0.] * (4 - len(joints)))

    def tube(self, label, points, widths, depths, skins, color, axis=(1, 0, 0), sides=8):
        """Make a closed tapered volume along landmark points.

        It has deliberate caps so FTK's ordinary material culling cannot turn
        a feather or wing surface invisible from one side.
        """
        start = len(self.data['positions'])
        points = np.asarray(points, dtype=float)
        if not (len(points) == len(widths) == len(depths) == len(skins) and len(points) >= 2):
            raise ValueError('Tube topology inputs disagree')
        rings = []
        for i, point in enumerate(points):
            tangent = points[min(i + 1, len(points) - 1)] - points[max(0, i - 1)]
            tangent_length = float(np.linalg.norm(tangent))
            if tangent_length < 1e-10:
                raise ValueError('Coincident authored tube landmarks')
            tangent /= tangent_length
            u = np.asarray(axis, dtype=float)
            u -= tangent * np.dot(u, tangent)
            if np.linalg.norm(u) < .01:
                u = np.cross(tangent, [0, 0, 1])
            u /= np.linalg.norm(u)
            v = np.cross(tangent, u)
            rings.append([
                point + u * widths[i] * math.cos(k * 2 * math.pi / sides)
                + v * depths[i] * math.sin(k * 2 * math.pi / sides)
                for k in range(sides)
            ])
        for i in range(len(points) - 1):
            for k in range(sides):
                next_k = (k + 1) % sides
                shade = color[k % len(color)] if isinstance(color, list) else color
                self.triangle([rings[i][k], rings[i][next_k], rings[i + 1][next_k]],
                              [skins[i], skins[i], skins[i + 1]], shade)
                self.triangle([rings[i][k], rings[i + 1][next_k], rings[i + 1][k]],
                              [skins[i], skins[i + 1], skins[i + 1]], shade)
        cap_color = color[0] if isinstance(color, list) else color
        for k in range(sides):
            next_k = (k + 1) % sides
            self.triangle([points[0], rings[0][next_k], rings[0][k]], [skins[0]] * 3, cap_color)
            self.triangle([points[-1], rings[-1][k], rings[-1][next_k]], [skins[-1]] * 3, cap_color)
        self.pieces.append({
            'name': label,
            'vertex_start': start,
            'vertex_count': len(self.data['positions']) - start,
        })

    def ellipsoid(self, label, center, size, bone, color, sides=10):
        """A faceted closed volume with independent horizontal and depth axes."""
        center = np.asarray(center, dtype=float)
        points = [center + np.array([0, t * size[1], 0]) for t in (-.98, -.70, 0, .70, .98)]
        radii = (.20, .71, 1.0, .71, .20)
        self.tube(
            label,
            points,
            [radius * size[0] for radius in radii],
            [radius * size[2] for radius in radii],
            [bone] * len(points),
            color,
            sides=sides,
        )

    def kite(self, label, points, skins, color, thickness=.025):
        """Closed four-corner flight panel, safe under one-sided materials."""
        start = len(self.data['positions'])
        points = np.asarray(points, dtype=float)
        if points.shape != (4, 3) or len(skins) != 4:
            raise ValueError('Kite requires four corners and four skins')
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-10:
            raise ValueError('Degenerate authored kite')
        normal /= length
        front, back = points + normal * thickness, points - normal * thickness
        face_color = color[0] if isinstance(color, list) else color
        for indices in ((0, 1, 2), (0, 2, 3)):
            self.triangle([front[i] for i in indices], [skins[i] for i in indices], face_color)
            reverse = indices[::-1]
            self.triangle([back[i] for i in reverse], [skins[i] for i in reverse], face_color)
        edge_color = color if not isinstance(color, list) else color[-1]
        for i in range(4):
            j = (i + 1) % 4
            self.triangle([front[i], back[i], back[j]], [skins[i], skins[i], skins[j]], edge_color)
            self.triangle([front[i], back[j], front[j]], [skins[i], skins[j], skins[j]], edge_color)
        self.pieces.append({
            'name': label,
            'vertex_start': start,
            'vertex_count': len(self.data['positions']) - start,
        })

    def write(self, name):
        source = OUT / f'{name}.source.json'
        source.write_text(json.dumps(self.data, separators=(',', ':')) + '\n')
        write_glb(OUT / f'{name}.glb', self.data, self.ref)
        validation = validate(OUT / f'{name}.glb', self.ref)
        (OUT / f'{name}.validation.json').write_text(json.dumps(validation, indent=2) + '\n')
        (OUT / f'{name}.pieces.json').write_text(json.dumps(self.pieces, indent=2) + '\n')
        return validation


def blend(a, b, amount):
    return np.asarray(a) * (1 - amount) + np.asarray(b) * amount


write_palette()
b = Surface(121238)

# Body: an original heavy eagle torso shaped around the actual spine landmarks.
b.tube(
    'Layered midnight body',
    [b.B[name] for name in ('Root_M', 'BackA_M', 'BackB_M', 'Chest_M')],
    [.63, .76, .70, .48],
    [.75, .91, .83, .55],
    ['Root_M', {'Root_M': .35, 'BackA_M': .65}, {'BackA_M': .40, 'BackB_M': .60}, {'BackB_M': .35, 'Chest_M': .65}],
    [0, 1, 2, 3, 2, 1, 0, 1],
    sides=10,
)
b.ellipsoid('Copper breast mantle', (0, 1.19, .48), (.48, .56, .42), 'Chest_M', [4, 5, 6, 7, 6, 5, 4, 5])
b.tube(
    'Sun-gold collar',
    [b.B[name] for name in ('Chest_M', 'Neck_M', 'Head_M')],
    [.42, .31, .35],
    [.43, .31, .36],
    ['Chest_M', {'Chest_M': .50, 'Neck_M': .50}, 'Head_M'],
    [5, 6, 7, 6, 5, 6, 7, 6],
    sides=10,
)
b.ellipsoid('Faceted eagle head', b.B['Head_M'] + np.array([0, .09, .10]), (.43, .38, .50), 'Head_M', [1, 2, 3, 2, 1, 2, 3, 2])

# Eyes, beak and crown are intentionally small but high contrast for portrait views.
for side, sign in (('right', 1), ('left', -1)):
    b.ellipsoid(f'{side} amber eye ring', b.B['Head_M'] + np.array([sign * .36, .16, .28]), (.075, .080, .075), 'Head_M', 7, sides=8)
    b.ellipsoid(f'{side} black pupil', b.B['Head_M'] + np.array([sign * .405, .16, .30]), (.026, .037, .032), 'Head_M', 9, sides=8)
for crest, offset in enumerate((-.18, 0, .18), 1):
    b.tube(
        f'Crown quill {crest}',
        [b.B['Head_M'] + np.array([offset, .23, -.13]), b.B['Head_M'] + np.array([offset * 1.4, .62, -.28])],
        [.045, .006], [.055, .006], ['Head_M', 'Head_M'], [6, 7, 6, 7], axis=(0, 0, 1), sides=6,
    )
b.tube(
    'Ivory hooked beak',
    [b.B['Head_M'] + np.array([0, .03, .31]), b.B['Jaw_M'], b.B['JawEnd_M'], b.B['JawEnd_M'] + np.array([0, -.08, .15])],
    [.29, .28, .11, .008], [.23, .20, .09, .006],
    ['Head_M', {'Head_M': .30, 'Jaw_M': .70}, {'Jaw_M': .35, 'JawEnd_M': .65}, 'JawEnd_M'],
    [8, 7, 8, 7, 8, 7, 8, 7], axis=(1, 0, 0), sides=8,
)
b.tube(
    'Warm lower beak',
    [b.B['Jaw_M'] + np.array([0, -.12, .02]), b.B['JawEnd_M'] + np.array([0, -.05, .02])],
    [.21, .025], [.12, .018], ['Jaw_M', 'JawEnd_M'], [4, 5, 4, 5], axis=(1, 0, 0), sides=7,
)

# Two independently articulated wing constructions follow every available
# shoulder-to-fingertip bone and use closed, original flight panels.
for side, sign in (('R', 1), ('L', -1)):
    scapula, shoulder, elbow, wrist, fingertip = [f'{part}_{side}' for part in ('Scapula', 'Shoulder', 'Elbow', 'Wrist', 'IndexFinger1')]
    body_edge = b.B['Chest_M'] + np.array([sign * .20, -.08, -.20])
    b.ellipsoid(f'{side} shoulder plumage', b.B[scapula] + np.array([sign * .04, 0, -.06]), (.36, .27, .38), { 'Chest_M': .35, scapula: .65 }, [1, 2, 3, 2, 1, 2, 3, 2])
    b.tube(
        f'{side} articulated leading wing',
        [b.B[name] for name in (scapula, shoulder, elbow, wrist, fingertip)],
        [.30, .37, .34, .25, .02], [.24, .22, .18, .14, .02],
        [scapula, {'Scapula_'+side: .40, shoulder: .60}, {'Shoulder_'+side: .40, elbow: .60}, {'Elbow_'+side: .35, wrist: .65}, fingertip],
        [0, 1, 2, 3, 2, 1, 0, 1], axis=(0, 0, 1), sides=9,
    )
    b.kite(
        f'{side} inner flight panel',
        [body_edge, b.B[shoulder] + np.array([0, .02, .02]), b.B[elbow] + np.array([0, -.08, -.30]), b.B['Chest_M'] + np.array([sign * .22, -.20, -.66])],
        ['Chest_M', shoulder, elbow, 'Chest_M'], [1, 2, 3, 2, 1, 2, 3, 2], thickness=.028,
    )
    b.kite(
        f'{side} middle flight panel',
        [b.B[shoulder] + np.array([0, -.04, -.10]), b.B[elbow] + np.array([0, .02, .02]), b.B[wrist] + np.array([0, -.06, -.30]), b.B[elbow] + np.array([0, -.16, -.54])],
        [shoulder, elbow, wrist, elbow], [2, 3, 4, 3, 2, 3, 4, 3], thickness=.024,
    )
    b.kite(
        f'{side} outer flight panel',
        [b.B[elbow] + np.array([0, -.04, -.12]), b.B[wrist] + np.array([0, .01, .03]), b.B[fingertip] + np.array([0, -.07, -.18]), b.B[wrist] + np.array([0, -.14, -.62])],
        [elbow, wrist, fingertip, wrist], [3, 4, 5, 6, 5, 4, 3, 4], thickness=.022,
    )
    # Five separated primary feathers stay tied to the wrist/index chain,
    # avoiding the detached static decoration that made early trials fragile.
    for feather, offset in enumerate((-.42, -.22, 0, .22, .42), 1):
        start = blend(b.B[wrist], b.B[fingertip], .34) + np.array([sign * offset * .12, -.03, -.12])
        mid = blend(b.B[wrist], b.B[fingertip], .82) + np.array([sign * offset * .22, -.06, -.34])
        end = b.B[fingertip] + np.array([sign * offset, -.14 - abs(offset) * .10, -.72 + abs(offset) * .08])
        b.tube(
            f'{side} primary sun feather {feather}',
            [start, mid, end], [.13, .115, .007], [.038, .031, .004],
            [{wrist: .65, fingertip: .35}, {wrist: .20, fingertip: .80}, fingertip],
            [4, 5, 6, 7, 6, 5, 4, 5], axis=(0, 1, 0), sides=7,
        )

# Full legs and talons use the Roc's native joint chains rather than staying
# root-rigid like the smaller crow rig.
for side, sign in (('R', 1), ('L', -1)):
    hip, knee, ankle, toe1, toe2 = [f'{part}_{side}' for part in ('Hip', 'Knee', 'Ankle', 'MiddleToe1', 'MiddleToe2')]
    b.tube(
        f'{side} gold-flecked leg', [b.B[name] for name in (hip, knee, ankle, toe1, toe2)],
        [.25, .22, .16, .12, .08], [.22, .19, .13, .09, .06],
        [hip, {'Hip_'+side: .45, knee: .55}, {'Knee_'+side: .45, ankle: .55}, {'Ankle_'+side: .45, toe1: .55}, toe2],
        [4, 5, 6, 7, 6, 5, 4, 5], axis=(1, 0, 0), sides=8,
    )
    foot = b.B[toe2]
    for claw, lateral in enumerate((-.16, 0, .16), 1):
        b.tube(
            f'{side} hooked talon {claw}',
            [foot + np.array([sign * lateral, 0, 0]), foot + np.array([sign * lateral * 1.25, -.06, .28]), foot + np.array([sign * lateral * 1.45, .01, .43])],
            [.052, .036, .006], [.042, .029, .005], [toe2] * 3, [10, 9, 10, 9], axis=(1, 0, 0), sides=6,
        )

# A central fan and two side feathers follow all tail chains.  The side plumes
# deliberately use their own hierarchy so they continue to articulate in turn.
b.tube(
    'Central sun tail fan', [b.B[name] for name in ('Root_M', 'Tail1_M', 'Tail2_M', 'Tail3_M', 'Tail3End_M')],
    [.48, .62, .70, .48, .015], [.28, .29, .23, .16, .008],
    ['Root_M', 'Tail1_M', 'Tail2_M', 'Tail3_M', 'Tail3End_M'], [1, 2, 3, 4, 5, 6, 7, 6], axis=(1, 0, 0), sides=9,
)
for side, sign in (('R', 1), ('L', -1)):
    side_a, side_b = f'Tail3ASide_{side}', f'Tail3Side_{side}'
    b.tube(
        f'{side} tail flight plume',
        [b.B['Tail2_M'], b.B[side_a], b.B[side_b], b.B[side_b] + np.array([sign * .12, .03, -.42])],
        [.33, .40, .28, .010], [.16, .14, .10, .006],
        ['Tail2_M', side_a, side_b, side_b], [2, 3, 4, 5, 6, 5, 4, 3], axis=(0, 1, 0), sides=8,
    )
    b.tube(
        f'{side} tail gold rail',
        [b.B[side_a] + np.array([0, .02, .03]), b.B[side_b] + np.array([0, .02, -.06])],
        [.028, .006], [.020, .005], [side_a, side_b], 7, axis=(0, 1, 0), sides=5,
    )

validation = b.write('sunspire-roc')
manifest = {
    'name': 'Sunspire Roc',
    'native_chassis': 'rocA',
    'reference_renderer': 121238,
    'renderer_path': 'enRoc01',
    'bone_count': len(b.names),
    'native_surface_copied': False,
    'authoring_inputs': 'bone_names and inverse bind matrices only',
    'art_status': 'Original Roc candidate generated and binary-validated; studio and live review pending.',
    'direct_validation': validation,
    'remaining': [
        'Editable Blender round-trip and studio visual review',
        'Fresh exact binding, idle, focused-hit and explicit death fixture capture',
        'Separate live readability verdict; no controller-wide claim',
    ],
}
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print('Sunspire Roc geometry and contract export complete')
