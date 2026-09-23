// Plan a text-only pivot migration; apply_patch performs the actual asset edits.
// Verification compares every child transform in its unchanged scene-parent space.
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
const root=process.cwd();
const defs=[
  {name:'Chair-Normal',guid:'115a5b3bc09d59849ad65c8156395d20'},
  {name:'Table-Normal',guid:'26d24882c78b7e349b78b028cf35cf9e'}
];
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n');
const docs=t=>[...t.matchAll(/^--- !u!(\d+) &(\d+)(?: stripped)?\n[\s\S]*?(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>({type:m[1],id:m[2],text:m[0]}));
const vec=(t,key,axes='xyz')=>{const m=t.match(new RegExp(`${key}: \\{([^}]+)\\}`));assert(m,`Missing ${key}`);return [...axes].map(a=>Number(m[1].match(new RegExp(`${a}: ([^,}]+)`))[1]));};
const props=b=>[...b.matchAll(/    - target: \{fileID: (\d+), guid: ([a-f0-9]+), type: 3\}\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)\n      objectReference: \{[^\n]+\}/g)].map(m=>({id:m[1],guid:m[2],key:m[3],value:m[4],text:m[0]}));
const rotate=(v,q)=>{
  const len=Math.hypot(...q);const [x,y,z,w]=q.map(n=>n/len);const [a,b,c]=v;
  const tx=2*(y*c-z*b),ty=2*(z*a-x*c),tz=2*(x*b-y*a);
  return [a+w*tx+y*tz-z*ty,b+w*ty+z*tx-x*tz,c+w*tz+x*ty-y*tx];
};
const add=(a,b)=>a.map((n,i)=>n+b[i]);const sub=(a,b)=>a.map((n,i)=>n-b[i]);const mul=(a,b)=>a.map((n,i)=>n*b[i]);
const fmt=n=>Math.abs(n)<1e-10?'0':Number(n.toFixed(10)).toString();
const localLine=v=>`m_LocalPosition: {x: ${fmt(v[0])}, y: ${fmt(v[1])}, z: ${fmt(v[2])}}`;
function transforms(t){return docs(t).filter(d=>d.type==='4'&&d.text.includes('m_LocalPosition:')).map(d=>({...d,
  parent:d.text.match(/m_Father: \{fileID: (\d+)\}/)[1],p:vec(d.text,'m_LocalPosition'),s:vec(d.text,'m_LocalScale'),q:vec(d.text,'m_LocalRotation','xyzw')}));}
function loadDef(def,text){
  const ts=transforms(text),r=ts.find(t=>t.parent==='0');assert(r);
  const children=ts.filter(t=>t.parent===r.id);assert.equal(children.length,ts.length-1,'No nested transforms expected');
  const lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];
  for(const t of children){assert(t.q.every((n,i)=>n===[0,0,0,1][i]));for(let a=0;a<3;a++){const half=a<2?Math.abs(t.s[a])*.5:0;lo[a]=Math.min(lo[a],t.p[a]-half);hi[a]=Math.max(hi[a],t.p[a]+half);}}
  return {...def,root:r,children,center:add(lo,hi).map(n=>n*.5)};
}
function effective(d,b){
  const overrides=props(b);const value=(id,key,fallback)=>{const p=overrides.find(p=>p.guid===d.guid&&p.id===id&&p.key===key);return p?Number(p.value):fallback;};
  return [d.root,...d.children].map(t=>({...t,p:t.p.map((n,i)=>value(t.id,`m_LocalPosition.${'xyz'[i]}`,n)),s:t.s.map((n,i)=>value(t.id,`m_LocalScale.${'xyz'[i]}`,n)),q:t.q.map((n,i)=>value(t.id,`m_LocalRotation.${'xyzw'[i]}`,n))}));
}
function validateInstance(beforeDef,afterDef,before,after){
  const a=effective(beforeDef,before),b=effective(afterDef,after);let maxError=0;
  for(let i=1;i<a.length;i++)for(const v of [[0,0,0],[-.5,-.5,0],[.5,.5,0],[-.5,.5,0],[.5,-.5,0]]){
    const point=(ts,t)=>add(ts[0].p,rotate(mul(add(t.p,rotate(mul(v,t.s),t.q)),ts[0].s),ts[0].q));
    maxError=Math.max(maxError,Math.hypot(...sub(point(a,a[i]),point(b,b[i]))));
  }
  assert(maxError<0.00001,`Geometry shifted ${maxError}`);return maxError;
}
function patchBlock(before,after){
  const a=before.trimEnd().split('\n'),b=after.trimEnd().split('\n');
  assert.equal(a.length,b.length,'This migration changes scalar values only');let out='';
  // Jump to the unique Unity document ID, then target each changed scalar in order.
  const indices=a.map((line,i)=>line!==b[i]?i:-1).filter(i=>i>=0);
  if(!indices.length)return '';
  out+=`@@ ${a[0]}\n`;
  let last=-10;
  for(const i of indices){
    if(last>=0)out+='@@\n';
    const start=Math.max(last+1,i-2);
    for(let j=start;j<i;j++)out+=' '+a[j]+'\n';
    out+='-'+a[i]+'\n'+'+'+b[i]+'\n';last=i;
  }
  return out;
}
const mode=process.argv[2]||'plan';
if(mode==='patches'){
  const folder=process.argv[3];const plan=JSON.parse(fs.readFileSync(path.join(folder,'plan.json'),'utf8'));
  for(const f of plan.files){
    const before=docs(fs.readFileSync(path.join(folder,f.beforeFile),'utf8'));
    const after=docs(fs.readFileSync(path.join(folder,f.afterFile),'utf8'));
    const patch=before.map(b=>patchBlock(b.text,after.find(a=>a.id===b.id).text)).join('');
    fs.writeFileSync(path.join(folder,f.patchFile),`*** Begin Patch\n*** Update File: ${path.join(root,f.path).replaceAll('\\','/')}\n${patch}*** End Patch\n`);
  }
  console.log('Patches generated in serialized document order');process.exit(0);
}
if(mode==='verify'||mode==='check-before'){
  const folder=process.argv[3];const plan=JSON.parse(fs.readFileSync(path.join(folder,'plan.json'),'utf8'));
  for(const f of plan.files)assert.equal(read(f.path),fs.readFileSync(path.join(folder,mode==='verify'?f.afterFile:f.beforeFile),'utf8'),`Unexpected file changes ${f.path}`);
  console.log(JSON.stringify({verified:true,...plan.summary}));process.exit(0);
}
const beforeDefs=defs.map(d=>loadDef(d,read(`Assets/Prefabs/Decorations/${d.name}.prefab`)));
for(const d of beforeDefs)assert(Math.hypot(...d.center)>.01,`${d.name} already centered`);
const changes=[];const afterDefs=[];
for(const d of beforeDefs){
  const file=`Assets/Prefabs/Decorations/${d.name}.prefab`;const before=read(file);let after=before,patch='';
  for(const t of transforms(before)){
    const p=t.id===d.root.id?[0,0,0]:sub(t.p,d.center);
    const updated=t.text.replace(/m_LocalPosition: \{[^}]+\}/,localLine(p));
    after=after.replace(t.text,updated);patch+=patchBlock(t.text,updated);
  }
  changes.push({path:file,before,after,patch});const ad=loadDef(d,after);
  assert(Math.hypot(...ad.center)<1e-7,'Prefab is not centered');afterDefs.push(ad);
}
function walk(dir){return fs.readdirSync(path.join(root,dir),{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(`${dir}/${e.name}`):[`${dir}/${e.name}`]);}
const scenes=walk('Assets').filter(p=>/\.(unity|prefab)$/.test(p)&&!changes.some(c=>c.path===p));
let instanceCount=0,childOverrides=0,maxError=0;const counts={};
for(const file of scenes){
  const before=read(file);if(!defs.some(d=>before.includes(d.guid)))continue;
  const blocks=docs(before),instances=blocks.filter(d=>d.type==='1001');let after=before,patch='',count=0;
  const targets=instances.filter(b=>beforeDefs.some(d=>b.text.includes(`m_SourcePrefab: {fileID: 100100000, guid: ${d.guid},`)));
  // Refuse added/nested children: blindly shifting the parent would move unrelated content.
  const rootRefs=blocks.filter(b=>b.type==='4'&&targets.some(t=>b.text.includes(`m_PrefabInstance: {fileID: ${t.id}}`))&&beforeDefs.some(d=>b.text.includes(`m_CorrespondingSourceObject: {fileID: ${d.root.id}, guid: ${d.guid},`))).map(b=>b.id);
  for(const b of blocks){
    if(rootRefs.some(id=>b.text.includes(`m_Father: {fileID: ${id}}`)||b.text.includes(`m_TransformParent: {fileID: ${id}}`)))throw Error(`Added child under shifted root: ${file} ${b.id}`);
  }
  for(const instance of targets){
    const d=beforeDefs.find(d=>instance.text.includes(`m_SourcePrefab: {fileID: 100100000, guid: ${d.guid},`));
    const ad=afterDefs.find(a=>a.guid===d.guid);const ts=effective(d,instance.text);
    const nextRoot=add(ts[0].p,rotate(mul(d.center,ts[0].s),ts[0].q));let updated=instance.text;
    for(const p of props(instance.text)){
      if(p.guid!==d.guid||!/^m_LocalPosition\.[xyz]$/.test(p.key))continue;
      const axis='xyz'.indexOf(p.key.at(-1));let v;
      if(p.id===d.root.id)v=nextRoot[axis];
      else if(d.children.some(t=>t.id===p.id)){v=Number(p.value)-d.center[axis];childOverrides++;}
      else continue;
      updated=updated.replace(p.text,p.text.replace(`      value: ${p.value}\n`,`      value: ${fmt(v)}\n`));
    }
    for(const axis of 'xyz')assert(props(instance.text).some(p=>p.guid===d.guid&&p.id===d.root.id&&p.key===`m_LocalPosition.${axis}`),`Missing root override ${file} ${instance.id}`);
    // Everything except position scalars must remain identical, including user color overrides.
    assert.equal(instance.text.replace(/(propertyPath: m_LocalPosition\.[xyz]\n      value:) [^\n]*/g,'$1 POSITION'),updated.replace(/(propertyPath: m_LocalPosition\.[xyz]\n      value:) [^\n]*/g,'$1 POSITION'));
    maxError=Math.max(maxError,validateInstance(d,ad,instance.text,updated));
    after=after.replace(instance.text,updated);patch+=patchBlock(instance.text,updated);count++;
  }
  if(count){counts[file]=count;instanceCount+=count;changes.push({path:file,before,after,patch});}
}
const folder=path.join(root,'.codex-temp',`CenteredFurniture-${Date.now()}`);fs.mkdirSync(folder,{recursive:true});
const files=changes.map((c,i)=>{
  fs.writeFileSync(path.join(folder,`${i}.before`),c.before);fs.writeFileSync(path.join(folder,`${i}.after`),c.after);
  fs.writeFileSync(path.join(folder,`${i}.patch`),`*** Begin Patch\n*** Update File: ${path.join(root,c.path).replaceAll('\\','/')}\n${c.patch}*** End Patch\n`);
  return {path:c.path,beforeFile:`${i}.before`,afterFile:`${i}.after`,patchFile:`${i}.patch`};
});
const summary={instanceCount,childOverrides,maxError,counts,centers:beforeDefs.map(d=>({name:d.name,center:d.center}))};
fs.writeFileSync(path.join(folder,'plan.json'),JSON.stringify({files,summary},null,2));
console.log(JSON.stringify({folder,files,summary},null,2));
