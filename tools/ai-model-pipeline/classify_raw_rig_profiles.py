#!/usr/bin/env python3
"""Classify raw rig ownership from exact native references; metadata only.

Requires inventory_skeletons.py and classify_rig_candidates.py output for the
same installation. Unknown roles remain unknown; no mesh or curves are exported.
"""

import argparse, collections, json, struct, re, subprocess, hashlib, shutil
from pathlib import Path
import UnityPy
from UnityPy.classes import PPtr

p = argparse.ArgumentParser(description=__doc__)
p.add_argument("--assets", type=Path, required=True)
p.add_argument("--inventory", type=Path, required=True)
p.add_argument("--classification", type=Path, required=True)
p.add_argument("--output", type=Path, required=True)
p.add_argument(
    "--audit",
    type=Path,
    help="Optional existing audit_skeletons JSON; hash-checked and summarized",
)
p.add_argument(
    "--ilspy",
    default=shutil.which("ilspycmd") or str(Path.home() / ".dotnet/tools/ilspycmd"),
)
args = p.parse_args()
inv = json.loads(args.inventory.read_text())
cls = json.loads(args.classification.read_text())
src = args.assets.resolve()
if not (
    hashlib.sha256(src.read_bytes()).hexdigest()
    == inv["source_sha256"]
    == cls["source_sha256"]
):
    raise ValueError("Source hash mismatch")
env = UnityPy.load(str(src))
a = next(f for f in env.files.values() if Path(f.name).name == src.name)
cache = {}


def ident(o):
    return (Path(o.assets_file.name).name, o.path_id)


def t(o):
    if o is None:
        return {}
    key = ident(o)
    if key not in cache:
        cache[key] = o.read_typetree(check_read=False)
    return cache[key]


def ref(o, pp):
    return (
        PPtr(**pp, assetsfile=o.assets_file).deref()
        if pp and pp.get("m_PathID")
        else None
    )


def components(go):
    return [ref(go, x["component"]) for x in t(go).get("m_Component", [])]


def ancestors(go):
    arr = []
    seen = set()
    while go and ident(go) not in seen:
        seen.add(ident(go))
        arr.append(go)
        tr = next(
            (
                c
                for c in components(go)
                if c.type.name in ("Transform", "RectTransform")
            ),
            None,
        )
        pa = ref(tr, t(tr).get("m_Father")) if tr else None
        go = ref(pa, t(pa).get("m_GameObject")) if pa else None
    return arr


def class_name(o):
    try:
        return t(ref(o, t(o).get("m_Script"))).get("m_ClassName")
    except FileNotFoundError:
        return None


# Explicit FTK_skinset declared prefix: ID,avatar,armor,nohelmet,helmet,backpack,boot.
dbenv = UnityPy.load(str(src.parent / "sharedassets1.assets"))
db = next(
    o
    for o in dbenv.objects
    if o.type.name == "MonoBehaviour" and class_name(o) == "FTK_skinsetDB"
)
raw = db.get_raw_data()
usage = collections.defaultdict(list)
for row in cls["skinset_rows"]:
    pos = row["db_byte_offset"]
    length = struct.unpack_from("<i", raw, pos)[0]
    if length < 0 or raw[pos + 4 : pos + 4 + length].decode() != row["skinset_id"]:
        raise ValueError("Skinset metadata offset mismatch: " + row["skinset_id"])
    at = (pos + 4 + length + 3) // 4 * 4
    for field, offset, kind in [
        ("m_Avatar", 0, "player_avatar"),
        ("m_Armor", 12, "player_body_apparel"),
        ("m_Helmet", 28, "player_headgear"),
        ("m_Backpack", 40, "player_backpack"),
        ("m_Boot", 52, "player_foot_apparel"),
    ]:
        fid, pid = struct.unpack_from("<iq", raw, at + offset)
        ob = ref(db, dict(m_FileID=fid, m_PathID=pid))
        if not ob:
            continue
        go = ob if ob.type.name == "GameObject" else ref(ob, t(ob).get("m_GameObject"))
        if go:
            usage[ident(go)].append(
                dict(
                    database_file=ident(db)[0],
                    database_component_id=db.path_id,
                    row_id=row["skinset_id"],
                    field=field,
                    referenced_object_id=pid,
                    referenced_asset_file=ident(ob)[0],
                    referenced_object_type=ob.type.name,
                    role=kind,
                )
            )
