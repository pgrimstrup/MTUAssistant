import { validDescription } from './CombatDescription.js';
import { validSession, validPointer } from './PresentationSync.js';

// Copy only public data, never Pixi objects or hidden combatants.
export function playerTokens(tokens) {
    return tokens.filter(t => t.isVisible === true && t.column != null && t.row != null)
        .map(t => ({id:t.id,characterId:t.characterId,name:t.name,isNpc:t.isNpc,hasTokenImage:t.hasTokenImage,
            isVisible:true,column:t.column,row:t.row,...(Number.isFinite(t.healthWidth) && ['green','orange','red'].includes(t.healthColour) ? {healthWidth:t.healthWidth,healthColour:t.healthColour} : {}),...(t.isCurrent === true ? {isCurrent:true} : {}),...(t.isDead === true ? {isDead:true} : {}),...(t.isUnconscious === true ? {isUnconscious:true} : {})}));
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
export function playerMovementRanges(ranges) {
    return ranges.filter(r => r.isVisible === true).map(r => ({id:r.id,column:r.column,row:r.row,diameter:r.diameter,colour:r.colour,isVisible:true}));
}
function validMovementRange(r) {
    return r.isVisible === true && Number.isInteger(r.id) && r.id > 0
        && [r.column,r.row].every(v => Number.isInteger(v) && v >= 0)
        && Number.isFinite(r.diameter) && r.diameter >= 1.5 && /^#[0-9a-fA-F]{6}$/.test(r.colour);
}
export function playerMovementTrails(trails,tokens) {
    return trails.filter(t => tokens.some(token => token.id === t.id && token.isVisible === true))
        .map(t => ({id:t.id,fromColumn:t.fromColumn,fromRow:t.fromRow,toColumn:t.toColumn,toRow:t.toRow,isNpc:tokens.find(token => token.id === t.id).isNpc,isVisible:true}));
}
function validMovementTrail(t) {
    return t.isVisible === true && Number.isInteger(t.id) && t.id > 0 && typeof t.isNpc === 'boolean'
        && [t.fromColumn,t.fromRow,t.toColumn,t.toRow].every(v => Number.isInteger(v) && v >= 0);
}
export function playerTargetSelection(actorId,targetId,tokens,rangeProfile = null) {
    const ids = [...new Set(Array.isArray(targetId) ? targetId : [targetId])].filter(id => id !== actorId && tokens.some(t => t.id === id && t.isVisible === true));
    return ids.length && tokens.some(t => t.id === actorId && t.isVisible === true)
        ? {actorId,targetId:ids[0],...(ids.length > 1 ? {targetIds:ids} : {}),...(rangeProfile ? {rangeProfile:{range:rangeProfile.range,scope:rangeProfile.scope,aimBonus:rangeProfile.aimBonus}} : {})} : null;
}
function validAttackModifiers(modifiers,isNpc) {
    return modifiers == null || (!isNpc && Array.isArray(modifiers.rows)
        && modifiers.rows.every(row => typeof row.name === 'string' && Number.isSafeInteger(row.dm))
        && Number.isSafeInteger(modifiers.total) && modifiers.total === modifiers.rows.reduce((total,row) => total+row.dm,0)
        && (modifiers.unavailableReason == null || typeof modifiers.unavailableReason === 'string'));
}
export function validBattlemapSnapshot(state) {
    if (state === null) return true;
    const scene = state?.scene, view = state?.view;
    return validDescription(state.description) && (state.actionSummary == null || (state.combatMode === true && state.tracker?.some(e => e.id === state.actionSummary.id && e.isVisible === true) && validAttackModifiers(state.actionSummary.attackModifiers,state.actionSummary.isNpc) && (state.actionSummary.targetAttackModifiers == null || (Array.isArray(state.actionSummary.targetAttackModifiers) && state.actionSummary.targetAttackModifiers.every(t => typeof t.name === "string" && state.tracker?.some(e => e.id === t.targetId && e.isVisible === true) && validAttackModifiers(t.modifiers,state.actionSummary.isNpc)))) && Array.isArray(state.actionSummary.columns) && state.actionSummary.columns.every(c => typeof c.title === "string" && Array.isArray(c.buttons) && c.buttons.every(b => typeof b.text === "string" && typeof b.selected === "boolean")) && Array.isArray(state.actionSummary.rows) && state.actionSummary.rows.every(r => typeof r.label === "string" && typeof r.value === "string" && (!state.actionSummary.isNpc || !["Armour","Weapon"].includes(r.label)))))
        && (state.round === undefined || (Number.isInteger(state.round) && state.round >= 0))
        && (state.movementTrails === undefined || (Array.isArray(state.movementTrails) && state.movementTrails.every(validMovementTrail)))
        && (state.movementRanges === undefined || (Array.isArray(state.movementRanges) && state.movementRanges.every(validMovementRange)))
        && (state.tracker === undefined || (Array.isArray(state.tracker) && state.tracker.every(validPlayerCombatant)))
        && !!scene && [scene.id,scene.campaignId,scene.gridSize].every(n => Number.isInteger(n) && n > 0)
        && /^#[0-9a-fA-F]{6}$/.test(scene.gridColour) && Number.isFinite(scene.gridlineWidth) && scene.gridlineWidth > 0
        && Number.isFinite(scene.gridOpacity) && scene.gridOpacity >= 0 && scene.gridOpacity <= 100
        && !!view && [view.x,view.y,view.scale,view.width,view.height,view.imageWidth,view.imageHeight].every(Number.isFinite)
        && [view.scale,view.width,view.height,view.imageWidth,view.imageHeight].every(n => n > 0)
        && Array.isArray(state.tokens)
        && (state.targetSelection == null || (Number.isInteger(state.targetSelection.actorId) && Number.isInteger(state.targetSelection.targetId)
            && (state.targetSelection.rangeProfile == null || (Number.isFinite(state.targetSelection.rangeProfile.range) && state.targetSelection.rangeProfile.range > 0 && typeof state.targetSelection.rangeProfile.scope === "boolean" && Number.isInteger(state.targetSelection.rangeProfile.aimBonus) && state.targetSelection.rangeProfile.aimBonus >= 0 && state.targetSelection.rangeProfile.aimBonus <= 6))
            && (state.targetSelection.targetIds == null || (Array.isArray(state.targetSelection.targetIds) && state.targetSelection.targetIds[0] === state.targetSelection.targetId && new Set(state.targetSelection.targetIds).size === state.targetSelection.targetIds.length && state.targetSelection.targetIds.every(id => Number.isInteger(id) && id !== state.targetSelection.actorId && state.tokens.some(t => t.id === id && t.isVisible === true))))
            && state.targetSelection.actorId !== state.targetSelection.targetId
            && state.tokens.some(t => t.id === state.targetSelection.actorId && t.isVisible === true)
            && state.tokens.some(t => t.id === state.targetSelection.targetId && t.isVisible === true)))
        && state.tokens.every(t => Number.isInteger(t.id) && t.id > 0
            && Number.isInteger(t.characterId) && t.characterId > 0 && t.isVisible === true
            && (t.isUnconscious === undefined || typeof t.isUnconscious === 'boolean')
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
