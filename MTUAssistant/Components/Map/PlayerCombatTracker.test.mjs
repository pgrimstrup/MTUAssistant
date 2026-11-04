import assert from 'node:assert/strict';
import { renderPlayerTracker } from '../../wwwroot/js/PlayerCombatTracker.js';
import { playerCombatants, validBattlemapSnapshot, connectBattlemapPresentation } from '../../wwwroot/js/BattlemapPresentationSync.js';
class Element {
    constructor(tag){this.tag=tag;this.children=[];this.style={};this.attributes={};this.textContent='';this.clientTop=0;this.clientHeight=160;this.scrollTop=0;this.scrolls=[];}
    appendChild(child){this.children.push(child);child.parent=this;return child;}
    replaceChildren(){this.children=[];this.scrollTop=0;}
    setAttribute(name,value){this.attributes[name]=value;}
    querySelector(selector){const id=selector.match(/data-combatant-id="(\d+)"/)?.[1];if(id===undefined)return null;const find=e=>e.attributes['data-combatant-id']===id ? e : e.children.map(find).find(Boolean);return find(this);}
    getBoundingClientRect(){if(this.tag==='aside')return {top:0,bottom:160};let host=this;while(host.parent)host=host.parent;const top=(Number(this.attributes['data-combatant-id'])-1)*100-host.scrollTop;return {top,bottom:top+100};}
    scrollBy(options){this.scrolls.push(options);this.scrollTop+=options.top;}
}
const doc={baseURI:'https://example.test/app/',createElement:tag=>new Element(tag)};
const host=new Element('aside');
const traveller={id:1,characterId:11,name:'Traveller <script>',isNpc:false,hasTokenImage:true,isVisible:true,initiative:12,healthWidth:50,healthColour:'orange',isDead:false,isCurrent:true,currentHp:10,currentStrength:3,currentDexterity:4,currentEndurance:3,originalStrength:10,originalDexterity:8,originalEndurance:7};
const npc={...traveller,id:2,characterId:12,name:'Enemy',isNpc:true,initiative:9,healthColour:'red',healthWidth:100,isDead:true,isCurrent:false,currentHp:-5,hasTokenImage:false};
const hidden={...npc,id:3,name:'Secret captain',isVisible:false};
const tracker=playerCombatants([traveller,npc,hidden]);
assert.equal(tracker.length,2); assert.equal(tracker[1].currentHp,undefined);
const state={combatMode:true,round:3,tracker,scene:{id:1,campaignId:4,gridSize:100,gridColour:'#000000',gridOpacity:50,gridlineWidth:2},tokens:[],view:{x:0,y:0,scale:2,width:1000,height:600,imageWidth:2000,imageHeight:2000}};
assert.ok(validBattlemapSnapshot(state));
assert.equal(validBattlemapSnapshot({...state,tracker:[hidden]}),false);
assert.equal(validBattlemapSnapshot({...state,tracker:[{...tracker[1],currentHp:-5}]}),false);
assert.equal(validBattlemapSnapshot({...state,tracker:[{...tracker[0],healthColour:'url(unsafe)'}]}),false);
renderPlayerTracker(host,state,doc);
assert.equal(host.hidden,false); assert.equal(host.children[0].textContent,'Combat Tracker - Round 3');
const list=host.children[1], rows=list.children;
assert.equal(rows[0].children[0].className,'portrait-frame friend');
assert.equal(rows[1].children[0].className,'portrait-frame foe');
assert.equal(rows.length,2); assert.equal(rows[0].attributes['aria-current'],'true');
assert.equal(rows[0].children[0].children[0].tag,'img'); assert.equal(rows[0].children[0].children[0].src,'https://example.test/app/api/characters/4/11/token');
assert.equal(rows[0].children[1].textContent,traveller.name,'names are assigned as text, not HTML');
assert.equal(rows[0].children[2].textContent,'12');
assert.equal(rows[0].children.some(e=>e.className==='hp'),false,'Traveller HP totals are not displayed');
assert.equal(rows[1].className,'dead'); assert.equal(rows[1].children[0].children[0].textContent,'E');
assert.equal(rows[1].children[3].children[0].style.width,'100%');
assert.equal(rows[1].children[3].children[0].style.backgroundColor,'red');
assert.equal(rows[1].children.some(e=>e.className==='hp'),false);
const allElements=e=>[e,...e.children.flatMap(allElements)];
assert.ok(!allElements(host).some(e=>['input','button','select'].includes(e.tag)),'player tracker has no editing controls');
renderPlayerTracker(host,{...state,view:{...state.view,x:-300}},doc);
assert.equal(host.children[1],list,'camera animation frames preserve tracker DOM and scroll position');
renderPlayerTracker(host,{...state,round:4,tracker:[{...tracker[0],currentHp:4,currentStrength:0,currentDexterity:4,currentEndurance:0,healthWidth:20,healthColour:'red'}]},doc);
assert.equal(host.children[0].textContent,'Combat Tracker - Round 4');
assert.equal(host.children[1].children.length,1,'hiding/removing combatants updates the list');
assert.equal(host.children[1].children[0].children.find(e=>e.className==='attributes').textContent,'STR 0/10 · DEX 4/8 · END 0/7');
renderPlayerTracker(host,{...state,combatMode:false,tracker:[]},doc);
assert.equal(host.children[0].textContent,'Combat Tracker');
renderPlayerTracker(host,{...state,combatMode:false},doc);
assert.equal(host.children[1].children[0].children[0].className,'portrait-frame');
assert.equal(host.children[1].children[1].children[0].className,'portrait-frame');
renderPlayerTracker(host,{...state,combatMode:false,tracker:[]},doc);
assert.equal(host.children[1].textContent,'No visible combatants.');
renderPlayerTracker(host,null,doc); assert.equal(host.hidden,true); assert.equal(host.children.length,0);
const values=new Map(), ports=[];
const createChannel=name=>{const p={name,postMessage(data){for(const q of ports)if(q!==p&&q.name===name)q.onmessage?.({data});},close(){}};ports.push(p);return p;};
const storage={getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,value)};
const admin=connectBattlemapPresentation({session:'player-tracker-session-1234',readOnly:false,storage,createChannel});
admin.publish(state);
let received;
const cast=connectBattlemapPresentation({session:'player-tracker-session-1234',readOnly:true,storage,createChannel,onState:s=>received=s});
assert.deepEqual(received.tracker,tracker,'late cast windows receive the complete filtered tracker');
assert.equal(received.round,3);
admin.publish({...state,round:4,tracker:[tracker[0]]}); assert.equal(received.round,4); assert.equal(received.tracker.length,1);
admin.publish(null); assert.equal(received,null);
cast.close();admin.close();
console.log('Player tracker rendering, read-only controls, live updates, round title, late joining and NPC HP privacy passed.');

