# 번영의 시대 — 임시 그림 교체 요청 (GPT 전달용)

> 이 문서를 그대로 GPT에게 주고, 아래 **참고 그림**을 함께 첨부해 주세요.
> 지금 게임에 들어간 그림은 코드(Python)로 그린 **임시 그림**이에요. 같은 이름 · 같은 크기로 다시 그려서 **같은 자리에 덮어쓰면** 게임에 바로 들어가요 (코드 · 설정은 안 바꿔도 됨).

---

## 1. 이 게임 그림의 공통 스타일

- 귀엽고 아늑한 **해달 왕국** 모바일 게임. 스티커 같은 그림체.
- **진한 코코아색 외곽선** `#4B2E22`, 굵기는 512px 그림 기준 약 6~8px (작은 그림은 비례해서).
- 파스텔 · 따뜻한 색, 부드러운 2~3단 명암 (위 · 왼쪽이 밝음), 반짝이 하이라이트 조금.
- 시점: 건물 · 소품은 **앞에서 살짝 내려다본 3/4 쿼터뷰** (참고 그림 `Workshop.png`과 같은 각도).
- 글자 · 로고 · 워터마크 넣지 않기.

### 첨부할 참고 그림 (프로젝트 안 경로)

| 무엇 | 경로 |
|---|---|
| 건물 그림체 · 각도 | `Assets/Art/Buildings/Detail4/Workshop.png`, `Quarry.png`, `Workyard.png`, `HouseRed.png` |
| 소품 그림체 | `Assets/Art/Settlement/Prop_SupplyBox.png`, `Prop_FarmSign.png`, `ICON_Lamp.png` |
| 아이템 아이콘 그림체 | `Assets/Art/Settlement/ICON_Item_Wood.png`, `Assets/Art/Settlement/ICON_Item_Apple.png` |
| 바꿀 건물 원본 (학술원 · 왕궁) | `Assets/Art/Settlement/Building_Archive.png`, `Assets/Art/Settlement/Prop_Castle.png` |
| 지금 임시 그림 (구성 참고) | 아래 표의 각 경로 그대로 |

---

## 2. 파일 규칙 (꼭 지키기)

1. **PNG, 투명 배경(알파)**. 흰 배경 · 체크무늬 배경 금지.
2. **파일 이름 · 크기(px)를 표와 똑같이**. 1024로 그렸다면 표의 크기로 줄이고, 비율이 다르면 투명 여백으로 맞춤.
3. **그림 아랫변 = 땅**. 소품 · 더미 · 수레는 그림 맨 아래에 바닥 그림자(반투명 타원)가 닿게, 아래에 빈 여백을 두지 않기 (게임이 그림 아랫변을 땅에 붙여요).
4. 위 · 왼쪽 · 오른쪽 여백은 6px 정도만.
5. **저장 위치**: 표의 경로에 **덮어쓰기**. 같은 이름의 `.png.meta` 파일은 **절대 지우거나 바꾸지 않기** (지우면 게임 연결이 끊김).
6. 다 넣은 뒤에는 `Tools/UIGen/industry_icons.py` · `industry_art.py`를 **다시 돌리지 않기** (임시 그림으로 덮어써요).

### 게임이 그림을 쓰는 방식 (크기 맞출 때 참고)
- 소품 · 더미 · 수레는 **가로 폭을 기준으로** 크기를 맞춰요 → 세로는 비율대로. 가로로 너무 넓은 여백이 있으면 그림이 작아 보여요.
- **더미 3단계**(적음 · 보통 · 많음)는 같은 폭으로 바뀌어요 → **세 장 모두 같은 캔버스 크기 · 같은 바닥 그림자 폭**으로, 쌓인 양만 달라지게.
- **수레 5장**(빈 수레 · 짐 실은 수레 4종)은 해달이 미는 동안 서로 바뀌어요 → **다섯 장 모두 같은 캔버스 크기, 수레 몸통 · 바퀴 위치 똑같이**, 위에 실린 짐만 다르게.
- 학술원 · 왕궁은 **원래 건물 그림 자리에 그대로 바꿔 끼워요** → 원본과 **같은 캔버스 크기, 건물 바닥(밑변) 위치 · 폭 똑같이**.

---

## 3. 그릴 그림 목록 (35장)

> 한 번에 3~5장씩 (같은 묶음끼리 — 더미 3단계, 수레 5장은 꼭 한 번에) 부탁하면 모양이 잘 맞아요.

