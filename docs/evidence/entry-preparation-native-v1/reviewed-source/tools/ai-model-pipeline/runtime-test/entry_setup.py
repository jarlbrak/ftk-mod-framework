"""Once-only native pre-entry discovery, with guarded story page progression."""
import re
import time
import story_setup as story

PINS=('heroInstanceId','dungeonInstanceId','hexInstanceId','movementInstanceId','gameDefinitionIdentity')

def identity(state):
    values=tuple(state.get(k) for k in PINS)
    story.require(all(type(v) is int and v!=0 for v in values),'Missing exact entry owner pins')
    return values

class Pages:
    def __init__(self,runner):
        self.runner=runner;self.pin=None;self.frame=-1;self.pending=None;self.previous=None;self.submitted=set()

    def observe(self,state):
        pin=story.scope(state)
        if self.pin is None:self.pin=pin
        story.require(pin==self.pin,'Native story owner changed during entry preparation')
        frame=state.get('frame')
        story.require(type(frame) is int and frame>self.frame,'Fresh native story frame required during entry preparation')
        self.frame=frame
        if self.pending is not None and story.progressed(self.pending,state):self.pending=None
        if state.get('actionableSurface') is not None and self.pending is None:
            if self.previous is not None:story.require(story.progressed(self.previous,state),'Story page did not advance')
            request=story.submission(state);key=story.page_key(request)
            story.require(key not in self.submitted and len(self.submitted)<64,'Story page repeated or64-page budget exceeded')
            self.submitted.add(key);self.pending=request;self.previous=request
            result=self.runner.helper('story-submit',request)
            story.require(result.get('status')=='native-story-page-submitted' and result.get('surface')==request['surface'],
                          'Unverified story submission; no retry')
            story.require(story.submission(result.get('before') or {})==request,'Story submission identity changed')
            self.runner.log('entry-story-page-submitted',{'request':request,'result':result})
            return False
        return story.complete(state) and self.pending is None

def ticket(state,key):
    value=state.get('ticket') or {}
    story.require(value.get('ticketId')==key and value.get('valid') is True,'Entry ticket invalid or changed')
    phase=value.get('phase')
    story.require(phase in ('position-submitted','discover-submitted'),'Entry ticket uncertain or unsupported phase')
    return value

def ready(state,key):
    value=ticket(state,key); continuation=value.get('continuation') or {}
    return (value.get('entryReady') is True and value.get('discoverSubmitted') is True
        and value.get('sameStoredCheckContinuation') is True
        and all(state.get(k) is True for k in ('atTargetHex','expectedQuestRegistered','expectedQuestDefinition','expectedQuestDestination'))
        and type(value.get('callbackCount')) is int and value['callbackCount']==1
        and type(continuation.get('waitCount')) is int and continuation['waitCount']==0 and continuation.get('local') is True
        and continuation.get('waitClients')=='Self' and story.complete(state.get('story') or {}))

def prepare(runner):
    initial=runner.helper('entry-preparation-state');pins=identity(initial)
    initial_story=initial.get('story') or {}; initial_story_pin=story.scope(initial_story)
    story.require(initial.get('ticket') is None and initial.get('positionEligible') is True,
                  'Native pre-entry positioning ineligible or already submitted')
    story.require(story.complete(initial_story),'Initial native story incomplete before positioning')
    result=runner.helper('entry-position',dict(zip(PINS,pins)))
    key=result.get('ticketId')
    story.require(result.get('status')=='native-position-submitted' and isinstance(key,str)
                  and re.fullmatch('[a-f0-9]{32}',key) is not None,'Unverified entry position submission; no retry')
    story.require(identity(result.get('before') or {})==pins,'Entry position owner changed')
    runner.log('entry-position-submitted',result)
    deadline=time.monotonic()+runner.a.wait_timeout;pages=Pages(runner);discovered=False
    pages.pin=initial_story_pin;pages.frame=initial_story.get('frame',-1)
    while time.monotonic()<deadline:
        state=runner.helper('entry-preparation-state',{'ticketId':key})
        story.require(identity(state)==pins,'Entry owner pins changed')
        value=ticket(state,key)
        story.require(value.get('discoverSubmitted') is discovered,'Unexpected native discovery submission')
        runner.log('entry-preparation-observation',state)
        quiet=pages.observe(state.get('story') or {})
        if not discovered and quiet and state.get('discoveryEligible') is True:
            discovered=True # Mark intent before exactly one potentially uncertain call.
            response=runner.helper('entry-discover',{'ticketId':key})
            story.require(response.get('status')=='native-discovery-submitted','Unverified native discovery; no retry')
            story.require(identity(response.get('before') or {})==pins,'Native discovery owner changed')
            runner.log('entry-discovery-submitted',response)
        elif discovered and quiet and ready(state,key):
            return {'ticketId':key,'pins':pins,'frame':state['story']['frame'],'storyPin':pages.pin}
        time.sleep(.25)
    raise TimeoutError('Native pre-entry discovery did not complete; no action retried')

def verify(runner,prepared):
    state=runner.helper('entry-preparation-state',{'ticketId':prepared['ticketId']})
    native_story=state.get('story') or {}
    story.require(identity(state)==prepared['pins'] and story.scope(native_story)==prepared['storyPin'],
                  'Native entry identity changed before dungeon entry')
    story.require(type(native_story.get('frame')) is int and native_story['frame']>prepared['frame'],
                  'Fresh pre-entry completion observation required')
    story.require(ready(state,prepared['ticketId']),'Native entry preparation no longer complete')
    runner.log('entry-preparation-verified-before-entry',state)
    return state
