import { renderPlayerActions } from './PlayerCombatActions.js';
import { switchCastView } from './CastWindow.js';
import { createBattlemap, restoreView } from './Battlemap.js';
import { connectBattlemapPresentation } from './BattlemapPresentationSync.js';
import { renderPlayerTracker } from './PlayerCombatTracker.js';

export function createPresentation(host,status,session,tracker,actions) {
    return new PresentedBattlemap(host,status,session,tracker,actions);
}
class PresentedBattlemap {
    constructor(host,status,session,tracker,actions) {
        Object.assign(this,{host,status,tracker,actions,disposed:false,busy:false,pending:undefined,map:null,key:null});
        this.session = session;
        this.connection = connectBattlemapPresentation({session,readOnly:true,onPointer:point => { this.pointer = point; this.map?.setPresentedPointer(point); },onState:state => {
            renderPlayerTracker(this.tracker,state); renderPlayerActions(this.actions,state);
            this.pending=state; void this.pump();
        }});
    }
    async pump() {
        if (this.busy || this.disposed) return;
        this.busy=true;
        try {
            while (this.pending !== undefined && !this.disposed) {
                const state=this.pending; this.pending=undefined;
                if (state === null) {
                    await this.map?.dispose(); this.map=null; this.key=null; this.host.replaceChildren();
                    this.status.textContent='Returning to Galaxy Map…';
                    if (!this.switching) { this.switching = true; switchCastView(this.session,false); }
                    continue;
                }
                const key=`${state.scene.campaignId}.${state.scene.id}.${state.scene.revision}`;
                if (key !== this.key) {
                    await this.map?.dispose(); this.map=null;
                    const scene=state.scene;
                    const map=await createBattlemap(this.host,`api/scenes/${scene.campaignId}/${scene.id}/image?v=${scene.revision}`,
                        scene,`presentation.${key}`,state.tokens,null,{readOnly:true});
                    if (this.disposed) { await map.dispose(); return; }
                    this.map=map; this.key=key;
                } else {
                    const wanted=new Set(state.tokens.map(t => t.id));
                    for (const id of this.map.characterTokens.keys()) if (!wanted.has(id)) this.map.removeToken(id);
                    await Promise.all(state.tokens.map(token => this.map.updateCombatant(token)));
                    this.map.setGrid(state.scene);
                }
                this.map.setEncounterState(state.combatMode === true,state.tokens.find(t => t.isCurrent === true)?.id ?? null,state.tokens.filter(t => t.isDead === true).map(t => t.id),0,[],[],[],null,null,null,state.tokens.filter(t => t.isUnconscious === true).map(t => t.id));
                this.map.setMovementRanges(state.movementRanges ?? []);
                this.map.setMovementTrails(state.movementTrails ?? []);
                this.map.setTargetSelection(state.targetSelection ?? null);
                this.map.view=restoreView(state.view,this.map.width,this.map.height,this.map.imageWidth,this.map.imageHeight) ?? this.map.view;
                this.map.setPresentedPointer(this.pointer ?? null);
                this.map.apply(); this.status.textContent=state.scene.name;
            }
        } catch {
            if (!this.disposed) this.status.textContent='Unable to display the Battlemap. Reload to reconnect.';
        } finally { this.busy=false; }
    }
    async dispose() {
        this.disposed=true; this.connection.close();
        renderPlayerTracker(this.tracker,null); renderPlayerActions(this.actions,null);
        await this.map?.dispose(); this.map=null;
    }
}
