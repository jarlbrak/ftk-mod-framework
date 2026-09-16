"""Author the original Tidecrown Sovereign against Sea King's binding landmarks.

Only ``bone_names`` and inverse bind matrices are read from the local extracted
reference.  The royal body, crown, cloak, beard, armor, and hands below are new
procedural surfaces; no native mesh positions, triangles, normals, UVs, or
texture pixels participate in this generator.
"""
from __future__ import annotations

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


# A nearest-sampled original palette.  It separates the silhouette into abyssal
# cloth, oxidized armor, sea-glass highlights, coral, gold, and a readable face.
PALETTE = [
    '071927',  # 0 abyss shadow
    '103143',  # 1 deep teal
    '1B5361',  # 2 storm teal
    '367A7F',  # 3 sea-weathered blue
    '71B7AD',  # 4 seafoam edge
    'B9E5D4',  # 5 pale foam / beard highlight
    '315A5B',  # 6 oxidized bronze shade
    '6F8A62',  # 7 aged verdigris
    'A95D36',  # 8 coral
    'D78D44',  # 9 amber
    'F0C86B',  # 10 tidecrown gold
    'E8F0D8',  # 11 ivory beard
    '101B22',  # 12 eye socket
    '5CE1D7',  # 13 sea-glass eye
    '4B2944',  # 14 imperial purple
    '1B252A',  # 15 charcoal outline
]


def write_palette() -> None:
    image = Image.new('RGB', (len(PALETTE) * 32, 32))
    for i, value in enumerate(PALETTE):
        image.paste(tuple(int(value[j:j + 2], 16) for j in (0, 2, 4)), (i * 32, 0, (i + 1) * 32, 32))
    image.save(OUT / 'tidecrown-sea-king_basecolor.png')


class Surface:
    """Explicit low-poly volumes in FTK mesh space, bound to the full palette."""

    def __init__(self, renderer_id: int):
        self.ref = np.load(ROOT / f'scratch/skeleton-audit/{renderer_id}/reference.npz', allow_pickle=False)
        self.names = self.ref['bone_names'].tolist()
        self.centers = np.linalg.inv(self.ref['bindposes'])[:, :3, 3]
        self.B = dict(zip(self.names, self.centers))
        self.data = {key: [] for key in ('positions', 'normals', 'uvs', 'triangles', 'joints', 'weights')}
        self.data['bone_names'] = self.names
        self.pieces: list[dict[str, object]] = []

    def weights(self, value):
        return {value: 1.0} if isinstance(value, str) else value

    def triangle(self, points, skins, color: int) -> None:
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

    def tube(self, label, points, widths, depths, skins, color, axis=(1, 0, 0), sides=8) -> None:
        """A capped tapered volume, safe with FTK's ordinary one-sided shader."""
        start = len(self.data['positions'])
        points = np.asarray(points, dtype=float)
        if not (len(points) == len(widths) == len(depths) == len(skins) and len(points) >= 2):
            raise ValueError('Tube topology inputs disagree')
        rings = []
        for i, point in enumerate(points):
            tangent = points[min(i + 1, len(points) - 1)] - points[max(0, i - 1)]
            length = float(np.linalg.norm(tangent))
            if length < 1e-10:
                raise ValueError('Coincident authored tube landmarks')
            tangent /= length
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
        self.pieces.append({'name': label, 'vertex_start': start, 'vertex_count': len(self.data['positions']) - start})

    def ellipsoid(self, label, center, size, bone, color, sides=10) -> None:
        """Faceted closed volume with independent width, height, and depth."""
        center = np.asarray(center, dtype=float)
        points = [center + np.array([0, t * size[1], 0]) for t in (-.98, -.70, 0, .70, .98)]
        radii = (.20, .71, 1.0, .71, .20)
        self.tube(label, points, [r * size[0] for r in radii], [r * size[2] for r in radii], [bone] * 5, color, sides=sides)

    def kite(self, label, points, skins, color, thickness=.05) -> None:
        """A closed plate or cloak panel with front, back, and all four edges."""
        start = len(self.data['positions'])
        points = np.asarray(points, dtype=float)
        if points.shape != (4, 3) or len(skins) != 4:
            raise ValueError('Kite requires four points and four skins')
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
        edge_color = color[-1] if isinstance(color, list) else color
        for i in range(4):
            j = (i + 1) % 4
            self.triangle([front[i], back[i], back[j]], [skins[i], skins[i], skins[j]], edge_color)
            self.triangle([front[i], back[j], front[j]], [skins[i], skins[j], skins[j]], edge_color)
        self.pieces.append({'name': label, 'vertex_start': start, 'vertex_count': len(self.data['positions']) - start})

    def write(self, name: str):
        (OUT / f'{name}.source.json').write_text(json.dumps(self.data, separators=(',', ':')) + '\n')
        write_glb(OUT / f'{name}.glb', self.data, self.ref)
        validation = validate(OUT / f'{name}.glb', self.ref)
        (OUT / f'{name}.validation.json').write_text(json.dumps(validation, indent=2) + '\n')
        (OUT / f'{name}.pieces.json').write_text(json.dumps(self.pieces, indent=2) + '\n')
        return validation


