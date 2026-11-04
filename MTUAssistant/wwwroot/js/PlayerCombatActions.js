import { tokenInitials } from './Battlemap.js';

const rendered = new WeakMap();
export function renderPlayerActions(host,state,doc = globalThis.document) {
    if (!host) return;
    const summary = state?.combatMode ? state.actionSummary : null;
    const key = JSON.stringify([state?.scene?.campaignId, summary]);
    if (rendered.get(host) === key) return;
    rendered.set(host,key);
    const scrollTop=host.scrollTop;
    host.replaceChildren(); host.hidden = !summary;
    if (!summary) return;
    const add = (parent,tag,text,className) => {
        const element=doc.createElement(tag); if (text != null) element.textContent=text;
        if (className) element.className=className;
        parent.appendChild(element); return element;
    };
    add(host,'h2','Combat Actions');
    const header=add(host,'header',null,'actor-heading');
    const frame=add(header,'span',null,`portrait-frame ${summary.isNpc ? 'foe' : 'friend'}`);
    const fallback=()=>add(frame,'span',tokenInitials(summary.name),'initials');
    if (summary.hasTokenImage) {
        const image=add(frame,'img'); image.alt='';
        image.src=new URL(`api/characters/${state.scene.campaignId}/${summary.characterId}/token`,doc.baseURI).href;
        image.onerror=()=>{frame.replaceChildren();fallback();};
    } else fallback();
    const identity=add(header,'div'); add(identity,'h3',summary.name); add(identity,'p',summary.secondary);
    const columns=add(host,'div',null,'action-columns');
    for (const column of summary.columns) {
        const group=add(columns,'section'); add(group,'h3',column.title);
        for (const button of column.buttons) {
            const item=add(group,'button',button.text,`action-button${button.selected ? ' chosen' : ''}`);
            item.type='button'; item.disabled=true; item.setAttribute('aria-pressed',String(button.selected));
        }
    }
    const section=add(host,'section',null,'action-summary'); add(section,'h2','Action Summary');
    const list=add(section,'dl');
    for (const row of summary.rows) {
        if (summary.isNpc && ['Armour','Weapon'].includes(row.label)) continue;
        add(list,'dt',row.label); add(list,'dd',row.value);
    }
    host.scrollTop=scrollTop;
}
