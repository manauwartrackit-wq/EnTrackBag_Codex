const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const ts=require('typescript');
const rx=require('rxjs');
function fixture(){
  const calls=[]; const timers=[]; const document={hidden:false};
  const service={};
  for(const name of ['getUsers','getRoles','getPermissions','getAccessTypes','getSessions','getAuditEvents','getActiveSessionCount']) service[name]=()=>{calls.push(name);return rx.of(name==='getActiveSessionCount'?1:[]);};
  service.createUser=request=>{calls.push(request);return rx.of({id:1,...request});};
  class AuthService{}; class AdministrationService{};
  const auth={hasPermission:()=>true,isAuthenticated:()=>true};
  const exports={};
  const code=ts.transpileModule(fs.readFileSync(require('node:path').join(__dirname,'../src/app/administration/administration.component.ts'),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022,experimentalDecorators:true}}).outputText;
  vm.runInNewContext(code,{exports,require:name=>name==='rxjs'?{...rx,forkJoin: sources=>rx.forkJoin({...sources})}:name==='@angular/core'?{Component:()=>cls=>cls,inject:type=>type===AuthService?auth:service}:name.includes('auth.service')?{AuthService}:name.includes('administration.service')?{AdministrationService}:{},document,setInterval:fn=>(timers.push(fn),timers.length),clearInterval:()=>{},Intl,Date});
  return {component:new exports.AdministrationComponent(),service,calls,timers,document};
}
function validForm(c){ Object.assign(c.userForm,{userName:'test',firstName:'Test',lastName:'User',email:'test@example.com',designation:'Engineer',password:'password123',confirmPassword:'password123'}); }
test('optional profile fields can be blank, permanent password and active account retained',()=>{
  const f=fixture();validForm(f.component);f.component.saveUser();
  assert.equal(f.calls.length,1);assert.equal(f.calls[0].password,'password123');
  assert.equal(f.calls[0].mustChangePassword,false);assert.equal(f.calls[0].isActive,true);
});
test('required fields, short password and mismatched confirmation cannot submit',()=>{
  for(const field of ['userName','firstName','lastName','email','designation','password','confirmPassword']){
    const f=fixture();validForm(f.component);f.component.userForm[field]='';f.component.saveUser();assert.equal(f.calls.length,0,field);
  }
  const f=fixture();validForm(f.component);f.component.userForm.password='short';f.component.userForm.confirmPassword='short';f.component.saveUser();assert.equal(f.calls.length,0);
});
test('refresh avoids hidden tabs, overlapping requests and requests after destruction',()=>{
  const f=fixture();f.component.ngOnInit();f.calls.length=0;
  const pending=new rx.Subject();let cancelled=false;
  f.service.getSessions=()=>{f.calls.push('sessions');return pending.pipe(rx.finalize(()=>cancelled=true));};
  f.timers[0]();f.timers[0]();assert.equal(f.calls.filter(x=>x==='sessions').length,1);
  f.component.ngOnDestroy();assert.equal(cancelled,true);f.timers[0]();assert.equal(f.calls.filter(x=>x==='sessions').length,1);
  const hidden=fixture();hidden.component.ngOnInit();hidden.calls.length=0;hidden.document.hidden=true;hidden.timers[0]();assert.equal(hidden.calls.length,0);
});
