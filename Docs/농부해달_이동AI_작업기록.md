# 농부 해달 — 배회·수확 AI 작업기록

작성일: 2026-09-23
문서 성격: 클로드와 진행한 캐릭터 이동/수확 AI 작업 세션 기록. 다음에 이어서 작업할 때 참고용. `농부해달_애니메이션_작업기록.md`(스프라이트/애니메이터 셋업)에 이어지는 내용이다.

## 1. 요청 배경

평상시에는 밭 주변을 돌아다니다가, 수확이 가능해지면 해당 밭고랑으로 이동해서 수확을 진행하는 농부 해달 NPC를 만드는 작업. 이전 세션에서 애니메이션 클립/컨트롤러/프리팹까지는 준비됐고, 이번 세션에서 실제 배회·이동·수확 로직을 구현했다.

## 2. 만든 것 — `FarmerOtterController.cs`

`Assets/Scripts/View/FarmerOtterController.cs` (신규)

`FarmerOtter` 프리팹(애니메이션 세션에서 만든 것)에 붙이는 컴포넌트. `Animator`/`SpriteRenderer`가 이미 프리팹에 있으므로 `RequireComponent`로만 명시.

- **배회**: Inspector에 손으로 배치한 `wanderWaypoints`(Transform 배열)를 랜덤 순회. 도착 후 `wanderWaitRangeSec` 범위에서 랜덤 대기(또는 아래 3번 랜덤 액션) 후 다음 지점으로.
- **수확 대상 탐색**: 매 프레임 `plotIndex`(기본 0) 밭의 슬롯 상태를 확인해 `AwaitingHarvest` 상태인 슬롯 중 **가장 가까운 것**(Inspector에 배치한 `slotAnchors[i]`와의 거리 기준)을 찾으면 배회를 끊고 그쪽으로 이동.
- **수확 동기화**: 슬롯 앵커에 도착하면 `Harvest` 트리거 재생 → Animator가 `Harvest` 상태를 완전히 벗어나 `Idle`로 돌아올 때까지 대기(폴링) → 그제서야 `GameManager.HarvestSlot(plotIndex, slotIndex)` 호출(실제 수확 + 인벤토리 반영 + 저장). 즉 **애니메이션이 끝나야 실제 수확 로직이 실행**된다.
- **디버그 패널과 충돌 방지**: 걸어가는 도중 다른 경로(디버그 패널의 수동 수확 버튼 등)로 슬롯이 이미 비워졌으면 조용히 배회로 복귀.

`GameManager.cs` 변경: `HarvestSlot(plotIndex, slotIndex)` 공개 메서드를 추가해 디버그 패널 버튼과 `FarmerOtterController`가 수확 로직을 공유하도록 정리. 디버그 패널 버튼 라벨은 "수확 (해달 대역)" → "수확 (수동)"으로 변경(이제는 오버라이드 용도).

## 3. 1차 플레이테스트 피드백 반영

Play 모드에서 첫 확인 후 사용자가 지적한 문제 2가지.

### 3.1 다리 짤림 + 대각선 이동

Farm.unity의 `Plot_*`는 `sortingOrder 0`, 그 자식인 `Slot_0/1/2`(작물 스프라이트)는 `sortingOrder 1`인데, 해달 프리팹의 `SpriteRenderer`는 정렬 순서가 지정되지 않아 기본값 0이었다. 즉 해달이 슬롯에 다가가면 작물 스프라이트가 해달보다 앞에 그려져 다리 쪽이 가려지는 현상이었다.

- `FarmerOtterController`에 `spriteSortingOrder = 2`(밭/슬롯보다 위)를 추가하고 `Awake()`에서 명시적으로 설정.
- 이동을 대각선(`Vector3.MoveTowards` 직선)에서 **상하 이동 → 좌우 이동** 2단계로 분리(`MoveTo`가 `MoveAxis`를 두 번 호출). 대각선 경로가 밭이랑 둔덕 아트를 비스듬히 가로지르며 생기던 시각적 문제도 완화.
- ⚠️ 참고: 이 sortingOrder 고정값 방식은 현재 씬에 걸을 수 있는 깊이 레이어가 Plot_1 하나뿐이라 성립한다. 밭 2·3번이 해금되어 서로 다른 Y 깊이를 오가게 되면 Y 기반 동적 정렬로 바꿔야 할 수 있음(아직 구현 안 함).

### 3.2 Idle 대기를 랜덤 액션으로

기존에는 웨이포인트 도착 후 그냥 `Idle` 애니메이션 상태로 대기했는데, 사용자가 별도 스프라이트로 만든 "랜덤 동작"으로 대체 요청.

- `FarmerOtterController`에 `randomActionTriggers`(string 배열, Animator 트리거 이름들)를 추가. 비어있으면 기존처럼 그냥 대기(하위호환), 채워져 있으면 그 중 하나를 랜덤으로 트리거하고 `Harvest`와 동일한 패턴(애니메이터가 `Idle`로 돌아올 때까지 대기)으로 완료를 감지.
- 이 시점에는 실제 랜덤 액션 스프라이트가 아직 없어서 배열은 비워둔 채로 커밋(기능 골격만 준비).

## 4. 스프라이트시트 블리딩(고스트) 버그 수정

Harvest 애니메이션 재생 중 캐릭터 옆에 다음 프레임이 옅게 겹쳐 보이는 문제 발견. 원인: `FarmerOtterSpriteSetup.cs`가 시트를 프레임 사이 여백 없이 딱 붙여 슬라이싱했고, `filterMode = Bilinear` 상태라 셀 경계 바로 앞에서 GPU가 옆 프레임 픽셀을 살짝 섞어 샘플링하는 전형적인 "스프라이트시트 블리딩" 현상이었다. Harvest 포즈가 프레임 경계에 가장 가깝게 닿아서 제일 눈에 띄었다.

