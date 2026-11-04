import { tokenInitials } from './Battlemap.js';
import { scrollCurrentRow } from './CombatTrackerScroll.js';

const rendered = new WeakMap();
export function renderPlayerTracker(host,state,doc = globalThis.document) {
    if (!host) return;
    const key = state === null ? 'closed' : JSON.stringify([state.scene.campaignId,state.scene.id,state.combatMode,state.round,state.tracker ?? []]);

    const previous = rendered.get(host);
    if (previous?.key === key) return;
    const currentId = state?.tracker?.find(e => e.isCurrent)?.id ?? null;
    const turn = currentId == null ? null : `${state.scene.campaignId}.${state.scene.id}.${state.round}.${currentId}`;
    rendered.set(host,{key,turn});
    const scrollTop = host.scrollTop;
    host.replaceChildren(); host.hidden = state === null;
    if (state === null) return;
    const title = doc.createElement('h2'); title.setAttribute('data-tracker-heading','true');
    title.textContent = 'Combat Tracker' + (state.combatMode ? ` - Round ${Math.max(1,state.round ?? 1)}` : '');
    host.appendChild(title);
    const rows = state.tracker ?? [];
    if (!rows.length) {
        const empty = doc.createElement('p'); empty.className = 'empty'; empty.textContent = 'No visible combatants.';
        host.appendChild(empty); return;
    }
    const list = doc.createElement('ol'); list.setAttribute('aria-label','Initiative order');
    host.appendChild(list);
    for (const entry of rows) {
        const row = doc.createElement('li');
        row.setAttribute('data-combatant-id',String(entry.id));
        row.className = [entry.isCurrent ? 'current' : '',entry.isDead ? 'dead' : ''].filter(Boolean).join(' ');
        if (entry.isCurrent) row.setAttribute('aria-current','true');
        const fallback = doc.createElement('span'); fallback.className = 'portrait initials';
        fallback.textContent = tokenInitials(entry.name); fallback.setAttribute('aria-hidden','true');
        const frame = doc.createElement('span');
        frame.className = 'portrait-frame' + (state.combatMode ? (entry.isNpc ? ' foe' : ' friend') : '');
        frame.setAttribute('aria-hidden','true'); row.appendChild(frame);
        if (entry.hasTokenImage) {
            const portrait = doc.createElement('img'); portrait.className = 'portrait'; portrait.alt = '';
            portrait.src = new URL(`api/characters/${state.scene.campaignId}/${entry.characterId}/token`,doc.baseURI).href;
            portrait.onerror = () => portrait.replaceWith(fallback);
            frame.appendChild(portrait);
        } else frame.appendChild(fallback);
        const name = doc.createElement('span'); name.className = 'name'; name.textContent = entry.name;
        const initiative = doc.createElement('span'); initiative.className = 'initiative';
        initiative.textContent = entry.initiative === null ? '—' : String(entry.initiative);
        initiative.setAttribute('aria-label',`Initiative ${initiative.textContent}`);
        row.appendChild(name); row.appendChild(initiative);
        const health = doc.createElement('div'); health.className = 'health-track';
        health.setAttribute('role','progressbar'); health.setAttribute('aria-label',`${entry.name} health`);
        health.setAttribute('aria-valuemin','0'); health.setAttribute('aria-valuemax','100');
        health.setAttribute('aria-valuenow',String(entry.healthWidth));
        const fill = doc.createElement('div'); fill.className = 'health-fill';
        fill.style.width = `${entry.healthWidth}%`; fill.style.backgroundColor = entry.healthColour;
        health.appendChild(fill); row.appendChild(health);
        if (!entry.isNpc && Number.isFinite(entry.currentHp)) {
            if ([entry.currentStrength,entry.currentDexterity,entry.currentEndurance].every(Number.isInteger)) {
                const attributes = doc.createElement('span'); attributes.className = 'attributes';
                const value = (current,original) => Number.isInteger(original) ? `${current}/${original}` : String(current);
                attributes.textContent = `STR ${value(entry.currentStrength,entry.originalStrength)} · DEX ${value(entry.currentDexterity,entry.originalDexterity)} · END ${value(entry.currentEndurance,entry.originalEndurance)}`;
                row.appendChild(attributes);
            }
        }
        list.appendChild(row);
    }
    host.scrollTop = scrollTop;
    if (turn !== previous?.turn) scrollCurrentRow(host,currentId);
}
