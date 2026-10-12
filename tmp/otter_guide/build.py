from pathlib import Path
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from PIL import Image
import pypdfium2 as pdfium

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Docs/캐릭터행동기획'
OUT.mkdir(parents=True,exist_ok=True)
QA=ROOT/'tmp/otter_guide/render'; QA.mkdir(parents=True,exist_ok=True)
pdfmetrics.registerFont(TTFont('KR','C:/Windows/Fonts/malgun.ttf'))
pdfmetrics.registerFont(TTFont('KRB','C:/Windows/Fonts/malgunbd.ttf'))
W,H=595.28,841.89
pdf=OUT/'해달왕국_캐릭터행동과_순간포착.pdf'
c=canvas.Canvas(str(pdf),pagesize=(W,H))
c.setTitle('해달왕국 캐릭터 행동과 순간 포착 기획')
c.setAuthor('OtterKingdom')
style=ParagraphStyle('body',fontName='KR',fontSize=10,leading=16,wordWrap='CJK',textColor='#303938')
def text(t,x,y,width=499,size=10,bold=False):
 s=ParagraphStyle('x',parent=style,fontSize=size,leading=size*1.55,fontName='KRB' if bold else 'KR')
 p=Paragraph(escape(t).replace('\n','<br/>'),s); _,h=p.wrap(width,800); p.drawOn(c,x,y-h); return y-h
def pic(path,x,y,w,h):
 im=Image.open(path).convert('RGBA'); iw,ih=im.size; k=min(w/iw,h/ih)
 c.drawImage(ImageReader(im),x+(w-iw*k)/2,y-h+(h-ih*k)/2,width=iw*k,height=ih*k,mask='auto')
page=0
def start(title,sub):
 global page
 page+=1
 c.setFillColorRGB(1,1,1);c.rect(0,0,W,H,fill=1,stroke=0)
 text('OTTER KINGDOM   /   CHARACTER ANIMATION',48,805,size=8)
 text(title,48,778,size=22,bold=True)
 text(sub,48,738,size=9)
 c.setStrokeColorRGB(.85,.88,.87);c.line(48,48,W-48,48)
 text('캐릭터 행동 기획  ·  2026년 10월 12일',48,37,size=8)
 text(f'{page:02}',W-72,37,width=30,size=8)
def end(): c.showPage()
def portrait(look):
 return ROOT/('Assets/Art/Otter/ICON_Otter_Farmer.png' if look=='Farmer' else f'Assets/Art/Otter/ICON_Otter_Cast_{look}.png')
