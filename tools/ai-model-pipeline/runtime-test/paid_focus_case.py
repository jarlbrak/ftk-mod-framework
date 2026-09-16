#!/usr/bin/env python3
"""One native paid focus slot; file protocol only, no attack or retries."""
import argparse
import hashlib
import json
import math
import re
import time
import uuid
from pathlib import Path



def read(path):
    return json.loads(path.read_text())


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


DEPLOYED_BINARIES = {'framework':'BepInEx/plugins/FTKModFramework.dll',
                     'helper':'BepInEx/plugins/FtkRuntimeModelTest.dll',
                     'content':'BepInEx/plugins/FtkRuntimeModelTestContent.dll'}


def measured_binaries(root):
    result={}
    for name,relative in DEPLOYED_BINARIES.items():
        path=root/relative
        if any(p.is_symlink() for p in (path,*path.parents)) or not path.is_file():
            raise RuntimeError('Missing or symlink deployed binary: '+relative)
        before=path.stat()
        if not 0<before.st_size<=64*1024*1024:raise RuntimeError('Invalid deployed binary size: '+relative)
        sha=digest(path);after=path.stat()
        if (before.st_dev,before.st_ino,before.st_size,before.st_mtime_ns)!=(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns):
            raise RuntimeError('Deployed binary changed during measurement: '+relative)
        result[name]={'path':str(path),'relativePath':relative,'sha256':sha,'bytes':after.st_size}
    return result

def require(ok, message):
    if not ok:raise RuntimeError(message)


