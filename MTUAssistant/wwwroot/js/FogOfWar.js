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
