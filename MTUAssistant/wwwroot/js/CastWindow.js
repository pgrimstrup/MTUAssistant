import { validSession } from './PresentationSync.js';
import { connectBattlemapPresentation } from './BattlemapPresentationSync.js';

const castWindows = new Map();

export function castUrl(session,battlemap,baseURI = document.baseURI) {
    if (!validSession(session)) throw Error('Open the cast window from an Admin map.');
    const url = new URL(battlemap ? 'battlemap-presentation' : 'presentation',baseURI);
    url.searchParams.set('session',session);
    return url;
}

export function attachCastWindow(link,session,battlemap,{baseURI = document.baseURI,open = (...args) => globalThis.open(...args)} = {}) {
    const url = castUrl(session,battlemap,baseURI);
    link.href = url.href;
    link.target = 'mtu-presentation-' + session;
    const click = event => {
        const existing = castWindows.get(session);
        if (existing && !existing.closed) {
            try {
                existing.location.href = url.href; existing.focus();
                event.preventDefault(); return;
            } catch { castWindows.delete(session); }
        }
        const popup = open(url.href,link.target,'popup,width=1280,height=800');
        if (popup) {
            popup.opener = null; castWindows.set(session,popup);
            event.preventDefault();
        }
    };
    link.addEventListener('click',click);
    return () => link.removeEventListener('click',click);
}

export function switchCastView(session,battlemap,{location = globalThis.location,baseURI = document.baseURI} = {}) {
    const url = castUrl(session,battlemap,baseURI);
    if (location.href !== url.href) location.replace(url.href);
}

// A Star Map cast follows Battlemap activation without opening another window.
export function watchBattlemap(session,{navigate = battlemap => switchCastView(session,battlemap),...options} = {}) {
    let switching = false;
    const connection = connectBattlemapPresentation({session,...options,readOnly:true,onState:state => {
        if (state !== null && !switching) { switching = true; navigate(true); }
    }});
    return {close:() => connection.close()};
}