# Native item DB wearable pointers: verified inherited FTK_itembase prefix.
itemdb = next(
    o
    for o in dbenv.objects
    if o.type.name == "MonoBehaviour" and class_name(o) == "FTK_itemsDB"
)
iraw = itemdb.get_raw_data()
icode = subprocess.check_output(
    [
        args.ilspy,
        str(src.parent / "Managed/Assembly-CSharp.dll"),
        "-t",
        "GridEditor.FTK_itembase",
    ],
    text=True,
)
match = re.search(r"public enum ID\s*\{(.*?)\n\t\}", icode, re.S)
if match is None:
    raise ValueError("Native item ID enum not found")
enum = match.group(1)
itemrows = []
itemerrors = []
for entry in enum.split(","):
    entry = entry.strip()
    if not entry:
        continue
    name, value = [x.strip() for x in entry.split("=")]
    if not (0 <= int(value) < 100000):
        continue
    nd = struct.pack("<i", len(name)) + name.encode()
    offsets = [i for i in range(0, len(iraw) - len(nd), 4) if iraw.startswith(nd, i)]
    if len(offsets) != 1:
        itemerrors.append(dict(id=name, occurrences=len(offsets)))
        continue
    pos = offsets[0]
    at = (pos + len(nd) + 3) // 4 * 4
    at += 24  # rarity/min/max/gold/bool/stock
    ptrs = {}
    for field in [
        "_icon",
        "_iconNonClickable",
        "_prefab",
        "m_WearablePrefab",
        "m_WearablePrefabM",
    ]:
        fid, pid = struct.unpack_from("<iq", iraw, at)
        at += 12
        ptrs[field] = dict(m_FileID=fid, m_PathID=pid)
    at += 24  # six individually aligned bool fields
    n = struct.unpack_from("<i", iraw, at)[0]
    at = (at + 4 + n + 3) // 4 * 4
    at += 8  # backpackEquip,cursed
    objtype, slot = struct.unpack_from("<ii", iraw, at)
    if not -1 <= objtype <= 15:
        raise ValueError("Invalid item prefix " + name)
    itemrows.append(dict(id=name, offset=pos, object_type=objtype))
    role = {
        6: "item_body_apparel",
        7: "item_headgear",
        9: "item_foot_apparel",
        5: "item_shield",
        4: "item_weapon",
    }.get(objtype, "item_other_wearable")
    for field in ["m_WearablePrefab", "m_WearablePrefabM"]:
        ob = ref(itemdb, ptrs[field])
        if ob:
            if ob.type.name != "GameObject":
                raise ValueError("Wearable not GameObject " + name)
            usage[ident(ob)].append(
                dict(
                    database_file=ident(itemdb)[0],
                    database_component_id=itemdb.path_id,
                    row_id=name,
                    field=field,
                    referenced_object_id=ob.path_id,
                    referenced_asset_file=ident(ob)[0],
                    referenced_object_type=ob.type.name,
                    role=role,
                )
            )
if itemerrors:
    raise ValueError("Missing or ambiguous native item rows " + str(itemerrors))
if struct.unpack_from("<i", iraw, min(r["offset"] for r in itemrows) - 4)[0] != len(
    itemrows
):
    raise ValueError("Item header count mismatch")
known = {r["rig_profile_fingerprint"] for r in inv["candidate_profiles"]}
if known != {r["rig_profile_fingerprint"] for r in cls["profiles"]}:
    raise ValueError("CEL classification profile set does not match inventory")
if struct.unpack_from(
    "<i", raw, min(r["db_byte_offset"] for r in cls["skinset_rows"]) - 4
)[0] != len(cls["skinset_rows"]):
    raise ValueError("Skinset header count mismatch")
