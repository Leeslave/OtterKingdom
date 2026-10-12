# 앞쪽 ¾뷰 컷아웃 리깅 실험

**사용자 확정 기준은 V9입니다.** 이후 “기준”은 [BASELINE.md](BASELINE.md)의 고정 결과물을 뜻합니다. 후속 수정본과 구분하세요.

## V11 — 큰 움직임 비교 예시

기준은 V9로 유지합니다. V10의 외형을 유지하면서 반 보폭 6→10px, 발 들기 6→11px, 팔 회전 진폭 .23→.38rad, 몸통 상하 계수 2.3→3.8px로 늘렸습니다. 머리·가방·꼬리의 지연도 조절했습니다. 앞다리 중심은 -16→-10px로 옮겨 커진 보폭의 뒤쪽 돌출을 줄였습니다. 주기는 .84초로 동일합니다. 조작·접지 검증 및 걷기 연속 프레임 확인 완료.

직전 수정본은 `preview-v10.html`, 큰 동작 예시는 `walk-v11.gif`, 나란히 비교는 `motion-comparison-v11.gif`입니다. 비교 왼쪽은 기준 V9가 아닌 직전 수정본 V10입니다.

## V10 — 기준 이후 수정본: 두꺼운 선과 둥근 바지통

기준은 여전히 V9입니다. V10은 바지 윤곽선을 3.5→5.2px로 두껍게, 모서리 반경을 5→10px로 둥글게 수정했습니다. 굵어진 선은 안쪽으로 보정하여 기존 바깥 크기와 보행 경로를 유지합니다. 브라우저 조작·접지 검증 및 프레임 합성 확인 완료. 최종 미리보기 `walk-v10.gif`. `Baseline-V9/`는 변경하지 않았습니다.

## V9 — 걸을 때 뒤쪽 돌출 방지

현재 미리보기는 V9입니다. 앞다리 중심을 -28→-16px로 안쪽 이동, 다리 크기 67×84→60×76px, 반 보폭 10→6px, 발 들기 8→6px, 들린 다리 회전 진폭 0.10→0.025rad로 조정했습니다. 발 디딤과 바닥 이동 속도는 함께 유지됩니다. 24프레임 걷기 캡처, 하체 연속 프레임 육안 확인 및 접지 검증 완료. 이전 버전 `preview-v8.html`, 최종 미리보기 `walk-v9.gif`.

## V8 — 일자 바지통

현재 미리보기는 V8입니다. 초록 바지통 양옆을 수직 평행선으로 만들었습니다. 발은 이미지 파츠, 바지통은 같은 갈색 외곽선과 초록 단색으로 그리는 둥근 사각형입니다. 이미지 생성 결과에 내부 투명도와 얼룩이 남아, 최종 렌더링에서는 바지 부분을 단색 도형으로 처리했습니다. 보행 곡선과 접지는 유지했습니다. 최종 미리보기 `walk-v8.gif`, 이전 버전 `preview-v7.html`.

이미지 도구: built-in image_gen. 참조 `parts/solid-leg.png`. 프롬프트 요지: 초록 바지 전체를 양옆이 수직 평행한 짧고 넓은 둥근 사각형으로 바꾸고, 허벅지 돌출·패임·주름·접힌 단을 없애며 갈색 발과 투명 외부 배경을 유지. 생성 파일 `leg-straight-v8.png`. 최종 렌더러는 해당 파일의 발 부분만 사용합니다.

## V7 — 다리 위쪽 돌출 축소

현재 미리보기는 V7입니다. 다리 바지 부분의 뒤쪽을 최대 5px, 앞쪽을 최대 2px 안으로 줄이고 발 시작점 전에 보정이 사라지도록 연결했습니다. 발 크기와 다리 전체 높이, 보행 곡선은 유지했습니다. 이전 버전은 `preview-v6.html`, 최종 미리보기는 `walk-v7.gif`입니다. 브라우저 조작 및 접지 검증, 프레임 합성 확인을 완료했습니다.

## V6 — 표시한 뒤쪽 윤곽 평평하게

현재 미리보기는 V6입니다. 사용자가 표시한 팔 아래쪽 볼록한 몸통 외곽을 직선에 가깝게 편 원화를 built-in image_gen으로 편집했습니다. `body-flat-v6.png`와 `parts/body-flat.png`에 저장했습니다. 다리 크기 및 동작은 V5와 동일합니다. 생성 이미지의 옷 내부 잔여 투명 영역은 렌더러에서 내부 천 색상 받침으로 가렸습니다. V5는 `preview-v5.html`, 최종 미리보기는 `walk-v6.gif`입니다.

