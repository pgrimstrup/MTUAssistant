const handlers = new WeakMap();
// Escape on a dialog fires "cancel" on the dialog itself. A file input inside it also fires a "cancel"
// that bubbles up when its picker is dismissed, and that must not close the dialog.
export function isDialogCancel(dialog, event) {
    return event.target === dialog;
}
export function open(dialog, reference) {
    const cancel = event => {
        if (!isDialogCancel(dialog, event)) return;
        event.preventDefault();
        reference.invokeMethodAsync("Cancel");
    };
    handlers.set(dialog, cancel);
    dialog.addEventListener("cancel", cancel);
    if (!dialog.open) dialog.showModal();
}
export function close(dialog) {
    if (!dialog) return;
    const cancel = handlers.get(dialog);
    if (cancel) dialog.removeEventListener("cancel", cancel);
    handlers.delete(dialog);
    if (dialog.open) dialog.close();
}