공통 프롬프트 앞부분 (각 프롬프트 앞에 붙이기):
> *Cute cozy mobile game asset for an otter kingdom game, sticker style, thick dark cocoa-brown outline (#4B2E22), soft pastel warm colors, gentle cel shading with light from top-left, 3/4 front view slightly from above, transparent background, centered, no text.*

### 3-1. 창고 자재 아이콘 — `Assets/Art/Item/Industry/` (256×256, 정사각 · 가운데)

| 파일 | 무엇 | 프롬프트 |
|---|---|---|
| `ICON_Item_Plank.png` | **판재** — 반듯하게 켠 널빤지 2~3장 묶음, 끝 단면 보이게 | *a neat stack of 3 sawn wooden planks with visible end grain, small green sprout accent* |
| `ICON_Item_StoneBlock.png` | **석재 블록** — 네모나게 다듬은 회색 돌 블록 | *a clean cut grey stone building block, cube, with chisel marks* |
| `ICON_Item_Ingot.png` | **금속 주괴** — 조개 구리를 녹인 구리빛 주괴 2개 | *two shiny copper metal ingots stacked, warm copper color, small sparkle* |
| `ICON_Item_Precision.png` | **정밀 부품** — 은색 톱니 + 금색 작은 톱니 + 나사 | *precision machine parts: a silver gear, a small golden gear and a screw* |

### 3-2. 가공 시설 앞 작업대 — `Assets/Art/Industry/`

| 파일 | 크기 | 무엇 · 조건 | 프롬프트 |
|---|---|---|---|
| `Prop_Sawhorse.png` | 376×316 | 목공소 **제재 작업대**: 통나무가 걸린 A자 받침 + 톱 | *a wooden sawhorse workbench holding a log with a hand saw stuck in it, small grass tuft* |
| `Prop_StoneBench.png` | 369×268 | 채석 작업소 **돌 재단대**: 낮은 돌 탁자 위 다듬는 블록 + 망치 · 정 | *a low stone cutting table with a half-cut stone block, hammer and chisel* |
| `Prop_Kiln.png` | 385×408 | 건설소 옆 **제련 가마**: 돌 돔 가마 · 굴뚝 · 불빛 + 앞에 작은 모루 | *a small dome-shaped stone smelting kiln with glowing fire opening, chimney, and an anvil in front* |

### 3-3. 시설에 쌓인 가공품 더미 — `Assets/Art/Industry/` (각 세트는 **같은 캔버스 크기**로)

| 파일 | 크기 (세 장 같게) | 무엇 |
|---|---|---|
| `Pile_Plank_0/1/2.png` | 각 400×260 | 판재 더미: 0 = 2장, 1 = 3~4장, 2 = 5~6장 엇갈려 쌓음 |
| `Pile_StoneBlock_0/1/2.png` | 각 400×300 | 석재 블록 더미: 0 = 1개, 1 = 3개 피라미드, 2 = 6개 피라미드 |
| `Pile_Ingot_0/1/2.png` | 각 450×240 | 금속 주괴 더미: 0 = 1개, 1 = 3개, 2 = 6개 피라미드 |
| `Pile_Precision_0/1/2.png` | 각 480×310 | 정밀 부품 상자 더미 (뚜껑 위로 톱니가 보이는 나무 상자): 0 = 1상자, 1 = 2상자, 2 = 5상자 |

프롬프트 예: *a small pile of 2 sawn wooden planks on the ground with a soft shadow* (개수만 바꿔서 세 장)

### 3-4. 수레 — `Assets/Art/Industry/` (다섯 장 **모두 500×360**, 수레 몸통 · 바퀴 위치 같게)

- **옆모습, 오른쪽으로 가는 방향.** 손잡이는 **왼쪽 위로** 뻗음 (해달이 왼쪽에서 밀어요. 게임이 왼쪽으로 갈 때 좌우를 뒤집어 씀).
- 나무 짐칸 + 큰 바퀴 2개.

| 파일 | 실은 짐 |
|---|---|
| `Cart_Empty.png` | 빈 수레 |
| `Cart_Plank.png` | 판재 3장 |
| `Cart_StoneBlock.png` | 석재 블록 3개 |
| `Cart_Ingot.png` | 금속 주괴 3개 |
| `Cart_Precision.png` | 톱니가 보이는 부품 상자 2개 |

프롬프트 예: *side view of a small wooden hand cart with two big wheels, handle sticking out to the upper left, moving to the right, loaded with 3 wooden planks*

| 파일 | 크기 | 무엇 | 프롬프트 |
|---|---|---|---|
| `Cart_Pennant.png` | 136×216 | **개량 수레 깃발**: 가는 나무 깃대 + 빨간 삼각 깃발(노란 줄), 깃대 아래가 그림 아래 가운데 | *a small red triangular pennant flag with a yellow stripe on a thin wooden pole* |

### 3-5. 광장 소품 · 꾸미기 — `Assets/Art/Industry/`

| 파일 | 크기 | 무엇 · 조건 | 프롬프트 |
|---|---|---|---|
| `Prop_WarehouseCrates.png` | 536×408 | **왕국 창고 자리**: 나무 상자 3개 더미 + 오른쪽에 상자 그림 표지판 | *a pile of three wooden supply crates with a small wooden sign showing a box icon* |
| `Prop_KingdomSign.png` | 252×382 | **왕국 표지판** (꾸미기 · 아이콘으로도 씀): 나무 기둥 + 판재 두 장 판에 조개 문장 | *a wooden signpost made of two planks with a pink scallop shell emblem, grass at the base* |
| `Prop_Fountain.png` | 500×436 | **광장 분수** (2×2 칸 꾸미기 · 아이콘): 둥근 돌 수반 2단 + 위로 솟는 물줄기 | *a round two-tier stone plaza fountain with water spraying up, light blue water* |
| `Prop_Flowerbed.png` | 512×232 | **돌 꽃밭** (2×1 칸, 가로로 긺): 돌 테두리 화단에 분홍 · 노랑 · 보라 · 흰 꽃 | *a long low flowerbed with a stone border, pink yellow purple and white flowers* |
| `Prop_RoyalBanner.png` | 195×506 | **왕립 산업단지 깃발** (가공 시설 옆 · 공사 아이콘): 긴 나무 깃대 + 보라색 세로 깃발(아래 제비꼬리) + 금 왕관 | *a tall wooden pole with a hanging purple royal banner with a golden crown emblem, swallowtail bottom* |
| `Prop_CapitalFlag.png` | 195×506 | **수도 광장 깃대** (공사 아이콘): 긴 나무 깃대 + 하늘색 세로 깃발 + 분홍 조개 문장 | *a tall wooden pole with a hanging sky-blue banner with a pink scallop shell emblem, swallowtail bottom* |

### 3-6. 건물 바뀐 모습 — `Assets/Art/Industry/` (**원본과 같은 크기 · 같은 밑변 위치**)

| 파일 | 크기 | 원본 | 무엇 |
|---|---|---|---|
| `Academy.png` | 400×420 | `Assets/Art/Settlement/Building_Archive.png` (기록관) | **왕립 학술원**: 기록관을 증축한 모습 — 같은 바닥 크기에 남색 지붕, 별 문장, 망원경 · 책 장식 |
| `Palace.png` | 700×760 | `Assets/Art/Settlement/Prop_Castle.png` (성) | **왕궁**: 성을 증축한 모습 — 같은 바닥 크기 · 같은 배치에 금빛 돔 지붕, 왕관 깃발, 조금 더 화려하게 |

프롬프트 예: *the same building as the reference archive, upgraded into a royal academy: indigo roof, a golden star emblem, a small telescope on the roof, same size and same footprint as the reference*

### 3-7. 마을 주택 미리보기 — `Assets/Art/Industry/` (주택 개량 창에만 쓰임)

| 파일 | 크기 | 무엇 |
|---|---|---|
| `TownHouse_Red.png` | 512×433 | `Assets/Art/Buildings/Detail4/HouseRed.png` 집 + 현관 왼쪽 등불 · 오른쪽 꽃 화분 |
| `TownHouse_Blue.png` | 512×381 | `Assets/Art/Buildings/Detail4/HouseBlue.png` 집 + 현관 왼쪽 등불 · 오른쪽 꽃 화분 |

> 광장에서는 집 그림(HouseRed/HouseBlue) 옆에 등불(`Assets/Art/Settlement/ICON_Lamp.png`) · 화분(`Prop_FlowerPot.png`)을 따로 세워요. 미리보기도 그 모습과 같게.

---

## 4. 바꾸지 않을 것

| 파일 | 까닭 |
|---|---|
| `Assets/Art/Industry/Paving_PlazaCenter.png`, `Paving_Road_*.png` | 광장 지도에서 흙길 모양을 그대로 따라 **스크립트가 만든** 돌길 (손으로 그리면 자리가 안 맞음). 돌 모양을 바꾸려면 `Tools/UIGen/plaza_paving.py`의 색 · 크기 값을 고침 |
| `Assets/Art/Industry/ICON_Paving.png`, `ICON_PavingRoad.png` | 위 돌길에서 잘라 낸 아이콘 (선택: 바꿔도 됨, 256×256 둥근 돌길 그림) |
| `Assets/Art/Buildings/Detail4/HouseRed.png`, `HouseBlue.png` | 팀 그림 (개량 주택으로 그대로 씀) |

---

## 5. 다 넣은 뒤 확인

1. 유니티를 열면 바뀐 PNG를 저절로 다시 불러와요 (메타는 그대로).
2. Play → 메뉴 `Tools/Industry/Dev — Learn All A Techs (Play)` → 마을 회관 [일터와 설비]
   - 가공 탭: 아이콘 · 더미 / 창고 탭: 자재 아이콘 · 왕국 공사 · 꾸미기 만들기
   - 광장: 목공소 · 채석 작업소 · 건설소 앞 작업대 · 더미, 수레 미는 해달, 왕국 창고 상자
3. 크기가 어색하면 (너무 크거나 작음) 그림 둘레 투명 여백을 먼저 확인 — 가로 여백이 넓으면 작아 보여요.
