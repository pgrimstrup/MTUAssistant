import { validSession, validPointer } from './PresentationSync.js';

// Copy only public data, never Pixi objects or hidden combatants.
export function playerTokens(tokens) {
    return tokens.filter(t => t.isVisible === true && t.column != null && t.row != null)
        .map(t => ({id:t.id,characterId:t.characterId,name:t.name,isNpc:t.isNpc,hasTokenImage:t.hasTokenImage,
            isVisible:true,column:t.column,row:t.row,...(t.isNpc && Number.isFinite(t.healthWidth) && ['green','orange','red'].includes(t.healthColour) ? {healthWidth:t.healthWidth,healthColour:t.healthColour} : {}),...(t.isCurrent === true ? {isCurrent:true} : {}),...(t.isDead === true ? {isDead:true} : {})}));
}
// Tracker rows can include visible, unplaced combatants. NPC HP totals stay private.
export function playerCombatants(entries) {
    return entries.filter(e => e.isVisible === true).map(e => ({
        id:e.id,characterId:e.characterId,name:e.name,isNpc:e.isNpc,hasTokenImage:e.hasTokenImage,isVisible:true,
        initiative:e.initiative,healthWidth:e.healthWidth,healthColour:e.healthColour,
        isDead:e.isDead === true,isCurrent:e.isCurrent === true,
        ...(!e.isNpc && Number.isFinite(e.currentHp) ? {currentHp:e.currentHp} : {}),
        ...(!e.isNpc && [e.currentStrength,e.currentDexterity,e.currentEndurance].every(Number.isInteger) ?
            {currentStrength:e.currentStrength,currentDexterity:e.currentDexterity,currentEndurance:e.currentEndurance} : {}),
        ...(!e.isNpc && [e.originalStrength,e.originalDexterity,e.originalEndurance].every(Number.isInteger) ?
            {originalStrength:e.originalStrength,originalDexterity:e.originalDexterity,originalEndurance:e.originalEndurance} : {})
    }));
}
function validPlayerCombatant(e) {
    return Number.isInteger(e.id) && e.id > 0 && Number.isInteger(e.characterId) && e.characterId > 0
        && e.isVisible === true && typeof e.name === 'string' && typeof e.isNpc === 'boolean'
        && (e.initiative === null || Number.isFinite(e.initiative))
        && Number.isFinite(e.healthWidth) && e.healthWidth >= 0 && e.healthWidth <= 100
        && ['green','orange','red'].includes(e.healthColour)
        && (e.isNpc ? [e.currentHp,e.currentStrength,e.currentDexterity,e.currentEndurance,e.originalStrength,e.originalDexterity,e.originalEndurance].every(v => v === undefined) : Number.isFinite(e.currentHp))
        && ([e.currentStrength,e.currentDexterity,e.currentEndurance].every(v => v === undefined) ||
            ([e.currentStrength,e.currentDexterity,e.currentEndurance].every(v => Number.isInteger(v) && v >= 0)
                && e.currentStrength+e.currentDexterity+e.currentEndurance === e.currentHp))
        && ([e.originalStrength,e.originalDexterity,e.originalEndurance].every(v => v === undefined) ||
            ([e.originalStrength,e.originalDexterity,e.originalEndurance].every(v => Number.isInteger(v) && v >= 0)
                && e.currentStrength <= e.originalStrength && e.currentDexterity <= e.originalDexterity && e.currentEndurance <= e.originalEndurance));
}
export function validBattlemapSnapshot(state) {
    if (state === null) return true;
    const scene = state?.scene, view = state?.view;
    return (state.round === undefined || (Number.isInteger(state.round) && state.round >= 0))
        && (state.tracker === undefined || (Array.isArray(state.tracker) && state.tracker.every(validPlayerCombatant)))
        && !!scene && [scene.id,scene.campaignId,scene.gridSize].every(n => Number.isInteger(n) && n > 0)
        && /^#[0-9a-fA-F]{6}$/.test(scene.gridColour) && Number.isFinite(scene.gridlineWidth) && scene.gridlineWidth > 0
        && Number.isFinite(scene.gridOpacity) && scene.gridOpacity >= 0 && scene.gridOpacity <= 100
        && !!view && [view.x,view.y,view.scale,view.width,view.height,view.imageWidth,view.imageHeight].every(Number.isFinite)
        && [view.scale,view.width,view.height,view.imageWidth,view.imageHeight].every(n => n > 0)
        && Array.isArray(state.tokens) && state.tokens.every(t => Number.isInteger(t.id) && t.id > 0
            && Number.isInteger(t.characterId) && t.characterId > 0 && t.isVisible === true
            && Number.isInteger(t.column) && t.column >= 0 && Number.isInteger(t.row) && t.row >= 0 && typeof t.name === 'string');
}
export function connectBattlemapPresentation({session,readOnly,onState,onPointer,storage,
    createChannel = name => new BroadcastChannel(name)}) {
    if (!validSession(session)) throw Error('Open the presentation using Present battlemap.');
    const key = 'mtu.battlemap.presentation.' + session;
    const channel = createChannel(key);
    let latest = null, pointer = null, closed = false;
    const receive = state => { if (!closed && validBattlemapSnapshot(state)) { latest=state; onState?.(state); } };
    channel.onmessage = ({data}) => {
        if (closed) return;
        if (!readOnly && data?.type === 'request') {
            channel.postMessage({type:'state',state:latest});
            channel.postMessage({type:'pointer',point:pointer});
        }
        else if (readOnly && data?.type === 'state') receive(data.state);
        else if (readOnly && data?.type === 'pointer' && validPointer(data.point)) onPointer?.(data.point);
    };
    if (readOnly) {
        try { const cached = (storage ?? globalThis.localStorage).getItem(key); if (cached != null) receive(JSON.parse(cached)); } catch { }
        channel.postMessage({type:'request'});
    }
    return {
        publish(state) {
            if (closed || readOnly || !validBattlemapSnapshot(state)) return;
            latest=state;
            try { (storage ?? globalThis.localStorage).setItem(key,JSON.stringify(state)); } catch { }
            channel.postMessage({type:'state',state});
        },
        publishPointer(point) {
            if (closed || readOnly || !validPointer(point)) return;
            pointer = point;
            channel.postMessage({type:'pointer',point});
        },
        close() {
            if (closed) return;
            if (!readOnly) channel.postMessage({type:'pointer',point:null});
            closed = true; channel.close();
        }
    };
}