rows=[
('새싹이','농부','등급 미정','Farmer','농사 담당','익은 작물을 양손으로 잡아 쏙 뽑는다. 수확물을 바구니에 담고 이마의 땀을 닦는다.'),
('깡깡이','광부','등급 미정','Miner','채굴 담당','곡괭이로 바위를 두드린다. 캐낸 돌을 살피다가 반짝이는 광석을 발견하면 높이 들어 보인다.'),
('첨벙이','낚시꾼','등급 미정','Fisher','낚시 담당','낚싯대를 던지고 찌를 바라본다. 입질이 오면 몸을 뒤로 젖히며 물고기를 끌어 올린다.'),
('뚝딱이','건설 해달','등급 미정','Sunny','건설 담당','설계도를 펼쳐 확인한다. 망치질을 하고 완성한 구조물을 가볍게 두드려 확인한다.'),
('또박이','게시판 관리 해달','등급 미정','Sailor','기록과 접수 담당','게시판의 삐뚤어진 종이를 반듯하게 고친다. 서류에 도장을 찍고 가지런히 쌓는다.'),
('몽실','탐험가','등급 미정','Explorer','왕국에 처음 온 탐험가','손을 이마에 대고 먼 곳을 살핀다. 지도로 길을 확인하고 탐험을 쉬며 간식을 꺼내 먹는다.'),
('꾸벅이','잠꾸러기','레어 반영','Night','늘 졸린 성격이며 기존 작업 특성은 나무캐기','크게 하품한다. 나무 의자에 앉아 꾸벅꾸벅 졸다가 고개가 툭 떨어지면 잠깐 깬다.'),
('물감이','화가','등급 미정','Berry','그림 그리기와 손재주','작은 그림판에 붓질한다. 뒤로 물러나 고개를 갸웃하며 완성한 그림을 감상한다.'),
('포근이','다정한 이웃','등급 미정','Snow','운반 특성이며 이웃 돕기를 좋아함','이웃의 짐을 함께 들어준다. 빨랫줄의 수건을 걷어 포개고 벤치의 방석을 정돈한다.'),
('쿵쿵이','나무꾼','흔함','Forest','나무캐기','나무를 양팔로 감싸 흔든다. 떨어진 나뭇가지를 모아 어깨에 멘다.'),
('데굴이','조약돌 수집가','흔함','Mint','채굴','동그란 돌을 앞발로 데굴데굴 굴린다. 마음에 드는 돌을 닦아 나란히 놓는다.'),
('보들이','새싹 돌보미','흔함','Sprout','농사','새싹 앞에 쪼그려 앉아 잎을 살핀다. 작은 물뿌리개로 물을 주고 잎을 살짝 쓰다듬는다.'),
('퐁당이','물놀이 해달','흔함','Ocean','낚시','물가에서 발로 물장구친다. 조개를 주워 배 위에 놓고 작은 돌로 톡톡 두드린다.'),
('영차','힘센 운반 해달','흔함','Pumpkin','운반','짐을 번쩍 들어 옮긴다. 짐을 내려놓고 팔을 털다가 자기 힘에 뿌듯해한다.'),
('짤랑이','장사꾼','흔함','Honey','장사','동전을 하나씩 세어 쌓는다. 물건을 닦아 진열하고 지나가는 해달에게 보여준다.'),
('재잘이','수다쟁이','레어','Cherry','장사','다른 해달에게 다가가 손짓하며 이야기한다. 자기 이야기에 먼저 웃고 상대의 반응을 살핀다.'),
('콩콩이','수리공','레어','Cocoa','건설','의자 다리나 장난감을 살핀다. 작은 망치로 콩콩 고치고 직접 사용해본다.'),
('꼼꼼이','공예가','레어','Lilac','손재주','퍼즐 조각을 색깔별로 정리한다. 작은 소품에 리본을 묶고 비뚤어진 부분을 고친다.'),
('두리번','길잡이','레어','Sky','탐험','갈림길에서 번갈아 주변을 살핀다. 나침반과 지도를 확인하고 갈 방향을 가리킨다.'),
('끄적이','기록가','레어','Ink','기록','마을을 관찰하며 수첩에 적는다. 가로등 아래에서 기록을 읽다가 생각이 떠오르면 급히 덧쓴다.'),
('살랑이','벚꽃 해달','에픽 한정','Blossom','벚꽃 바람개비를 좋아하는 한정 해달','벚꽃 바람개비에 입김을 분다. 떨어지는 꽃잎을 따라가 양손으로 받아 머리에 얹는다.'),
]
common=[('이동','상하좌우 걷기, 멈춰 서기, 방향 돌리기'),('대기','눈 깜빡이기, 주변 두리번거리기, 기지개 켜기'),('의자','의자에 올라앉기 → 발이나 몸으로 리듬 타기 → 내려오기'),('축구공','공을 짧게 차며 드리블하기, 굴러가는 공 따라가기'),('퍼즐','조각 집기 → 방향 돌려 맞춰보기 → 끼우고 기뻐하기'),('식사','식탁이나 피크닉 매트에서 간식 먹기'),('교류','다른 해달에게 손 흔들기, 마주 보고 이야기하기')]
moments=[
('꾸벅이','Night','졸음은 옮는 거야','나무 벤치에서 졸고 있고 옆자리가 비어 있음','지나가던 해달이 옆에 앉아 하품하다 함께 잠든다. 마지막에는 서로 기대어 고개를 꾸벅인다.','두 해달이 서로 기대어 졸고 있는 순간'),
('재잘이','Cherry','우리끼리 비밀인데','식탁이나 피크닉 매트에서 다른 해달과 대화 중','귓속말을 하자 상대가 놀라고 둘이 배를 잡고 웃는다. 옆 해달까지 궁금해서 얼굴을 들이민다.','둘이 웃는 사이 세 번째 해달이 얼굴을 들이미는 순간'),
('콩콩이','Cocoa','손님 편안하시죠','빈 나무 의자를 수리하고 다른 해달이 근처에 있음','고친 의자에 친구를 앉혀본다. 친구가 편안하게 몸을 흔들자 콩콩이가 의자 옆에서 같은 박자로 몸을 흔든다.','앉은 친구와 콩콩이가 같은 박자로 움직이는 순간'),
('꼼꼼이','Lilac','마지막 조각은 거기','다른 해달과 퍼즐을 맞추는 중','마지막 조각을 찾다가 친구 머리 위에 붙은 것을 발견한다. 조각을 떼어 끼우고 둘이 하이파이브한다.','퍼즐을 완성하고 둘의 앞발이 마주치는 순간'),
('두리번','Sky','아 이쪽이었구나','가로등 근처에서 지도를 보고 있고 다른 해달이 다가옴','지도를 거꾸로 보고 고민한다. 친구가 지도를 돌려주자 둘이 동시에 고개를 기울였다가 길을 깨닫고 끄덕인다.','바로 놓인 지도를 함께 보고 고개를 끄덕이는 순간'),
('끄적이','Ink','오늘의 모델','밤에 가로등 아래에서 기록 중','옆 해달이 수첩을 들여다보니 자기 얼굴 낙서가 있다. 모델이 된 해달이 같은 표정을 지으면 끄적이가 그림을 덧그린다.','수첩 속 그림과 모델 해달이 같은 표정을 짓는 순간'),
('살랑이','Blossom','우리 마을에 봄이 왔어요','벚꽃 바람개비로 놀고 있고 다른 해달들이 근처에 있음','바람개비가 빠르게 돌며 꽃잎이 흩날린다. 살랑이가 빙글 돌자 친구들이 모여 꽃잎을 잡고 모두 머리에 꽃잎을 하나씩 얹는다.','모여든 해달들 머리 위에 꽃잎이 하나씩 놓인 순간'),
]
candidates=[('새싹이','당근 하나에 둘이서','큰 당근이 안 뽑혀 친구와 함께 당기다가 둘 다 엉덩방아를 찧는다.'),('깡깡이','너도 반짝여','광석의 반짝임이 친구 얼굴에 비쳐 서로 반짝이는 얼굴을 구경한다.'),('첨벙이','오늘의 대어는 화분','물고기 대신 낡은 장화를 낚자 친구가 꽃 한 송이를 꽂아준다.'),('뚝딱이','현장 감독의 새 모자','설계도에 몰두한 사이 친구가 머리 위에 작은 나무토막을 쌓는다.'),('또박이','접수 중 작은 소동','바람에 날리는 서류를 친구와 잡다가 둘 얼굴에 한 장씩 붙는다.'),('몽실','목적지는 간식','지도 위에 간식을 올려두자 친구들이 모여 탐험 회의가 간식 시간이 된다.'),('물감이','모델님 눈 떠주세요','초상화를 그리다가 모델이 잠들어 그림도 자는 얼굴로 수정한다.'),('포근이','담요 반쪽 나눠요','졸고 있는 친구에게 담요를 덮어주다가 자신도 옆에서 잠든다.')]

