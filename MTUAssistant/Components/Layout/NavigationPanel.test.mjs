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
const pageElement=()=>({classList:{values:new Set(),toggle(name,on){if(on)this.values.add(name);else this.values.delete(name);},contains(name){return this.values.has(name);}}});
const page=pageElement(),sidebar={},button=new Button();
initialize(page,sidebar,button,storage);
assert.equal(sidebar.hidden,false);
assert.equal(button.attributes['aria-expanded'],'true');
button.click();
assert.equal(sidebar.hidden,false);
assert.equal(page.classList.contains('navigation-collapsed'),true);
assert.equal(button.attributes['aria-label'],'Expand navigation');
assert.equal(button.attributes['aria-expanded'],'false');
const nextSidebar={},nextButton=new Button(),nextPage=pageElement();
initialize(nextPage,nextSidebar,nextButton,storage);
assert.equal(nextSidebar.hidden,false);
assert.equal(nextPage.classList.contains('navigation-collapsed'),true,'collapsed preference survives a new layout');
nextButton.click();
assert.equal(nextSidebar.hidden,false);
assert.equal(nextButton.title,'Collapse navigation');
initialize(page,sidebar,button,storage);
assert.equal(button.handlers.size,1,'initialization does not duplicate handlers');
assert.equal(sidebar.hidden,false);
assert.equal(page.classList.contains('navigation-collapsed'),false,'expanded preference persists too');
dispose(page);
button.click();
assert.equal(sidebar.hidden,false);
dispose(nextPage);
const denied={getItem(){throw Error('Denied');},setItem(){throw Error('Denied');}};
initialize(page,sidebar,button,denied);
button.click();
assert.equal(sidebar.hidden,false);
assert.equal(page.classList.contains('navigation-collapsed'),true,'rail still toggles when storage is unavailable');
dispose(page);
console.log('Navigation collapse/expand, persistent preferences, accessible labels, listener cleanup and storage failures passed.');
