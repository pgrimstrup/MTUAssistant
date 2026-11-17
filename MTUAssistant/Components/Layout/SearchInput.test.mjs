import assert from 'node:assert/strict';
import { scrollActiveIntoView } from '../../wwwroot/js/SearchInput.js';

// The highlighted result is scrolled to the nearest edge of the list, never centred.
const calls = [];
const active = { scrollIntoView: options => calls.push(options) };
const list = { querySelector: selector => (selector === 'li.active' ? active : null) };
scrollActiveIntoView(list);
assert.deepEqual(calls, [{ block: 'nearest' }]);

// No highlighted result, or no list, does nothing and never throws.
scrollActiveIntoView({ querySelector: () => null });
scrollActiveIntoView(null);
scrollActiveIntoView(undefined);
assert.equal(calls.length, 1);
console.log('Search input active result scrolling passed.');
