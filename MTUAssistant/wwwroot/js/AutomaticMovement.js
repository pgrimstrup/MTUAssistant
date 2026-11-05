export function automaticMoveCount(distance,speed) {
    if (!Number.isFinite(distance) || !Number.isFinite(speed) || speed <= 0) return null;
    return Math.max(1,Math.min(3,Math.ceil(Math.max(0,distance-.8)/speed-1e-9)));
}
export function automaticMoveRange(context,column,row) {
    const distance=Math.hypot(column-context.column,row-context.row)*1.5;
    const moves=automaticMoveCount(distance,context.speed);
    return moves === null ? null : {moves,range:{id:context.id,column:context.column,row:context.row,
        diameter:1.5+context.speed*2*moves,colour:context.colour,isVisible:context.isVisible}};
}