def blend(a: str, b: str, amount: float) -> dict[str, float]:
    return {a: 1 - amount, b: amount}


write_palette()
s = Surface(121357)
B = s.B

# A broad, floating royal robe keeps the source inside the native mesh's low
# floor envelope.  Sea King's Knee transforms are in the palette but unused by
# native surface weights, so the original robe deliberately does not invent a
# long below-floor leg section on those joints.
s.tube(
    'Abyssal flared royal robe',
    [B['Root_M'] + np.array([0, -.05, 0]), B['Root_M'] + np.array([0, .42, .02]), B['BackA_M'], B['BackB_M']],
    [2.22, 2.08, 1.66, 1.48], [1.16, 1.23, 1.10, .96],
    ['Root_M', blend('Root_M', 'BackA_M', .45), blend('Root_M', 'BackA_M', .78), blend('BackA_M', 'BackB_M', .62)],
    [0, 1, 2, 1, 0, 1, 2, 3, 2, 1], sides=12,
)
s.tube(
    'Layered storm cuirass',
    [B['Root_M'] + np.array([0, .35, .03]), B['BackA_M'], B['BackB_M'], B['Chest_M'], B['Neck_M']],
    [1.38, 1.50, 1.62, 1.72, 1.10], [.88, 1.00, 1.06, .98, .73],
    [blend('Root_M', 'BackA_M', .35), blend('Root_M', 'BackA_M', .74), blend('BackA_M', 'BackB_M', .58), blend('BackB_M', 'Chest_M', .62), blend('Chest_M', 'Neck_M', .60)],
    [1, 2, 3, 2, 1, 2, 3, 4, 3, 2], sides=12,
)
s.ellipsoid('Verdigris breastplate', B['Chest_M'] + np.array([0, -.20, .46]), (1.66, 1.57, .40), 'Chest_M', [6, 7, 6, 3, 6, 7, 6, 9], sides=12)
s.ellipsoid('Coral heart reliquary', B['Chest_M'] + np.array([0, -.08, .89]), (.38, .48, .105), 'Chest_M', [8, 9, 10, 9, 8, 9], sides=9)
s.ellipsoid('Sea-glass heart', B['Chest_M'] + np.array([0, -.08, 1.00]), (.15, .22, .040), 'Chest_M', [4, 13, 5, 13, 4], sides=8)

# Separate front scale plates articulate with the chest instead of a static
# overlay.  Their shallow closed volumes make the armor readable at combat range.
for row, (y, width) in enumerate(((3.10, 1.22), (3.62, 1.42), (4.14, 1.50), (4.66, 1.32)), 1):
    for side, sign in (('R', 1), ('L', -1)):
        x = sign * width * .46
        s.ellipsoid(f'{side} chest scale {row}', np.array([x, y, .97]), (.33, .28, .075), 'Chest_M', [6, 7, 9, 7, 6, 3], sides=7)

# The rear mantle has three independently closed panels.  It follows the spine
# only; it intentionally does not borrow external tentacles, trident, shield, or
# native breakable props.
for side, sign in (('R', 1), ('L', -1)):
    s.kite(
        f'{side} tide mantle',
        [B['Chest_M'] + np.array([sign * .45, .22, -.74]), B['Chest_M'] + np.array([sign * 1.70, -.38, -1.04]), B['BackA_M'] + np.array([sign * 1.93, -.38, -.98]), B['BackA_M'] + np.array([sign * .36, -.08, -.68])],
        ['Chest_M', 'Chest_M', 'BackA_M', 'BackA_M'], [14, 1, 2, 3, 2, 1, 14], thickness=.060,
    )
