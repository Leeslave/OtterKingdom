// 웹 빌드: persistentDataPath(/idbfs)에 쓴 파일을 브라우저 저장소(IndexedDB)로 옮긴다.
// 기본 웹 템플릿은 autoSyncPersistentDataPath가 꺼져 있어, 이걸 부르지 않으면 새로 고칠 때 세이브가 사라진다 (WebFileSync.cs)
mergeInto(LibraryManager.library, {
  $OtterFileSync: {
    running: false,
    pending: false,
    flush: function () {
      OtterFileSync.pending = true;
      if (OtterFileSync.running) return;
      OtterFileSync.running = true;
      OtterFileSync.pending = false;
      FS.syncfs(false, function (err) {
        OtterFileSync.running = false;
        if (err)
          console.warn("[WebFileSync] 브라우저 저장소로 옮기지 못했어요: " + err);
        // 동기화 도중 다시 저장했다면 마지막 변경까지 한 번 더 옮긴다.
        if (OtterFileSync.pending) OtterFileSync.flush();
      });
    }
  },
  OtterSyncFiles__deps: ["$OtterFileSync"],
  OtterSyncFiles: function () {
    OtterFileSync.flush();
  },
});
