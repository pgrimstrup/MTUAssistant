export function playerDescription(data) {
    if (!data?.isAnimal) return data ?? null;
    const mask = fields => fields.map(field => field.label === 'Hits' ? {...field,value:'**'} : {...field});
    return {...data,overview:mask(data.overview),rows:mask(data.rows)};
}
export function validDescription(data) {
    const fields = rows => Array.isArray(rows) && rows.every(r => typeof r.label === 'string' && typeof r.value === 'string');
    return data == null || (Number.isInteger(data.id) && data.id > 0 && Number.isInteger(data.campaignId) && data.campaignId > 0
        && Number.isInteger(data.characterId) && data.characterId > 0 && typeof data.name === 'string'
        && typeof data.hasTokenImage === 'boolean' && typeof data.isAnimal === 'boolean' && typeof data.description === 'string'
        && typeof data.skills === 'string' && fields(data.overview) && fields(data.attributes) && fields(data.rows));
}
export class DescriptionDialog {
    constructor(readOnly = false, onClose = null) { this.readOnly = readOnly; this.onClose = onClose; }
    show(data) {
        if (!validDescription(data)) return;
        if (data == null) { this.dispose(); return; }
        if (this.readOnly) data = playerDescription(data);
        const key = JSON.stringify(data);
        if (this.key === key) return;
        this.dispose(); this.key = key;
        const make = (tag,text,className) => { const e=document.createElement(tag); if (text != null) e.textContent=text; if (className) e.className=className; return e; };
        const dialog = this.dialog = make('dialog',null,'combat-description-dialog');
        dialog.setAttribute('aria-label', data.name);
        const header = make('header',null,'combat-description-header');
        if (data.hasTokenImage) { const image=make('img',null,'combat-description-token'); image.src='api/characters/'+data.campaignId+'/'+data.characterId+'/token'; image.alt=data.name+' token'; header.append(image); }
        else header.append(make('span',data.name.split(/\s+/).slice(0,2).map(w=>w[0]??'').join('').toUpperCase(),'combat-description-token'));
        header.append(make('h2',data.name));
        const close = () => { this.dispose(); this.onClose?.(); };
        if (!this.readOnly) { const button=make('button','Close','btn btn-sm btn-outline-secondary combat-description-close'); button.type='button'; button.addEventListener('click',close); header.append(button); }
        dialog.addEventListener('cancel',e => { e.preventDefault(); if (!this.readOnly) close(); });
        dialog.append(header);
        const overview=make('div',null,'combat-description-overview');
        for (const field of data.overview) { const cell=make('div'); cell.append(make('span',field.label,'combat-description-label'),make('span',field.value)); overview.append(cell); }
        dialog.append(overview);
        if (!data.isAnimal) {
            const body=make('div',null,'combat-description-body'), attributes=make('div',null,'combat-description-attributes');
            for (const field of data.attributes) { const cell=make('div'); cell.append(make('span',field.label,'combat-description-label'),make('span',field.value)); attributes.append(cell); }
            const skills=make('div',null,'combat-description-skills'); skills.append(make('span','Skills','combat-description-label'),make('span',data.skills)); body.append(attributes,skills); dialog.append(body);
        }
        const table=make('table',null,'combat-description-rows');
        for (const field of data.rows) { const row=make('tr'), label=make('th',field.label); label.scope='row'; row.append(label,make('td',field.value)); table.append(row); }
        dialog.append(table);
        if (data.description) dialog.append(make('h3','Description'),make('p',data.description,'combat-description-text'));
        document.body.append(dialog); dialog.showModal();
    }
    dispose() { if (this.dialog) { this.dialog.close(); this.dialog.remove(); this.dialog=null; } this.key=null; }
}
