import { validSession } from './PresentationSync.js';

// Copy only public data, never Pixi objects or hidden combatants.
export function playerTokens(tokens) {
    return tokens.filter(t => t.isVisible === true && t.column != null && t.row != null)
        .map(t => ({id:t.id,characterId:t.characterId,name:t.name,isNpc:t.isNpc,hasTokenImage:t.hasTokenImage,
            isVisible:true,column:t.column,row:t.row}));
}
export function validBattlemapSnapshot(state) {
    if (state === null) return true;
    const scene = state?.scene, view = state?.view;
    return !!scene && [scene.id,scene.campaignId,scene.gridSize].every(n => Number.isInteger(n) && n > 0)
        && /^#[0-9a-fA-F]{6}$/.test(scene.gridColour) && Number.isFinite(scene.gridlineWidth) && scene.gridlineWidth > 0
        && Number.isFinite(scene.gridOpacity) && scene.gridOpacity >= 0 && scene.gridOpacity <= 100
        && !!view && [view.x,view.y,view.scale,view.width,view.height,view.imageWidth,view.imageHeight].every(Number.isFinite)
        && [view.scale,view.width,view.height,view.imageWidth,view.imageHeight].every(n => n > 0)
        && Array.isArray(state.tokens) && state.tokens.every(t => Number.isInteger(t.id) && t.id > 0
            && Number.isInteger(t.characterId) && t.characterId > 0 && t.isVisible === true
            && Number.isInteger(t.column) && t.column >= 0 && Number.isInteger(t.row) && t.row >= 0 && typeof t.name === 'string');
}
export function connectBattlemapPresentation({session,readOnly,onState,storage,
    createChannel = name => new BroadcastChannel(name)}) {
    if (!validSession(session)) throw Error('Open the presentation using Present battlemap.');
    const key = 'mtu.battlemap.presentation.' + session;
    const channel = createChannel(key);
    let latest = null, closed = false;
    const receive = state => { if (!closed && validBattlemapSnapshot(state)) { latest=state; onState?.(state); } };
    channel.onmessage = ({data}) => {
        if (closed) return;
        if (!readOnly && data?.type === 'request') channel.postMessage({type:'state',state:latest});
        else if (readOnly && data?.type === 'state') receive(data.state);
    };
    if (readOnly) {
        try { receive(JSON.parse((storage ?? globalThis.localStorage).getItem(key) ?? 'null')); } catch { }
        channel.postMessage({type:'request'});
    }
    return {
        publish(state) {
            if (closed || readOnly || !validBattlemapSnapshot(state)) return;
            latest=state;
            try { (storage ?? globalThis.localStorage).setItem(key,JSON.stringify(state)); } catch { }
            channel.postMessage({type:'state',state});
        },
        close() { if (!closed) { closed=true; channel.close(); } }
    };
}