renderPlayerTracker(host,state,doc);
assert.equal(host.children[1].children[0].children.find(e=>e.className==='attributes').textContent,'STR 3/10 · DEX 4/8 · END 3/7');
assert.equal(host.children[1].children[1].children.some(e=>e.className==='attributes'),false,'NPC attribute values remain private');
assert.equal(tracker[1].currentStrength,undefined);
assert.equal(tracker[1].originalStrength,undefined);
assert.equal(validBattlemapSnapshot({...state,tracker:[{...tracker[0],currentEndurance:9}]}),false,'attribute sum must match HP');
const nextTurn={...state,round:5,tracker:[{...tracker[0],isCurrent:false},{...tracker[1],isCurrent:true,isDead:false}]};
renderPlayerTracker(host,nextTurn,doc);
assert.deepEqual(host.scrolls.at(-1),{top:40,behavior:'smooth'},'cast scrolls the newly selected row into view');
const scrollCount=host.scrolls.length;
host.scrollTop=30;
renderPlayerTracker(host,{...nextTurn,tracker:[{...nextTurn.tracker[0],name:'Updated traveller'},nextTurn.tracker[1]]},doc);
assert.equal(host.scrolls.length,scrollCount,'HP/name updates on the same turn do not force scrolling');
assert.equal(host.scrollTop,30,'tracker rebuilds preserve manual scroll position');
renderPlayerTracker(host,{...nextTurn,round:6},doc);
assert.equal(host.scrolls.length,scrollCount+1,'the same combatant in a new round is revealed again');
console.log('Cast attribute breakdown, NPC attribute privacy, turn scrolling and manual scroll preservation passed.');
