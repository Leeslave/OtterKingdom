# 웨이터 3/4뷰 · Unity 리깅

2026-10-13. 첨부한 정면 웨이터를 참고해 built-in image_gen으로 앞/뒤 3/4뷰 파츠를 생성했다. 별무늬 귀, 두 갈래 앞머리, 졸린 눈, 주황 나비넥타이, 갈색 조끼, 흰 소매를 유지했다. 앞뒤 각각 7개 렌더 부위이며 좌우는 반전한다.

- `preview.html`: 걷기/대기와 네 방향 미리보기.
- `unity-0.png` ~ `unity-3.png`: 실제 Unity에서 렌더한 북동/북서/남동/남서.
- `atlas.png`, `parts/`: 생성 원화와 추출 부위. 투명도를 보존했다.
- `rig.json`: 웨이터 관절 위치. 농부 V14와 같은 발 접지/보폭/주기.
- `Assets/Resources/WaiterRigV14`: Unity용 스프라이트와 리그 데이터.
- `Assets/Prefabs/Plaza/PlazaOtter_WaiterRigged.prefab`: 농부와 같은 QuarterRigView, OtterVisualController, OtterWanderAgent 구성.

`Plaza_RiggedPlacement`의 Otters 아래 (-1.8, 0.6)에 추가했다. Play 하면 농부와 동일하게 광장 배회 시스템으로 움직인다. 원래 농부 두 명과 V9 웨이터 작업은 유지했다. 웨이터 높이는 약 1.67 유닛이다.

새로운 주문/서빙 게임 로직은 포함하지 않는다. 전용 감은 눈 이미지는 아직 없으며, 앉기 자세는 공통 리깅을 사용한다.

검증: 브라우저 네 방향, 14개 부위 로드, 디딤 발 높이 오차 0. Unity 6000.3.8f1 격리 프로젝트에서 실제 QuarterRigView로 네 방향 각각 7개 부위 렌더 성공. 실제 광장 Play 세션을 원격 조작한 것은 아니다.

재생성: `node Tools/WaiterRig/build-unity.cjs` (sharp 필요). 스프라이트 메타데이터는 현재 파츠 피벗/PPU 200을 유지한다.

## 일자 다리 수정
첨부 설정을 approved-settings.json으로 보존하고 미리보기 초기값 및 Unity 파츠에 적용했다. 몸통 높이 82%, 머리 Y +17, 가까운 팔 Y -7, 먼 팔 Y -10. straight-legs-v2.png는 내장 image_gen으로 만든 앞/뒤 일자 다리 원화이며 기존 다리 원화는 보존했다. 새 저장 키로 이전 브라우저 값이 첨부 설정을 덮어쓰지 않도록 했다.
