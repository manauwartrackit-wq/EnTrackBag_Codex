// Dependency-free tests for the actual Angular TypeScript logic. Angular DI/DOM
// are replaced with deterministic fakes; no real timers or credentials are used.
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');
const rx = require('rxjs');

function fixture() {
  let now = 1000000000000;
  const storage = new Map();
  const requests = [], navigations = [], intervals = [], deferred = [], listeners = {};
  class MonitoringService {}
  const monitoring={disconnected:new rx.Subject(),openPage:async()=>{},disconnect:async()=>{monitoring.stops++;},stops:0};
  class HttpClient {}
  class Router {}
  class NavigationEnd { constructor(url) { this.urlAfterRedirects = url; } }
  class HttpContextToken { constructor(factory) { this.factory = factory; } }
  class HttpContext {
    values = new Map();
    set(key, value) { this.values.set(key, value); return this; }
    get(key) { return this.values.has(key) ? this.values.get(key) : key.factory(); }
  }
  const router = { events: new rx.Subject(), navigateByUrl: url => navigations.push(url) };
  const response = { accessToken:'token', userName:'test', displayName:'Test', sessionId:1,
    expiresAtUtc: new Date(now + 8*3600000).toISOString(), roles:[], permissions:[] };
  const http = {};
  for (const method of ['get', 'post']) http[method] = (url, ...args) => {
    requests.push({ method, url, args });
    return rx.of(url.endsWith('/login') ? response : null);
  };
  const cache = {};
  let auth;
  const mocks = {
    '@angular/core': { Injectable: () => cls => cls, inject: token => token===HttpClient ? http : token===Router ? router : token===MonitoringService ? monitoring : auth },
    '@angular/common/http': { HttpClient, HttpContext, HttpContextToken },
    '@angular/router': { Router, NavigationEnd },
    'rxjs': rx,
    './monitoring.service': {MonitoringService},
  };
  function load(file) {
    file = path.resolve(__dirname, '..', file);
    if (cache[file]) return cache[file];
    const exports = {};
    cache[file] = exports;
    const js = ts.transpileModule(fs.readFileSync(file, 'utf8'), {
      compilerOptions:{ module:ts.ModuleKind.CommonJS, target:ts.ScriptTarget.ES2022, experimentalDecorators:true },
    }).outputText;
    vm.runInNewContext(js, {
      exports,
      require: name => mocks[name] || (name.startsWith('.') ? load(path.relative(path.resolve(__dirname,'..'),path.resolve(path.dirname(file),name+'.ts'))) : require(name)),
      localStorage: {getItem:key=>storage.get(key)??null, setItem:(key,value)=>storage.set(key,value), removeItem:key=>storage.delete(key)},
      document:{addEventListener:(name,fn)=>listeners[name]=fn},
      window:{addEventListener:(name,fn)=>listeners[name]=fn},
      Date: class extends Date { static now(){return now;} },
      setInterval: fn => intervals.push(fn), setTimeout: fn => (deferred.push(fn),deferred.length),
      clearTimeout: id => deferred[id-1]=()=>{}, queueMicrotask:()=>{},
      atob, console,
    }, {filename:file});
    return exports;
  }
  const AuthService=load('src/app/services/auth.service.ts').AuthService;
  auth=new AuthService();
  const interceptor=load('src/app/core/auth.interceptor.ts').authInterceptor;
  const background=load('src/app/core/request-activity.ts').BACKGROUND_REQUEST;
  function login(){auth.login({userName:'test',password:'unused'}).subscribe();requests.length=0;}
  function request(url='http://localhost:5100/api/test', bg=false){
    return {url, context:new HttpContext().set(background,bg), clone(options){return {...this,...options};}};
  }
  return {auth,http,monitoring,login,requests,navigations,intervals,deferred,listeners,storage,router,NavigationEnd,
    advance:ms=>now+=ms, interceptor,request, background};
}

test('session validation never overlaps and resumes after completion or error',()=>{
  const f=fixture(); f.login(); let calls=0; let pending=new rx.Subject();
  f.http.get=()=>{calls++;return pending;};
  f.monitoring.disconnected.next(); f.monitoring.disconnected.next();
  assert.equal(calls,1);
  pending.complete(); pending=new rx.Subject(); f.monitoring.disconnected.next();
  assert.equal(calls,2);
  pending.error(new Error('offline')); f.monitoring.disconnected.next();
  assert.equal(calls,3);
});

