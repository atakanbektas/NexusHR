import {randomBytes} from 'node:crypto';
import {spawn} from 'node:child_process';
import {command,sleep} from './lab.mjs';
const trace=randomBytes(16).toString('hex'),span=randomBytes(8).toString('hex'),secret='SYNTHETIC-PRIVACY-PROBE-NEVER-STORE';
const now=String(BigInt(Date.now())*1_000_000n);
async function send(path,payload){await new Promise((done,reject)=>{const c=spawn(process.env.DOCKER_EXE??'docker',['exec','-i','nexushr-gateway','curl','--fail','--silent','-H','Content-Type: application/json','--data-binary','@-',`http://engineering-collector:4318/v1/${path}`],{windowsHide:true,stdio:['pipe','ignore','ignore']});c.stdin.end(JSON.stringify(payload));c.on('error',reject);c.on('exit',code=>code===0?done():reject(new Error('Telemetry probe ingestion failed')));});}
const resource={attributes:[{key:'service.name',value:{stringValue:'NexusHR.PrivacyProbe'}},{key:'sensitive.resource',value:{stringValue:secret}}]};
await send('logs',{resourceLogs:[{resource,scopeLogs:[{scope:{name:'engineering-privacy-test'},logRecords:[{timeUnixNano:now,traceId:trace,spanId:span,severityNumber:17,severityText:'ERROR',body:{stringValue:secret},attributes:[{key:'secret',value:{stringValue:secret}}]}]}]}]});
await send('traces',{resourceSpans:[{resource,scopeSpans:[{scope:{name:'engineering-privacy-test'},spans:[{traceId:trace,spanId:span,name:secret,kind:1,startTimeUnixNano:now,endTimeUnixNano:String(BigInt(now)+1_000_000n),status:{code:2,message:secret},attributes:[{key:'url.full',value:{stringValue:`http://synthetic.invalid?token=${secret}`}}],events:[{timeUnixNano:now,name:secret,attributes:[{key:'exception.message',value:{stringValue:secret}}]}]}]}]}]});
let traceBody='',logBody='';
for(let n=0;n<20;n++){
 await sleep(1000);
 traceBody=await command(['exec','nexushr-gateway','curl','--silent','-H','Accept: application/json',`http://engineering-tempo:3200/api/traces/${trace}`]);
 const query=encodeURIComponent('{service_name="NexusHR.PrivacyProbe"}');
 logBody=await command(['exec','nexushr-gateway','curl','--silent',`http://engineering-loki:3100/loki/api/v1/query_range?query=${query}&limit=10`]);
 const parsed=JSON.parse(traceBody);
 const found=(parsed.batches??parsed.resourceSpans??[]).some(b=>(b.scopeSpans??[]).some(s=>s.spans.some(v=>Buffer.from(v.traceId,'base64').toString('hex')===trace)));
 if(found&&logBody.includes('Application event'))break;
}
const parsed=JSON.parse(traceBody);
const found=(parsed.batches??parsed.resourceSpans??[]).some(b=>(b.scopeSpans??[]).some(s=>s.spans.some(v=>Buffer.from(v.traceId,'base64').toString('hex')===trace)));
if(!found||!logBody.includes('Application event'))throw new Error('Persisted telemetry was not observed');
if(traceBody.includes(secret)||logBody.includes(secret))throw new Error('Sensitive synthetic telemetry reached storage');
console.log('Real OTLP ingestion, Tempo trace and Loki log persistence verified. Secret body/URL/resource/event/status values absent from stored telemetry.');
console.log(`Synthetic privacy-probe trace: ${trace}`);
