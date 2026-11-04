// Scroll only the tracker panel, leaving the battlemap and page camera untouched.
export function scrollCurrentRow(container,id) {
    if (!container || !Number.isInteger(id)) return;
    const row = container.querySelector(`[data-combatant-id="${id}"]`);
    if (!row) return;
    const bounds = container.getBoundingClientRect(), item = row.getBoundingClientRect();
    const panelTop = bounds.top + container.clientTop, bottom = panelTop + container.clientHeight;
    const heading = container.querySelector("[data-tracker-heading]");
    const top = Math.max(panelTop,heading?.getBoundingClientRect().bottom ?? panelTop);
    const delta = item.top < top ? item.top-top : item.bottom > bottom ? item.bottom-bottom : 0;
    if (delta) container.scrollBy({top:delta,behavior:'smooth'});
}
