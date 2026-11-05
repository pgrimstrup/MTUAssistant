const panels = new WeakMap();
const storageKey = 'mtu.navigation.collapsed';

export function initialize(page, sidebar, button, storage) {
    dispose(page);
    let collapsed = false;
    try { storage ??= globalThis.localStorage; collapsed = storage.getItem(storageKey) === 'true'; } catch { }
    const render = () => {
        sidebar.hidden = collapsed;
        button.setAttribute('aria-expanded', String(!collapsed));
        const label = collapsed ? 'Expand navigation' : 'Collapse navigation';
        button.setAttribute('aria-label', label);
        button.title = label;
    };
    const toggle = () => {
        collapsed = !collapsed;
        render();
        try { storage.setItem(storageKey, String(collapsed)); } catch { }
        globalThis.dispatchEvent?.(new Event('resize'));
    };
    button.addEventListener('click', toggle);
    panels.set(page, {button, toggle});
    render();
}

export function dispose(page) {
    const panel = panels.get(page);
    if (!panel) return;
    panel.button.removeEventListener('click', panel.toggle);
    panels.delete(page);
}
