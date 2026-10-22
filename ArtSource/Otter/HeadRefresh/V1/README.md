# 머리 시트 적용 · 2026-10-19

첨부 시트를 내장 image_gen으로 투명화하고 글자 및 곰돌이 잠옷의 얼굴 옆 손을 제거했습니다. PROMPT.md에 전체 프롬프트를 저장했습니다.

manifest.json의 34개 리깅에 기본 앞모습 머리를 적용했습니다. 남동·남서는 새 얼굴을 사용합니다. 뒷머리 및 수면/특수 표정 파츠는 이번 앞모습 시트 적용 범위에 포함하지 않았습니다.

Unity Resources의 front-head.png 및 브라우저 내장 이미지를 교체했습니다. 스프라이트 크기, 메타데이터 GUID/관절점, 몸통, 사지와 애니메이션은 유지했습니다. 원본 앞머리와 기존 미리보기는 backup/에 보관했습니다. 농부 Baseline-V9는 수정하지 않았습니다.

각각 네 방향 렌더(총 136회)를 확인했고 verification.json 및 overview.png에 결과를 저장했습니다. 이번 작업에서는 Unity 에디터 렌더를 별도로 실행하지 않았습니다.

다른 파츠 빌더를 재실행한 뒤에는 `node Tools/HeadRefresh/apply.cjs`로 머리 교체를 다시 적용합니다. `check.cjs`는 Chrome 미리보기 검증, `gallery.cjs`는 목록 생성 스크립트입니다.
