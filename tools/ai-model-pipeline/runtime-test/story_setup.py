"""Bounded native story setup. No generic dismissal, combat UI, or action retries."""
import time

def require(value,message):
    if not value:raise RuntimeError(message)

def complete(state):
    signals=state.get('signals') or {};panels=[state.get('portrait') or {},state.get('questConfirm') or {}];fsms=state.get('presenterFsms') or []
    return (state.get('complete') is True and state.get('messagePresent') is False and state.get('messageType')=='None'
        and state.get('queueCount')==0 and state.get('currentContinuationPresent') is False
        and len(fsms)==3 and all(f.get('present') is True and f.get('enabled') is False for f in fsms)
        and signals.get('modalOpen') is False and signals.get('choiceOpen') is False and signals.get('warnings')==[]
        and all(p.get('activeSelf') is False and p.get('activeInHierarchy') is False and p.get('fullyOpened') is False for p in panels))

def scope(state):
    require(all(state.get(k) is True for k in ('inSession','livingOwnedParty','outsideCombat','outsideDungeon')),'Story setup requires living owned SP overworld outside both combat sessions/dungeon')
    require(all(type(state.get(k)) is int and state[k]!=0 for k in ('coordinatorId','presenterId','heroInstanceId')),'Story native owner IDs unavailable')
    require(state.get('status') not in ('unsupported-choice','unsupported-modal'),'Unknown modal/choice; no automatic story operation')
    return tuple(state[k] for k in ('root','coordinatorId','presenterId','heroInstanceId'))+(state.get('coreIdentity'),)

def submission(state):
    surface=state.get('actionableSurface');require(surface in ('portrait','questConfirm'),'No exact actionable story page')
    require(state.get('messageType')=='StoryQuestMessage' and state.get('messagePresent') is True and state.get('messageClosed') is False,'Unknown native story message')
    require(state.get('presenterMessageId')==state.get('messageId'),'Coordinator/presenter message mismatch')
    require(state.get('questPresent') is True and state.get('questRegisteredReference') is True,'Native presenter quest must be exact registered object')
    signals=state.get('signals') or {};require(signals.get('modalType')=='StoryQuestMessage' and signals.get('choiceOpen') is False,'Story signals changed')
    keys=('coordinatorId','messageId','presenterId','pageIndex','questId','heroInstanceId','phase','contentSha256');result={k:state[k] for k in keys};panel=state[surface]
    result.update(surface=surface,componentId=panel['componentId'],panelId=panel['panelId'],clickInstanceId=panel['focus']['instanceId'],clickContinuationId=panel['focus']['continuationId'])
    require(all(type(v) is int for k,v in result.items() if k not in ('surface','phase','contentSha256')),'Exact story integer identities required')
    require(result['messageId']>=0 and result['pageIndex']>=0 and result['questId']!=0 and result['componentId']!=0 and result['panelId']!=0,'Invalid story identity')
    return result

def page_key(request):return tuple(request[k] for k in ('coordinatorId','presenterId','heroInstanceId','messageId','pageIndex','phase','contentSha256','surface'))

def progressed(pending,state):
    if state.get('messagePresent') is False:return True
    current=state.get('messageId');page=state.get('pageIndex')
    require(type(current) is int and type(page) is int,'Missing native story progress IDs')
    if current!=pending['messageId']:return True
    phase=state.get('phase');content=state.get('contentSha256')
    if phase=='unavailable' and state.get('actionableSurface') is None:return False
    if phase==pending['phase'] and content==pending['contentSha256']:
        require(page>=pending['pageIndex'],'Native story page regressed without native phase advancement')
        return page>pending['pageIndex']
    if state.get('actionableSurface')=='questConfirm':
        return page>pending['pageIndex'] # Native final page increments before the confirmation path.
    if (pending['phase']=='DeliverMultiQuestMsgClosed' and phase=='DeliverSubQuestMsgClosed'
            and content!=pending['contentSha256'] and state.get('actionableSurface')=='portrait'):
        return True # Native rebuilds its messages and resets index for subquests.
    if state.get('actionableSurface') is None:return False
    raise RuntimeError('Unexpected native story phase/content transition')

def clear(runner):
    deadline=time.monotonic()+runner.a.wait_timeout;pin=None;pending=None
    submitted=set(getattr(runner,'story_page_keys',()));runner.story_page_keys=submitted
    clear_since=None;last_frame=-1;previous=None
    while time.monotonic()<deadline:
        state=runner.helper('story-state');runner.log('story-observation',state);current=scope(state)
        if pin is None:pin=current
        require(current==pin,'Story root/native owner/Core identity changed')
        require(type(state.get('frame')) is int and state['frame']>last_frame,'Fresh advancing story frame required');last_frame=state['frame']
        if pending is not None and progressed(pending,state):
            runner.log('story-native-progress',{'submission':pending,'observed':state});pending=None
        if complete(state):
            clear_since=time.monotonic() if clear_since is None else clear_since
            if time.monotonic()-clear_since>=2:
                runner.log('story-completion-observed',state);return state
        else:clear_since=None
        if state.get('actionableSurface') is not None and pending is None:
            if previous is not None:require(progressed(previous,state),'Native story phase/page did not advance')
            request=submission(state);key=page_key(request)
            require(key not in submitted and len(submitted)<64,'Story page already submitted or64-page bound exceeded')
            submitted.add(key);pending=request;previous=request # Preserve uncertainty before submitting exactly once.
            result=runner.helper('story-submit',request)
            require(result.get('status')=='native-story-page-submitted' and result.get('surface')==request['surface'],'Story submission outcome unverified; never retry')
            before=result.get('before') or {};require(submission(before)==request,'Native submission identity differs from observed request')
            runner.log('story-page-submitted',{'request':request,'result':result})
        time.sleep(.25)
    raise TimeoutError('Native story setup did not complete within budget; no action retried')

def staging_status(state):
    signals=state.get('signals') or {}
    if signals.get('modalOpen') is False and signals.get('choiceOpen') is False:return 'binding_metadata_observed'
    if signals.get('modalOpen') is True and signals.get('choiceOpen') is False and signals.get('modalType')=='StoryQuestMessage':return 'pending_story_message'
    raise RuntimeError('Unknown post-staging modal/choice; no automatic dismissal or combat action')
