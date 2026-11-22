// Polygons drawn on the Battlemap. Experimental and GM-only: nothing here enters the player snapshot.
// Points are counted in half squares from the map's top-left corner, so a point is either a square
// corner (both values even) or a square centre (both values odd).
// An active polygon is solid and blocks line of sight. An inactive one is ignored, like an open door.
export const polygonColour = 0xff0000;
// Outside Polygon mode polygons are drawn faintly in blue so they do not get in the way of play.
export const polygonIdleColour = 0x3b82f6;
// How a polygon is drawn on the GM map: its colour and the opacity of its outline and of its fill (0 = no fill).
// Active polygons are shaded; inactive ones are an outline only.
export function polygonStyle(editing,active) {
    return editing
        ? {colour:polygonColour,line:active ? 1 : .45,fill:active ? .1 : 0}
        : {colour:polygonIdleColour,line:.3,fill:active ? .15 : 0};
}
export const maxPolygons = 200, maxPolygonPoints = 200;
// A press that moves less than this many pixels is a click that adds a point; further is a drag that pans.
export const polygonClickSlop = 4;
// Right-clicking this close to an outline, in screen pixels, picks that polygon.
export const polygonHitPixels = 6;
export const samePolygonPoint = (a,b) => a == null || b == null ? a == b : a.x === b.x && a.y === b.y;
export function validPolygonPoint(point) {
    return point != null && Number.isInteger(point.x) && Number.isInteger(point.y) && point.x >= 0 && point.y >= 0 && (point.x-point.y)%2 === 0;
}
export function validPolygons(polygons) {
    return Array.isArray(polygons) && polygons.length <= maxPolygons && polygons.every(polygon => Array.isArray(polygon?.points)
        && polygon.points.length >= 3 && polygon.points.length <= maxPolygonPoints && polygon.points.every(validPolygonPoint)
        && (polygon.active === undefined || typeof polygon.active === 'boolean'));
}
// Map pixels for a point.
export const polygonPosition = (point,size) => ({x:point.x*size/2,y:point.y*size/2});
// The nearest square corner or square centre to a map position, or null when the position is off the image.
export function polygonSnap(position,size,imageWidth,imageHeight) {
    if(!position || !(size>0) || !(position.x>=0 && position.y>=0 && position.x<=imageWidth && position.y<=imageHeight)) return null;
    const maxX=Math.floor(2*imageWidth/size), maxY=Math.floor(2*imageHeight/size);
    const column=Math.floor(position.x/size), row=Math.floor(position.y/size);
    let nearest=null, nearestDistance=Infinity;
    // Corners and centres of the square under the pointer and of its neighbours, leaving out any that are off
    // the image. The neighbours matter in a partial square at the map edge, where the nearest ones are cut off.
    for(let y=2*row-1;y<=2*row+3;y++) for(let x=2*column-1;x<=2*column+3;x++) {
        if(x<0 || y<0 || x>maxX || y>maxY || (x-y)%2 !== 0) continue;
        const distance=Math.hypot(x*size/2-position.x,y*size/2-position.y);
        if(distance<nearestDistance) { nearest={x,y}; nearestDistance=distance; }
    }
    return nearest;
}
// The result of clicking a snap point while drawing. Clicking the first point closes the polygon once it
// has three points. Clicking the point just placed, or the first point too early, changes nothing.
export function addPolygonPoint(points,point) {
    if(samePolygonPoint(points[0],point)) return {points,closed:points.length>=3};
    if(samePolygonPoint(points.at(-1),point) || points.length>=maxPolygonPoints) return {points,closed:false};
    return {points:[...points,point],closed:false};
}
function segmentDistance(position,a,b) {
    const dx=b.x-a.x, dy=b.y-a.y, length=dx*dx+dy*dy;
    const t=length ? Math.max(0,Math.min(1,((position.x-a.x)*dx+(position.y-a.y)*dy)/length)) : 0;
    return Math.hypot(position.x-a.x-t*dx,position.y-a.y-t*dy);
}
// The most recently drawn polygon at a map position: inside it, or within `tolerance` map pixels of its
// outline. Returns its index, or -1 when there is none.
export function polygonAt(polygons,position,size,tolerance=0) {
    for(let index=polygons.length-1;index>=0;index--) {
        const points=polygons[index].points.map(point=>polygonPosition(point,size));
        let inside=false, near=false;
        for(let i=0,j=points.length-1;i<points.length;j=i++) {
            const a=points[i], b=points[j];
            if(segmentDistance(position,a,b)<=tolerance) { near=true; break; }
            if((a.y>position.y) !== (b.y>position.y) && position.x<(b.x-a.x)*(position.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
        }
        if(near || inside) return index;
    }
    return -1;
}
