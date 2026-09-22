#!/usr/bin/env node
/**
 * Exercises `WebClient/wwwroot/store.js` outside a browser.
 *
 * The browser check proves the page; this proves the module underneath it, and runs in
 * a second rather than a minute. It matters because the module's job is mostly its
 * failure behaviour: a private window, storage that goes away mid-session, a database
 * that will not open. Those are awkward to arrange in a real browser and trivial here.
 *
 *   node tools/store-check.mjs
 */

import { readFileSync } from 'node:fs';
import { indexedDB } from 'fake-indexeddb';

const SOURCE = new URL('../WebClient/wwwroot/store.js', import.meta.url);

/// Loads a fresh copy of the module against a given environment. Fresh each time
/// because it remembers whether the database opened, which is the point of several of
/// these cases.
function load({ withIndexedDb = true } = {}) {
  const sandbox = { window: {}, indexedDB: withIndexedDb ? indexedDB : undefined };
  const run = new Function('window', 'indexedDB', readFileSync(SOURCE, 'utf8'));
  run(sandbox.window, sandbox.indexedDB);
  return sandbox.window.clrStore;
}

const failures = [];
const check = (condition, description) => {
  if (condition) {
    console.log(`  ok    ${description}`);
  } else {
    failures.push(description);
    console.error(`  FAIL  ${description}`);
  }
};

// ---- a store that works -----------------------------------------------------

{
  console.log('a store that works');
  const store = load();

  check((await store.available()).ok, 'opens');

  // What a successful write answers with is part of the contract, not an
  // implementation detail. IndexedDB's put resolves with the key, and letting that
  // through made the .NET side read a hash string where it expected a boolean and
  // conclude that storage was unavailable: every write went to memory, silently, and
  // a reload lost the log. A write is a command; it answers that it worked.
  const appended = await store.appendTransaction(2, '{"seq":2}');
  check(appended.ok && appended.value === true, 'a stored transaction answers true, not its key');

  const stored = await store.putBlob('hash-contract', 'content');
  check(stored.ok && stored.value === true, 'stored content answers true, not its hash');

  await store.appendTransaction(1, '{"seq":1}');

  const all = await store.readAllTransactions();
  check(all.ok, 'reads back what was appended');
  check(
    JSON.stringify(all.value) === JSON.stringify(['{"seq":1}', '{"seq":2}']),
    'returns transactions in sequence order, not insertion order');

  await store.putBlob('hash-a', 'content');
  const blob = await store.getBlob('hash-a');
  check(blob.ok && blob.value === 'content', 'stores and returns content by hash');

  const missing = await store.getBlob('nothing');
  check(missing.ok && missing.value === null, 'a hash that was never stored is null, not an error');

  await store.putBlob('hash-a', 'content');
  const again = await store.readAllTransactions();
  check(again.ok, 'storing the same content twice is not an error');

  await store.appendTransaction(1, '{"seq":1,"v":2}');
  const replaced = await store.readAllTransactions();
  check(
    replaced.value.length === 2 && replaced.value[0] === '{"seq":1,"v":2}',
    'a transaction written twice under one sequence number replaces it rather than duplicating');

  check((await store.clear()).ok, 'clears');
  const emptied = await store.readAllTransactions();
  check(emptied.ok && emptied.value.length === 0, 'is empty after clearing');
  const goneBlob = await store.getBlob('hash-a');
  check(goneBlob.ok && goneBlob.value === null, 'clearing removes content as well as transactions');
}

// ---- a browser with no storage ----------------------------------------------

{
  console.log('a browser with no IndexedDB, such as a private window');
  const store = load({ withIndexedDb: false });

  const available = await store.available();
  check(!available.ok, 'reports that it is unavailable');
  check(typeof available.error === 'string' && available.error.length > 0, 'says why');

  // Every function has to answer rather than throw, because the caller is mid-write
  // and a rejected promise crossing the interop boundary is a broken page.
  const append = await store.appendTransaction(1, '{}');
  check(!append.ok && append.error, 'appending answers with a failure rather than throwing');

  const read = await store.readAllTransactions();
  check(!read.ok, 'reading answers with a failure');

  const put = await store.putBlob('h', 'c');
  check(!put.ok, 'storing content answers with a failure');

  const get = await store.getBlob('h');
  check(!get.ok, 'reading content answers with a failure');

  const cleared = await store.clear();
  check(!cleared.ok, 'clearing answers with a failure');
}

// ---- storage that fails once it is open -------------------------------------

{
  console.log('storage that fails after it opened');
  const store = load();
  await store.available();

  // A store that is gone is what clearing site data with the page open looks like
  // from in here: the handle is still held, and every call against it fails.
  const result = await store.getBlob(Symbol('not a key'));

  check(typeof result.ok === 'boolean', 'answers with a result rather than throwing');
  check(!result.ok, 'reports the failure');
  check(typeof result.error === 'string' && result.error.length > 0, 'says why');

  // And it keeps working afterwards, rather than latching into a failed state for a
  // call that was only ever going to fail on its own.
  const after = await store.appendTransaction(9, '{"seq":9}');
  check(after.ok, 'a later call still reaches storage');
}

console.log('');

if (failures.length > 0) {
  console.error(`${failures.length} failure(s).`);
  process.exit(1);
}

console.log('All checks passed.');