편집 프롬프트 요지: 기존 몸통의 왼쪽 소매 아래부터 바지 하단까지 볼록한 엉덩이 윤곽과 돌출을 없애고, 약간 안쪽으로 기울어진 직선과 작은 둥근 모서리로 연결. 다른 파츠는 추가하지 않고 외부 배경은 투명하게 유지. 최종 보정 프롬프트: “Repair ONLY the accidental dark/transparency hole on the lower LEFT green fabric. Fill that hole with smooth opaque matching green cloth. All pixels INSIDE this torso outline must be opaque, no holes or transparent patches. Preserve this exact straight left hip outline, every shape, pocket, buttons and colors. Keep the exterior background genuinely transparent. Do not redesign anything. Return the isolated repaired torso.”

## V5 — 다리 크기와 뒤쪽 돌출부 미세 조정

현재 미리보기는 V5입니다. 다리 폭 62→67px(약 8%)로 종횡비를 유지한 채 키웠습니다. 몸통 아래쪽 뒤 윤곽을 최대 8px 안으로 줄이는 렌더링을 적용했습니다. 가슴과 몸통 앞쪽 경계는 유지합니다. 보행 곡선은 V4와 같습니다. `preview-v4.html`에 이전 버전 보존, 최종 반복 미리보기는 `walk-v5.gif`입니다.

## V4 — 다리 원화 재제작

현재 미리보기는 V4입니다. 기존 상체는 유지하고, 바지와 작은 발이 붙어 있는 새 다리 원화를 built-in image_gen으로 생성했습니다. `leg-v4.png`가 생성 원본, `parts/solid-leg.png`가 투명 여백을 잘라낸 파츠입니다. 무릎 IK와 바지 이미지 변형은 제거했습니다. 새 그림의 종횡비를 유지하고 발바닥을 기준으로 이동·회전합니다. 보폭 18→10px, 발 들기 16→8px. 이전 버전은 `preview-v3.html`에 보존했습니다. `walk-v4.gif`가 최종 미리보기입니다.

생성 프롬프트: “Redraw ONLY ONE complete short chunky otter leg asset, matching this reference's green overalls, dark brown thick outline and cute flat cartoon style. A single INTEGRATED leg: broad rounded green trouser stub seamlessly attached to a small rounded dark brown paw at bottom, facing slightly right in front three-quarter view. Very stocky, total silhouette width approximately 85% of height. Pants occupy top 78 percent, paw bottom 22 percent. Wide solid green fabric all the way down, no skinny ankle, no exposed shin, no huge shoe, NO long human foot or toes. Tiny paw protrudes only slightly past trouser width. Green trouser top rounded and fully filled so it can tuck behind the body. Standing straight neutral weight-bearing pose, flat bottom. No knee bend, no horizontal bands, no boot cuff, no extra pieces, no whole character. One connected clean opaque asset centered with generous transparent padding. Actual alpha transparency outside. Preserve reference colors. This replaces distorted thin legs in a cutout rig.”

검증: 조작 옵션, 모든 보폭에서 접지 기준점 유지, 24개 프레임 캡처 및 합성 이미지 육안 확인. 뒷모습과 Unity 연결은 아직 포함하지 않습니다.

## V3 — 다리 두께 수정

현재 미리보기는 V3입니다. 바지통 폭을 앞다리 34→58px, 뒷다리 32→54px로 약 70% 늘렸습니다. 발 폭은 48→60px로 늘리고 발목 피벗을 맞췄습니다. 다리 길이, 보행 주기, 접지 높이는 유지했습니다. V2는 `preview-v2.html`, `walk-v2.gif`에 보존했습니다. 최종 GIF는 `walk-v3.gif`입니다.

## V2 — 발 디딤 수정

현재 `preview.html`은 V2입니다. 하단 **이전 다리와 비교** 체크박스로 V1 다리 동작을 볼 수 있습니다. 독립 원본은 `preview-v1.html`, `walk-v1.gif`로 보존했습니다.

- 생성한 `legs-v2.png`에서 바지와 발을 분리(`parts/trouser.png`, `parts/foot.png`). 기존 상체 파츠 유지.
- 짧은 다리와 넓은 발로 비율 변경, 허벅지·종아리 두 관절 IK와 바지 이미지 변형 적용.
- 주기 0.84초, 접지 60% / 발을 옮기는 구간 40%. 양발 반 주기 차이.
- 디딘 발은 수평 유지, 반대발은 위로 들어 올리고 발목 회전. 바닥 이동 속도를 접지 발의 후진 속도에 맞춤.
- 모든 보폭 설정의 관절 도달 거리와 접지 높이 검증, 브라우저 오류 및 비교 옵션 확인.

