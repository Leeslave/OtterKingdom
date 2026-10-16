const {chromium}=require('playwright');
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 try{
 const page=await browser.newPage({viewport:{width:1200,height:950}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:8773/preview.html');await page.evaluate(()=>window.rigReady);
 await page.click('#pause');
 const original=await page.evaluate(()=>document.querySelector('canvas').toDataURL());
 await page.selectOption('#tunePart','nearArm');await page.locator('[data-number="x"]').fill('25');await page.locator('[data-number="x"]').dispatchEvent('input');
 if(await page.evaluate(()=>RIG.bones.find(b=>b.name==='nearArm').pos[0])!==-35)throw Error('Front arm position');
 if(await page.evaluate(()=>RIG.rearBones.find(b=>b.name==='nearArm').pos[0])!==85)throw Error('Rear arm position');
 await page.selectOption('#tunePart','nearLeg');await page.locator('[data-number="y"]').fill('15');await page.locator('[data-number="y"]').dispatchEvent('input');
 if(await page.evaluate(()=>legPose(0,true,pose(0)).sole[1])!==15)throw Error('Leg position');
 await page.selectOption('#tunePart','head');await page.locator('[data-number="width"]').fill('120');await page.locator('[data-number="width"]').dispatchEvent('input');
 if(await page.evaluate(()=>RIG.bones.find(b=>b.name==='head').width)!==324)throw Error('Head size');
 if(original===await page.evaluate(()=>document.querySelector('canvas').toDataURL()))throw Error('Canvas unchanged');
 await page.reload();await page.evaluate(()=>window.rigReady);
 if(await page.evaluate(()=>rigTuning.values.head.width)!==120)throw Error('Persistence');
 await page.click('#resetAll');
 if(await page.evaluate(()=>rigTuning.values.head.width)!==100)throw Error('Reset');
 await page.selectOption('#tunePart','body');
 await page.screenshot({path:'ArtSource/Otter/WaiterRig/V14/tuning-panel.png'});
 await page.setViewportSize({width:390,height:844});
 if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Mobile overflow');
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS: arm front/rear, leg offset, head size, redraw, persistence, reset, mobile layout');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
