// The Galaxy Map jump destination, saved in browser storage by sector ID and hex. Shared by the
// map, which sets it, and Passengers and Cargo, which preselects it.
export const DESTINATION_STORAGE_KEY = "mtu.map.destination";

const validDestination = value => Number.isInteger(value?.sectorId) && value.sectorId > 0
    && typeof value.hex === "string" && /^(0[1-9]|[12]\d|3[0-2])(0[1-9]|[1-3]\d|40)$/.test(value.hex);

export function loadMapDestination(storage) {
    try {
        const saved = JSON.parse((storage ?? globalThis.localStorage).getItem(DESTINATION_STORAGE_KEY) ?? "null");
        return validDestination(saved) ? { sectorId: saved.sectorId, hex: saved.hex } : null;
    } catch { return null; }
}

export function saveMapDestination(destination, storage) {
    try {
        storage ??= globalThis.localStorage;
        if (destination === null) storage.removeItem(DESTINATION_STORAGE_KEY);
        else if (validDestination(destination))
            storage.setItem(DESTINATION_STORAGE_KEY, JSON.stringify({ sectorId: destination.sectorId, hex: destination.hex }));
    } catch { /* The destination remains usable without browser storage. */ }
}
