import { presentationSession } from './PresentationSync.js';
import { connectBattlemapPresentation, playerTokens } from './BattlemapPresentationSync.js';
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
        this.world = new PIXI.Container();
        this.background = new PIXI.Container();
        this.tokens = new PIXI.Container();
        this.fog = new PIXI.Container();
        this.grid = new PIXI.Graphics();
        this.world.addChild(this.background);
        this.app.stage.addChild(this.world, this.grid, this.tokens, this.fog);
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
        this.down = event => {
            if (this.readOnly || event.button !== 0 || this.dragging) return;
            event.preventDefault();
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
            if (token?.saving) return;
            this.dragging = token ? { id:event.pointerId, token, column:token.column, row:token.row,
                offsetX:point.x-token.column*this.scene.gridSize, offsetY:point.y-token.row*this.scene.gridSize }
                : { id:event.pointerId, x:event.clientX, y:event.clientY };
            this.host.setPointerCapture(event.pointerId);
        };
        this.move = event => {
            if (this.readOnly) return;
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
            if (event.type !== 'pointerup') { this.positionToken(drag.token, drag.column, drag.row); return; }
            if (drag.column === drag.token.column && drag.row === drag.token.row) return;
            drag.token.saving = true;
            this.trackSave((async () => {
                let saved = false;
                try { saved = await this.callback.invokeMethodAsync('MoveToken', drag.token.id, drag.token.column, drag.token.row); }
                finally {
                    if (!this.disposed && this.characterTokens.get(drag.token.id) === drag.token) {
                        if (!saved) this.positionToken(drag.token, drag.column, drag.row);
                        drag.token.saving = false;
                    }
                }
            })());
        };
        this.wheel = event => {
            event.preventDefault(); if (this.readOnly) return; const bounds = this.host.getBoundingClientRect();
            this.zoom(Math.exp(-event.deltaY * .0015), { x:event.clientX-bounds.left, y:event.clientY-bounds.top });
        };
        this.preventDrag = event => event.preventDefault();
        this.host.addEventListener('dragstart',this.preventDrag);
        this.host.addEventListener('selectstart',this.preventDrag);
        this.host.addEventListener('pointerdown', this.down);
        this.host.addEventListener('pointermove', this.move);
        this.host.addEventListener('pointerup', this.up);
        this.host.addEventListener('pointercancel', this.up);
        this.host.addEventListener('lostpointercapture', this.up);
        this.host.addEventListener('wheel', this.wheel, { passive:false });
        this.resizeObserver = new ResizeObserver(() => {
            const width = this.host.clientWidth, height = this.host.clientHeight;
            if (!width || !height || this.disposed) return;
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
    hitToken(point) {
        return [...this.characterTokens.values()].reverse().find(token =>
            point.x >= token.column*this.scene.gridSize && point.x < (token.column+1)*this.scene.gridSize &&
            point.y >= token.row*this.scene.gridSize && point.y < (token.row+1)*this.scene.gridSize);
    }
    positionToken(token, column, row) {
        token.column = column; token.row = row;
        token.container.position.set(column*this.scene.gridSize, row*this.scene.gridSize);
        this.publishPresentation();
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
        this.characterTokens.set(data.id, token);
        this.tokens.addChild(container);
        this.positionToken(token, data.column, data.row);
        this.refreshFallback(token);
        const fallback = token.fallback;
        container.addChild(fallback);
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
            fallback.visible = false; container.addChild(sprite);
        } catch { /* Keep the initials marker if the character image cannot be decoded. */ }
    }
    removeToken(id, publish = true) {
        const token = this.characterTokens.get(id);
        if (!token) return;
        if (this.dragging?.token === token) {
            const pointerId = this.dragging.id; this.dragging = null;
            if (this.host.hasPointerCapture(pointerId)) this.host.releasePointerCapture(pointerId);
        }
        token.container.destroy({children:true}); token.texture?.destroy(true); token.fallbackTexture?.destroy(true); token.filter?.destroy();
        this.characterTokens.delete(id);
        if (publish) this.publishPresentation();
    }
    tokenAppearance(token) {
        token.container.alpha = token.isVisible === false ? .4 : 1;
        if (token.isVisible === false) {
            token.filter ??= new PIXI.ColorMatrixFilter();
            token.filter.desaturate(); token.container.filters = [token.filter];
        } else token.container.filters = [];
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
        link.href = new URL(`battlemap-presentation?session=${encodeURIComponent(session)}`, document.baseURI).href;
        this.presentation = connectBattlemapPresentation({session, readOnly:false});
        this.publishPresentation();
    }
    publishPresentation() {
        if (!this.presentation || this.disposed || this.readOnly || this.presentationCleared) return;
        this.presentation.publish({scene:this.scene, tokens:playerTokens([...this.characterTokens.values()]),
            view:{...this.view, width:this.width,height:this.height,imageWidth:this.imageWidth,imageHeight:this.imageHeight}});
    }
    closePresentation() { this.presentationCleared = true; this.presentation?.publish(null); }
    draw() {
        const {x,y,scale} = this.view;
        this.world.position.set(x,y); this.world.scale.set(scale);
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
        if (this.readOnly) return;
        try { sessionStorage.setItem(this.key, JSON.stringify({ ...this.view, width:this.width, height:this.height,
            imageWidth:this.imageWidth, imageHeight:this.imageHeight })); } catch { }
    }
    zoom(factor, point = { x:this.width/2, y:this.height/2 }) {
        const fit = Math.min(this.width/this.imageWidth, this.height/this.imageHeight);
        this.view = zoomView(this.view, factor, point, fit/8, Math.max(8, fit*8)); this.apply();
    }
    zoomIn() { this.zoom(2); }
    zoomOut() { this.zoom(.5); }
    fit() { this.view = fitView(this.width, this.height, this.imageWidth, this.imageHeight); this.apply(); }
    async dispose() {
        if (this.disposed) return;
        this.apply(); this.disposed = true; this.resizeObserver?.disconnect();
        for (const [name, handler] of [['pointerdown',this.down],['pointermove',this.move],['pointerup',this.up],['pointercancel',this.up],['lostpointercapture',this.up],['wheel',this.wheel]])
            if (handler) this.host.removeEventListener(name, handler);
        if(this.dragging && this.host.hasPointerCapture(this.dragging.id)) this.host.releasePointerCapture(this.dragging.id);
        this.dragging=null;
        for (const token of this.characterTokens.values()) { token.texture?.destroy(true); token.fallbackTexture?.destroy(true); token.filter?.destroy(); }
        this.characterTokens.clear();
        this.presentation?.close();
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

