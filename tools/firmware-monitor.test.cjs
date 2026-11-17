const {test}=require('node:test');
const assert=require('node:assert/strict');
const {inspect,notify}=require('./firmware-monitor.cjs');
const metadata=v=>({version:v,firmware:{name:'Lennox_FW_V'+v+'.bin',size:3266308,url:'https://example.invalid/?secret=never-publish'}});
test('numeric version comparison, no equal/older alerts',()=>{
 assert.equal(inspect(metadata('17.0.37')).newer,false);
 assert.equal(inspect(metadata('17.0.9')).newer,false);
 assert.equal(inspect(metadata('17.0.100')).newer,true);
 assert.equal(inspect(metadata('18.0.0')).newer,true);
});
test('invalid or unrelated metadata is rejected',()=>{
 for(const v of ['17.0.38 @someone','x','17.0','999999999999.0.0'])assert.throws(()=>inspect(metadata(v)));
 assert.throws(()=>inspect({...metadata('18.0.0'),firmware:{name:'Other.bin',size:10}}));
 assert.throws(()=>inspect({...metadata('18.0.0'),firmware:{name:'Lennox_FW_V18.0.0.bin',size:0}}));
});
test('creates one sanitized notification; closed issues also deduplicate',async()=>{
 const created=[];
 const github={rest:{issues:{listForRepo(){},async create(p){created.push(p);return {data:{number:7}};}}},async paginate(){return [];}};
 const context={repo:{owner:'RougeNuage',repo:'silver-moon'}};
 assert.equal(await notify(github,context,inspect(metadata('17.0.37'))),'unchanged');
 assert.equal(created.length,0);
 assert.equal(await notify(github,context,inspect(metadata('18.0.0'))),'created');
 assert.match(created[0].body,/@RougeNuage/);
 assert.doesNotMatch(created[0].body,/secret|example.invalid/);
 github.paginate=async(method,args)=>{assert.equal(args.state,'all');return [{body:created[0].body,state:'closed'}];};
 assert.equal(await notify(github,context,inspect(metadata('18.0.0'))),'already-notified');
 assert.equal(created.length,1);
});
test('network errors, oversized data and malformed JSON do not become updates',async()=>{
 const {query}=require('./firmware-monitor.cjs');
 await assert.rejects(()=>query(async()=>new Response('{}',{status:503})));
 await assert.rejects(()=>query(async()=>new Response('x'.repeat(65537))));
 await assert.rejects(()=>query(async()=>new Response('private invalid body')),/not valid JSON/);
 const value=await query(async(url,options)=>{
  assert.equal(options.redirect,'error');
  assert.equal(JSON.parse(options.body).device.serial_number,'x');
  return new Response(JSON.stringify(metadata('18.0.0')));
 });
 assert.equal(value.version,'18.0.0');
 assert.equal(value.url,undefined);
});
test('monitor baseline matches desktop supported firmware',()=>{
 const fs=require('node:fs'),path=require('node:path');
 const source=fs.readFileSync(path.join(__dirname,'../src/SilverMoon/Application/FirmwarePreparation.cs'),'utf8');
 const {baseline}=require('./firmware-monitor.cjs');
 assert.ok(source.includes('GetString()!="'+baseline+'"'));
});
test('four-day cadence continues across month/year boundaries',()=>{
 const {isDue}=require('./firmware-monitor.cjs');
 assert.equal(isDue(new Date('2026-11-17T13:23:00Z')),true);
 assert.equal(isDue(new Date('2026-11-18T13:23:00Z')),false);
 assert.equal(isDue(new Date('2026-11-21T13:23:00Z')),true);
 assert.equal(isDue(new Date('2026-12-03T13:23:00Z')),true);
 assert.equal(isDue(new Date('2027-01-04T13:23:00Z')),true);
});