const fs=require('fs'),path=require('path'),sharp=require('sharp');
module.exports=async(atlas,art,edges,flipTail)=>{
 const images={},types=['head','body','arm','leg','tail'];
 for(let row=0;row<2;row++)for(let col=0;col<5;col++){
 const left=Math.max(0,edges[col]-50),top=row?530:0,width=Math.min(1536,edges[col+1]+50)-left,height=row?494:530;
 const original=path.join(art,'original-atlas.png');
 const file=col===2&&art.includes('ShyRig')&&fs.existsSync(original)?original:atlas;
 const cell=await sharp(file).extract({left,top,width,height}).ensureAlpha().raw().toBuffer();
 const seen=new Uint8Array(width*height);let largest=[];
 for(let i=0;i<seen.length;i++)if(!seen[i]&&cell[i*4+3]>=128){let group=[],stack=[i];seen[i]=1;while(stack.length){const j=stack.pop();group.push(j);const x=j%width,y=Math.floor(j/width);for(const n of[x?j-1:-1,x<width-1?j+1:-1,y?j-width:-1,y<height-1?j+width:-1])if(n>=0&&!seen[n]&&cell[n*4+3]>=128){seen[n]=1;stack.push(n);}}if(group.length>largest.length)largest=group;}
 if(largest.length<100)throw Error('Empty part '+row+'/'+col);
 const keep=new Uint8Array(width*height);for(const j of largest){keep[j]=1;const x=j%width,y=Math.floor(j/width);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<width&&y+dy>=0&&y+dy<height)keep[(y+dy)*width+x+dx]=1;}
 for(let j=0;j<keep.length;j++)if(!keep[j])cell[j*4+3]=0;
 const clean=await sharp(cell,{raw:{width,height,channels:4}}).png().toBuffer();let png=await sharp(clean).trim({threshold:8}).png().toBuffer();
 if(flipTail&&row&&col===4)png=await sharp(png).flop().png().toBuffer();
 const key=(row?'rear-':'front-')+types[col];images[key]=png;fs.writeFileSync(path.join(art,'parts',key+'.png'),png);
 }return images;
};
