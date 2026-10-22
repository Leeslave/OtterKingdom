# 디테일 4 건물 아트 적용

승인 시안: `ArtSource/Buildings/ConceptV4`의 3장.  
원본 추출: built-in `image_gen`, transparent_background=true.  
적용 명령: sharp가 설치된 NODE_PATH를 지정한 뒤 `node Tools/Buildings/import-detail4.cjs`.

## 추출 프롬프트

Production asset extraction edit. Preserve EXACTLY the six existing building illustrations, their design, colors, outlines, details and isometric angles in this image. Do not redesign or add anything. REMOVE ALL TEXT including top title, captions, separators. Remove entire ivory background and all cast ground shadows to TRUE TRANSPARENT alpha, preserving cream-colored building walls and all opaque interior surfaces. Return the same six objects as a clean sprite sheet in their same reading order, equal-sized uniform grid cells, each complete building centered with at least 10 percent clear transparent margin in its own cell, no cropped parts and no overlaps. Keep original grid orientation: if source is 3 columns by 2 rows retain that; if source is 2 columns by 3 rows retain that. No labels or UI. Maintain all six whole building silhouettes. This is background removal and layout normalization of approved assets, not a new design.

## 적용

- `Assets/Art/Buildings/Detail4`: 18종 개별 투명 스프라이트.
- `manifest.json`: 원본 셀 경계, 새 GUID, 교체한 기존 PNG 경로.
- 기존 광장 건물 PNG 22개(메뉴 아이콘·주택 마무리 단계 포함)는 기존 크기와 GUID를 유지해 교체했다.
- 작은 집, 병원, 마을 길드, 길드 지부는 공유 그림 대신 전용 스프라이트를 사용한다.
- 작은 집의 스프라이트는 기존 집과 동일한 캔버스·PPU·피벗으로 공사 단계의 기준 크기를 유지한다. 마지막 공사 단계도 작은 집으로 연결한다.
- 건물 정의 11종의 아이콘도 해당 새 건물로 연결했다.
- 에디터 데이터 재생성 메뉴도 작은 집·길드 지부와 건물 아이콘의 새 경로를 유지한다.
- 선착장과 광산 입구는 개별 스프라이트 및 건설 메뉴 아이콘을 제공한다. 기존 낚시·광산 씬의 배경에 합쳐진 시설 이미지는 이 작업에서 다시 그리지 않았다.
- 초기 공사 골조·밤 창문 빛 효과는 기존 리소스를 유지한다.
- `before/`는 이번 교체 이전 이미지 백업이며 Git의 이전 버전으로도 복구할 수 있다.
- `contact.png`는 추출된 실제 스프라이트를 초록 바탕에 놓은 확인용 이미지다. Unity 게임 화면 캡처가 아니다.