`FarmerOtterSpriteSetup.cs` 수정:
- 슬라이싱 rect에 안쪽 여백 추가(`ColumnInsetPx = 2`, `RowInsetPx = 1`).
- `textureCompression`을 `Uncompressed`로 변경(블록 압축 블리딩 가능성도 제거).
- `Run()`을 재실행해도 안전하도록 변경 — 기존 `Assets/Animations/FarmerOtter/` 폴더와 `FarmerOtter.prefab`을 지우고 재생성. **원래는 같은 경로에 `CreateAsset`을 다시 시도해 재실행 시 실패했음.**

⚠️ **주의**: 이 도구를 재실행하면 AnimatorController/클립/프리팹이 전부 새로 만들어진다. 그 사이 애니메이터를 직접 손으로 수정한 게 있다면(예: 상태를 손으로 추가) 재실행 후 다시 해줘야 한다.

## 5. 랜덤 아이들 액션 스프라이트 3종 반영

사용자가 새 스프라이트시트 3장 제공 (각 1774×887px, 8열×4행):

| 파일 | 원본 내용 |
|---|---|
| `Assets/Sprites/Characters/FarmerOtter/FarmerOtter_IdleAction_Net.png` | 채집망 휘두르기 |
| `Assets/Sprites/Characters/FarmerOtter/FarmerOtter_IdleAction_Stretch.png` | 기지개/하품 |
| `Assets/Sprites/Characters/FarmerOtter/FarmerOtter_IdleAction_Eat.png` | 먹는(핥는) 동작 |

각 시트는 4행 구조로, 기존 캐릭터 시트와 같은 방식(0행=측면, 1행=측면 변형, 2행=정면 클로즈업, 3행=뒷모습)으로 추정됨.

**클로드가 임의로 정한 가정 (사용자 확인 안 됨)**: 이동 직전까지 측면(Walk) 포즈였던 것과 자연스럽게 이어지도록 **0행(측면)만** 사용하도록 결정. 1~3행은 슬라이싱은 해두되 컨트롤러에 연결하지 않음 — 기존 `Walk_Alt`/`Idle_Back`을 안 쓰는 것과 같은 방식. 만약 정면 클로즈업(2행) 등 다른 의도였다면 `FarmerOtterSpriteSetup.cs`의 `IdleActionUsedRow` 값만 바꾸고 재실행하면 된다.

`FarmerOtterSpriteSetup.cs` 확장:
- `SliceIdleActionSheetRow()`: 시트 하나를 슬라이싱(블리딩 방지 인셋 동일 적용)하고 지정한 행의 8프레임만 반환.
- `BuildController()`가 각 액션을 `Any State → 트리거("Net"/"Stretch"/"Eat") → 해당 상태 → Exit Time 1 → Idle`로 연결 — `Harvest`와 완전히 같은 패턴이라 `FarmerOtterController`의 범용 "Idle로 돌아올 때까지 대기" 로직이 그대로 작동함.

`FarmerOtterController.randomActionTriggers` 기본값을 `{ "Net", "Stretch", "Eat" }`로 코드에서 지정 — **새로 붙이는 컴포넌트는 자동 적용되지만, 씬에 이미 배치된 `FarmerOtter`(빈 배열이 직렬화되어 있음)는 Inspector에서 수동으로 채워야 한다** (Unity가 기존 직렬화 인스턴스에 새 기본값을 소급 적용하지 않음).

## 6. 아직 안 한 것 / 다음 후보

- 사용자가 아직 유니티 에디터에서 `OtterKingdom > Tools > Setup Farmer Otter Animations`를 (수정된 버전으로) 재실행하지 않음 — 이 문서 작성 시점 기준으로 4번(블리딩 수정)·5번(랜덤 액션) 변경사항이 실제 프로젝트에 반영됐는지 미확인.
- 씬의 `FarmerOtter` 오브젝트에 `wanderWaypoints`/`slotAnchors`/`randomActionTriggers`가 Inspector에서 실제로 채워졌는지, Play 모드에서 랜덤 액션 3종이 정상 재생되는지 미확인.
- 0행(측면) 선택이 맞는지 사용자 확인 필요 (5번 참고).
- 밭 2·3번이 해금되어 여러 깊이 레이어를 오가게 될 경우 sortingOrder를 Y 기반 동적 정렬로 바꿔야 함(3.1 참고, 아직 불필요해서 미착수).
- Walk_Alt, Idle_Back, 각 랜덤 액션 시트의 1~3행 용도 여전히 미정 (예비 소재로 존재).

## 7. 관련 파일 경로

- `Assets/Scripts/View/FarmerOtterController.cs` (신규 — 배회/수확 AI)
- `Assets/Scripts/GameManager.cs` (수정 — `HarvestSlot()` 공개 메서드 추가)
- `Assets/Editor/FarmerOtterSpriteSetup.cs` (수정 — 블리딩 인셋, Uncompressed, idempotent 재실행, 랜덤 액션 시트 슬라이싱/컨트롤러 배선)
- `Assets/Sprites/Characters/FarmerOtter/FarmerOtter_IdleAction_Net.png`, `_Stretch.png`, `_Eat.png` (신규 아트 에셋)
- 참고: `농부해달_애니메이션_작업기록.md`(선행 세션), `M1_밭구현_작업기록.md`(밭/슬롯 구조), [해달왕국 채팅 결정사항](project_otterkingdom_decisions.md)(클로드 메모리)
