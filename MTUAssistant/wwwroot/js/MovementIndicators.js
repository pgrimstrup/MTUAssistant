// Measure between square centres (origin and target are {column,row}). Squares are 1.5 m.
export function measureGeometry(origin,target,gridSize,view) {
    return movementGeometry({fromColumn:origin.column,fromRow:origin.row,toColumn:target.column,toRow:target.row},gridSize,view);
}
// The square under a world point, clamped to the image so dragging off the map stops at its edge.
export function measureCell(point,gridSize,imageWidth,imageHeight) {
    const clamp=(value,size)=>Math.min(Math.max(0,Math.floor(value/gridSize)),Math.max(0,Math.ceil(size/gridSize)-1));
    return {column:clamp(point.x,imageWidth),row:clamp(point.y,imageHeight)};
}
export const measureLabel = distance => `${distance.toFixed(1)} m`;
export function movementGeometry(trail,gridSize,view) {
    const start={x:view.x+(trail.fromColumn+.5)*gridSize*view.scale,y:view.y+(trail.fromRow+.5)*gridSize*view.scale};
    const end={x:view.x+(trail.toColumn+.5)*gridSize*view.scale,y:view.y+(trail.toRow+.5)*gridSize*view.scale};
    return {start,end,midpoint:{x:(start.x+end.x)/2,y:(start.y+end.y)/2},distance:Math.hypot(trail.toColumn-trail.fromColumn,trail.toRow-trail.fromRow)*1.5};
}
export function weaponRangeBand(distance,profile) {
    if (!(profile?.range > 0)) return null;
    if (distance > 100 && !(profile.scope && profile.aimBonus >= 1)) return 'Extreme Range';
    return distance < profile.range*.25 ? 'Short Range' : distance < profile.range ? 'Medium Range'
        : distance < profile.range*2 ? 'Long Range' : distance < profile.range*4 ? 'Extreme Range' : 'Out of Range';
}
