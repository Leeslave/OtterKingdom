import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
const source = fs.readFileSync(fileURLToPath(new URL('../../Assets/Plugins/WebGL/WebFileSync.jslib', import.meta.url)), 'utf8');
const callbacks = [];
let calls = 0;
let active = 0;
let warnings = 0;
const context = vm.createContext({
  LibraryManager: { library: {} },
  mergeInto: Object.assign,
  console: { warn() { warnings++; } },
  FS: { syncfs(populate, cb) {
    assert.equal(populate, false, 'must save to IndexedDB, never reload old files');
    assert.equal(++active, 1, 'syncfs must not overlap');
    calls++;
    callbacks.push(err => { active--; cb(err); });
  }}
});
vm.runInContext(source, context);
context.OtterFileSync = context.LibraryManager.library.$OtterFileSync;
const flush = context.LibraryManager.library.OtterSyncFiles;
flush();
for (let i = 0; i < 100; i++) flush();
assert.equal(calls, 1, 'bursts coalesce while a sync is active');
callbacks.shift()(null);
assert.equal(calls, 2, 'save made during sync gets one final sync');
callbacks.shift()(null);
assert.equal(active, 0);
flush();
callbacks.shift()(new Error('quota'));
assert.equal(warnings, 1);
flush();
callbacks.shift()(null);
assert.equal(active, 0, 'an error must allow the next save');
assert.equal(callbacks.length, 0);
console.log('Web sync checks: burst coalescing, no overlap, final write, error recovery passed');
