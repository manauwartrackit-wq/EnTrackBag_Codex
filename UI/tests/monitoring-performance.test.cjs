const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const ts=require('typescript');
const rx=require('rxjs');
function load(relative,mocks,storage=new Map([['access_token','token']])){
  const file=path.join(__dirname,'../src/app',relative);
  const exports={};
  const code=ts.transpileModule(fs.readFileSync(file,'utf8'),{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.CommonJS,experimentalDecorators:true}}).outputText;
  vm.runInNewContext(code,{exports,localStorage:{getItem:key=>storage.get(key)??null},require:name=>mocks[name]??(name==='rxjs'?rx:name.includes('environment')?{environment:{apiUrl:'http://localhost:5100/api',monitoringHubUrl:'http://localhost:5100/hubs/monitoring'}}:{}),console});
  return exports;
}
test('monitoring shares one connection and one handler set, stops on logout',async()=>{
  let builds=0,starts=0,stops=0;const handlers={};const invocations=[];
  const connection={state:'Disconnected',on:(event,fn)=>{assert.equal(handlers[event],undefined);handlers[event]=fn;},onclose:()=>{},onreconnecting:()=>{},onreconnected:fn=>connection.reconnected=fn,
    start:async()=>{starts++;connection.state='Connected';},stop:async()=>{stops++;connection.state='Disconnected';},invoke:async(...args)=>invocations.push(args)};
  class HubConnectionBuilder{withUrl(){return this;}withAutomaticReconnect(){return this;}build(){builds++;return connection;}}
  const {MonitoringService}=load('services/monitoring.service.ts',{'@angular/core':{Injectable:()=>x=>x},'@microsoft/signalr':{HubConnectionBuilder,HubConnectionState:{Disconnected:'Disconnected',Connected:'Connected'}}});
  const service=new MonitoringService();
  await Promise.all([service.openPage('/dashboard/summary'),service.openPage('/dashboard/summary')]);
  await service.openPage('/device-status');
  assert.equal(builds,1);assert.equal(starts,1);assert.equal(Object.keys(handlers).length,4);
  let payload;service.devices.subscribe(x=>payload=x);handlers.SystemUpdated({summary:1});assert.equal(payload.summary,1);
  await service.openPage('/administration');assert.equal(invocations.at(-1)[1],'');
  await service.disconnect();assert.equal(stops,1);
});
test('concurrent GET subscriptions share a request, completed data is not stale-cached',()=>{
  let calls=0;let pending=new rx.Subject();
  class HttpContext{set(){return this;}}
  const {ApiService}=load('core/api.service.ts',{'@angular/core':{Injectable:()=>x=>x,inject:()=>({get:()=>{calls++;return pending;}})},'@angular/common/http':{HttpContext}});
  const api=new ApiService();const a=[],b=[];
  api.get('dashboard/kpis').subscribe(x=>a.push(x));api.get('dashboard/kpis').subscribe(x=>b.push(x));
  assert.equal(calls,1);pending.next(42);pending.complete();assert.deepEqual(a,[42]);assert.deepEqual(b,[42]);
  pending=new rx.Subject();api.get('dashboard/kpis').subscribe();assert.equal(calls,2);
});
test('feature routes are lazy and login does not fetch SLA notifications',()=>{
  const routes=fs.readFileSync(path.join(__dirname,'../src/app/app.routes.ts'),'utf8');
  assert.equal((routes.match(/loadComponent:/g)||[]).length,8);
  assert.equal(/component:/.test(routes),false);
  for(const file of ['login/login.component.ts','app.component.ts'])
    assert.equal(fs.readFileSync(path.join(__dirname,'../src/app',file),'utf8').includes('notifications.refresh()'),false);
});
