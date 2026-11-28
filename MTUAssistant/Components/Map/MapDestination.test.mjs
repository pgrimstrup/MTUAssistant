import assert from 'node:assert/strict';
import { DESTINATION_STORAGE_KEY, loadMapDestination, saveMapDestination } from '../../wwwroot/js/MapDestination.js';

const memory = () => {
    const values = new Map();
    return { values, getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
};

// The Galaxy Map and Passengers and Cargo share one key and one format.
assert.equal(DESTINATION_STORAGE_KEY, 'mtu.map.destination');
const storage = memory();
assert.equal(loadMapDestination(storage), null, 'no destination saved');
saveMapDestination({ sectorId: 22, hex: '0205', extra: 'ignored' }, storage);
assert.equal(storage.values.get(DESTINATION_STORAGE_KEY), '{"sectorId":22,"hex":"0205"}', 'only sector ID and hex are stored');
assert.deepEqual(loadMapDestination(storage), { sectorId: 22, hex: '0205' });
saveMapDestination(null, storage);
assert.equal(storage.values.has(DESTINATION_STORAGE_KEY), false, 'clearing removes the saved destination');
console.log('Map destination save, load and clear passed.');

// Anything malformed reads as no destination, so a page never preselects a bad value.
for (const bad of ['{', 'null', '[]', '{"sectorId":"22","hex":"0205"}', '{"sectorId":0,"hex":"0205"}', '{"sectorId":1.5,"hex":"0205"}',
    '{"sectorId":22}', '{"sectorId":22,"hex":"205"}', '{"sectorId":22,"hex":"0000"}', '{"sectorId":22,"hex":"3301"}', '{"sectorId":22,"hex":"0141"}']) {
    storage.values.set(DESTINATION_STORAGE_KEY, bad);
    assert.equal(loadMapDestination(storage), null, `invalid saved destination ${bad}`);
}
for (const hex of ['0101', '3240', '1910']) {
    storage.values.set(DESTINATION_STORAGE_KEY, JSON.stringify({ sectorId: 5, hex }));
    assert.deepEqual(loadMapDestination(storage), { sectorId: 5, hex }, `valid hex ${hex}`);
}
storage.values.set(DESTINATION_STORAGE_KEY, '{"sectorId":5,"hex":"0101"}');
saveMapDestination({ sectorId: 5, hex: 'bad' }, storage);
assert.equal(storage.values.get(DESTINATION_STORAGE_KEY), '{"sectorId":5,"hex":"0101"}', 'an invalid destination is never saved');
// The map keeps the hex the jump route starts from; a bad one makes the whole value invalid.
saveMapDestination({ sectorId: 5, hex: '0101', from: { sectorId: 6, hex: '3240', extra: 1 } }, storage);
assert.equal(storage.values.get(DESTINATION_STORAGE_KEY), '{"sectorId":5,"hex":"0101","from":{"sectorId":6,"hex":"3240"}}');
assert.deepEqual(loadMapDestination(storage), { sectorId: 5, hex: '0101', from: { sectorId: 6, hex: '3240' } });
for (const from of [null, 7, { sectorId: 6 }, { sectorId: 6, hex: '0000' }]) {
    storage.values.set(DESTINATION_STORAGE_KEY, JSON.stringify({ sectorId: 5, hex: '0101', from }));
    assert.equal(loadMapDestination(storage), null, `invalid route start ${JSON.stringify(from)}`);
}
console.log('Map destination validation passed.');

// Blocked or missing browser storage never throws.
const blocked = { getItem() { throw Error('blocked'); }, setItem() { throw Error('blocked'); }, removeItem() { throw Error('blocked'); } };
assert.equal(loadMapDestination(blocked), null);
saveMapDestination({ sectorId: 5, hex: '0101' }, blocked);
saveMapDestination(null, blocked);
console.log('Map destination storage failure checks passed.');
