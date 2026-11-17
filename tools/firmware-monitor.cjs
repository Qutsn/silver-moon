'use strict';
// Keep this baseline aligned with the reviewed desktop firmware, not the newest
// server response. Discovery must never grant permission to patch unknown bytes.
const baseline='17.0.37';
function versionParts(value){
 if(typeof value!=='string'||!/^\d{1,6}\.\d{1,6}\.\d{1,6}$/.test(value))throw new Error('Invalid firmware version');
 return value.split('.').map(Number);
}
function inspect(data){
 const parts=versionParts(data?.version);
 const name='Lennox_FW_V'+data.version+'.bin';
 if(data?.firmware?.name!==name||!Number.isSafeInteger(data.firmware.size)||data.firmware.size<=0)
  throw new Error('Unexpected Lennox firmware metadata');
 const old=versionParts(baseline);
 const first=parts.findIndex((n,i)=>n!==old[i]);
 // Only sanitized fields cross into logs/issues. Signed download URLs are omitted.
 return {version:data.version,name,size:data.firmware.size,newer:first>=0&&parts[first]>old[first]};
}
async function query(fetcher=fetch){
 const response=await fetcher('https://fota.marshall.software/msdk/firmware',{
  method:'POST',redirect:'error',signal:AbortSignal.timeout(30000),
  headers:{'Content-Type':'application/json','User-Agent':'SilverMoon-FirmwareMonitor/1.0'},
  body:JSON.stringify({channel:'prod',device:{codename:'Lennox',serial_number:'x',firmware_version:'0.0.1'},client:{type:'marshall-app',platform:'android',version:'3.9.2'}})
 });
 if(!response.ok)throw new Error('Firmware metadata request failed: HTTP '+response.status);
 const chunks=[];let size=0;
 for await(const chunk of response.body){
  size+=chunk.length;
  if(size>65536)throw new Error('Firmware metadata exceeds 64 KiB');
  chunks.push(chunk);
 }
 let data;
 try{data=JSON.parse(Buffer.concat(chunks).toString('utf8'));}
 catch{throw new Error('Firmware metadata is not valid JSON');}
 return inspect(data);
}
async function notify(github,context,result){
 if(!result.newer)return 'unchanged';
 const marker='<!-- silver-moon-firmware:Lennox:'+result.version+' -->';
 // Include closed issues: closing an alert must not cause another daily email.
 const issues=await github.paginate(github.rest.issues.listForRepo,{...context.repo,state:'all',per_page:100});
 if(issues.some(i=>!i.pull_request&&i.body?.includes(marker)))return 'already-notified';
 await github.rest.issues.create({...context.repo,
  title:'Middleton firmware update detected: '+result.version,
  body:marker+'\n\n@RougeNuage\n\nThe official Lennox production metadata now advertises **'+result.version+'**, newer than the supported **'+baseline+'**.\n\n- File: `'+result.name+'`\n- Advertised size: '+result.size+' bytes\n\nReview the new firmware before changing the supported hashes or patch offsets. This monitor checks metadata only; it has not downloaded, validated or installed the image.\n\n---\n\n官方 Lennox 生产接口报告了高于当前支持版本的新固件。请独立分析新版本后再更新补丁与哈希。监测仅查询元数据，未下载、验证或安装该固件。'
 });
 return 'created';
}
// Use UTC calendar days, not day-of-month steps (which drift at month boundaries).
function isDue(date=new Date()){
 const day=Date.UTC(date.getUTCFullYear(),date.getUTCMonth(),date.getUTCDate());
 const elapsed=Math.round((day-Date.UTC(2026,10,17))/86400000);
 return elapsed>=0 && elapsed%4===0;
}
module.exports={baseline,inspect,query,notify,isDue};