참고: [Nintendo의 New Super Mario Bros. 2 소개](https://www.nintendo.co.jp/3ds/abej/about/index.html). 짧은 다리와 두드러진 발 실루엣을 목표로 재해석했습니다. 게임 모션 데이터를 추출하거나 프레임 단위로 복제한 작업은 아닙니다.

V2 이미지 생성: built-in image_gen, 기존 atlas를 참조하여 초록 바지통 한 개와 오른쪽을 향한 넓은 갈색 수달 발 한 개를 투명 배경에 분리 생성. 정확한 프롬프트: “Create ONLY TWO separate animation rig components on transparent background matching the exact green fabric, brown paw, dark brown outlines and cute flat cartoon shading of this otter reference. Left half: ONE SHORT STRAIGHT GREEN TROUSER LEG without any foot, thick cylindrical short pant tube with rounded hidden hip cap and a neat cuff at bottom. Silhouette almost straight parallel sides, NOT a huge bulb at top, NO kink, NO knee joint drawing. Right half: ONE chunky broad brown OTTER FOOT/PAW pointing to the right in front three-quarter view, flat horizontal sole, round broad toe, short rounded ankle emerging at the upper left, brown fur not a shoe, no green fabric. Two separate fully opaque cutout pieces, generously spaced, same scale, aligned on common baseline, crisp clean edges, genuinely transparent surroundings. This is for a short stocky character with expressive stepping, foot about 1.4 times trouser width. Do not draw a complete character, legs pair, labels, checkerboard, shadows or glow.”

이하 내용은 V1 제작 기록입니다. V2에서는 다리에 IK와 이미지 변형이 추가되었습니다.

`preview.html`을 브라우저로 열면 바로 재생됩니다. 외부 라이브러리나 서버 없이 실행 가능한 파일입니다.

- 걷기 / 대기, 일시정지, 속도와 보폭 조절
- 관절 표시, 파츠 펼치기, 좌우 반전, 약 150px 크기 비교
- `atlas.png`: 투명 파츠 원본
- `parts/`: 분리된 6개 PNG. 팔과 다리는 각각 두 관절에서 재사용
- `rig.json`: 부모 관계, 관절 위치, 이미지 피벗, 크기, 그리기 순서
- `walk.gif`: 같은 브라우저 리깅 엔진에서 캡처한 24프레임 반복 미리보기

## 범위

앞쪽 오른쪽 ¾뷰 한 방향의 변환 계층 기반 강체 컷아웃 프로토타입입니다. 프레임별 생성 이미지를 교체하는 애니메이션이 아닙니다. Unity에 적용한 상태는 아니며, SpriteSkin 메시 변형이나 IK는 포함하지 않습니다. 좌우 반전은 모션 확인용으로, 장비의 실제 좌우를 보존하지 않습니다. 뒷모습은 별도 원화가 필요합니다.

시안에서 그대로 추출한 픽셀이 아닌, 시안의 캐릭터를 참고해 이미지 도구로 생성하고 숨겨진 부분을 보완한 파츠입니다. 얼굴·소매·가방과 신체 비율에 차이가 있습니다. 원화 일치도와 작업 동작용 손 포즈는 다음 검토 대상입니다.

## 재생성

Node.js와 `sharp`가 설치된 환경에서 저장소 루트 기준:

```
node Tools/QuarterRig/build.cjs
```

`Tools/QuarterRig/preview.html`은 템플릿이고, 결과 HTML에는 이미지와 리깅 데이터가 내장됩니다. 모션 곡선은 템플릿의 `pose()`에서 수정합니다. `check.cjs`는 Playwright와 로컬 Chrome으로 조작 검증 및 프레임 캡처를 합니다.

## 이미지 생성 기록

방식: built-in image_gen. 참조: 사용자가 첨부한 6열 캐릭터 시안의 첫 번째 캐릭터.

첫 프롬프트: 동일한 오른쪽 앞 ¾뷰 수달, 노란 탐험가 모자, 초록 멜빵바지와 갈색 가방을 유지하고, 투명 배경 위 3열 × 2행에 머리+모자 / 몸통 / 팔 / 다리 / 꼬리 / 가방을 분리. 가려진 어깨·허리·관절도 채우고 파츠 간 겹침 없이 생성.

최종 정리 프롬프트: “Background extraction cleanup ONLY. Preserve exactly these SIX separated otter rig pieces, their placement, colors and silhouette. Remove ALL dark background, brown glow, haze, shadow, gradients OUTSIDE the six outlined pieces. Outside pieces must be 100% transparent alpha=0. Inside all six pieces must be fully opaque alpha=255 including outlines. Clean crisp anti-aliased cutout edges. No changes to art or composition. Transparent game asset atlas.”

검증: 실제 배경 합성으로 투명도 확인, 브라우저 스크립트 오류 없음, 걷기/대기 전환 및 표시 옵션 작동, 24개 샘플 프레임 캡처. 관절은 강체이므로 일부 자세에서 종이 인형 같은 겹침이 보일 수 있습니다.
