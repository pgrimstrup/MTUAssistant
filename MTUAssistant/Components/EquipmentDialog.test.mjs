import assert from 'node:assert/strict';
import { isDialogCancel, open } from '../wwwroot/js/EquipmentDialog.js';

// A minimal dialog that keeps its listeners, so a "cancel" event can be sent to it.
function fakeDialog() {
    const listeners = [];
    return {
        open: false,
        showModal() { this.open = true; },
        addEventListener(type, listener) { if (type === 'cancel') listeners.push(listener); },
        removeEventListener() {},
        fire(target) {
            let prevented = false;
            for (const listener of listeners) listener({ target, preventDefault: () => { prevented = true; } });
            return prevented;
        },
    };
}

const dialog = fakeDialog();
const fileInput = { type: 'file' };
assert.equal(isDialogCancel(dialog, { target: dialog }), true);
assert.equal(isDialogCancel(dialog, { target: fileInput }), false);

const calls = [];
open(dialog, { invokeMethodAsync: name => calls.push(name) });
assert.equal(dialog.open, true);

// Dismissing a file picker inside the dialog bubbles "cancel" from the input: the dialog stays open.
assert.equal(dialog.fire(fileInput), false);
assert.deepEqual(calls, []);

// Escape fires "cancel" on the dialog itself: it is handed to Blazor, which decides whether to close.
assert.equal(dialog.fire(dialog), true);
assert.deepEqual(calls, ['Cancel']);
