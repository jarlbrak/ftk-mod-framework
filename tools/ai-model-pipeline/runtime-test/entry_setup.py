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
        self.submitted.update(getattr(runner,'story_page_keys',()))

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

def wait_position_eligible(runner):
    """Await only the source-known Visit→Dungeon startup handoff; never move to force it."""
    deadline=time.monotonic()+runner.a.wait_timeout
    pins=None;pages=Pages(runner);seen_dungeon=False;quest_ids={}
    while time.monotonic()<deadline:
        state=runner.helper('entry-preparation-state')
        observed=identity(state)
        if pins is None:pins=observed
        story.require(observed==pins,'Entry owner pins changed before positioning')
        story.require(state.get('ticket') is None,'Entry positioning already submitted; no retry')
        definition=state.get('currentQuestDefinition');quest_id=state.get('currentQuestId')
        story.require(definition in ('ftkmf_hollowmire_arrive','ftkmf_hollowmire_crypt')
                      and type(quest_id) is int and quest_id!=0 and state.get('expectedQuestRegistered') is True,
                      'Unexpected or unregistered current startup quest; no entry operation')
        if definition in quest_ids:story.require(quest_ids[definition]==quest_id,'Registered startup quest instance changed')
        else:quest_ids[definition]=quest_id
        native_story=state.get('story') or {}
        quiet=pages.observe(native_story)
        if definition=='ftkmf_hollowmire_arrive':
            story.require(not seen_dungeon,'Native startup quest regressed from Dungeon to Visit')
            story.require(state.get('positionEligible') is False,'Visit quest cannot authorize dungeon positioning')
        else:
            seen_dungeon=True
            story.require(state.get('expectedQuestDefinition') is True,'Exact native DungeonQuestDef required')
            if native_story.get('messagePresent') is True:
                story.require(native_story.get('questId')==quest_id,'Presenter does not identify the current Dungeon quest')
            if native_story.get('actionableSurface') is not None:
                story.require(state.get('expectedQuestDestination') is True,'Exact Dungeon destination required before new story submission')
        runner.log('entry-pre-position-observation',state)
        if definition=='ftkmf_hollowmire_crypt' and quiet and state.get('positionEligible') is True:
            story.require(state.get('expectedQuestDestination') is True,'Exact Dungeon destination required before positioning')
            return state,pins,pages
        time.sleep(.25)
    raise TimeoutError('Native Visit-to-Dungeon startup eligibility did not complete; no entry mutation submitted')


def prepare(runner):
    initial,pins,pages=wait_position_eligible(runner)
    initial_story=initial['story'];initial_story_pin=pages.pin
    result=runner.helper('entry-position',dict(zip(PINS,pins)))
    key=result.get('ticketId')
    story.require(result.get('status')=='native-position-submitted' and isinstance(key,str)
                  and re.fullmatch('[a-f0-9]{32}',key) is not None,'Unverified entry position submission; no retry')
    story.require(identity(result.get('before') or {})==pins,'Entry position owner changed')
    runner.log('entry-position-submitted',result)
    deadline=time.monotonic()+runner.a.wait_timeout;discovered=False
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
