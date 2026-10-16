# 낚시 해달 3/4뷰 리깅

승인된 회갈색 털, 짧은 갈기, 파란 선원복 콘셉트를 앞/뒤 컷아웃 리그로 만들었다. 기존 농부·웨이터 파일과 V9 기준은 유지한다.

- `preview.html`: 남동/남서/북동/북서 걷기, 대기, 앞쪽 낚싯대 들기 자세, 팔·다리 위치 및 몸통·머리 크기 조절. 조절값은 이 캐릭터 전용 브라우저 저장소에 보존하고 JSON으로 내보낼 수 있다.
- `approved-concept.png`: 사용자 승인 콘셉트.
- `atlas.png`, `parts/`, `rod-source.png`: 내장 image_gen으로 만든 부위와 낚싯대. 원화의 투명도를 보존해 분리했다.
- `Assets/Resources/FisherRigV1`: 앞/뒤 각 7개 스프라이트, 낚싯대, 관절 JSON.
- `Assets/Prefabs/Plaza/PlazaOtter_FisherRigged.prefab`: 기존 QuarterRigView / OtterWanderAgent / OtterVisualController를 사용하는 걷기 리그. 크기는 약 1.67 유닛.
- `Plaza_RiggedPlacement`: Otters 아래 (3.2, 0.6)에 추가. Play 시 기존 광장 배회 시스템으로 이동한다.

낚싯대 들기는 브라우저용 자세 시안이며 아직 Unity 낚시 게임 로직이나 앉아서 낚는 동작에 연결하지 않았다. Unity에는 걷기/대기 외형을 연결했다. 브라우저 조절값도 Unity에 자동 반영되지는 않는다.

재생성: `node Tools/FisherRig/build.cjs`. 확인: `node Tools/FisherRig/check.cjs` (sharp, Playwright, Chrome 필요).
검증: 네 방향 표시, 15개 이미지 로드, 디딤 발 높이 오차 0, 낚싯대 자세 및 조절/초기화. Unity 검증 이미지는 unity-0~3.png.