records = []
roleclasses = {
    "Armor": "body_apparel_component",
    "Helmet": "headgear_component",
    "Weapon": "weapon_component",
    "Shield": "shield_component",
    "Diorama": "diorama_object",
    "DioramaDungeon": "diorama_object",
    "DioramaBoat": "diorama_object",
    "MiniHexInfo": "map_object",
    "MiniHexEnemy": "map_enemy_object",
    "MiniHexDungeon": "map_dungeon_object",
    "BoatPrefab": "boat_prefab_component",
    "AirShip": "airship_component",
    "MiniHexAirShip": "map_airship_component",
    "DungeonEncounterBase": "dungeon_encounter_object",
}
for r in inv["renderers"]:
    ob = a.objects[r["renderer_path_id"]]
    go = ref(ob, t(ob).get("m_GameObject"))
    chain = ancestors(go)
    ce = []
    ue = []
    for anc in chain:
        ue.extend(usage.get(ident(anc), []))
        for co in components(anc):
            if co.type.name == "MonoBehaviour":
                cn = class_name(co)
                ce.append(
                    dict(
                        component_class=cn,
                        component_id=co.path_id,
                        game_object_id=anc.path_id,
                        game_object_name=t(anc).get("m_Name"),
                    )
                )
    roles = sorted(
        {u["role"] for u in ue}
        | {
            roleclasses[c["component_class"]]
            for c in ce
            if c["component_class"] in roleclasses
        }
    )
    fp = r.get("rig_profile_fingerprint")
    category = (
        "existing_CEL_profile"
        if fp in known
        else ("no_profile" if not fp else "additional_raw_profile")
    )
    if r.get("character_event_listeners"):
        roles.append("character_event_listener_ancestor")
    records.append(
        dict(
            renderer_id=r["renderer_path_id"],
            renderer_name=r["renderer_name"],
            ancestor_names=r["ancestor_names"],
            profile=fp,
            scope_category=category,
            joint_count=r.get("joint_count"),
            mesh_id=r.get("mesh_path_id"),
            errors=r.get("errors", []),
            positive_roles=roles or ["unknown"],
            ancestor_components=ce,
            usage_references=ue,
            no_profile_reason=(
                "missing_mesh"
                if not r.get("mesh_path_id")
                else "bone_bindpose_count_mismatch"
                if r.get("joint_count") != r.get("bindpose_count")
                else "no_skinned_palette_or_bindposes"
            )
            if not fp
            else None,
        )
    )
profiles = []
for fp in sorted({r["profile"] for r in records if r["profile"]}):
    rr = [r for r in records if r["profile"] == fp]
    profiles.append(
        dict(
            profile=fp,
            scope_category=rr[0]["scope_category"],
            renderer_ids=[r["renderer_id"] for r in rr],
            representative_renderer_id=rr[0]["renderer_id"],
            positive_roles=sorted({v for r in rr for v in r["positive_roles"]}),
        )
    )
extra = [r for r in records if r["scope_category"] == "additional_raw_profile"]
summary = dict(
    raw_renderers=len(records),
    all_profiles=len(profiles),
    CEL_profiles=len(known),
    recognized_native_item_rows=len(itemrows),
    additional_profiles=sum(
        p["scope_category"] == "additional_raw_profile" for p in profiles
    ),
    additional_renderers=len(extra),
    additional_role_sets=dict(
        collections.Counter("|".join(r["positive_roles"]) for r in extra)
    ),
    no_profile_renderers=[r["renderer_id"] for r in records if not r["profile"]],
)
result = dict(
    source_file=str(src),
    source_sha256=inv["source_sha256"],
    evidence_inputs={
        str(path.resolve()): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in [
            args.inventory,
            args.classification,
            src.parent / "sharedassets1.assets",
            src.parent / "Managed/Assembly-CSharp.dll",
        ]
    },
    summary=summary,
    profiles=profiles,
    renderers=records,
    limitations=[
        "Roles come only from exact component ancestry or declared FTK_skinset/FTK_items PPtrs; names never classify usage.",
        "Known component absence is not proof unused; unknown remains counted.",
        "Component ownership does not prove a live spawnpath, rigquality or framework support.",
        "Existing_CEL_profile classification is exact fingerprint equivalence, not equal meshidentity.",
    ],
)
extra_profile_roles = collections.Counter()
for pr in profiles:
    if pr["scope_category"] != "additional_raw_profile":
        continue
    roles = set(pr["positive_roles"]) - {"unknown"}
    role = (
        "unknown_only"
        if not roles
        else (
            "apparel_evidence"
            if any("apparel" in v for v in roles)
            else "noncharacter_component_evidence"
        )
    )
    extra_profile_roles[role] += 1
summary["additional_profile_role_counts"] = dict(extra_profile_roles)
summary["non_CEL_renderers_sharing_existing_CEL_profile"] = sum(
    not inv["renderers"][i].get("character_event_listeners")
    and r["scope_category"] == "existing_CEL_profile"
    for i, r in enumerate(records)
)
if args.audit is not None:
    audit = json.loads(args.audit.read_text())
    if audit["source_sha256"] != inv["source_sha256"]:
        raise ValueError("Audit source hash mismatch")
    result["offline_roundtrip_evidence"] = dict(
        path=str(args.audit.resolve()),
        sha256=hashlib.sha256(args.audit.read_bytes()).hexdigest(),
        summary=audit["summary"],
        claim="Native roundtrip only; no custom-art,live-animation or ownership inference.",
    )
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps(summary, indent=2))
