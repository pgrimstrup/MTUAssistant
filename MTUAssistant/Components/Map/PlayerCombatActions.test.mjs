import assert from 'node:assert/strict';
import {renderPlayerActions} from '../../wwwroot/js/PlayerCombatActions.js';
import {validBattlemapSnapshot,connectBattlemapPresentation} from '../../wwwroot/js/BattlemapPresentationSync.js';
class Element {
    constructor(tag){this.tag=tag;this.children=[];this.attributes={};this.textContent='';this.scrollTop=0;}
    appendChild(child){this.children.push(child);return child;}
    replaceChildren(){this.children=[];this.scrollTop=0;}
    setAttribute(key,value){this.attributes[key]=value;}
}
const doc={baseURI:'https://example.test/',createElement:tag=>new Element(tag)};
const host=new Element('aside');
const summary={id:1,characterId:10,name:'Captain <script>',secondary:'Alex',isNpc:false,hasTokenImage:true,
    columns:[{title:'Minor Before',buttons:[{text:'Aim x1',selected:true}]},{title:'Major Action',buttons:[{text:'Ranged Attack',selected:true}]},{title:'Minor After',buttons:[{text:'Move',selected:false}]}],
    rows:[{label:'Combatant',value:'Captain <script>'},{label:'Armour',value:'Suit (DR +12)'},{label:'Weapon',value:'Rifle (100m, 3D)'},{label:'Stance',value:'Prone (-1)'},{label:'Reactions Made',value:'2 Reactions (DM -2)'},{label:'Target',value:'Guard — Short Range (DM +1)'}]};
const state={combatMode:true,round:1,actionSummary:summary,scene:{id:1,campaignId:4,gridSize:100,gridColour:'#000000',gridOpacity:50,gridlineWidth:2},tokens:[],tracker:[{id:1,characterId:10,name:'Captain',isNpc:false,isVisible:true,initiative:10,currentHp:21,healthWidth:100,healthColour:'green'}],view:{x:0,y:0,scale:1,width:800,height:600,imageWidth:1000,imageHeight:1000}};
const all=e=>[e,...e.children.flatMap(all)];
renderPlayerActions(host,state,doc);
assert.equal(host.hidden,false);assert.ok(all(host).some(e=>e.textContent === '2 Reactions (DM -2)'));assert.ok(all(host).some(e=>e.textContent === 'Combatant Summary'));assert.ok(!all(host).some(e=>e.textContent === 'Target Reaction'));assert.ok(!all(host).some(e=>e.textContent === 'Target Reaction Stance'));
assert.equal(all(host).find(e=>e.tag==='h3').textContent,summary.name,'names remain literal text');
const buttons=all(host).filter(e=>e.tag==='button');
assert.equal(buttons.length,3);assert.ok(!buttons.some(b=>b.textContent === "Reset"),"Reset is Admin only");assert.ok(buttons.every(e=>e.disabled));assert.equal(buttons[0].attributes['aria-pressed'],'true');
assert.ok(all(host).some(e=>e.textContent==='Rifle (100m, 3D)'));
host.scrollTop=123;const oldHeading=host.children[0];renderPlayerActions(host,state,doc);assert.equal(host.children[0],oldHeading,'camera broadcasts do not rebuild panel');assert.equal(host.scrollTop,123);
renderPlayerActions(host,{...state,actionSummary:{...summary,isNpc:true}},doc);
assert.ok(!all(host).some(e=>['Armour','Weapon','Suit (DR +12)','Rifle (100m, 3D)'].includes(e.textContent)),'enemy equipment never rendered');
assert.ok(validBattlemapSnapshot(state));
assert.equal(validBattlemapSnapshot({...state,actionSummary:{...summary,id:99}}),false,'hidden actors rejected');
assert.equal(validBattlemapSnapshot({...state,actionSummary:{...summary,isNpc:true}}),false,'enemy gear rejected in public payload');
const values=new Map(),ports=[];
const createChannel=name=>{const port={name,postMessage(data){for(const p of ports)if(p!==port&&p.name===name)p.onmessage?.({data});},close(){}};ports.push(port);return port;};
const storage={getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,value)};
const admin=connectBattlemapPresentation({session:'combat-action-summary-1234',readOnly:false,storage,createChannel});admin.publish(state);
let received;const cast=connectBattlemapPresentation({session:'combat-action-summary-1234',readOnly:true,storage,createChannel,onState:s=>received=s});
assert.deepEqual(received.actionSummary,summary,'late cast receives action selection');
admin.publish({...state,actionSummary:{...summary,rows:[{label:'Stance',value:'Prone (-1)'}]}});assert.equal(received.actionSummary.rows[0].value,'Prone (-1)');
admin.publish({...state,actionSummary:null});assert.equal(received.actionSummary,null);
cast.close();admin.close();
renderPlayerActions(host,{...state,combatMode:false},doc);assert.equal(host.hidden,true);
renderPlayerActions(host,null,doc);assert.equal(host.children.length,0);
console.log('Cast actions are read-only, preserve selections/scroll, update live/late, hide enemy gear and hidden actors, and clear outside combat.');
