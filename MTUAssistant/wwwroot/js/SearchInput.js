export function selectAll(input) {
    input.focus({ preventScroll: true });
    input.select();
}

// Keeps the keyboard-highlighted result visible by scrolling the results list
// only as far as needed.
export function scrollActiveIntoView(list) {
    const active = list?.querySelector?.("li.active");
    active?.scrollIntoView({ block: "nearest" });
}
