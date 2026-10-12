# 화면에서 빨간 "!" 말풍선(걷는 해달 머리 위)을 찾아 바로 아래(해달 몸)를 누름.
#   python3 bang.py            찾기만
#   python3 bang.py X Y        (X, Y)에 가장 가까운 "!" 해달을 누름
import os,sys,json,urllib.request
PORT=os.environ.get('QA_PORT','8777')
from PIL import Image
def post(ep,d={}):
    return urllib.request.urlopen(urllib.request.Request(f'http://127.0.0.1:{PORT}/'+ep,data=json.dumps(d).encode(),method='POST'),timeout=300).read().decode()
def find(path):
    im=Image.open(path).convert('RGB'); W,H=im.size; px=im.load()
    seen=set(); res=[]
    for y in range(130,800,2):
        for x in range(0,W,2):
            if (x,y) in seen: continue
            r,g,b=px[x,y]
            if r>200 and g<125 and b<115 and r-g>90:
                st=[(x,y)]; seen.add((x,y)); pts=[]
                while st:
                    a,c=st.pop(); pts.append((a,c))
                    for dx,dy in((2,0),(-2,0),(0,2),(0,-2)):
                        n=(a+dx,c+dy)
                        if 0<=n[0]<W and 0<=n[1]<H and n not in seen:
                            rr,gg,bb=px[n]
                            if rr>200 and gg<125 and bb<115 and rr-gg>90: seen.add(n); st.append(n)
                if 8<=len(pts)<=150:
                    xs=[p[0] for p in pts]; ys=[p[1] for p in pts]
                    w=max(xs)-min(xs); h=max(ys)-min(ys)
                    if h>=w*1.5 and h>=10:
                        cx=(max(xs)+min(xs))//2; cy=(max(ys)+min(ys))//2
                        white=sum(1 for dx in(-12,12) for dy in(-8,0,8) if 0<=cx+dx<W and px[cx+dx,cy+dy][0]>238 and px[cx+dx,cy+dy][1]>190 and px[cx+dx,cy+dy][2]>150)
                        if white>=4: res.append((cx,cy))
    return res
if __name__=='__main__':
    p=post('shot',{'name':'bang'})
    b=find(p); print('bangs',b)
    if b and len(sys.argv)>1:
        tx,ty=map(int,sys.argv[1:3]) if len(sys.argv)>2 else (225,450)
        cx,cy=min(b,key=lambda q:(q[0]-tx)**2+(q[1]-ty)**2)
        print(post('click',{'x':cx,'y':cy+55,'hold':120}),cx,cy+55)
