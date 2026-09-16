#!/usr/bin/env python3
"""One native trap choice; file protocol only, no retries or forced outcomes."""
import argparse
import hashlib
import json
import math
import re
import time
import uuid
from pathlib import Path
from run_case import read, digest, measured_binaries


def require(ok, message):
    if not ok:raise RuntimeError(message)


class TrapCase:
    def __init__(self, args):
        self.a=args;self.root=args.root.resolve(strict=True)
        require(args.root.is_absolute() and self.root==args.root.absolute() and self.root.parent.name=='scratch'
                and not any(p.is_symlink() for p in (args.root,*args.root.parents)), 'Exact nonsymlink owned root under scratch required')
        self.session=args.session;self.binaries=measured_binaries(self.root)
        require(self.binaries['helper']['sha256']==args.helper_sha256,'Explicit helper hash mismatch')
        self.sources={str(Path(__file__).resolve()):digest(Path(__file__).resolve()),
                      str(Path(__file__).resolve().with_name('run_case.py')):digest(Path(__file__).resolve().with_name('run_case.py'))}
        self.check_inputs()
        self.case=uuid.uuid4().hex;self.output=self.root/'model-test-output'/('trap-case-'+self.case);self.output.mkdir(exist_ok=False)
        self.journal=self.output/'journal.jsonl';self.pending=None
        self.log('pins',{'root':str(self.root),'session':self.session,'catalogSha256':args.catalog_sha256,
                        'binaries':self.binaries,'sources':self.sources,'measurement':'on_disk_binary_sha256, not loaded memory'})

    def log(self,kind,data):
        with self.journal.open('a') as stream:stream.write(json.dumps({'kind':kind,'monotonic':time.monotonic(),'data':data})+'\n')

    def check_inputs(self):
        for path in (self.root,self.root/'model-test-output'):
            require(not any(p.is_symlink() for p in (path,*path.parents)),'Owned output/root ancestry changed')
        for p in [self.root/'model-test-session.json',self.root/'model-test-profiles.json']:
            require(p.is_file() and not p.is_symlink(),'Missing/symlink input')
        require(read(self.root/'model-test-session.json')['session']==self.session,'Session changed')
        require(digest(self.root/'model-test-profiles.json')==self.a.catalog_sha256,'Catalog changed')
        require(measured_binaries(self.root)==self.binaries,'Binary pins changed')
        for path,sha in self.sources.items():require(digest(Path(path))==sha,'Runner source changed')

    def helper(self,op,payload=None):
        self.check_inputs();require(self.pending is None,'Unresolved helper command; do not submit another')
        command_path=self.root/'model-test-command.json'
        if command_path.exists():
            require(not command_path.is_symlink(),'Symlink helper command')
            old=read(command_path);identifier=old.get('id')
            require(isinstance(identifier,str) and re.fullmatch('[A-Za-z0-9_-]{1,80}',identifier),'Malformed previous command')
            require(old.get('session')!=self.session or (self.root/'model-test-output'/(identifier+'.json')).is_file(),'Another helper command remains pending')
        identifier=uuid.uuid4().hex;command=dict(payload or {},id=identifier,session=self.session,op=op)
        request=self.output/(identifier+'-request.json')
        with request.open('x') as stream:stream.write(json.dumps(command,indent=2)+'\n')
        result=self.root/'model-test-output'/(identifier+'.json')
        require(not result.exists() and not result.is_symlink(),'New command result path already exists')
        self.log('command-intent',{'requestPath':str(request),'requestSha256':digest(request),'resultPath':str(result),'op':op})
        # Permanent pending identity before delivery; timeout or write uncertainty never permits another command.
        self.pending={'id':identifier,'resultPath':str(result),'op':op}
        temporary=self.root/('trap-command-'+identifier+'.tmp')
        with temporary.open('x') as stream:json.dump(command,stream)
        temporary.replace(command_path)
        deadline=time.monotonic()+40
        while True:
            self.check_inputs()
            if result.exists():
                require(not result.is_symlink(),'Symlink helper result')
                sha=digest(result);raw=read(result);require(digest(result)==sha,'Helper result changed during observation')
                require(raw.get('id')==identifier and raw.get('session')==self.session,'Helper response identity mismatch')
                self.log('command-result',{'path':str(result),'sha256':sha,'result':raw})
                self.pending=None
                return raw
            if time.monotonic()>=deadline:raise TimeoutError('Helper result uncertain; retain exact pending ID, never retry')
            time.sleep(.05)

    def state_identity(self,state):
        identity=state.get('identity') or {}
        require(identity.get('root')==str(self.root) and identity.get('session')==self.session,'Trap root/session mismatch')
        require(identity.get('level')==self.a.level and identity.get('room')==self.a.room,'Expected trap room changed')
        require((identity.get('pins') or {}).get('helper',{}).get('assemblyFileSha256')==self.a.helper_sha256,'Observed helper pin mismatch')
        require((identity.get('pins') or {}).get('core',{}).get('assemblyFileSha256')==self.binaries['framework']['sha256'],'Observed framework pin mismatch')
        for key in ('heroId','dungeonId','trapId'):
            require(type(identity.get(key)) is int and identity[key]!=0,'Native instance identity missing: '+key)
        require(state.get('alreadySubmitted') is False,'Trap already submitted')
        for gate in ('scope','noModal','nativeTrap','heroQueue','nativeVote','trapActive'):
            require(state.get(gate) is True,'Native trap guard unavailable: '+gate)
        require((state.get('buttons') or {}).get(self.a.option,{}).get('usable') is True,'Requested native button unavailable')
        require(isinstance(state.get('ticketId'),str) and re.fullmatch('[a-f0-9]{32}',state['ticketId']),'Missing exact native observation ticket')
        return identity

    def run(self):
        out={'status':'stopped','terminal':False,'submission':None,'errors':[],'journal':str(self.journal)}
        try:
            # Excludes option, command and button; retains claim even if the process loses the response.
            key=hashlib.sha256(json.dumps([self.session,self.a.level,self.a.room]).encode()).hexdigest()
            claim=self.root/'model-test-output'/('trap-claim-'+key+'.json')
            with claim.open('x') as stream:json.dump({'case':self.case,'journal':str(self.journal)},stream)
            self.log('permanent-claim',{'path':str(claim)})
            started=time.monotonic();state=self.helper('trap-state');require(state.get('ok') is True,'Trap read rejected')
            identity=self.state_identity(state)
            # Conservative local bound includes state roundtrip; native helper additionally enforces realtime/frame expiry.
            require(time.monotonic()-started<4,'Ticket roundtrip exceeded safe submission window; no choice submitted')
            result=self.helper('trap-submit',{'ticketId':state['ticketId'],'identity':identity,'option':self.a.option})
            out['submission']=result
            require(result.get('ok') is True and result.get('status')=='submitted','Native submission not confirmed; no further command')
            deadline=time.monotonic()+self.a.outcome_timeout
            while True:
                self.check_inputs()
                if time.monotonic()>=deadline:
                    out['status']='observation_timeout_outcome_pending';break
                fixture=self.helper('fixture-state');out['lastFixture']=fixture
                require(fixture.get('ok') is True and (fixture.get('identity') or {}).get('root')==str(self.root)
                        and fixture['identity'].get('session')==self.session,'Fixture identity unavailable/changed')
                surfaces=fixture.get('voteSurfaces')
                require(isinstance(surfaces,list) and len(surfaces)==1 and surfaces[0].get('heroInstanceId')==identity['heroId']
                        and surfaces[0].get('alive') is True,'Actual hero ownership/living status changed during outcome observation')
                dungeon=fixture.get('dungeon') or {};ready=fixture.get('strictReady') or {}
                require(dungeon.get('level')==self.a.level,'Dungeon level changed unexpectedly')
                require(dungeon.get('room') in (self.a.room,self.a.room+1),'Dungeon room changed unexpectedly')
                if ready.get('ok') is True:
                    require(ready.get('level')==self.a.level and ready.get('room')==self.a.room+1 and dungeon.get('room')==self.a.room+1,'Ready without expected native room progress')
                    out.update(status='native_ready_observed',terminal=True);break
                time.sleep(.25)
        except Exception as error:
            out['errors'].append(str(error))
            if self.pending is not None:
                out['status']='observation_pending_command_uncertain';out['pending']=self.pending.copy()
            self.log('stopped',{'error':str(error),'pending':self.pending,'retry':False})
        path=self.output/'trap-case-result.json'
        with path.open('x') as stream:json.dump(out,stream,indent=2);stream.write('\n')
        return path,out


def arguments(argv=None):
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',required=True,type=Path);p.add_argument('--session',required=True)
    p.add_argument('--option',required=True,choices=['Disarm','Proceed']);p.add_argument('--level',required=True,type=int);p.add_argument('--room',required=True,type=int)
    p.add_argument('--catalog-sha256',required=True);p.add_argument('--helper-sha256',required=True)
    p.add_argument('--outcome-timeout',type=float,default=180)
    a=p.parse_args(argv)
    if not a.root.is_absolute() or a.level<0 or a.room<0 or not re.fullmatch('[a-f0-9]{32}',a.session):p.error('Absolute root, exact session, nonnegative indices required')
    if any(not re.fullmatch('[a-f0-9]{64}',s) for s in (a.catalog_sha256,a.helper_sha256)):p.error('Exact lowercase catalog/helper SHA256 required')
    if not math.isfinite(a.outcome_timeout) or not 1<=a.outcome_timeout<=1800:p.error('Finite outcome timeout1..1800 required')
    return a


if __name__=='__main__':
    path,result=TrapCase(arguments()).run();print(path);raise SystemExit(0 if result['terminal'] else 2)
