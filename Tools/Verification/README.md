# 진행 개편 검증

프로젝트 루트에서 PowerShell로 실행한다. Unity를 실행하지 않는 검사는 실제 화면 플레이를 대신하지 않는다.

```powershell
& Tools/Verification/build-check.ps1
dotnet run --project Tools/Verification/ScoutingChecks.csproj --no-restore
dotnet run --project Tools/Verification/RuntimeModelChecks.csproj --no-restore
node Tools/Verification/check-web-sync.mjs
node Tools/Verification/check-content.mjs
node Tools/Verification/economy-report.mjs
```

- `build-check.ps1`: 설치된 Unity 참조와 프로젝트의 생성된 csproj를 사용해 런타임·에디터·전체 테스트 소스를 다시 컴파일한다. 원본 csproj는 바꾸지 않는다. 새 소스도 포함한다.
- `ScoutingChecks`: 게임의 `ScoutingProgress.cs`와 실제 NUnit 테스트 11개를 그대로 실행한다.
- `RuntimeModelChecks`: 앞에서 컴파일한 실제 게임 어셈블리에 대해 정착·정찰·생산 통합 NUnit 테스트 7개를 실행한다. Unity 네이티브 호출이 필요한 테스트는 실행하지 않는다. 다른 Unity 설치 위치에서는 `-p:UnityManagedDir=.../Data/Managed/UnityEngine`을 준다.
- `check-web-sync.mjs`: IndexedDB 동기화의 연속 저장·중복 실행 방지·마지막 변경 동기화·비동기 오류 뒤 회복을 검사한다. 브라우저 재실행 검증은 별도다.
- `check-content.mjs`: 실제 데이터 에셋 참조, 등록 퀘스트 ID·선행 조건·레벨별 메인, 새 진행표와 에셋 일치, 이야기 선행 조건 순환, 사진관 해금 조건을 검사한다.

진행표는 `Tools/Progression/flow.json`에서 조정한다. `node Tools/Progression/apply-flow.mjs` 또는 Unity 메뉴 `Tools/Settlement/Apply Progression Flow`로 적용한다. 기존 생성 메뉴도 새 진행표를 적용한다. 퀘스트는 `QuestSetup.cs`에서도 같은 목표를 사용한다.

전체 Unity EditMode 테스트:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Unity.exe' -batchmode -nographics -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testResults tmp/full-results.xml -logFile tmp/full-tests.log
```

Unity Editor 라이선스 활성화가 필요하다. 테스트 결과 XML이 없거나 프로세스가 라이선스 오류로 끝났으면 테스트를 통과한 것이 아니다.