start('해달왕국 캐릭터 행동 기획','21종의 공통 동작과 고유 행동 및 순간 포착 장면')
y=text('모든 해달은 이동과 놀이 동작을 공유하고, 성격과 직업을 드러내는 고유 행동을 가진다. 레어 이상 해달은 특별한 장면을 시작하며, 플레이어는 이를 발견하고 포착해 도감에 모은다.',48,694,size=12)
pic(ROOT/'ArtSource/Otter/FarmerCast/lineup.png',48,600,499,400)
text('그림 1  프로젝트의 해달 외형 참고 20종  ·  농부 새싹이는 별도 외형',48,190,size=9)
text('문서 범위',48,156,size=12,bold=True)
text('행동 기획 초안이다. 기존 이름과 특성을 바탕으로 하며, 세부 동작과 순간 포착 장면은 제안이다. 꾸벅이는 요청 기준으로 레어를 반영했다. 다른 이야기·전문 해달의 등급은 미정이며, 방문 해달의 등급은 기존 방문 등급을 따른다.',48,132,size=9)
end()
start('공통 동작과 순간 포착 규칙','공통 동작은 21종 모두에게 적용하는 제작 목표')
y=692
for title,body in common:
 text(title,48,y,width=65,bold=True); y=text(body,123,y,width=423)-15
