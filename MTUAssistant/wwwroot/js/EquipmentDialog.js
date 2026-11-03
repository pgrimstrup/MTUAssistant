const handlers = new WeakMap();
export function open(dialog, reference) {
    const cancel = event => {
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
