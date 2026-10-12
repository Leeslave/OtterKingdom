# WebQA — Claude 클라우드 세션에서 웹 빌드 플레이 QA

Claude Code 클라우드 세션(claude.ai/code)의 헤드리스 Chromium으로 Unity Play 웹 빌드를 띄워, 스크린샷을 보며 누르고 끌어서 플레이하는 도구.
5~8차 QA(`Docs/QA_플레이테스트_기록.md`)를 이걸로 했다. 화면을 보고 누르는 방식이라 타격감 · 소리 · 빠른 조작은 판단 못 함.

## 다른 사람이 이어서 할 때 — 새 세션 첫 메시지

> develop 브랜치에서 Tools/WebQA/README.md를 읽고, Docs/QA_플레이테스트_기록.md의 마지막 "이어서 할 때"부터 웹 빌드로 이어서 QA 해줘. 세이브는 Docs/QA_saves/web8_lv19_save.json

## 준비 (사람이 한 번)

1. **저장소 접근**: `Leeslave/OtterKingdom` 협업자 + claude.ai에 GitHub 연결
2. **네트워크 허용**: 세션 위쪽 환경 칩(예: 「환경이름 · OtterKingdom」) → Edit → Network access → 허용 도메인에 세 개 (환경은 계정마다 따로)
   - `play.unity.com` · `play-prod.struckd.com` · `cdn.play.unity.com` (게임 파일 loader.js · wasm · data가 여기)
   - 바꾼 뒤 막히면 새 세션
3. **빌드**: Unity Play에 최신 빌드 업로드 (지금 주소 `play.unity.com/en/games/1b13367a-07d5-4ca7-80a9-9ff2598b3cc5/otterkingdom`).
   게임 칸 주소는 `https://play-prod.struckd.com/api/v1/games/game/<게임ID>/build/latest/frame` — 다른 게임 ID면 `QA_URL`로.

## 띄우기

```bash
# 프로필 · 스크린샷은 저장소 밖(스크래치패드)에. 백그라운드로 (최대 2시간 → 끝나면 다시 띄움, 프로필은 남음)
QA_DIR=<스크래치패드>/qa node Tools/WebQA/driver.js > <스크래치패드>/qa/driver.log 2>&1   # run_in_background
# 준비될 때까지 기다리며 로딩 40초
curl -sS --retry-connrefused --retry 40 --retry-delay 3 -X POST http://127.0.0.1:8777/wait -d '{"ms":40000}'
```

- Playwright 1.56 + `/opt/pw-browsers` Chromium(SwiftShader WebGL 2.0)으로 450×900 세로. `playwright install` 하지 말 것
- Playwright 위치는 컨테이너마다 다름: driver.js가 `PLAYWRIGHT_PATH` → `/opt/node-tools/…` → `/opt/node22/lib/node_modules/playwright` 순으로 찾음. `bang.py` 등은 PIL이 필요 (`pip install pillow`)
- `page.goto: net::ERR_CERT_AUTHORITY_INVALID`면 브라우저 인증서 저장소가 비어 있는 것 (8차). 검증을 끄지 말고 프록시 CA를 넣음:
  ```bash
  apt-get install -y libnss3-tools   # certutil
  cd <스크래치패드> && awk '/BEGIN CERT/{n++} {print > ("ca-" n ".pem")}' /root/.ccr/agent-proxy-ca.crt
  for f in ca-*.pem; do certutil -d sql:$HOME/.pki/nssdb -A -t "C,," -n "ccr-$f" -i $f; done
  ```
- 첫 화면(타이틀)은 `(225,600)`을 두 번(3초 간격) 눌러야 들어감 (첫 클릭은 캔버스 포커스)
- 콘솔: `q.sh logs '{"n":40}'` (`Shader … not supported` · `Bounds Source is not assigned` · `No FarmBalanceData` 경고는 원래 있음)

## 명령 (`q.sh <명령> '<json>'`)

| 명령 | 예 | 메모 |
|---|---|---|
| shot | `{"name":"s01"}` | `QA_DIR/shots/s01.png` 경로를 돌려줌 → Read로 봄 |
| click | `{"x":225,"y":553,"hold":100,"n":1,"gap":120}` | hold = 누르고 있는 ms |
| drag | `{"x1":60,"y1":450,"x2":400,"y2":450,"steps":25,"dt":30}` | 카메라 이동 · 목록 스크롤 · 미리보기 끌기 |
| wait | `{"ms":60000}` | 300초 넘으면 curl이 끊김 → 나눠서 |
| eval | `{"js":"…"}` | 페이지에서 JS (IndexedDB 등) |
| reload / logs / quit | | |

- 스크린샷 두 장 비교: PIL로 나란히 붙여 한 번에 Read (토큰 절약)
- `python3 bang.py X Y`: 화면의 빨간 "!" 말풍선을 찾아 (X,Y)에 가장 가까운 해달을 누름. 확인 창이 떴는지는 `(60,450)` 픽셀이 밝은 크림색(R>250, G>238)인지로 봄

## 세이브

