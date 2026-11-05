import assert from 'node:assert/strict';
import { initialize, dispose } from '../../wwwroot/js/NavigationPanel.js';
class Button {
    attributes = {};
    handlers = new Set();
    setAttribute(name, value) { this.attributes[name] = value; }
    addEventListener(name, handler) { assert.equal(name, 'click'); this.handlers.add(handler); }
    removeEventListener(name, handler) { this.handlers.delete(handler); }
    click() { for (const handler of this.handlers) handler(); }
}
const values = new Map();
const storage = {getItem:key=>values.get(key),setItem:(key,value)=>values.set(key,value)};
const page={},sidebar={},button=new Button();
initialize(page,sidebar,button,storage);
assert.equal(sidebar.hidden,false);
assert.equal(button.attributes['aria-expanded'],'true');
button.click();
assert.equal(sidebar.hidden,true);
assert.equal(button.attributes['aria-label'],'Expand navigation');
assert.equal(button.attributes['aria-expanded'],'false');
const nextSidebar={},nextButton=new Button(),nextPage={};
initialize(nextPage,nextSidebar,nextButton,storage);
assert.equal(nextSidebar.hidden,true,'collapsed preference survives a new layout');
nextButton.click();
assert.equal(nextSidebar.hidden,false);
assert.equal(nextButton.title,'Collapse navigation');
initialize(page,sidebar,button,storage);
assert.equal(button.handlers.size,1,'initialization does not duplicate handlers');
assert.equal(sidebar.hidden,false,'expanded preference persists too');
dispose(page);
button.click();
assert.equal(sidebar.hidden,false);
dispose(nextPage);
const denied={getItem(){throw Error('Denied');},setItem(){throw Error('Denied');}};
initialize(page,sidebar,button,denied);
button.click();
assert.equal(sidebar.hidden,true,'panel still toggles when storage is unavailable');
dispose(page);
console.log('Navigation collapse/expand, persistent preferences, accessible labels, listener cleanup and storage failures passed.');
