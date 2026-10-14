# 야구 해달 리깅용 파츠

첨부 디자인의 밝은 털, 짙은 야구모자, 크림색 반팔, 노란 단추와 주머니가 있는 청색 멜빵바지, 접힌 바짓단을 유지해 앞·뒤 3/4뷰로 재구성했다.

- `reference.png`: 사용자 원본.
- `atlas.png`, `parts/`: 내장 image_gen 생성 원화와 추출 부위.
- `preview.html`: 네 방향 걷기/대기, 팔·다리 위치, 몸통·머리 크기 조절, 자동 저장과 JSON 내보내기/불러오기.
- `rig.json`: 관절 및 발 접지 설정. 방향별 머리/몸통/양팔/양다리/꼬리 7개.
- `Assets/Resources/BaseballRigV1`: Unity용 스프라이트 14개와 피벗 메타데이터, 리그 JSON.
- `Assets/Prefabs/Plaza/PlazaOtter_BaseballRigged.prefab`: QuarterRigView 및 기존 광장 배회 시스템을 사용하는 프리팹.
- `Plaza_RiggedPlacement`: Otters 아래 (0, -2.8)에 비교용 배치.

이번 범위는 걷기/대기용 리깅이며 투구나 타격 동작은 포함하지 않는다. 브라우저 조절값은 Unity에 자동 반영되지 않는다. 다른 캐릭터와 조절 저장 키를 분리했다.

재생성: `node Tools/BaseballRig/build.cjs`. 검증: `node Tools/BaseballRig/check.cjs`. 브라우저 네 방향, 이미지 로드, 발 디딤 높이 오차 0, 크기 조절/초기화를 확인했다. Unity 렌더 캡처는 `unity-0.png`부터 `unity-3.png`까지다.