test('login stores a session, logout contacts server and clears token',()=>{
  const f=fixture(); f.login();
  assert.equal(f.auth.getAccessToken(),'token');
  f.auth.logout();
  assert.equal(f.requests[0].url,'http://localhost:5200/api/auth/logout');
  assert.equal(f.auth.getAccessToken(),null);
  assert.equal(f.navigations.at(-1),'/login');
});
test('five minutes idle clears token and redirects, polling does not renew',()=>{
  const f=fixture(); f.login(); const before=f.storage.get('last_activity_at');
  f.advance(299999); f.monitoring.disconnected.next();
  assert.equal(f.storage.get('last_activity_at'),before);
  assert.equal(f.auth.getAccessToken(),'token');
  f.advance(1); f.intervals[0]();
  assert.equal(f.auth.getAccessToken(),null);
  assert.equal(f.navigations.at(-1),'/login');
});
test('trusted interactions and navigation report activity; synthetic events do not',()=>{
  const f=fixture();f.login();
  f.listeners.change({isTrusted:false});assert.equal(f.requests.length,0);
  f.advance(1000);f.listeners.change({isTrusted:true});
  assert.equal(f.requests.at(-1).url,'http://localhost:5200/api/auth/activity');
  const before=f.requests.length;
  f.router.events.next(new f.NavigationEnd('/dashboard/summary')); // initial boot
  assert.equal(f.requests.length,before);
  f.advance(60000);f.router.events.next(new f.NavigationEnd('/administration'));
  assert.equal(f.requests.length,before+1);
});
test('an interaction after timeout cannot revive browser session',()=>{
  const f=fixture();f.login();f.advance(300000);f.listeners.change({isTrusted:true});
  assert.equal(f.requests.length,0);assert.equal(f.auth.getAccessToken(),null);
});
test('neither foreground nor background API traffic counts as activity',()=>{
  const f=fixture();f.login();let sent;
  f.advance(1000);f.interceptor(f.request(), req => {sent=req;return rx.of(null)}).subscribe();
  assert.equal(sent.setHeaders['X-User-Activity'],undefined);
  const before=f.storage.get('last_activity_at');f.advance(1000);
  f.interceptor(f.request(undefined,true),req=>{sent=req;return rx.of(null)}).subscribe();
  assert.equal(sent.setHeaders['X-User-Activity'],undefined);
  assert.equal(f.storage.get('last_activity_at'),before);
});
test('401 clears token; delayed old-session 401 cannot clear new login',()=>{
  const f=fixture();f.login();
  f.interceptor(f.request(),()=>rx.throwError(()=>({status:401}))).subscribe({error:()=>{}});
  assert.equal(f.auth.getAccessToken(),null);
  f.login();const old=new rx.Subject();
  f.interceptor(f.request(),()=>old).subscribe({error:()=>{}});
  f.storage.set('access_token','new-token');old.error({status:401});
  assert.equal(f.auth.getAccessToken(),'new-token');
});
test('JWT is not attached to external URLs and 403 does not log out',()=>{
  const f=fixture();f.login();let sent;
  f.interceptor(f.request('https://example.com/api'),req=>{sent=req;return rx.of(null)}).subscribe();
  assert.equal(sent.setHeaders,undefined);
  f.interceptor(f.request(),()=>rx.throwError(()=>({status:403}))).subscribe({error:()=>{}});
  assert.equal(f.auth.getAccessToken(),'token');
});
test('another tab logging out clears this tab and redirects',()=>{
  const f=fixture();f.login();f.listeners.storage({key:'access_token',newValue:null});
  assert.equal(f.auth.getAccessToken(),null);assert.equal(f.navigations.at(-1),'/login');
});

test('a newer immediate activity report cancels the old trailing callback',()=>{
  const f=fixture();f.login();f.auth.recordActivity();
  f.advance(100);f.auth.recordActivity();
  f.advance(60000);f.auth.recordActivity();
  assert.equal(f.requests.length,2);
  f.deferred.forEach(fn=>fn());
  assert.equal(f.requests.length,2);
});

test('slow activity calls cannot overlap; only new input schedules a follow-up',()=>{
  const f=fixture();f.login();let calls=0;let pending=new rx.Subject();
  f.http.post=()=>{calls++;return pending;};
  f.auth.recordActivity();f.advance(60000);f.auth.recordActivity();f.auth.recordActivity();
  assert.equal(calls,1);
  const first=pending;pending=new rx.Subject();first.complete();
  assert.equal(calls,2);pending.complete();
  assert.equal(calls,2);
  assert.equal(f.listeners.scroll,undefined);
});

test('queued activity cannot extend a five-minute idle session',()=>{
  const f=fixture();f.login();f.auth.recordActivity();f.advance(100);f.auth.recordActivity();
  f.advance(300000);f.deferred.forEach(fn=>fn());
  assert.equal(f.auth.getAccessToken(),null);assert.equal(f.requests.length,1);
  assert.equal(f.navigations.at(-1),'/login');
});

test('activity reports are limited to once per minute, idle has no heartbeat',()=>{
  const f=fixture();f.login();f.auth.recordActivity();
  for(let i=0;i<59;i++){f.advance(1000);f.listeners.input({isTrusted:true});}
  assert.equal(f.requests.length,1);
  f.advance(1000);f.deferred.at(-1)();assert.equal(f.requests.length,2);
  assert.equal(f.requests[1].args[1].headers['X-Activity-Age-Ms'],'1000');
  const before=f.requests.length;f.advance(60000);f.intervals[0]();assert.equal(f.requests.length,before);
  assert.equal(f.listeners.scroll,undefined);assert.equal(f.listeners.wheel,undefined);
  assert.equal(f.listeners.mousemove,undefined);assert.equal(f.listeners.keydown,undefined);
  f.advance(300000);f.intervals[0]();assert.equal(f.monitoring.stops,1);
  assert.equal(f.auth.getAccessToken(),null);
});
