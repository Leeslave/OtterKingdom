# 요리 해달 3/4뷰 리깅

승인된 요리 해달 콘셉트의 꿀갈색 털, 둥근 앞머리, 흰 요리사 모자와 크림색 앞치마를 앞/뒤 컷아웃 파츠로 제작했다. 몸통의 중복 팔다리를 제거하고, 다리는 일자 형태로 구성했다.

- preview.html: 네 방향 걷기/대기, 팔·다리 위치 및 몸통·머리 크기 조절, 자동 저장, JSON 내보내기/불러오기.
- atlas.png / parts/: 내장 image_gen으로 생성한 원화와 추출 파츠.
- approved-concept.png: 사용자 승인 시안.
- Assets/Resources/ChefRigV1: Unity용 앞/뒤 각각 7개 스프라이트와 관절 JSON.
- Assets/Prefabs/Plaza/PlazaOtter_ChefRigged.prefab: 기존 QuarterRigView와 광장 배회 코드를 재사용한다.
- Plaza_RiggedPlacement: Otters 아래 (-3.2, 0.6)에 추가. 다른 캐릭터들은 그대로 유지했다.

이번 작업은 걷기/대기 외형 리깅이다. 냄비를 젓거나 요리를 만드는 게임 동작은 아직 연결하지 않았다. 미리보기 조절값은 이 캐릭터 전용으로 저장되며 Unity에 자동 반영되지는 않는다.

재생성: node Tools/ChefRig/build.cjs. 브라우저 검증: node Tools/ChefRig/check.cjs.
검증 범위: 네 방향 렌더, 14개 부위 로드, 발 디딤 높이 오차 0, 머리 크기 조절 및 초기화, Unity 격리 프로젝트의 네 방향 렌더.
