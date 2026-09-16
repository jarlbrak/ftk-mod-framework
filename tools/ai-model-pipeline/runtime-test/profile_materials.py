"""Strict additive enemy material-slot descriptors and file pin enumeration."""
import re


def slots(renderer):
    kind=renderer.get('rendererKind','SkinnedMeshRenderer')
    if kind not in ('SkinnedMeshRenderer','MeshRenderer'):raise ValueError('Unsupported renderer kind')
    if kind=='MeshRenderer' and 'materialSlots' in renderer:raise ValueError('Static MeshRenderer assignments do not support material slots')
    if 'materialSlots' not in renderer:return None
    if 'textureFile' in renderer or 'disableNativeEmission' in renderer:
        raise ValueError('Per-slot mode forbids renderer texture/emission fields')
    values=renderer['materialSlots']
    if not isinstance(values,list) or not 2<=len(values)<=4:raise ValueError('Expected2..4 material slots')
    primitive=set();native=set()
    for value in values:
        if not isinstance(value,dict) or set(value)-{'primitiveIndex','nativeMaterialSlot','textureFile','disableNativeEmission'}:
            raise ValueError('Unknown slot fields')
        for key,seen in [('primitiveIndex',primitive),('nativeMaterialSlot',native)]:
            index=value.get(key)
            if type(index) is not int or not 0<=index<len(values) or index in seen:raise ValueError('Slot mapping must be a bijection')
            seen.add(index)
        if 'disableNativeEmission' in value and type(value['disableNativeEmission']) is not bool:raise ValueError('Emission option must be boolean')
        if 'textureFile' in value:asset_name(value['textureFile'],'.png')
    return values


def asset_name(name,extension):
    if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z0-9_-][A-Za-z0-9_.-]{0,119}'+re.escape(extension),name):
        raise ValueError('Invalid profile asset basename')
    return name


def asset_names(profile):
    names=[]
    for renderer in profile['renderers']:
        names.append(asset_name(renderer['glbFile'],'.glb'))
        options=slots(renderer)
        if options is None:
            if renderer.get('textureFile') is not None:names.append(asset_name(renderer['textureFile'],'.png'))
        else:
            names.extend(asset_name(slot['textureFile'],'.png') for slot in options if slot.get('textureFile') is not None)
    return sorted(set(names))


def validate_observation(observed,renderer,assignment,first=False):
    """Identity/slot coverage only, never animation or visual acceptance."""
    expected=slots(assignment)
    if expected is None:raise ValueError('Explicit slot profile required')
    if not isinstance(observed,dict):raise ValueError('Requested material observation absent')
    for field,source in [('rendererInstanceId','instanceId'),('ownerInstanceId','ownerInstanceId'),('celInstanceId','celInstanceId')]:
        if observed.get(field)!=renderer.get(source):raise ValueError('Material observation owner/renderer changed')
    if observed.get('celRelativeRendererPath')!=assignment['rendererPath']:raise ValueError('Material observation path changed')
    current=observed.get('slots')
    if not isinstance(current,list) or len(current)!=len(expected):raise ValueError('Incomplete material slot observation')
    ids=[]
    for index,item in enumerate(current):
        if item.get('slot')!=index or type(item.get('instanceId')) is not int or item['instanceId']==0 or item.get('inCurrentLeaseResources') is not True:
            raise ValueError('Material slot identity/ownership unavailable')
        ids.append(item['instanceId'])
    if len(set(ids))!=len(ids):raise ValueError('Expected distinct private native slot materials')
    for option in expected:
        item=current[option['nativeMaterialSlot']]
        if 'textureFile' in option:
            texture=item.get('_MainTex') or {}
            if texture.get('name')!='ftkmf_'+option['textureFile'] or texture.get('inCurrentLeaseResources') is not True:
                raise ValueError('Configured slot texture not observed as owned resource')
        if option.get('disableNativeEmission',False):
            if item.get('emissionKeyword') is not False:raise ValueError('Requested slot emission disable not observed')
            if item.get('_EmissionMapSupported') and item.get('_EmissionMap') is not None:raise ValueError('Emission map still present')
            if 'emissionColor' in item and item['emissionColor'][:3]!=[0,0,0]:raise ValueError('Emission color not black')
    lease=observed.get('lease') or {}
    if lease.get('acquired') is not True or lease.get('applied') is not True:raise ValueError('Acquired applied lease required')
    if first:
        geometry=observed.get('geometry') or {}
        if geometry.get('submeshCount')!=len(expected) or len(geometry.get('submeshes',[]))!=len(expected):raise ValueError('Actual submesh geometry missing')
        for index,part in enumerate(geometry['submeshes']):
            if part.get('nativeMaterialSlot')!=index or type(part.get('indices')) is not int or part['indices']<3 or part['indices']%3:
                raise ValueError('Nonempty actual triangle submesh required')