for i,(file,label) in enumerate([('Assets/Art/Settlement/ICON_Chair.png','의자'),('Assets/Art/Item/Toy/ICON_Toy_SoccerBall.png','축구공'),('Assets/Art/Item/Toy/ICON_Toy_Puzzle.png','퍼즐'),('Assets/Art/Item/Toy/ICON_Toy_CherryPinwheel.png','벚꽃 바람개비')]):
 x=54+i*125;pic(ROOT/file,x,425,94,85);text(label,x,335,width=110,size=9)
text('그림 2  프로젝트 소품 이미지  ·  행동에 사용하는 물건의 형태 참고',48,305,size=9)
text('포착의 흐름',48,268,size=13,bold=True)
text('평소 고유 행동 → 장소와 동료 조건 충족 → 확률로 특별 장면 발생 → 플레이어가 직접 포착 → 도감 등록',48,240,size=11)
text('제안 규칙',48,184,size=13,bold=True)
text('공통 놀이는 해당 가구나 장난감이 있을 때 발생한다. 레어 이상 조건은 장면을 시작하는 주인공에게 적용하고, 함께 등장하는 해달은 흔함이어도 참여할 수 있다. 장면은 재발생하며 도감에는 제목, 주인공, 함께한 해달, 첫 발견 날짜를 남긴다. 미발견 장면은 짧은 힌트만 보여준다. 확률과 유지 시간은 플레이 검증 후 정한다.',48,159,size=10)
end()
for group in range(3):
 start(f'캐릭터별 고유 행동 {group+1}', '전원 공통 동작 포함  ·  외형은 현재 프로젝트 이미지  ·  아래 세부 행동은 기획 제안')
 y=697
 for name,role,tier,look,trait,body in rows[group*7:group*7+7]:
  pic(portrait(look),48,y,66,65)
  text(f'{name}  {role}  /  {tier}',130,y,width=415,size=11,bold=True)
  text(f'기존 설정  {trait}',130,y-21,width=415,size=8)
  text(body,130,y-38,width=415,size=9)
  y-=88
 end()
for group,(a,b) in enumerate([(0,4),(4,7)]):
 start(f'순간 포착 장면 {group+1}','레어 이상 해달의 특별 행동 제안  ·  도감 제목은 가제')
 y=696
 for name,look,title,condition,scene,key in moments[a:b]:
  pic(portrait(look),48,y,55,55)
  text(f'{name}  /  {title}',118,y,width=428,size=12,bold=True)
  yy=text('조건  '+condition,118,y-24,width=428,size=9)-7
  yy=text(scene,118,yy,width=428,size=10)-7
  text('포착 지점  '+key,118,yy,width=428,size=9)
  y-=153
 if group==1:
  text('연출을 구분하는 기준',48,211,size=13,bold=True)
  text('같은 직업도 고유 행동의 중심을 달리한다. 새싹이는 수확, 보들이는 돌보기, 뚝딱이는 건축, 콩콩이는 수리, 또박이는 정리, 끄적이는 관찰 기록에 집중한다. 순간 포착은 이 성격이 동료와 만났을 때 생기는 작은 사건으로 구성한다.',48,184,size=10)
 end()
