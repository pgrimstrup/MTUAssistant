import assert from 'node:assert/strict';
import { findHex, observeClicks } from '../../wwwroot/js/WorldSelection.js';

const width = 32 * Math.cos(Math.PI / 6);
// Every centre in the map, including all sector seams and alternating columns.
for (let col = 0; col < 512; col++) {
    for (let row = 0; row < 320; row++) {
        assert.deepEqual(findHex(1 + (col + .5) * width / 32, 1 + row + (col % 2 ? 1 : .5)), {
            sectorColumn: Math.floor(col / 32), sectorRow: Math.floor(row / 40),
            column: col % 32 + 1, row: row % 40 + 1
        });
    }
}
const handlers = new Map();
const canvas = {
    addEventListener(name, handler, options) { assert.equal(options.passive, true); handlers.set(name, handler); },
    removeEventListener(name) { handlers.delete(name); }
};
let clicks = 0;
const dispose = observeClicks(canvas, () => clicks++);
const fire = (name, props = {}) => handlers.get(name)({ pointerId: 1, isPrimary: true, button: 0, clientX: 10, clientY: 10, ...props });
fire('pointerdown'); fire('pointerup'); assert.equal(clicks, 1);
fire('pointerdown'); fire('pointermove', {clientX: 15}); fire('pointermove'); fire('pointerup'); assert.equal(clicks, 1);
fire('pointerdown'); fire('pointerup', {clientY: 20}); assert.equal(clicks, 1);
fire('pointerdown'); fire('pointerdown', {pointerId: 2, isPrimary: false}); fire('pointerup', {pointerId: 2}); fire('pointerup'); assert.equal(clicks, 1);
for (const name of ['pointercancel', 'pointerleave', 'wheel']) {
    fire('pointerdown'); fire(name); fire('pointerup'); assert.equal(clicks, 1);
}
fire('pointerdown', {button: 2}); fire('pointerup', {button: 2}); assert.equal(clicks, 1);
dispose(); assert.equal(handlers.size, 0);
console.log('World selection: all map centres, click/drag/pinch/cancel/wheel and cleanup checks passed.');
