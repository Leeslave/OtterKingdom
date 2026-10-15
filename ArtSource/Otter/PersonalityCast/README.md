# 해달 41–50 리깅

index.html에서 각각의 미리보기를 열 수 있습니다. 각 캐릭터의 ArtSource/Otter/<이름>Rig/V1에 원화, 분리 파츠, 조절 설정, 자급식 preview.html을 저장했습니다.

41 Shy · 42 Playful · 43 Laidback · 44 Neat · 45 Fluffy · 46 Fashion · 47 Sleepy · 48 Walker · 49 Quiet · 50 Lively

각각 앞/뒤 ¾뷰, 좌우 반전으로 네 방향 걷기를 지원합니다. 움직임은 농부 V14 기반이며 인물별 고유 행동은 추가하지 않았습니다. 머리, 몸통, 양팔, 양다리, 꼬리의 위치와 크기를 조절하고 JSON으로 저장할 수 있습니다. 브라우저 조절값은 Unity에 자동 반영되지 않습니다.

Unity: Assets/Resources/<이름>RigV1, Assets/Prefabs/Plaza/PlazaOtter_<이름>Rigged.prefab, Plaza_RiggedPlacement 씬에 배치했습니다.

검증: 각 14개 이미지 로딩, 네 방향 렌더, 조절 UI, 발 접지 검사 통과. 격리된 Unity 프로젝트에서 각 방향의 7개 SpriteRenderer 로딩 및 렌더 검증 통과. 각 V1/verification.json 및 unity-0~3.png 참고.

이미지 제작: 내장 image_gen 사용. 생성/수정 프롬프트는 Tools/PersonalityRig/PROMPTS.md와 FIX_PROMPTS.md. 재빌드: Tools/<이름>Rig/build.cjs, 검증: check.cjs. Unity 메타데이터/씬 연결: Tools/PersonalityRig/integrate.py.