start('등급 확정 후 추가할 장면','아래 8종이 레어 이상으로 정해질 경우에만 적용하는 후보')
y=694
for name,title,body in candidates:
 text(f'{name}  /  {title}',48,y,size=11,bold=True)
 y=text(body,48,y-22,size=10)-17
text('참고 자료와 이미지 출처',48,155,size=12,bold=True)
text('프로젝트 내부 자료: 해달 모습20 작업기록, 광장생활 작업기록, OtterCastSetup, SettlementSetup.Traits, GachaSetup. 외형 이미지: Assets/Art/Otter 및 ArtSource/Otter/FarmerCast/lineup.png. 소품 이미지: Assets/Art/Settlement 및 Assets/Art/Item/Toy. 이미지는 외형 참고이며 제안 동작의 완성 예시는 아니다.',48,130,size=8)
end();c.save()

md=['# 해달왕국 캐릭터 행동과 순간 포착 기획','', '작성일: 2026-10-12','', '21종의 공통 동작과 고유 행동을 정리한 기획 초안. 기존 이름과 특성을 바탕으로 하며 세부 동작과 순간 포착은 제안이다. 꾸벅이는 요청 기준으로 레어를 반영한다. 다른 이야기·전문 해달의 등급은 미정이고 방문 해달은 기존 방문 등급을 따른다.','', '![외형 참고 20종](../../ArtSource/Otter/FarmerCast/lineup.png)','', '## 공통 동작','']
md += [f'- **{a}**: {b}' for a,b in common]
md+=['','공통 놀이는 물건이 배치되어 있을 때 발생한다. 아래 캐릭터는 모두 공통 동작을 포함한다.','','## 캐릭터별 고유 행동','']
for name,role,tier,look,trait,body in rows:
 rel=portrait(look).relative_to(ROOT).as_posix();md += [f'### {name} {role}',f'![{name}](../../{rel})','',f'- 등급: {tier}',f'- 기존 설정: {trait}',f'- 고유 행동 제안: {body}','']
md+=['## 순간 포착','', '평소 고유 행동 → 조건 충족 → 확률로 특별 장면 발생 → 플레이어가 직접 포착 → 도감 등록. 레어 이상 조건은 주인공에게 적용하며 흔함 해달도 동료로 참여할 수 있다. 장면은 재발생하고 도감에는 제목, 주인공, 함께한 해달, 첫 발견 날짜를 남긴다. 미발견 상태는 짧은 힌트를 보여준다. 확률과 유지 시간은 플레이 검증 후 정한다.','']
for name,look,title,condition,scene,key in moments:md += [f'### {name} {title}',f'- 조건: {condition}',f'- 장면 제안: {scene}',f'- 포착 지점: {key}','']
md+=['## 등급 확정 후 추가할 장면','', '해당 캐릭터가 레어 이상으로 정해질 경우에만 적용하는 후보.','']+[f'- **{name} — {title}**: {body}' for name,title,body in candidates]
md+=['','## 제작 참고','', '같은 직업도 새싹이는 수확, 보들이는 돌보기, 뚝딱이는 건축, 콩콩이는 수리, 또박이는 정리, 끄적이는 관찰 기록으로 구분한다. 이미지들은 프로젝트의 외형 참고 자료이며 제안 동작의 완성 예시는 아니다.','', '참고: Docs/해달_모습20_작업기록.md, Docs/광장생활_작업기록.md, Assets/Editor/Settlement/OtterCastSetup.cs, Assets/Editor/Settlement/SettlementSetup.Traits.cs, Assets/Editor/Gacha/GachaSetup.cs.']
(OUT/'해달왕국_캐릭터행동과_순간포착.md').write_text('\n'.join(md),encoding='utf-8')
doc=pdfium.PdfDocument(str(pdf))
for i,p in enumerate(doc):
 im=p.render(scale=1.4).to_pil(); im.save(QA/f'page-{i+1}.png')
print(f'{len(doc)} pages | {pdf}')