class PaidFocusCase:
    def __init__(self, args):
        self.a=args;self.root=args.root.resolve(strict=True)
        require(args.root.is_absolute() and self.root==args.root.absolute() and self.root.parent.name=='scratch'
                and not any(p.is_symlink() for p in (args.root,*args.root.parents)), 'Exact nonsymlink owned root under scratch required')
        self.session=args.session;self.binaries=measured_binaries(self.root)
        require(self.binaries['helper']['sha256']==args.helper_sha256,'Explicit helper hash mismatch')
        self.sources={str(Path(__file__).resolve()):digest(Path(__file__).resolve())}
        self.check_inputs()
        self.case=uuid.uuid4().hex;self.output=self.root/'model-test-output'/('paid-focus-case-'+self.case);self.output.mkdir(exist_ok=False)
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
        temporary=self.root/('paid-focus-command-'+identifier+'.tmp')
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

    def identity(self,state,initial=False):
        require(state.get('ok') is True,'Paid-focus read rejected')
        identity=state.get('identity') or {};pins=state.get('pins') or {}
        require(identity.get('root')==str(self.root) and identity.get('session')==self.session,'Native root/session mismatch')
        require(type(identity.get('level')) is int and type(identity.get('room')) is int and identity.get('level')==self.a.level and identity.get('room')==self.a.room,'Expected room changed')
        for key in ('heroId','dungeonId','esId','mcId','dummyId','celId','targetId','targetCelId','stanceId','buttonId','slotsId','hudId','weaponInstanceId'):
            require(type(identity.get(key)) is int and identity[key]!=0,'Missing native identity: '+key)
        require(type(identity.get('turn')) is int and identity['turn']>0,'Missing native combat turn')
        for bucket,name in [('helper','helper'),('core','framework')]:
            require(pins.get(bucket,{}).get('assemblyFileSha256')==self.binaries[name]['sha256'],'Observed binary pin mismatch: '+bucket)
        for field in ('availableFocus','spentFocus','frame','animationCount'):
            require(type(state.get(field)) is int and state[field]>=0,'Missing integer '+field)
        if initial:
            require(state.get('alreadySubmitted') is False,'Native turn already submitted')
            for gate in ('scope','noModal','normalAttack','inputReady','canPay','stanceReady'):
                require(state.get(gate) is True,'Native payment gate unavailable: '+gate)
            require(state.get('focusing') is False and state.get('interrupt') is False and state['animationCount']==0,'Native focus already pending')
            require(isinstance(state.get('ticketId'),str) and re.fullmatch('[a-f0-9]{32}',state['ticketId']),'Missing observation ticket')
        return identity

    def payment(self,operation,initial,submission_id,current):
        require(isinstance(operation,dict) and operation.get('submissionId')==submission_id,'Wrong native payment submission identity')
        require(operation.get('claimRetained') is True,'Native claim missing')
        status=operation.get('status')
        require(status in ('submitted','payment-complete'),'Native payment stopped/unproven: '+str(status))
        if status=='submitted':return False
        require(operation.get('error') is None and type(operation.get('callbackCount')) is int and operation.get('callbackCount')==1 and operation.get('callbackEvidenceTruncated') is False,'Invalid/duplicate callback evidence')
        baseline=operation.get('beforeSubmission') or {};before=operation.get('callbackBefore') or {};after=operation.get('callbackAfter') or {}
        for snapshot in (baseline,before,after):require(snapshot.get('identity')==initial['identity'],'Callback ownership/turn/profile changed')
        require(baseline.get('pins')==initial['pins'],'Submission binary pins changed')
        for key in ('availableFocus','spentFocus'):
            require(type(baseline.get(key)) is int and type(before.get(key)) is int and baseline[key]==initial[key]==before.get(key),'Callback baseline changed: '+key)
            require(type(after.get(key)) is int,'Missing callback payment integer')
        require(after['availableFocus']==before['availableFocus']-1 and after['spentFocus']==before['spentFocus']+1,'Native callback did not pay exactly one slot')
        require(before.get('focusing') is True and before.get('interrupt') is False and after.get('focusing') is False,'Native focusing/interrupt mismatch')
        for snapshot in (before,after):
            for gate in ('scope','normalAttack','stanceReady'):require(snapshot.get(gate) is True,'Callback native scope changed')
        callbacks=operation.get('callbacks');require(isinstance(callbacks,list) and len(callbacks)==1,'Exact raw callback required')
        callback=callbacks[0]
        require(type(callback.get('index')) is int and callback.get('index')==1 and callback.get('previousStatus')=='submitted' and callback.get('classification')=='payment-complete'
                and callback.get('observationError') is None and callback.get('nativeException') is None and callback.get('before')==before and callback.get('after')==after,'Raw callback evidence mismatch')
        frames=[initial['frame'],baseline.get('frame'),before.get('frame'),after.get('frame'),current['frame']]
        require(all(type(x) is int for x in frames) and frames==sorted(frames),'Payment chronology changed')
        require(current['availableFocus']==after['availableFocus'] and current['spentFocus']==after['spentFocus'],'Payment later changed/refunded')
        return True

    def run(self):
        out={'status':'stopped','terminal':False,'submission':None,'errors':[],'journal':str(self.journal),'attackSubmitted':False}
        try:
            started=time.monotonic();state=self.helper('paid-focus-state');identity=self.identity(state,initial=True);out['before']=state
            key=hashlib.sha256(json.dumps([identity[k] for k in ('root','session','dungeonId','level','room','esId','mcId','turn','heroId')],separators=(',',':')).encode()).hexdigest()
            claim=self.root/'model-test-output'/('paid-focus-claim-'+key+'.json')
            with claim.open('x') as stream:json.dump({'case':self.case,'journal':str(self.journal),'identity':identity},stream)
            self.log('permanent-claim',{'path':str(claim),'sha256':digest(claim)})
            require(time.monotonic()-started<4,'Observation roundtrip exceeded safe ticket window; no focus submitted')
            submit=self.helper('paid-focus-submit',{'ticketId':state['ticketId'],'identity':identity});out['submission']=submit
            require(submit.get('ok') is True,'Native submission not confirmed; no further commands')
            operation=submit.get('operation') or {};submission_id=submit['id']
            require(operation.get('submissionId')==submission_id and operation.get('status') in ('submitted','payment-complete'),'Native submission unproven; no further commands')
            deadline=time.monotonic()+self.a.outcome_timeout
            while True:
                self.check_inputs()
                if time.monotonic()>=deadline:out['status']='observation_timeout_payment_pending';break
                current=self.helper('paid-focus-state');out['lastState']=current
                require(self.identity(current)==identity and current['pins']==state['pins'],'Observed native ownership/turn/pins changed')
                paid=self.payment(current.get('operation'),state,submission_id,current)
                if paid and current.get('focusing') is False and current['animationCount']==0:
                    require(current.get('interrupt') is False and all(current.get(g) is True for g in ('scope','normalAttack','stanceReady','inputReady','noModal')),'Current hero not ready after payment')
                    out.update(status='native_payment_and_hero_ready_observed',terminal=True);break
                time.sleep(.1)
        except Exception as error:
            out['errors'].append(str(error))
            if self.pending is not None:out.update(status='observation_pending_command_uncertain',pending=self.pending.copy())
            self.log('stopped',{'error':str(error),'pending':self.pending,'retry':False})
        path=self.output/'paid-focus-case-result.json'
        with path.open('x') as stream:json.dump(out,stream,indent=2);stream.write('\n')
        return path,out


def arguments(argv=None):
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',required=True,type=Path);p.add_argument('--session',required=True)
    p.add_argument('--level',required=True,type=int);p.add_argument('--room',required=True,type=int)
    p.add_argument('--catalog-sha256',required=True);p.add_argument('--helper-sha256',required=True)
    p.add_argument('--outcome-timeout',type=float,default=30)
    a=p.parse_args(argv)
    if not a.root.is_absolute() or min(a.level,a.room)<0 or not re.fullmatch('[a-f0-9]{32}',a.session):p.error('Absolute root/exact session/nonnegative room required')
    if any(not re.fullmatch('[a-f0-9]{64}',v) for v in (a.catalog_sha256,a.helper_sha256)):p.error('Exact lowercase SHA256 pins required')
    if not math.isfinite(a.outcome_timeout) or not 1<=a.outcome_timeout<=300:p.error('Finite outcome timeout1..300 required')
    return a


if __name__=='__main__':
    path,result=PaidFocusCase(arguments()).run();print(path);raise SystemExit(0 if result['terminal'] else 2)
