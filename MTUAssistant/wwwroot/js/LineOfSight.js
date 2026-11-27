// Line-of-sight fog (experimental). Polygons are solid: nothing inside one is seen, and sight does not pass
// through one. Like polygon points, positions are counted in half squares from the map's top-left corner,
// so the centre of square (column,row) is (2*column+1, 2*row+1) and the geometry stays in whole numbers.
const squareCentre = (column,row) => ({x:2*column+1,y:2*row+1});
// Squares are 1.5 m across.
export const squareMetres = 1.5;
// How far a Traveller sees, in squares: the higher of the map's Ambient Light and the Traveller's
// own Visual Distance, both in metres. 0 means sight clears no fog and the GM reveals the map by hand.
export function sightRangeSquares(ambientMetres,characterMetres) {
    return Math.max(Number(ambientMetres) || 0,Number(characterMetres) || 0,0)/squareMetres;
}
// How far to step off a line of sight, in half squares, when checking what lies on either side of it.
const nudge = 1e-3;
function onSide(position,a,b) {
    const ex=b.x-a.x, ey=b.y-a.y, px=position.x-a.x, py=position.y-a.y, length=ex*ex+ey*ey;
    if(!length) return Math.hypot(px,py)<=1e-9;
    if(Math.abs(ex*py-ey*px)>1e-9*Math.sqrt(length)) return false;
    const along=px*ex+py*ey;
    return along>=-1e-9 && along<=length+1e-9;
}
// True when a position is strictly inside a polygon (even-odd rule). A position on its outline is not inside.
export function insidePolygon(position,points) {
    let inside=false;
    for(let i=0,j=points.length-1;i<points.length;j=i++) {
        const a=points[i], b=points[j];
        if(onSide(position,a,b)) return false;
        if((a.y>position.y) !== (b.y>position.y) && position.x<(b.x-a.x)*(position.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
    }
    return inside;
}
const solid = (position,polygons) => polygons.some(polygon => insidePolygon(position,polygon.points));
// True when there is solid ground right beside a position on both sides of a line of sight through it. The
// line is squeezed shut there, as between two polygons that touch at a corner or share a side.
function pinched(position,dx,dy,polygons) {
    const heading=Math.atan2(dy,dx);
    let left=false, right=false;
    for(let step=1;step<18 && !(left && right);step++) {
        const turn=step*Math.PI/18;
        left ||= solid({x:position.x+nudge*Math.cos(heading+turn),y:position.y+nudge*Math.sin(heading+turn)},polygons);
        right ||= solid({x:position.x+nudge*Math.cos(heading-turn),y:position.y+nudge*Math.sin(heading-turn)},polygons);
    }
    return left && right;
}
// True when nothing solid lies on the straight line between two positions. Touching one polygon's corner or
// running along one polygon's side does not block. Crossing the inside of a polygon does, and so does passing
// between polygons that leave no gap. `polygons` is [{points}].
export function sightClear(from,to,polygons) {
    const dx=to.x-from.x, dy=to.y-from.y, length=dx*dx+dy*dy;
    if(!length) return !solid(from,polygons);
    // Where the line meets an outline, as fractions of the way from `from` to `to`.
    const cuts=[];
    for(const {points} of polygons)
        for(let i=0,j=points.length-1;i<points.length;j=i++) {
            const a=points[j], b=points[i], ex=b.x-a.x, ey=b.y-a.y, ax=a.x-from.x, ay=a.y-from.y;
            const denominator=dx*ey-dy*ex;
            if(denominator !== 0) {
                const t=(ax*ey-ay*ex)/denominator, u=(ax*dy-ay*dx)/denominator;
                if(t>=0 && t<=1 && u>=0 && u<=1) cuts.push(t);
            } else if(ax*dy-ay*dx === 0) {
                // The side lies along the line of sight, so only its two ends matter.
                for(const end of [a,b]) {
                    const t=((end.x-from.x)*dx+(end.y-from.y)*dy)/length;
                    if(t>=0 && t<=1) cuts.push(t);
                }
            }
        }
    const at=t=>({x:from.x+dx*t,y:from.y+dy*t});
    if(!cuts.length) return !solid(at(.5),polygons);
    cuts.push(0,1); cuts.sort((p,q)=>p-q);
    let previous=0;
    for(const cut of cuts) {
        if(cut-previous<1e-9) continue;
        // Between two neighbouring cuts the line is wholly inside or wholly outside, so the midpoint decides.
        const middle=at((previous+cut)/2);
        if(solid(middle,polygons) || pinched(middle,dx,dy,polygons)) return false;
        if(cut<1 && pinched(at(cut),dx,dy,polygons)) return false;
        previous=cut;
    }
    return true;
}
// The squares a viewer standing on a square can see: those whose centre is within `range` squares of the
// viewer's centre, is not inside a polygon, and has a clear straight line from the viewer's centre.
// A viewer standing inside a polygon sees nothing. Returns [{column,row}], the viewer's own square included.
export function visibleSquares(viewer,polygons,columns,rows,range) {
    if(!viewer || !Number.isInteger(viewer.column) || !Number.isInteger(viewer.row) || !(range>=0)
        || viewer.column<0 || viewer.row<0 || viewer.column>=columns || viewer.row>=rows) return [];
    const eye=squareCentre(viewer.column,viewer.row), reach=Math.floor(range);
    // Only polygons that come within reach of the viewer can matter.
    const near=[];
    for(const polygon of polygons ?? []) {
        const points=polygon.points;
        let minX=Infinity, minY=Infinity, maxX=-Infinity, maxY=-Infinity;
        for(const point of points) { minX=Math.min(minX,point.x); minY=Math.min(minY,point.y); maxX=Math.max(maxX,point.x); maxY=Math.max(maxY,point.y); }
        if(maxX<eye.x-2*reach-1 || minX>eye.x+2*reach+1 || maxY<eye.y-2*reach-1 || minY>eye.y+2*reach+1) continue;
        if(insidePolygon(eye,points)) return [];
        near.push({points,minX,minY,maxX,maxY});
    }
    const seen=[];
    for(let row=Math.max(0,viewer.row-reach);row<=Math.min(rows-1,viewer.row+reach);row++)
        for(let column=Math.max(0,viewer.column-reach);column<=Math.min(columns-1,viewer.column+reach);column++) {
            const across=column-viewer.column, down=row-viewer.row;
            if(across*across+down*down>range*range) continue;
            const target=squareCentre(column,row);
            const left=Math.min(eye.x,target.x), right=Math.max(eye.x,target.x), top=Math.min(eye.y,target.y), bottom=Math.max(eye.y,target.y);
            // Polygons whose bounds miss the line cannot block it or hold its far end.
            const inWay=near.filter(polygon => polygon.maxX>=left && polygon.minX<=right && polygon.maxY>=top && polygon.minY<=bottom);
            if(!solid(target,inWay) && sightClear(eye,target,inWay)) seen.push({column,row});
        }
    return seen;
}
