// The Galaxy Map jump destination, saved in browser storage by sector ID and hex. Shared by the
// map, which sets it, and Passengers and Cargo, which preselects it. The map also keeps the hex
// its jump route starts from, as an optional from (sector ID and hex); other readers ignore it.
export const DESTINATION_STORAGE_KEY = "mtu.map.destination";

const validPlace = value => Number.isInteger(value?.sectorId) && value.sectorId > 0
    && typeof value.hex === "string" && /^(0[1-9]|[12]\d|3[0-2])(0[1-9]|[1-3]\d|40)$/.test(value.hex);
const validDestination = value => validPlace(value) && (value.from === undefined || validPlace(value.from));
const stored = value => ({ sectorId: value.sectorId, hex: value.hex,
    ...(value.from === undefined ? {} : { from: { sectorId: value.from.sectorId, hex: value.from.hex } }) });

export function loadMapDestination(storage) {
    try {
        const saved = JSON.parse((storage ?? globalThis.localStorage).getItem(DESTINATION_STORAGE_KEY) ?? "null");
        return validDestination(saved) ? stored(saved) : null;
    } catch { return null; }
}

export function saveMapDestination(destination, storage) {
    try {
        storage ??= globalThis.localStorage;
        if (destination === null) storage.removeItem(DESTINATION_STORAGE_KEY);
        else if (validDestination(destination))
            storage.setItem(DESTINATION_STORAGE_KEY, JSON.stringify(stored(destination)));
    } catch { /* The destination remains usable without browser storage. */ }
}