s.kite(
    'Central tide mantle',
    [B['Chest_M'] + np.array([-.42, .13, -1.00]), B['Chest_M'] + np.array([.42, .13, -1.00]), B['BackA_M'] + np.array([.72, -.50, -1.18]), B['BackA_M'] + np.array([-.72, -.50, -1.18])],
    ['Chest_M', 'Chest_M', 'BackA_M', 'BackA_M'], [0, 1, 14, 1, 0, 1], thickness=.060,
)

# Shoulder armor and segmented arms follow each real arm chain.  The hands use
# the full native finger hierarchy so trident attack poses bend the new gauntlet
# details instead of leaving decorative fingers frozen at the wrist.
for side, sign in (('R', 1), ('L', -1)):
    scapula, shoulder, elbow, wrist = [f'{part}_{side}' for part in ('Scapula', 'Shoulder', 'Elbow', 'Wrist')]
    s.ellipsoid(f'{side} oceanic pauldron', B[scapula] + np.array([sign * .13, -.06, .13]), (.91, .57, .68), blend('Chest_M', scapula, .64), [6, 7, 9, 10, 9, 7, 6], sides=10)
    s.tube(
        f'{side} armored arm', [B[scapula], B[shoulder], B[elbow], B[wrist]],
        [.59, .70, .58, .46], [.55, .62, .51, .45],
        [scapula, blend(scapula, shoulder, .70), blend(shoulder, elbow, .70), blend(elbow, wrist, .72)],
        [6, 7, 3, 2, 3, 7, 6, 9], axis=(0, 0, 1), sides=10,
    )
    s.tube(
        f'{side} coral forearm ridge', [B[elbow] + np.array([0, .10, .40]), B[wrist] + np.array([0, .10, .34])],
        [.095, .028], [.09, .025], [elbow, wrist], [8, 9, 10, 9, 8], axis=(0, 1, 0), sides=6,
    )
    s.ellipsoid(f'{side} tide gauntlet', B[wrist] + np.array([sign * .15, -.04, .16]), (.58, .48, .56), wrist, [1, 2, 3, 4, 3, 2, 1], sides=9)
    for finger in ('MiddleFinger', 'IndexFinger', 'PinkyFinger', 'RingFinger'):
        f1, f2, f3, f4 = [f'{finger}{number}_{side}' for number in range(1, 5)]
        s.tube(
            f'{side} {finger} armored digit', [B[wrist], B[f1], B[f3], B[f4]],
            [.17, .145, .095, .025], [.16, .135, .085, .020],
            [blend(wrist, f1, .58), f1, blend(f2, f3, .63), f4], [6, 7, 3, 4, 3, 7, 6], axis=(0, 0, 1), sides=6,
        )
    t1, t2, t3 = [f'ThumbFinger{number}_{side}' for number in range(1, 4)]
    s.tube(
        f'{side} thumb armored digit', [B[wrist], B[t1], B[t2], B[t3]],
        [.19, .15, .09, .025], [.17, .14, .085, .020],
        [blend(wrist, t1, .58), t1, t2, t3], [6, 7, 3, 4, 3, 7, 6], axis=(0, 0, 1), sides=6,
    )

# Keep hip elements small and above the source floor.  The native knee joints
# remain in the exact palette (as required by the GLB), but no geometry is
# invented below the observed Sea King body envelope.
for side, sign in (('R', 1), ('L', -1)):
    hip = f'Hip_{side}'
    s.kite(
        f'{side} high tide tasset',
        [B['Root_M'] + np.array([sign * .34, .36, .78]), B['Root_M'] + np.array([sign * 1.22, .30, .56]), B[hip] + np.array([sign * .52, .04, .72]), B[hip] + np.array([sign * .18, .12, .93])],
        ['Root_M', 'Root_M', hip, hip], [6, 7, 9, 7, 6], thickness=.050,
    )
s.ellipsoid('Gold tide belt', B['Root_M'] + np.array([0, .43, .08]), (1.53, .20, 1.00), 'Root_M', [6, 7, 10, 9, 10, 7, 6], sides=12)

