const SESSION_KEY = "mtu.presentation.session";
const PREFIX = "mtu.presentation.";

export function validSession(value) {
    return typeof value === "string" && /^[a-zA-Z0-9-]{16,80}$/.test(value);
}

export function presentationSession(storage, newId = () => crypto.randomUUID?.()
    ?? Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, "0")).join("")) {
    try {
        storage ??= globalThis.sessionStorage;
        const existing = storage.getItem(SESSION_KEY);
        if (validSession(existing)) return existing;
        const id = newId();
        storage.setItem(SESSION_KEY, id);
        return id;
    } catch { return newId(); }
}

export function validMapState(state) {
    return state?.version === 1 && [state.x, state.y, state.zoom].every(Number.isFinite)
        && state.zoom > 0 && state.zoom <= 192
        && (state.selected === null || (state.selected
            && [state.selected.x, state.selected.y].every(Number.isFinite)
            && (state.selected.key == null || typeof state.selected.key === "string")));
}

// Snapshots are paired to one Admin tab, shared only within this browser profile/origin.
export function validPointer(point) {
    return point === null || (!!point && [point.x, point.y].every(Number.isFinite));
}

export function connectPresentation({ session, readOnly, onState, onPointer,
    storage, createChannel = name => new BroadcastChannel(name) }) {
    if (!validSession(session)) throw new Error("Open the presentation from the Galaxy Map's Present map link.");
    const key = PREFIX + session;
    const channel = createChannel(key);
    let latest = null;
    let pointer = null;
    let closed = false;
    const receive = state => {
        if (closed || !validMapState(state)) return;
        latest = state;
        onState?.(state);
    };
    channel.onmessage = ({ data }) => {
        if (closed) return;
        if (!readOnly && data?.type === "request") {
            if (latest) channel.postMessage({ type: "state", state: latest });
            channel.postMessage({ type: "pointer", point: pointer });
        }
        else if (readOnly && data?.type === "state") receive(data.state);
        else if (readOnly && data?.type === "pointer" && validPointer(data.point)) onPointer?.(data.point);
    };
    if (readOnly) {
        try { receive(JSON.parse((storage ?? globalThis.localStorage).getItem(key) ?? "null")); } catch { /* Live updates still work without storage. */ }
        channel.postMessage({ type: "request" });
    }
    return {
        publish(state) {
            if (closed || readOnly || !validMapState(state)) return;
            latest = state;
            try { (storage ?? globalThis.localStorage).setItem(key, JSON.stringify(state)); } catch { /* Live updates still work without storage. */ }
            channel.postMessage({ type: "state", state });
        },
        publishPointer(point) {
            if (closed || readOnly || !validPointer(point)) return;
            pointer = point;
            channel.postMessage({ type: "pointer", point });
        },
        close() {
            if (closed) return;
            if (!readOnly) channel.postMessage({ type: "pointer", point: null });
            closed = true;
            channel.close();
        }
    };
}
