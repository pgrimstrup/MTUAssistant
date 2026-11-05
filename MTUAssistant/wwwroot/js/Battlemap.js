import { automaticMoveRange } from './AutomaticMovement.js';
import { movementGeometry, weaponRangeBand } from './MovementIndicators.js';
import { attachCastWindow } from './CastWindow.js';
import { presentationSession } from './PresentationSync.js';
import { connectBattlemapPresentation, playerTokens, playerCombatants, playerMovementRanges, playerMovementTrails, playerTargetSelection } from './BattlemapPresentationSync.js';
export function fitView(width, height, imageWidth, imageHeight) {
    const scale = Math.min(width / imageWidth, height / imageHeight);
    return { x: (width - imageWidth * scale) / 2, y: (height - imageHeight * scale) / 2, scale };
}
export function zoomView(view, factor, point, min, max) {
    const scale = Math.max(min, Math.min(max, view.scale * factor));
    return { x: point.x - (point.x - view.x) * scale / view.scale,
        y: point.y - (point.y - view.y) * scale / view.scale, scale };
}
export function restoreView(saved, width, height, imageWidth, imageHeight) {
    if (!saved || ![saved.x, saved.y, saved.scale, saved.width, saved.height].every(Number.isFinite)
        || saved.scale <= 0 || saved.imageWidth !== imageWidth || saved.imageHeight !== imageHeight) return null;
    return { x: saved.x + (width - saved.width) / 2, y: saved.y + (height - saved.height) / 2, scale: saved.scale };
}
export function snapToken(x, y, size, imageWidth, imageHeight) {
    return { column:Math.max(0, Math.min(Math.floor((imageWidth-size)/size), Math.round(x/size))),
        row:Math.max(0, Math.min(Math.floor((imageHeight-size)/size), Math.round(y/size))) };
}
export function tokenInitials(name) {
    return String(name ?? '').trim().split(/\s+/).filter(Boolean).slice(0,2)
        .map(word => Array.from(word)[0]).join('').toUpperCase() || '?';
}
export function drawFallbackToken(canvas, name, isNpc) {
    // Rasterize at a fixed resolution, independent of a potentially tiny map grid.
    const context = canvas.getContext('2d');
    const size = canvas.width, centre = size/2;
    context.clearRect(0,0,size,size);
    context.beginPath(); context.arc(centre,centre,size*.46,0,Math.PI*2);
    context.fillStyle = isNpc ? '#8b493c' : '#284f86'; context.fill();
    context.strokeStyle = '#ffffff'; context.lineWidth = size*.025; context.stroke();
    const initials = tokenInitials(name);
    let fontSize = size*.3;
    context.font = `bold ${fontSize}px Arial, sans-serif`;
    const width = context.measureText(initials).width;
    if (width > size*.76) {
        fontSize *= size*.76/width;
        context.font = `bold ${fontSize}px Arial, sans-serif`;
    }
    context.fillStyle = '#ffffff'; context.textAlign = 'center'; context.textBaseline = 'middle';
    context.fillText(initials,centre,centre);
    return initials;
}
export async function createBattlemap(host, url, scene, key, tokens = [], callback = null, options = {}, presentationLink = null) {
    const map = new Battlemap(host, scene, key);
    map.callback = callback; map.readOnly = !!options.readOnly;
    try { await map.start(url); await Promise.all(tokens.filter(t => t.column != null && t.row != null).map(token => map.upsertToken(token))); if (presentationLink) map.attachPresentation(presentationLink); return map; }
    catch (error) { await map.dispose(); throw error; }
}
export class Battlemap {
    constructor(host, scene, key) {
        Object.assign(this, { host, scene, key: 'mtu.battlemap.' + key });
        this.disposed = false;
        this.characterTokens = new Map();
        this.pendingSaves = new Set();
        this.automaticMoveQueue = Promise.resolve();
        this.movementTrails = new Map(); this.movementLabels = new Map(); this.movementEpoch = 0;
    }
    async start(url) {
        if (!globalThis.PIXI) await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = new URL('lib/pixi/pixi.min.js', document.baseURI).href;
            script.onload = resolve;
            script.onerror = () => { script.remove(); reject(Error('Unable to load PixiJS.')); };
            document.head.appendChild(script);
        });
        this.image = new Image(); this.image.alt = this.scene.name; this.image.draggable = false;
        await new Promise((resolve, reject) => {
            this.image.onload = resolve; this.image.onerror = () => reject(Error('Unable to load scene background.'));
            this.image.src = new URL(url, document.baseURI).href;
        });
        this.imageWidth = this.image.naturalWidth; this.imageHeight = this.image.naturalHeight;
        if (!this.imageWidth || !this.imageHeight) throw Error('Empty scene background.');
        this.width = this.host.clientWidth; this.height = this.host.clientHeight;
        this.app = new PIXI.Application();
        await this.app.init({width:this.width,height:this.height,background:0x080c14,
            antialias:true,autoDensity:true,resolution:Math.min(window.devicePixelRatio || 1,2)});
        this.initialized = true;
        this.app.canvas.draggable = false;
        this.app.canvas.setAttribute('aria-label', this.scene.name + ' battlemap');
        this.host.replaceChildren(this.app.canvas);
        if (this.readOnly) {
            this.presentationPointer = document.createElement('div');
            this.presentationPointer.className = 'presentation-pointer';
            this.presentationPointer.setAttribute('aria-hidden','true');
            this.presentationPointer.hidden = true;
            this.host.appendChild(this.presentationPointer);
        }
        this.world = new PIXI.Container();
        this.background = new PIXI.Container();
        this.tokens = new PIXI.Container();
        this.fog = new PIXI.Container();
        this.rangeGraphics = new PIXI.Graphics();
        this.grid = new PIXI.Graphics();
        this.movementOverlay = new PIXI.Container();
        this.movementStroke = new PIXI.Graphics(); this.movementOverlay.addChild(this.movementStroke);
        this.targetRingGraphics = new PIXI.Graphics();
        this.world.addChild(this.background);
        this.app.stage.addChild(this.world, this.grid, this.rangeGraphics, this.movementOverlay, this.targetRingGraphics, this.tokens, this.fog);
        this.placementPreview = new PIXI.Graphics();
        this.tokens.addChild(this.placementPreview);
        this.tiles = new Map();
        this.overviewScale = Math.min(1,2048/Math.max(this.imageWidth,this.imageHeight));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1,Math.round(this.imageWidth*this.overviewScale));
        canvas.height = Math.max(1,Math.round(this.imageHeight*this.overviewScale));
        canvas.getContext('2d').drawImage(this.image,0,0,canvas.width,canvas.height);
        this.overviewTexture = PIXI.Texture.from(canvas);
        const overview = new PIXI.Sprite(this.overviewTexture);
        overview.width = this.imageWidth; overview.height = this.imageHeight;
        this.background.addChild(overview);
        let saved; try { saved = JSON.parse(sessionStorage.getItem(this.key) ?? 'null'); } catch { }
        this.view = restoreView(saved, this.width, this.height, this.imageWidth, this.imageHeight)
            ?? fitView(this.width, this.height, this.imageWidth, this.imageHeight);
        this.apply();
        this.pulseTime = 0;
        this.animateTurn = ticker => {
            this.pulseTime += ticker.deltaMS;
            this.advancePan(ticker.deltaMS);
            this.drawTargetRing();
            const token = this.characterTokens.get(this.selectedId);
            if (token && this.combatMode && !token.isDead) this.drawTurnRing(token);
        };
        this.app.ticker.add(this.animateTurn);
        this.down = event => {
            if (this.readOnly || event.button !== 0 || this.dragging) return;
            event.preventDefault();
            this.cancelPan();
            const point = this.pointerWorld(event);
            if (this.placement) {
                if (point.x < 0 || point.y < 0 || point.x >= this.imageWidth || point.y >= this.imageHeight) return;
                const instanceId = this.placement;
                const cell = snapToken(point.x-this.scene.gridSize/2, point.y-this.scene.gridSize/2,
                    this.scene.gridSize, this.imageWidth, this.imageHeight);
                this.cancelPlacement();
                this.trackSave((async () => {
                    const token = await this.callback.invokeMethodAsync('PlaceToken', instanceId, cell.column, cell.row);
                    if (token && !this.disposed) await this.upsertToken(token);
                })());
                return;
            }
            const token = this.hitToken(point);
            if (token?.saving || (token && this.pendingSaves.size > 0)) return;
            this.dragging = token ? { id:event.pointerId, token, column:token.column, row:token.row,
                offsetX:point.x-token.column*this.scene.gridSize, offsetY:point.y-token.row*this.scene.gridSize }
                : { id:event.pointerId, x:event.clientX, y:event.clientY };
            if (token) { this.dragging.previousTrail = this.movementTrails.get(token.id); this.dragging.movementEpoch = this.movementEpoch; }
            this.host.setPointerCapture(event.pointerId);
        };
        this.move = event => {
            if (this.readOnly) return;
            this.pointerPosition = {clientX:event.clientX,clientY:event.clientY};
            this.publishPresentationPointer();
            const point = this.pointerWorld(event);
            if (!this.dragging) {
                const token = this.hitToken(point);
                this.host.title = token?.name ?? '';
                this.host.style.cursor = this.placement ? 'crosshair' : token ? 'move' : 'grab';
                if (this.placement) this.previewPlacement(point);
                return;
            }
            if (this.dragging.id !== event.pointerId) return;
            if (this.dragging.token) {
                const cell = snapToken(point.x-this.dragging.offsetX, point.y-this.dragging.offsetY,
                    this.scene.gridSize, this.imageWidth, this.imageHeight);
                if (cell.column !== this.dragging.token.column || cell.row !== this.dragging.token.row)
                    this.previewAutomaticMovement(this.dragging,cell.column,cell.row);
                this.positionToken(this.dragging.token, cell.column, cell.row);
                return;
            }
            this.view.x += event.clientX - this.dragging.x; this.view.y += event.clientY - this.dragging.y;
            this.dragging.x = event.clientX; this.dragging.y = event.clientY; this.apply();
        };
        this.up = event => {
            if (this.dragging?.id !== event.pointerId) return;
            const drag = this.dragging;
            this.dragging = null;
            if (this.host.hasPointerCapture(event.pointerId)) this.host.releasePointerCapture(event.pointerId);
            if (!drag.token) return;
            if (event.type !== 'pointerup') {
                this.restoreMovementTrail(drag); this.positionToken(drag.token, drag.column, drag.row);
                if (drag.automaticMove) {
                    drag.token.saving=true;
                    this.trackSave(this.automaticMoveQueue.catch(()=>{}).then(()=>this.callback.invokeMethodAsync('CancelAutomaticMove',drag.token.id,drag.automaticMove.context.round))
                        .finally(()=>{drag.token.saving=false;this.clearAutomaticMovementPreview();}));
                }
                return;
            }
            if (drag.column === drag.token.column && drag.row === drag.token.row && !drag.automaticMove) return;
            drag.token.saving = true;
            this.trackSave((async () => {
                let saved = false;
                try { await this.automaticMoveQueue.catch(()=>{}); saved = await this.callback.invokeMethodAsync('MoveToken', drag.token.id, drag.token.column, drag.token.row, drag.automaticMove?.context.round ?? null, drag.automaticMove?.context.column ?? null, drag.automaticMove?.context.row ?? null); }
                finally {
                    if (!this.disposed && this.characterTokens.get(drag.token.id) === drag.token) {
                        if (!saved) { this.restoreMovementTrail(drag); this.positionToken(drag.token, drag.column, drag.row); }
                        drag.token.saving = false;
                        this.clearAutomaticMovementPreview();
                    }
                }
            })());
        };
        this.wheel = event => {
            event.preventDefault(); if (this.readOnly) return; const bounds = this.host.getBoundingClientRect();
            this.zoom(Math.exp(-event.deltaY * .0015), { x:event.clientX-bounds.left, y:event.clientY-bounds.top });
        };
        this.pointerLeave = () => { this.pointerPosition = null; this.publishPresentationPointer(); };
        this.visibilityChange = () => { if (document.hidden) this.pointerLeave(); };
        this.host.addEventListener('pointerleave',this.pointerLeave);
        this.host.addEventListener('pointercancel',this.pointerLeave);
        globalThis.addEventListener('blur',this.pointerLeave);
        globalThis.addEventListener('pagehide',this.pointerLeave);
        document.addEventListener('visibilitychange',this.visibilityChange);
        this.preventDrag = event => event.preventDefault();
        this.host.addEventListener('dragstart',this.preventDrag);
        this.host.addEventListener('selectstart',this.preventDrag);
        this.doubleClick = event => { void this.selectTokenAt(event); };
        this.host.addEventListener('dblclick', this.doubleClick);
        this.host.addEventListener('pointerdown', this.down);
        this.host.addEventListener('pointermove', this.move);
        this.host.addEventListener('pointerup', this.up);
        this.host.addEventListener('pointercancel', this.up);
        this.host.addEventListener('lostpointercapture', this.up);
        this.host.addEventListener('wheel', this.wheel, { passive:false });
        this.resizeObserver = new ResizeObserver(() => {
            const width = this.host.clientWidth, height = this.host.clientHeight;
            if (!width || !height || this.disposed) return;
            this.cancelPan();
            this.view.x += (width - this.width) / 2; this.view.y += (height - this.height) / 2;
            this.width = width; this.height = height; this.app.renderer.resize(width,height); this.apply();
        });
        this.resizeObserver.observe(this.host);
    }
    pointerWorld(event) {
        const rect = this.host.getBoundingClientRect();
        return { x:(event.clientX-rect.left-this.host.clientLeft-this.view.x)/this.view.scale,
            y:(event.clientY-rect.top-this.host.clientTop-this.view.y)/this.view.scale };
    }
    async selectTokenAt(event) {
        if (this.readOnly || this.disposed || this.placement || !this.callback || event.button !== 0) return;
        const token = this.hitToken(this.pointerWorld(event));
        if (!token) return;
        event.preventDefault();
        try {
            await this.flushTokenMoves();
            if (!this.disposed) await this.callback.invokeMethodAsync('SelectMapToken',token.id);
        } catch (error) { if (!this.disposed) console.warn('Unable to select the battlemap token.',error); }
    }
    hitToken(point) {
        return [...this.characterTokens.values()].reverse().find(token =>
            point.x >= token.column*this.scene.gridSize && point.x < (token.column+1)*this.scene.gridSize &&
            point.y >= token.row*this.scene.gridSize && point.y < (token.row+1)*this.scene.gridSize);
    }
    positionToken(token, column, row) {
        if (this.combatMode && this.dragging?.token === token && this.dragging.movementEpoch === this.movementEpoch) {
            const origin=this.dragging.automaticMove?.context;
            this.updateMovementTrail(token, origin?.column ?? this.dragging.column, origin?.row ?? this.dragging.row, column, row);
        }
        token.column = column; token.row = row;
        token.container.position.set(column*this.scene.gridSize, row*this.scene.gridSize);
        this.drawMovementIndicators(); this.drawTargetRing();
        this.publishPresentation();
    }
    clearAutomaticMovementPreview() {
        this.automaticMovePreview=null;
        this.drawMovementRanges();
        this.publishPresentation();
    }
    previewAutomaticMovement(drag,column,row) {
        if (this.readOnly || !this.combatMode || drag.movementEpoch !== this.movementEpoch || !this.automaticMoveContext || this.automaticMoveContext.id !== drag.token.id) return;
        if (!drag.automaticMove) {
            const context=this.automaticMoveContext;
            if (!context || context.id !== drag.token.id || context.id !== this.selectedId) return;
            drag.automaticMove={context:{...context},moves:null};
        }
        const session=drag.automaticMove;
        const preview=automaticMoveRange(session.context,column,row);
        if (!preview) return;
        this.automaticMovePreview=preview.range;
        this.drawMovementRanges();
        if (session.moves === preview.moves) return;
        session.moves=preview.moves;
        this.automaticMoveQueue=this.automaticMoveQueue.catch(()=>{}).then(async()=>{
            if (this.disposed || drag.movementEpoch !== this.movementEpoch) return;
            await this.callback.invokeMethodAsync('PreviewAutomaticMove',drag.token.id,column,row,session.context.round,session.context.column,session.context.row);
        });
        this.trackSave(this.automaticMoveQueue);
    }
    trackSave(promise) {
        const tracked = promise.catch(error => {
            if (!this.disposed) console.warn('Unable to save a battlemap token.', error);
        }).finally(() => this.pendingSaves.delete(tracked));
        this.pendingSaves.add(tracked);
    }
    async flushTokenMoves() { await Promise.allSettled([...this.pendingSaves]); }
    beginPlacement(characterId) {
        this.placement = characterId;
        this.host.style.cursor = 'crosshair';
    }
    cancelPlacement() {
        this.placement = null; this.placementPreview?.clear();
        this.host.style.cursor = 'grab';
    }
    previewPlacement(point) {
        this.placementPreview.clear();
        if (point.x < 0 || point.y < 0 || point.x >= this.imageWidth || point.y >= this.imageHeight) return;
        const size = this.scene.gridSize;
        const cell = snapToken(point.x-size/2, point.y-size/2, size, this.imageWidth, this.imageHeight);
        this.placementPreview.rect(cell.column*size, cell.row*size, size, size)
            .fill({color:0x70c9ff,alpha:.25}).stroke({color:0x70c9ff,width:2/this.view.scale});
    }
    async upsertToken(data) {
        if (data.column == null || data.row == null || (this.readOnly && !data.isVisible)) { this.removeToken(data.id); return; }
        this.removeToken(data.id, false);
        const size = this.scene.gridSize;
        const container = new PIXI.Container();
        const token = {...data, container};
        token.halo = new PIXI.Graphics(); container.addChild(token.halo);
        token.turnRing = new PIXI.Graphics(); container.addChild(token.turnRing);
        token.label = new PIXI.Container();
        token.labelBackground = new PIXI.Graphics();
        token.labelText = new PIXI.Text({text:data.name,style:{fontFamily:'Arial',fontSize:14,fill:data.isNpc ? 0xffb3b3 : 0xb3ffb3}});
        token.label.addChild(token.labelBackground,token.labelText);
        container.addChild(token.label);
        token.healthBar = new PIXI.Graphics(); container.addChild(token.healthBar);
        this.characterTokens.set(data.id, token);
        this.tokens.addChild(container);
        this.positionToken(token, data.column, data.row);
        this.refreshFallback(token);
        const fallback = token.fallback;
        container.addChildAt(fallback,1);
        this.tokenAppearance(token); this.publishPresentation();
        if (!data.hasTokenImage) return;
        const image = new Image();
        try {
            await new Promise((resolve,reject) => {
                image.onload = resolve; image.onerror = reject;
                image.src = new URL(`api/characters/${this.scene.campaignId}/${data.characterId}/token`,document.baseURI).href;
            });
            if (this.disposed || this.characterTokens.get(data.id) !== token) return;
            token.texture = PIXI.Texture.from(image);
            const sprite = new PIXI.Sprite(token.texture);
            token.imageSprite = sprite; token.imageAspect = image.naturalWidth/image.naturalHeight;
            const displaySize = this.scene.gridSize;
            const scale = displaySize/Math.max(image.naturalWidth,image.naturalHeight);
            sprite.width = image.naturalWidth*scale; sprite.height = image.naturalHeight*scale;
            sprite.position.set((displaySize-sprite.width)/2,(displaySize-sprite.height)/2);
            fallback.visible = false; container.addChildAt(sprite,2); this.tokenAppearance(token);
        } catch { /* Keep the initials marker if the character image cannot be decoded. */ }
    }
    removeToken(id, publish = true) {
        const token = this.characterTokens.get(id);
        if (!token) return;
        if (id === this.selectedId) this.cancelPan();
        if (this.dragging?.token === token) {
            const pointerId = this.dragging.id; this.dragging = null;
            if (this.host.hasPointerCapture(pointerId)) this.host.releasePointerCapture(pointerId);
        }
        token.container.destroy({children:true}); token.texture?.destroy(true); token.fallbackTexture?.destroy(true); token.filter?.destroy();
        this.characterTokens.delete(id);
        this.movementTrails.delete(id); this.drawMovementIndicators(); this.drawTargetRing();
        if (publish) this.publishPresentation();
    }
    setCombatMode(active) {
        this.setEncounterState(active,this.selectedId,[...(this.deadIds ?? [])],this.combatRound,this.playerTracker ?? [],[...(this.tokenHealth?.values() ?? [])],this.movementRanges ?? [],this.targetId ?? null,this.targetRange ?? null,this.actionSummary ?? null,[...(this.unconsciousIds ?? [])],this.automaticMoveContext ?? null);
    }
    setEncounterState(active,selectedId,deadIds = [],round = 0,tracker = [],health = tracker,ranges = [],targetId = null,targetRange = null,actionSummary = null,unconsciousIds = [],automaticMoveContext = null) {
        const turnChanged = active === true && (!this.combatMode || selectedId !== this.selectedId || round !== this.combatRound);
        this.combatRound = round;
        this.playerTracker = playerCombatants(tracker);
        this.tokenHealth = new Map(health.map(e => [e.id,e]));
        this.combatMode = active === true;
        if (!this.combatMode || turnChanged) this.clearMovementTrails();
        if (!this.combatMode || turnChanged) this.automaticMovePreview=null;
        this.automaticMoveContext=automaticMoveContext;
        if (this.automaticMovePreview && (!automaticMoveContext || automaticMoveContext.id !== this.automaticMovePreview.id)) this.automaticMovePreview=null;
        this.movementRanges = ranges;
        this.drawMovementRanges();
        this.selectedId = selectedId;
        this.targetActorId = selectedId; this.targetId = targetId; this.targetRange = targetRange; this.actionSummary = actionSummary;
        this.deadIds = new Set(deadIds);
        this.unconsciousIds = new Set(unconsciousIds);
        for (const token of this.characterTokens.values()) this.tokenAppearance(token);
        this.drawMovementIndicators(); this.drawTargetRing();
        if (!this.combatMode || turnChanged) this.cancelPan();
        if (turnChanged && !this.readOnly) this.panToToken(selectedId);
        this.publishPresentation();
    }
    updateMovementTrail(token,fromColumn,fromRow,toColumn,toRow) {
        if (!this.combatMode || this.readOnly) return;
        const previous = this.movementTrails.get(token.id);
        const trail = {id:token.id,fromColumn:previous?.fromColumn ?? fromColumn,fromRow:previous?.fromRow ?? fromRow,toColumn,toRow,isNpc:token.isNpc};
        if (trail.fromColumn === toColumn && trail.fromRow === toRow) this.movementTrails.delete(token.id);
        else this.movementTrails.set(token.id,trail);
    }
    restoreMovementTrail(drag) {
        if (drag.movementEpoch !== this.movementEpoch) return;
        if (drag.previousTrail) this.movementTrails.set(drag.token.id,drag.previousTrail);
        else this.movementTrails.delete(drag.token.id);
    }
    resetTokenMovement(id) {
        this.movementTrails.delete(id);
        this.movementRanges=(this.movementRanges ?? []).filter(range=>range.id!==id);
        if (this.automaticMovePreview?.id===id) this.automaticMovePreview=null;
        this.drawMovementRanges();
        this.drawMovementIndicators();
        this.publishPresentation();
    }
    clearMovementTrails() { this.movementEpoch++; this.movementTrails.clear(); this.drawMovementIndicators(); }
    setMovementTrails(trails) { this.movementTrails = new Map(trails.map(t => [t.id,t])); this.drawMovementIndicators(); }
    setTargetSelection(selection) {
        this.targetActorId = selection?.actorId ?? null; this.targetId = selection?.targetId ?? null; this.targetRange = selection?.rangeProfile ?? null;
        this.drawMovementIndicators(); this.drawTargetRing();
    }
    targetTrail() {
        if (!this.combatMode) return null;
        const actor = this.characterTokens.get(this.targetActorId), target = this.characterTokens.get(this.targetId);
        if (!actor || !target || actor.id === target.id) return null;
        return {id:'target',fromColumn:actor.column,fromRow:actor.row,toColumn:target.column,toRow:target.row,isNpc:actor.isNpc,isTarget:true};
    }
    drawTargetRing() {
        const graphics = this.targetRingGraphics;
        if (!graphics || !this.view) return;
        graphics.clear();
        const trail = this.targetTrail();
        if (!trail) return;
        const {end} = movementGeometry(trail,this.scene.gridSize,this.view);
        const wave = (Math.sin((this.pulseTime ?? 0)*Math.PI/700)+1)/2;
        graphics.circle(end.x,end.y,this.scene.gridSize*this.view.scale*(.64+.07*wave))
            .stroke({color:trail.isNpc ? 0xff0000 : 0x00ff00,alpha:.5+.5*wave,width:3});
    }
    drawMovementIndicators() {
        const graphics = this.movementStroke;
        if (!graphics || !this.view) return;
        graphics.clear();
        const visible = new Set();
        const target = this.targetTrail();
        const indicators = [...this.movementTrails.values(),...(target ? [target] : [])];
        if (this.combatMode) for (const trail of indicators) {
            const {start,end,midpoint,distance} = movementGeometry(trail,this.scene.gridSize,this.view);
            const colour = trail.isTarget ? (trail.isNpc ? 0xff0000 : 0x00ff00) : 0xffffff;
            graphics.moveTo(start.x,start.y).lineTo(end.x,end.y).stroke({color:colour,alpha:.25,width:6});
            graphics.circle(start.x,start.y,5).fill({color:colour,alpha:.25});
            let label = this.movementLabels.get(trail.id);
            if (!label) {
                const container = new PIXI.Container(), background = new PIXI.Graphics();
                const text = new PIXI.Text({text:'',style:{fontFamily:'Arial',fontSize:14,fontWeight:'bold',fill:0xffffff}});
                container.addChild(background,text); this.movementOverlay.addChild(container);
                label = {container,background,text}; this.movementLabels.set(trail.id,label);
            }
            const band = trail.isTarget ? weaponRangeBand(distance,this.targetRange) : null;
            label.text.text = `${distance.toFixed(1)} m${band ? ` · ${band}` : ""}`;
            const width = label.text.width+10, height = label.text.height+6;
            label.text.position.set(5,3);
            label.background.clear().rect(0,0,width,height).fill({color:0x000000,alpha:.75});
            label.container.position.set(midpoint.x-width/2,midpoint.y-height/2);
            visible.add(trail.id);
        }
        for (const [id,label] of this.movementLabels) if (!visible.has(id)) { label.container.destroy({children:true}); this.movementLabels.delete(id); }
    }
    setMovementRanges(ranges) { this.movementRanges = ranges; this.drawMovementRanges(); }
    displayMovementRanges() {
        return this.automaticMovePreview
            ? [...(this.movementRanges ?? []).filter(r=>r.id!==this.automaticMovePreview.id),this.automaticMovePreview]
            : this.movementRanges ?? [];
    }
    drawMovementRanges() {
        const graphics = this.rangeGraphics;
        if (!graphics || !this.view) return;
        graphics.clear();
        graphics.position.set(this.view.x,this.view.y); graphics.scale.set(this.view.scale);
        if (!this.combatMode) return;
        const size = this.scene.gridSize;
        const ranges=this.displayMovementRanges();
        for (const range of ranges) {
            const radius = range.diameter/1.5*size/2;
            graphics.circle((range.column+.5)*size,(range.row+.5)*size,radius)
                .fill({color:range.colour,alpha:.15}).stroke({color:range.colour,alpha:.7,width:1/this.view.scale});
        }
    }
    panToToken(id) {
        const token = this.characterTokens.get(id);
        if (this.disposed || !this.view || !token || token.isDead ||
            ![token.column,token.row,this.width,this.height].every(Number.isFinite)) return;
        const size = this.scene.gridSize, scale = this.view.scale;
        const x = this.width/2-(token.column+.5)*size*scale;
        const y = this.height/2-(token.row+.5)*size*scale;
        this.panAnimation = {fromX:this.view.x,fromY:this.view.y,x,y,scale,elapsed:0,duration:550};
    }
    advancePan(deltaMs) {
        const pan = this.panAnimation;
        if (!pan || this.disposed) return;
        pan.elapsed = Math.min(pan.duration,pan.elapsed+Math.max(0,deltaMs));
        const progress = pan.elapsed/pan.duration;
        const eased = progress*progress*(3-2*progress);
        this.view = {x:pan.fromX+(pan.x-pan.fromX)*eased,y:pan.fromY+(pan.y-pan.fromY)*eased,scale:pan.scale};
        if (progress === 1) this.panAnimation = null;
        // Publish the animated camera frames so the cast view follows the same pan.
        this.apply();
    }
    cancelPan() { this.panAnimation = null; }
    drawTurnRing(token) {
        if (!token.turnRing) return;
        token.turnRing.clear();
        if (!this.combatMode || token.id !== this.selectedId || token.isDead) return;
        const size = this.scene.gridSize;
        const wave = (Math.sin((this.pulseTime ?? 0)*Math.PI/700)+1)/2;
        token.turnRing.circle(size/2,size/2,size*(.61+.07*wave))
            .stroke({color:token.isNpc ? 0xff0000 : 0x00ff00,alpha:.5+.5*wave,width:Math.max(size*.035,2/this.view.scale)});
    }
    drawTokenHealth(token) {
        const bar = token.healthBar;
        if (!bar) return;
        bar.clear();
        const health = this.tokenHealth?.get(token.id) ?? token;
        if (Number.isFinite(health.healthWidth)) {
            token.healthWidth = health.healthWidth; token.healthColour = health.healthColour;
        }
        bar.visible = this.combatMode === true && Number.isFinite(token.healthWidth);
        if (!bar.visible) return;
        const scale = this.view.scale, width = this.scene.gridSize*.8;
        const x = this.scene.gridSize*.1, y = -10/scale, height = 5/scale;
        bar.rect(x,y,width,height).fill({color:0x000000,alpha:.75});
        const colour = {red:0xff0000,orange:0xffa500,green:0x008000}[token.healthColour] ?? 0xff0000;
        bar.rect(x,y,width*Math.max(0,Math.min(100,token.healthWidth))/100,height).fill({color:colour});
        bar.rect(x,y,width,height).stroke({color:0xffffff,alpha:.6,width:1/scale});
    }
    decorateToken(token) {
        const size = this.scene.gridSize;
        token.isDead = this.deadIds?.has(token.id) ?? false;
        token.isUnconscious = this.unconsciousIds?.has(token.id) ?? false;
        token.isCurrent = token.id === this.selectedId && !token.isDead;
        this.drawTurnRing(token);
        this.drawTokenHealth(token);
        token.halo.clear();
        if (this.combatMode) token.halo.circle(size/2,size/2,size*.57).fill({color:token.isNpc ? 0xff0000 : 0x00ff00,alpha:.25});
        token.labelText.text = token.name;
        token.labelText.style.fill = token.isNpc ? 0xffb3b3 : 0xb3ffb3;
        token.label.scale.set(1/this.view.scale);
        const width = token.labelText.width + 10, height = token.labelText.height + 4;
        token.label.position.set(size/2-width/(2*this.view.scale),size+4/this.view.scale);
        token.labelText.position.set(5,2);
        token.labelBackground.clear().rect(0,0,width,height).fill({color:0x000000,alpha:.5});
    }
    tokenAppearance(token) {
        this.decorateToken(token);
        token.container.alpha = token.isVisible === false ? .4 : 1;
        if (token.isDead || token.isUnconscious || token.isVisible === false) {
            token.filter ??= new PIXI.ColorMatrixFilter();
            if (token.isDead) {
                // Keep image detail and transparency while washing the portrait in red.
                token.filter.matrix = [
                    .5,.45,.15,0,.12,
                    .08,.10,.02,0,0,
                    .08,.10,.02,0,0,
                    0,0,0,1,0
                ];
            } else if (token.isUnconscious) {
                token.filter.matrix = [
                    .5,.45,.15,0,.12,
                    .5,.45,.15,0,.12,
                    .08,.10,.02,0,0,
                    0,0,0,1,0
                ];
            } else token.filter.desaturate();
        }
        // Filter portraits only, keeping faction markers and labels legible.
        token.container.filters = [];
        for (const portrait of [token.fallback,token.imageSprite])
            if (portrait) portrait.filters = token.isDead || token.isUnconscious || token.isVisible === false ? [token.filter] : [];
    }
    refreshFallback(token) {
        const key = `${token.isNpc}:${tokenInitials(token.name)}`;
        if (token.fallbackKey === key) return;
        const canvas = document.createElement('canvas'); canvas.width = canvas.height = 512;
        drawFallbackToken(canvas,token.name,token.isNpc);
        const texture = PIXI.Texture.from(canvas), previous = token.fallbackTexture;
        if (token.fallback) token.fallback.texture = texture;
        else token.fallback = new PIXI.Sprite(texture);
        token.fallback.width = token.fallback.height = this.scene.gridSize;
        token.fallbackTexture = texture; token.fallbackKey = key;
        previous?.destroy(true);
    }
    async updateCombatant(data) {
        if (data.column == null || data.row == null || (this.readOnly && !data.isVisible)) { this.removeToken(data.id); return; }
        const token = this.characterTokens.get(data.id);
        if (!token) { await this.upsertToken(data); return; }
        Object.assign(token, data); this.positionToken(token, data.column, data.row);
        this.refreshFallback(token);
        this.tokenAppearance(token); this.publishPresentation();
    }
    setGrid(scene) {
        const oldSize = this.scene.gridSize;
        this.scene = {...this.scene, ...scene};
        if (this.scene.gridSize !== oldSize) {
            const size = this.scene.gridSize;
            this.placementPreview?.clear();
            for (const token of this.characterTokens.values()) {
                token.container.position.set(token.column*size,token.row*size);
                if (token.fallback) token.fallback.width = token.fallback.height = size;
                if (token.imageSprite) {
                    token.imageSprite.width = size*Math.min(1,token.imageAspect);
                    token.imageSprite.height = size*Math.min(1,1/token.imageAspect);
                    token.imageSprite.position.set((size-token.imageSprite.width)/2,(size-token.imageSprite.height)/2);
                }
            }
        }
        this.apply();
    }
    attachPresentation(link) {
        const session = presentationSession();
        this.detachCastWindow = attachCastWindow(link,session,true);
        this.presentation = connectBattlemapPresentation({session, readOnly:false});
        this.publishPresentation();
    }
    publishPresentation() {
        if (!this.presentation || this.disposed || this.readOnly || this.presentationCleared) return;
        this.presentation.publish({combatMode:this.combatMode === true,round:this.combatRound ?? 0,actionSummary:this.combatMode && this.playerTracker?.some(e => e.id === this.actionSummary?.id && e.isVisible === true) ? this.actionSummary : null,tracker:this.playerTracker ?? [],movementRanges:playerMovementRanges(this.displayMovementRanges()),movementTrails:playerMovementTrails([...this.movementTrails.values()],[...this.characterTokens.values()]),targetSelection:this.combatMode ? playerTargetSelection(this.targetActorId,this.targetId,[...this.characterTokens.values()],this.targetRange) : null,scene:this.scene, tokens:playerTokens([...this.characterTokens.values()]),
            view:{...this.view, width:this.width,height:this.height,imageWidth:this.imageWidth,imageHeight:this.imageHeight}});
    }
    publishPresentationPointer() {
        if (this.disposed || this.presentationCleared || this.readOnly || !this.presentation?.publishPointer || !this.view) return;
        let point = null;
        if (this.pointerPosition) {
            const bounds = this.host.getBoundingClientRect();
            const x = this.pointerPosition.clientX-bounds.left-this.host.clientLeft;
            const y = this.pointerPosition.clientY-bounds.top-this.host.clientTop;
            if (x >= 0 && y >= 0 && x <= this.width && y <= this.height)
                point = {x:(x-this.view.x)/this.view.scale,y:(y-this.view.y)/this.view.scale};
        }
        const key = JSON.stringify(point);
        if (key === this.publishedPointerKey) return;
        this.publishedPointerKey = key;
        this.presentation.publishPointer(point);
    }
    setPresentedPointer(point) { this.presentedPointer = point; this.drawPresentationPointer(); }
    drawPresentationPointer() {
        const marker = this.presentationPointer, point = this.presentedPointer;
        if (!marker) return;
        if (!point || !this.view) { marker.hidden = true; return; }
        const x = this.view.x+point.x*this.view.scale, y = this.view.y+point.y*this.view.scale;
        marker.hidden = x < 0 || y < 0 || x > this.width || y > this.height;
        marker.style.left = `${x}px`; marker.style.top = `${y}px`;
    }
    closePresentation() { this.pointerLeave?.(); this.presentationCleared = true; this.presentation?.publish(null); }
    draw() {
        const {x,y,scale} = this.view;
        this.drawMovementRanges();
        this.drawMovementIndicators(); this.drawTargetRing();
        this.world.position.set(x,y); this.world.scale.set(scale);
        for (const token of this.characterTokens.values()) this.decorateToken(token);
        this.tokens?.position.set(x,y); this.tokens?.scale.set(scale);
        this.fog?.position.set(x,y); this.fog?.scale.set(scale);
        const left=Math.max(0,-x/scale), top=Math.max(0,-y/scale);
        const right=Math.min(this.imageWidth,(this.width-x)/scale), bottom=Math.min(this.imageHeight,(this.height-y)/scale);
        this.drawTiles(left,top,right,bottom);
        this.grid.clear();
        if(left>right || top>bottom) return;
        const {gridSize:size,gridColour:color,gridOpacity:opacity,gridlineWidth:width}=this.scene;
        if(!opacity) return;
        // Screen-space grid strokes remain the configured width at every zoom.
        for(let i=Math.ceil(left/size);i<=Math.floor(right/size);i++)
            this.grid.moveTo(x+i*size*scale,y+top*scale).lineTo(x+i*size*scale,y+bottom*scale);
        for(let i=Math.ceil(top/size);i<=Math.floor(bottom/size);i++)
            this.grid.moveTo(x+left*scale,y+i*size*scale).lineTo(x+right*scale,y+i*size*scale);
        this.grid.stroke({color,alpha:opacity/100,width});
    }
    drawTiles(left,top,right,bottom) {
        // Use bounded textures and upload only visible tiles of large backgrounds.
        const size=1024, visible=new Set();
        const firstCol=Math.max(0,Math.floor(left/size)), firstRow=Math.max(0,Math.floor(top/size));
        const lastCol=Math.min(Math.ceil(this.imageWidth/size)-1,Math.floor(right/size));
        const lastRow=Math.min(Math.ceil(this.imageHeight/size)-1,Math.floor(bottom/size));
        if(this.view.scale>this.overviewScale && (lastCol-firstCol+1)*(lastRow-firstRow+1)<=64) {
            for(let row=firstRow;row<=lastRow;row++) for(let col=firstCol;col<=lastCol;col++) {
                const key=`${col},${row}`; visible.add(key);
                if(this.tiles.has(key)) continue;
                const canvas=document.createElement('canvas');
                canvas.width=Math.min(size,this.imageWidth-col*size); canvas.height=Math.min(size,this.imageHeight-row*size);
                canvas.getContext('2d').drawImage(this.image,col*size,row*size,canvas.width,canvas.height,0,0,canvas.width,canvas.height);
                const texture=PIXI.Texture.from(canvas), sprite=new PIXI.Sprite(texture);
                sprite.position.set(col*size,row*size); this.background.addChild(sprite);
                this.tiles.set(key,{sprite,texture});
            }
        }
        for(const [key,tile] of this.tiles) if(!visible.has(key)) {
            tile.sprite.destroy(); tile.texture.destroy(true); this.tiles.delete(key);
        }
    }
    apply() {
        if (this.disposed || !this.view) return;
        if (this.world) this.draw();
        this.publishPresentation();
        this.publishPresentationPointer();
        this.drawPresentationPointer();
        if (this.readOnly) return;
        try { sessionStorage.setItem(this.key, JSON.stringify({ ...this.view, width:this.width, height:this.height,
            imageWidth:this.imageWidth, imageHeight:this.imageHeight })); } catch { }
    }
    zoom(factor, point = { x:this.width/2, y:this.height/2 }) {
        this.cancelPan();
        const fit = Math.min(this.width/this.imageWidth, this.height/this.imageHeight);
        this.view = zoomView(this.view, factor, point, fit/8, Math.max(8, fit*8)); this.apply();
    }
    zoomIn() { this.zoom(2); }
    zoomOut() { this.zoom(.5); }
    fit() { this.cancelPan(); this.view = fitView(this.width, this.height, this.imageWidth, this.imageHeight); this.apply(); }
    async dispose() {
        if (this.disposed) return;
        this.cancelPan();
        if (this.animateTurn) this.app?.ticker.remove(this.animateTurn);
        this.apply(); this.disposed = true; this.resizeObserver?.disconnect();
        for (const [name, handler] of [['dblclick',this.doubleClick],['pointerdown',this.down],['pointermove',this.move],['pointerup',this.up],['pointercancel',this.up],['lostpointercapture',this.up],['wheel',this.wheel]])
            if (handler) this.host.removeEventListener(name, handler);
        if(this.dragging && this.host.hasPointerCapture(this.dragging.id)) this.host.releasePointerCapture(this.dragging.id);
        this.dragging=null;
        for (const token of this.characterTokens.values()) { token.texture?.destroy(true); token.fallbackTexture?.destroy(true); token.filter?.destroy(); }
        this.characterTokens.clear();
        this.movementTrails.clear(); this.movementLabels.clear();
        this.presentation?.close();
        this.detachCastWindow?.();
        this.presentationPointer?.remove();
        if (this.pointerLeave) {
            this.host.removeEventListener('pointerleave',this.pointerLeave);
            this.host.removeEventListener('pointercancel',this.pointerLeave);
            globalThis.removeEventListener('blur',this.pointerLeave);
            globalThis.removeEventListener('pagehide',this.pointerLeave);
        }
        if (this.visibilityChange) document.removeEventListener('visibilitychange',this.visibilityChange);
        if(this.initialized) this.app.destroy(true,{children:true});
        for(const tile of this.tiles?.values() ?? []) tile.texture.destroy(true);
        this.tiles?.clear(); this.overviewTexture?.destroy(true); this.image=null;
        if(this.preventDrag) {
            this.host.removeEventListener('dragstart',this.preventDrag);
            this.host.removeEventListener('selectstart',this.preventDrag);
        }
        this.host.style.cursor = ''; this.host.title = '';
        await Promise.allSettled([...this.pendingSaves]);
    }
}
