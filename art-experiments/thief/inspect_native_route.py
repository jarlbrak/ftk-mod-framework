#!/usr/bin/env python3
"""Record identity and transform metadata only for the two native paired templates."""
import argparse
import hashlib
import json
from pathlib import Path
import UnityPy

OUT = Path(__file__).resolve().parent


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def inspect(assets, assembly):
    env = UnityPy.load(str(assets))
    source = next(f for f in env.files.values() if Path(f.name).name == assets.name)

    def vector(value, fields='xyz'):
        return [getattr(value, field) for field in fields]

    def walk(obj, path):
        go = obj.read()
        components = [pair.component.deref() for pair in go.m_Component]
        transform = next(c.read() for c in components if c.type.name == 'Transform')
        row = {'path': path, 'gameObjectId': obj.path_id, 'active': go.m_IsActive,
               'components': [c.type.name for c in components],
               'localPosition': vector(transform.m_LocalPosition),
               'localRotation': vector(transform.m_LocalRotation, 'xyzw'),
               'localScale': vector(transform.m_LocalScale)}
        renderers = [c for c in components if c.type.name == 'MeshRenderer']
        if renderers:
            assert len(renderers) == 1
            renderer = renderers[0].read()
            filters = [c for c in components if c.type.name == 'MeshFilter']
            assert len(filters) == 1 and len(renderer.m_Materials) == 1
            assert 'SkinnedMeshRenderer' not in row['components']
            pointer = filters[0].read().m_Mesh
            assert pointer.m_PathID != 0
            row.update(rendererId=renderers[0].path_id,
                       meshPointer={'fileId': pointer.m_FileID, 'pathId': pointer.m_PathID},
                       materialPointers=[{'fileId': p.m_FileID, 'pathId': p.m_PathID}
                                         for p in renderer.m_Materials])
        rows = [row]
        for pointer in transform.m_Children:
            child = pointer.read().m_GameObject.deref()
            rows += walk(child, child.read().m_Name if path == '.' else path + '/' + child.read().m_Name)
        return rows

    templates = []
    for name, root_id, weapon_id in [('dualKnife', 32973, 43859), ('dualDagger', 36256, 48358)]:
        assert source.objects[root_id].read().m_Name == name
        assert source.objects[weapon_id].read().m_Name == name
        rows = walk(source.objects[root_id], '.')
        assert {r['path'] for r in rows if 'rendererId' in r} == {
            name, name + '/Break', name + '/Break/Break', 'offHandWeapon'}
        templates.append({'template': name, 'displayRootId': root_id,
                          'weaponRootId': weapon_id, 'hierarchy': rows})
    report = {'schema': 'ftkmf.thief-paired-route-metadata.v1',
              'sourceAsset': assets.name, 'sourceSha256': digest(assets),
              'assembly': assembly.name, 'assemblySha256': digest(assembly),
              'scope': 'Identity, hierarchy, components, transform values, mesh pointers and material slot pointers only. No native mesh, texture, bounds, weights or animation data is decoded.',
              'generator': Path(__file__).name, 'generatorSha256': digest(Path(__file__)),
              'templates': templates}
    (OUT / 'native-route.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: two paired templates; four rigid renderers each; no native surface reads')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--assembly', type=Path, required=True)
    args = parser.parse_args()
    inspect(args.assets, args.assembly)
