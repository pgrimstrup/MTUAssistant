import assert from 'node:assert/strict';
import { DescriptionDialog, validDescription, playerDescription } from '../../wwwroot/js/CombatDescription.js';
import { validBattlemapSnapshot } from '../../wwwroot/js/BattlemapPresentationSync.js';
class Element {
    constructor(tag) { this.tag=tag; this.children=[]; this.events={}; this.attributes={}; this.textContent=''; }
    append(...elements) { this.children.push(...elements); }
    setAttribute(k,v) { this.attributes[k]=v; }
    addEventListener(k,fn) { this.events[k]=fn; }
    showModal() { this.open=true; }
    close() { this.open=false; }
    remove() { this.removed=true; }
}
globalThis.document={body:new Element('body'),createElement:tag=>new Element(tag)};
const all=e=>[e,...e.children.flatMap(all)];
const data={id:1,campaignId:2,characterId:3,name:'Guard <script>',hasTokenImage:true,isAnimal:false,overview:[{label:'Species',value:'Human'}],attributes:[{label:'STR',value:'9'}],skills:'Recon 1',rows:[{label:'Weapons',value:'Rifle'},{label:'Armour',value:'Cloth'}],description:'Line one\nLine two <script>'};
let closed=0;const admin=new DescriptionDialog(false,()=>closed++); admin.show(data);
assert.ok(admin.dialog.open); assert.equal(all(admin.dialog).find(e=>e.tag==='h2').textContent,data.name);
assert.equal(all(admin.dialog).find(e=>e.tag==='img').className,'combat-description-token');
assert.ok(all(admin.dialog).some(e=>e.textContent===data.description),'multiline descriptions remain literal text');
const original=admin.dialog;admin.show(data);assert.equal(admin.dialog,original,'unchanged updates preserve dialog and focus');
all(admin.dialog).find(e=>e.tag==='button').events.click();assert.equal(closed,1);assert.equal(admin.dialog,null);
const cast=new DescriptionDialog(true);cast.show(data);assert.equal(all(cast.dialog).filter(e=>e.tag==='button').length,0,'cast has no editing or close controls');
let prevented=false;cast.dialog.events.cancel({preventDefault(){prevented=true;}});assert.ok(prevented && cast.dialog.open,'cast follows admin closing');
cast.show({...data,isAnimal:true,attributes:[],rows:[{label:'Hits',value:'20'},{label:'Attacks',value:'Bite'}]});
assert.ok(!all(cast.dialog).some(e=>e.className==='combat-description-body'),'animals use their own layout');
cast.show(null);assert.equal(cast.dialog,null,'closing or clearing on admin closes cast dialog');
assert.equal(validDescription({...data,rows:[{label:'Weapons',value:123}]}),false);
console.log('Combat description dialog rendering, literal text, animal layout, Admin close and read-only Cast lifecycle passed.');
import { connectBattlemapPresentation } from '../../wwwroot/js/BattlemapPresentationSync.js';
const channels=[];
const createChannel=()=>{ const channel={postMessage(message){ for(const other of channels) if(other!==channel) other.onmessage?.({data:message}); },close(){channels.splice(channels.indexOf(channel),1);}};channels.push(channel);return channel; };
const saved=new Map(); const storage={setItem:(k,v)=>saved.set(k,v),getItem:k=>saved.get(k)??null};
const session='description-test-session';
const publisher=connectBattlemapPresentation({session,readOnly:false,createChannel,storage});
const snapshot={description:data,combatMode:false,round:0,scene:{id:1,campaignId:2,gridSize:100,gridColour:'#000000',gridlineWidth:1,gridOpacity:50},tokens:[],view:{x:0,y:0,scale:1,width:800,height:600,imageWidth:1000,imageHeight:1000}};
assert.ok(validBattlemapSnapshot(snapshot));
publisher.publish(snapshot);
const received=[];
const viewer=connectBattlemapPresentation({session,readOnly:true,createChannel,storage,onState:state=>received.push(state)});
assert.equal(received.at(-1).description.name,data.name,'late Cast connections receive the open description');
publisher.publish({...snapshot,description:null});assert.equal(received.at(-1).description,null,'Admin closing is broadcast to Cast');
assert.equal(validBattlemapSnapshot({...snapshot,description:{...data,description:123}}),false,'invalid description payloads are rejected');
viewer.close();publisher.close();
console.log('Description Cast mirroring, late joining, closing and payload validation passed.');

const animalData={...data,isAnimal:true,overview:[{label:'Animal',value:'Beast'},{label:'Hits',value:'83'},{label:'Speed',value:'9m'}],rows:[{label:'Attacks',value:'Bite'}]};
const publicAnimal=playerDescription(animalData);
assert.equal(publicAnimal.overview.find(f=>f.label==='Hits').value,'**');
assert.equal(animalData.overview.find(f=>f.label==='Hits').value,'83','GM retains original Hits');
assert.equal(publicAnimal.overview.find(f=>f.label==='Speed').value,'9m');
assert.equal(playerDescription(data),data,'character stat blocks are unchanged');
cast.show(animalData);assert.ok(all(cast.dialog).some(e=>e.textContent==='**'));assert.ok(!all(cast.dialog).some(e=>e.textContent==='83'),'Cast masks older cached descriptions too');
cast.dispose();
console.log('Cast Animal Hits masking and original GM data preservation passed.');
