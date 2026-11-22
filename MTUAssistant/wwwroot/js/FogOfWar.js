export const fogKey = (column,row) => `${column},${row}`;
export function validFog(cells) {
    return Array.isArray(cells) && cells.every(c => c && Number.isInteger(c.column) && c.column >= 0 && Number.isInteger(c.row) && c.row >= 0);
}
export function fogCells(keys) {
    return [...keys].map(key => { const [column,row] = key.split(',').map(Number); return {column,row}; });
}
export function fogVisible(keys,column,row) {
    return keys == null || keys.has(fogKey(column,row));
}
// Shift-drag clears fog and Ctrl-drag sets it. Both modifiers together are ambiguous, so neither applies.
export function fogStrokeMode(event) {
    if (!event || Boolean(event.shiftKey) === Boolean(event.ctrlKey)) return null;
    return event.shiftKey ? 'clear' : 'set';
}
export const fogBrushSizes = [1,3,5,9];
// Move one size up (direction > 0) or down, staying within the dropdown's sizes.
export function stepFogBrush(size,direction) {
    const index = fogBrushSizes.indexOf(size);
    if (index < 0) return 3;
    return fogBrushSizes[Math.min(fogBrushSizes.length-1,Math.max(0,index+Math.sign(direction)))];
}
// Interpolate in cell space so fast pointer motion cannot leave gaps.
export function paintFog(keys,from,to,size,reveal,columns,rows) {
    const steps = Math.max(Math.abs(to.column-from.column),Math.abs(to.row-from.row),1);
    const radius = Math.floor(size/2);
    for (let i=0;i<=steps;i++) {
        const column = Math.round(from.column+(to.column-from.column)*i/steps);
        const row = Math.round(from.row+(to.row-from.row)*i/steps);
        for(let y=Math.max(0,row-radius);y<=Math.min(rows-1,row+radius);y++)
            for(let x=Math.max(0,column-radius);x<=Math.min(columns-1,column+radius);x++) {
                const key=fogKey(x,y);
                if(reveal) keys.add(key); else keys.delete(key);
            }
    }
}
// Emit concealed rectangles, merging uninterrupted rows and adjacent revealed cells.
// Cost depends on revealed cells rather than the number of squares on a large map.
export function fogRectangles(keys,width,height,size) {
    if(keys == null) return [];
    const byRow=new Map(), rectangles=[];
    for(const key of keys) {
        const [column,row]=key.split(',').map(Number);
        if(column*size>=width || row*size>=height) continue;
        if(!byRow.has(row)) byRow.set(row,[]);
        byRow.get(row).push(column);
    }
    const rect=(x,y,w,h)=>{if(w>0 && h>0)rectangles.push({x,y,width:w,height:h});};
    let top=0;
    for(const [row,columns] of [...byRow].sort((a,b)=>a[0]-b[0])) {
        const y=row*size, h=Math.min(size,height-y);
        rect(0,top,width,y-top);
        let left=0;
        for(const column of columns.sort((a,b)=>a-b)) {
            const x=column*size;
            rect(left,y,x-left,h);
            left=Math.min(width,x+size);
        }
        rect(left,y,width-left,h);
        top=y+h;
    }
    rect(0,top,width,height-top);
    return rectangles;
}
export function fogEqual(a,b) {
    if(a == null || b == null) return a == b;
    if(a.size !== b.size) return false;
    for(const key of a) if(!b.has(key)) return false;
    return true;
}
// Players see fog fade in over this many squares from the revealed area. The GM view keeps hard edges.
export const fogGradientSquares = 5;
// Shape of the fade: 1 darkens evenly; below 1 darkens sooner, hiding more of the map near the edge.
export const fogGradientCurve = 1;
// Texels per square in the gradient texture: 4 normally, fewer on huge grids to keep it near 1024 across.
export function fogGradientSamples(columns,rows) {
    return Math.max(1,Math.min(4,Math.floor(1024/Math.max(columns,rows,1))));
}
// Fog darkness per texel (0 clear, 255 black) at `samples` texels per square, or null when fog is off.
// Revealed squares are clear. Hidden squares darken with straight-line distance from the nearest revealed
// square and are fully black from `depth` squares in. It is built from the revealed squares alone.
export function fogGradientField(keys,columns,rows,samples,depth=fogGradientSquares,curve=fogGradientCurve) {
    if(keys == null) return null;
    const width=columns*samples, height=rows*samples, alpha=new Uint8Array(width*height);
    // Everything further than `reach` texels from a revealed texel is black, so distances stop there.
    const reach=Math.ceil(depth*samples)+1, far=reach+1, span=Math.ceil(depth)+1;
    const revealed=new Uint8Array(columns*rows);
    for(const key of keys) {
        const [column,row]=key.split(',').map(Number);
        if(column>=0 && column<columns && row>=0 && row<rows) revealed[row*columns+column]=1;
    }
    // For each row of squares: texels from each texel to the nearest revealed one on its left or right.
    const run=new Uint16Array(width*rows);
    for(let row=0;row<rows;row++) {
        const start=row*width;
        let distance=far;
        for(let x=0;x<width;x++) {
            distance=revealed[row*columns+Math.floor(x/samples)] ? 0 : Math.min(far,distance+1);
            run[start+x]=distance;
        }
        distance=far;
        for(let x=width-1;x>=0;x--) {
            distance=run[start+x] === 0 ? 0 : Math.min(far,distance+1);
            if(distance<run[start+x]) run[start+x]=distance;
        }
    }
    // Combine that with the vertical offset to each nearby row of squares for the straight-line distance.
    for(let y=0;y<height;y++) {
        const row=Math.floor(y/samples), first=Math.max(0,row-span), last=Math.min(rows-1,row+span);
        for(let x=0;x<width;x++) {
            let best=far*far;
            for(let r=first;r<=last;r++) {
                const across=run[r*width+x];
                if(across>reach) continue;
                const down=r<row ? y-(r*samples+samples-1) : r>row ? r*samples-y : 0;
                const squared=across*across+down*down;
                if(squared<best) best=squared;
            }
            // Distances run between texel centres; half a texel back is the edge of the revealed square.
            const shade=Math.min(1,Math.max(0,Math.sqrt(best)-.5)/(samples*depth));
            alpha[y*width+x]=Math.round(255*(curve === 1 ? shade : Math.pow(shade,curve)));
        }
    }
    return {width,height,alpha};
}
export function fogPublicState(map) {
    const tokens=[...map.characterTokens.values()].filter(t=>fogVisible(map.revealedFog,t.column,t.row));
    const ids=new Set(tokens.filter(t=>t.isVisible === true).map(t=>t.id));
    const visiblePoint=(column,row)=>fogVisible(map.revealedFog,column,row);
    const trails=[...map.movementTrails.values()].filter(t=>ids.has(t.id) && visiblePoint(t.fromColumn,t.fromRow) && visiblePoint(t.toColumn,t.toRow));
    // A movement radius can expose a position from before movement: suppress it in covered cells too.
    const ranges=map.displayMovementRanges().filter(r=>ids.has(r.id) && visiblePoint(r.column,r.row));
    const tracker=(map.playerTracker ?? []).filter(e=>map.revealedFog == null || ids.has(e.id));
    const targets=map.targetIds ?? (map.targetId == null ? [] : [map.targetId]);
    const summary=map.actionSummary;
    const summaryVisible=summary && ids.has(summary.id) && targets.every(id=>ids.has(id));
    return {tokens,trails,ranges,tracker,summary:summaryVisible ? summary : null,
        description:map.description && (map.revealedFog == null || ids.has(map.description.id)) ? map.description : null};
}