- 게임 세이브는 IndexedDB `/idbfs/6005a7260c1b7009def43bce62143115/save.json` (게임 주소가 같으면 같은 폴더. 다르면 `idb.py keys`)
- **넣기**: `python3 idb.py load Docs/QA_saves/web7_lv18_save.json` → `q.sh reload` → 타이틀 두 번 누르기
- **저장시키기**: 이 빌드는 `autoSyncPersistentDataPath`가 꺼져 있어 게임 안 저장이 IndexedDB로 안 감 → 설정(톱니 `(418,53)`) 열고 음악/효과음 칸 `(340,358)`을 두 번 누른 뒤 닫기 `(408,192)` → `PlayerPrefs.Save()`가 save.json을 옮김. `savets.py get`의 시각이 바뀌었으면 성공 (안 바뀌면 `(340,411)`도 시도)
- **꺼내기**: `python3 idb.py dump Docs/QA_saves/webN_lvXX_save.json` (커밋해 두면 다음 사람이 이어 감)
- **오프라인 보상 시험**: 저장시킨 뒤 `savets.py set <과거 시각>` → `q.sh reload` → 들어가면 "쉬는 동안 모인 것" 창. 세이브 JSON은 들여쓰기(`"lastSaveUtc": "…"`) 형식

## 화면 좌표 (450×900, Lv.18 기준)

| 곳 | 좌표 |
|---|---|
| 위쪽 부탁 띠 [보기] | (400,104) — **hold 200** 이상이어야 카메라가 감 |
| 부탁 창 큰 버튼(만나러 가기 · 자리 고르기 · 작은 집 짓기) | (220,724) hold 200 / 창 닫기 X (405,530) |
| 완료 창 [확인] | (225,553) · 그 뒤 "OO 완성! [닫기]" (225,205) |
| 확인 대화 [예] / [아니오] | (137,490) / (312,490) |
| 아래 탭: 도감 · 퀘스트 · 꾸미기 · 가방 · 이동 | (52,858) · (140,858) · (225,858) · (308,858) · (405,865) |
| 이동 창: 광장 · 밭 · 낚시터 | (243,620) · (362,617) · (243,720) |
| 퀘스트 [모두 받기] / 창 닫기 | (225,834) / (418,55) |
| 가방 탭: 전체 · 작물 · 물고기 · 광물 | (77,381) · (147,381) · (217,381) · (380,381)대 (탭 순서는 화면 확인) |
| 가방 첫 칸 → [판매] → [최대] → [팔기] | (92,483) → (373,184) → (307,467) → (295,557) · 가방 닫기 (420,40) |
| 꾸미기 모드: 미리보기 ✓ / [완료] | 미리보기 위 초록 ✓ / (390,53) |
| 요정 상점: 첫 칸(목재 x10) → 수량 + → [구매] → 닫기 | (128,390) → (313,453) 연타 → (300,585) → (417,52) |
| 밭 강화 · 낚싯대 강화 버튼 | 화면 왼쪽 아래 (92,755) · 강화 (137,516대) |

- 요정 상점은 광장 서쪽 해달 상점 건물(요정이 앞에 서 있음)을 누름. 카메라를 오른쪽으로 끌어(`x1:60→x2:400` 3번) 찾음
- 오프라인 농사 NPC: 밭 오른쪽 위 초록 모자 해달 (360,245) → 칸마다 [변경] → 작물
- 시설 · 집 짓기: 부탁 창 → 꾸미기 모드 미리보기 → 빈 땅을 누르면 미리보기가 그리로 옮겨짐 → 초록이면 ✓. 광장이 꽉 차 Lv.16+ 건물은 서쪽 끝(카메라 오른쪽으로 7번 끌기)에 놓음

## 알아 둔 것 (도구 · 빌드)

- 걷는 해달은 카메라가 가는 동안 걸어가 버림 → `bang.py`로 잡음 (7차에 코드로 고침: 새 빌드부터는 8초 서 있음)
- 이야기 부탁의 [만나러 가기]가 엉뚱한 곳으로 가면 해달이 있는 곳을 직접 찾아 누름
- 목재는 요정 상점 "목재 x10"(300골드)으로 사는 게 가장 빠름. 골드는 가방에서 다이아몬드 · 물고기 · 작물 판매
- 가방 16칸이 금방 참 → 가방 잠긴 칸을 눌러 조개 100으로 4칸씩 확장
- 시설은 한 번에 하나만 지음. 공사 시간: 상점 5분 · 목공소 6 · 채석 7 · 창고 8 · 어업 9 · 건설소 10 · 기록관 11분 (건설소 뒤 −15%)
- 작은 집은 지을수록 30%씩 비싸짐 (600 → 780 → 960 → … 8번째 2,220G)
- 백그라운드 명령은 최대 2시간 → driver가 꺼지면 다시 띄우고 `wait` 40초 뒤 타이틀 두 번 (진행은 마지막 저장 시점부터)
- 1회 QA(새 빌드 · 한 단계 레벨)에 토큰이 꽤 듦: 스크린샷은 꼭 필요할 때만, 기다림은 `wait`로 묶어서
