#!/usr/bin/env python3
"""One-shot continuation after a verified accepted start_run; never submits start_run."""
import argparse
import json
from pathlib import Path
from run_case import Runner,read,digest,living,validate_inventory


def checkpoint(entries):
    provenance=[e['data'] for e in entries if e['kind']=='provenance']
    binaries=[e['data'] for e in entries if e['kind']=='deployed-binaries']
    if len(provenance)!=1 or len(binaries)!=1 or provenance[0]['mode']!='new-run':raise ValueError('Exactly one pinned original new-run journal required')
    starts=[(i,e['data']) for i,e in enumerate(entries) if e['kind']=='http-request' and e['data'].get('path')=='/action' and (e['data'].get('payload') or {}).get('action')=='start_run']
    if len(starts)!=1:raise ValueError('Exactly one original start_run submission required')
    index,start=starts[0]
    results=[e['data']['result'] for e in entries if e['kind']=='http-result' and e['data']['id']==start['id']]
    if len(results)!=1 or results[0].get('ok') is not True or (results[0].get('result') or {}).get('phase')!='starting':raise ValueError('Original start_run lacks exact successful starting response; uncertain action cannot resume')
    for e in entries[index+1:]:
        if e['kind']=='helper-request' or (e['kind']=='http-request' and (e['data'].get('path')!='/state' or e['data'].get('payload') is not None)):
            raise ValueError('Later helper/action already submitted; continuation refused')
    expected={'adventure':'HollowMire','party':1}
    if provenance[0].get('class'):expected['class']=provenance[0]['class']
    if start['payload'].get('args')!=expected:raise ValueError('Original start configuration mismatch')
    if not any(e['kind']=='fresh-process-claim' for e in entries) or entries[-1]['kind']!='stopped':raise ValueError('Stopped claimed original run required')
    return provenance[0],binaries[0]


def continue_unsent(runner):
    party=runner.wait(lambda s:s.get('singlePlayer') is True and living(s),'resumed actual living party')
    runner.verify_party_class(party)
    if (party.get('dungeon') or {}).get('inDungeon') is not False or (party.get('combat') or {}).get('active') is not False:
        raise ValueError('Continuation requires actual overworld outside dungeon/combat')
    runner.helper('quiet-tutorials');runner.helper('fortify-party',{'targetMaxHp':999})
    runner.clear_intro();state=runner.state();runner.verify_party_class(state)
    if not living(state) or state.get('singlePlayer') is not True or (state.get('dungeon') or {}).get('inDungeon') is not False or (state.get('combat') or {}).get('active') is not False:
        raise ValueError('Overworld identity changed before entry')
    runner.verify_story_clear()
    runner.prepare_entry()
    runner.action('enter_dungeon',{'dungeonId':'FloodedCrypt'})
    runner.helper('stage-enemy',{'enemy':runner.a.enemy,'level':0,'room':1,'regenerate':True})
    pending=runner.wait_for_encounter()
    if pending is not None:return pending
    inventory=runner.helper('inventory',{'scope':'enemies'});matches=validate_inventory(inventory,runner.profile)
    return runner.staging_result(matches,runner.state())


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',required=True,type=Path);parser.add_argument('--port',required=True,type=int)
    parser.add_argument('--origin-journal',required=True,type=Path);parser.add_argument('--origin-sha256',required=True)
    args=parser.parse_args();origin=args.origin_journal
    if not 1<=args.port<=65535:parser.error('Valid port required')
    if any(p.is_symlink() for p in (origin,*origin.parents)) or not origin.is_file() or digest(origin)!=args.origin_sha256:raise ValueError('Exact nonsymlink origin journal hash required')
    entries=[json.loads(line) for line in origin.read_text().splitlines() if line.strip()];previous,binaries=checkpoint(entries)
    if origin.parent.parent!=args.root/'model-test-output' or previous['root']!=str(args.root) or previous['port']!=args.port:raise ValueError('Original root/port/journal ownership mismatch')
    session=read(args.root/'model-test-session.json')['session']
    if session!=previous['session'] or binaries['session']!=session:raise ValueError('Original session changed')
    marker=read(args.root/'model-test-output'/('new-run-session-'+session+'.json'))
    if marker['journal']!=str(origin) or marker['case']!=previous['case']:raise ValueError('Original fresh-process claim mismatch')
    options=argparse.Namespace(root=args.root,port=args.port,enemy=previous['enemy'],class_key=previous.get('class'),mode='resume-after-start',operation_timeout=40,wait_timeout=120)
    runner=Runner(options)
    if runner.binary_pins!=binaries['binaries'] or runner.profile!=previous['profile'] or digest(args.root/'model-test-profiles.json')!=previous['profileInputSha256']:raise ValueError('Original binary/profile pins changed')
    current=[json.loads(line) for line in runner.journal.read_text().splitlines()]
    for kind in ('profile-assets','enemy-registration','player-registration'):
        old=[e['data'] for e in entries if e['kind']==kind];new=[e['data'] for e in current if e['kind']==kind]
        if old!=new:raise ValueError('Original metadata/asset pins changed: '+kind)
    claim=origin.parent/'resume-after-start.claim.json'
    with claim.open('x') as stream:json.dump({'originSha256':args.origin_sha256,'continuationJournal':str(runner.journal),'session':session},stream)
    runner.log('resume-after-start-origin',{'path':str(origin),'sha256':args.origin_sha256,'claim':str(claim),'boundary':'one successful start, no later actions/helper requests; start_run will not be repeated'})
    try:
        if digest(origin)!=args.origin_sha256:raise ValueError('Origin changed before continuation')
        result=continue_unsent(runner)
    except Exception as error:
        result={'status':'stopped','error':str(error),'journal':str(runner.journal),'note':'No action retry. Resume claim remains; inspect original and continuation journals before further actions.'}
    runner.log('result',result)
    with (runner.output/'result.json').open('x') as stream:json.dump(result,stream,indent=2)
    print(json.dumps(result,indent=2));raise SystemExit(0 if result['status']=='binding_metadata_observed' else 1)

if __name__=='__main__':main()