# A face designed for both combat and the unusual Sea King head portrait setup:
# front-facing sea-glass eyes, a bold ivory beard, and a crown that remains above
# the brow rather than occupying the native PortraitCam clearance.
head = B['Head_M'] + np.array([0, .08, .18])
s.ellipsoid('Storm-carved royal head', head, (1.08, .92, .91), 'Head_M', [1, 2, 3, 2, 1, 3, 2], sides=12)
s.ellipsoid('Full ivory beard', B['Jaw_M'] + np.array([0, -.30, .80]), (.94, .64, .27), blend('Head_M', 'Jaw_M', .66), [11, 5, 11, 5, 11, 15], sides=11)
for side, sign in (('R', 1), ('L', -1)):
    s.ellipsoid(f'{side} sea-glass eye', head + np.array([sign * .47, .22, .86]), (.16, .115, .055), 'Head_M', [12, 13, 5, 13, 12], sides=8)
    s.tube(
        f'{side} heavy bronze brow', [head + np.array([sign * .12, .42, .82]), head + np.array([sign * .67, .33, .68])],
        [.11, .075], [.14, .10], ['Head_M', 'Head_M'], [6, 7, 10, 7, 6], axis=(0, 1, 0), sides=6,
    )
    s.tube(
        f'{side} curled ivory moustache', [B['Jaw_M'] + np.array([sign * .05, .03, .94]), B['Jaw_M'] + np.array([sign * .58, -.03, 1.05]), B['Jaw_M'] + np.array([sign * .75, -.18, .83])],
        [.11, .095, .020], [.07, .06, .014], ['Jaw_M'] * 3, [11, 5, 11, 5], axis=(0, 1, 0), sides=7,
    )
s.tube(
    'Royal mouth guard', [B['Jaw_M'] + np.array([-.46, -.14, .89]), B['Jaw_M'] + np.array([0, -.19, .96]), B['Jaw_M'] + np.array([.46, -.14, .89])],
    [.034, .042, .034], [.047, .055, .047], ['Jaw_M'] * 3, 15, axis=(0, 1, 0), sides=6,
)

# Hair_M is a palette entry with no native surface weight, and it sits well above
# the actual skull.  The crown therefore stays visibly attached to Head_M while
# the full 60-joint palette remains preserved in the exported skin.
crown = head + np.array([0, .62, .05])
s.tube(
    'Tidecrown front circlet', [crown + np.array([-.95, -.12, .40]), crown + np.array([0, -.02, .61]), crown + np.array([.95, -.12, .40])],
    [.19, .22, .19], [.16, .18, .16], ['Head_M'] * 3, [8, 9, 10, 9, 8], axis=(0, 1, 0), sides=8,
)
s.tube(
    'Tidecrown rear circlet', [crown + np.array([-.88, -.15, -.25]), crown + np.array([0, -.05, -.48]), crown + np.array([.88, -.15, -.25])],
    [.17, .20, .17], [.15, .16, .15], ['Head_M'] * 3, [6, 7, 10, 7, 6], axis=(0, 1, 0), sides=8,
)
for number, (x, height, z) in enumerate(((-.76, .32, .29), (-.38, .47, .39), (0, .56, .47), (.38, .47, .39), (.76, .32, .29)), 1):
    base = crown + np.array([x, .02, z])
    s.tube(
        f'Coral tidecrown point {number}', [base, base + np.array([x * .05, height * .58, .02]), base + np.array([x * .08, height, .03])],
        [.15, .105, .018], [.13, .095, .014], ['Head_M'] * 3, [8, 9, 10, 9, 8], axis=(0, 0, 1), sides=6,
    )
s.ellipsoid('Crown sea-glass seal', crown + np.array([0, -.04, .72]), (.19, .19, .055), 'Head_M', [4, 13, 5, 13, 4], sides=8)

validation = s.write('tidecrown-sea-king')
manifest = {
    'name': 'Tidecrown Sovereign',
    'native_chassis': 'seaKing',
    'reference_renderer': 121357,
    'renderer_path': 'enSeaKing',
    'bone_count': len(s.names),
    'native_surface_copied': False,
    'authoring_inputs': 'bone_names and inverse bind matrices only',
    'art_status': 'Original Sea King candidate generated and binary-validated; studio and live review pending.',
    'direct_validation': validation,
    'accessory_boundary': 'The source replaces enSeaKing only. Native trident, shield/breakable props, tentacles, portrait cache, and ragdoll components remain external game-owned objects.',
    'remaining': [
        'Editable Blender round-trip and studio review',
        'Fresh exact seaKing/enSeaKing binding at native scale',
        'Native attack, successful hit, ragdoll/death, portrait, material, culling, and cleanup checks',
    ],
}
(OUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print('Tidecrown Sovereign geometry and contract export complete')
