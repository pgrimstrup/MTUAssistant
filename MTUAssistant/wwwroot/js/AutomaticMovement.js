// The first burstMoves Moves (speed bursts) cover double the per-Move distance.
export function automaticMoveReach(moves,burstMoves=0) {
    return moves+Math.min(moves,Math.max(0,burstMoves||0));
}
export function automaticMoveCount(distance,speed,burstMoves=0) {
    if (!Number.isFinite(distance) || !Number.isFinite(speed) || speed <= 0) return null;
    const needed=Math.max(0,distance-.8)/speed-1e-9;
    for (let moves=1; moves<3; moves++) if (automaticMoveReach(moves,burstMoves) >= needed) return moves;
    return 3;
}
export function automaticMoveRange(context,column,row) {
    const distance=Math.hypot(column-context.column,row-context.row)*1.5;
    const moves=automaticMoveCount(distance,context.speed,context.burstMoves);
    return moves === null ? null : {moves,range:{id:context.id,column:context.column,row:context.row,
        diameter:1.5+context.speed*2*automaticMoveReach(moves,context.burstMoves),colour:context.colour,isVisible:context.isVisible}};
}
