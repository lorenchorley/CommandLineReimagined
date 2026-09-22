// The log, in the browser's own storage.
//
// One database for this origin, two stores: `transactions` keyed by sequence number,
// and `blobs` keyed by content hash. Nothing leaves the tab; there is no server to
// send it to.
//
// Every function here returns a promise and none of them reject. IndexedDB is absent
// in a private window, blocked by site-data settings, and can fail at any point in a
// session if the user clears storage while the page is open. A terminal that throws in
// that case is a broken page; a terminal that reports it and keeps working in memory
// is a terminal. So each call answers with a result object and the caller decides.
//
// Exposed as window.clrStore for the .NET side to call through Blazor's interop.

const DATABASE = 'clr-terminal';
const VERSION = 1;
const TRANSACTIONS = 'transactions';
const BLOBS = 'blobs';

let database = null;
let unavailable = null;

/// Wraps an IndexedDB request in a promise. Its two callbacks are the only way to know
/// how it went, so every call in this file goes through here.
function request(operation) {
  return new Promise((resolve, reject) => {
    operation.onsuccess = () => resolve(operation.result);
    operation.onerror = () => reject(operation.error ?? new Error('The request failed.'));
  });
}

/// Opens the database once, and remembers a failure so that a page without storage
/// does not try to open it again on every write.
async function open() {
  if (database) return database;
  if (unavailable) throw unavailable;

  if (typeof indexedDB === 'undefined' || indexedDB === null) {
    unavailable = new Error('This browser has no IndexedDB.');
    throw unavailable;
  }

  try {
    const opening = indexedDB.open(DATABASE, VERSION);

    opening.onupgradeneeded = () => {
      const db = opening.result;
      if (!db.objectStoreNames.contains(TRANSACTIONS)) db.createObjectStore(TRANSACTIONS, { keyPath: 'seq' });
      if (!db.objectStoreNames.contains(BLOBS)) db.createObjectStore(BLOBS, { keyPath: 'hash' });
    };

    database = await request(opening);

    // The tab can lose the database mid-session, when another tab upgrades it or the
    // user clears site data. Forgetting it here means the next call reopens rather
    // than writing into a handle that is already closed.
    database.onclose = () => { database = null; };
    database.onversionchange = () => { database.close(); database = null; };

    return database;
  } catch (e) {
    unavailable = e instanceof Error ? e : new Error(String(e));
    throw unavailable;
  }
}

/// Runs work against one store inside a transaction, and resolves only once that
/// transaction has actually committed. Resolving on the request instead would report a
/// write as done that a later failure could still roll back.
async function within(store, mode, work) {
  const db = await open();

  return new Promise((resolve, reject) => {
    let result;

    try {
      const transaction = db.transaction(store, mode);
      transaction.oncomplete = () => resolve(result);
      transaction.onerror = () => reject(transaction.error ?? new Error('The transaction failed.'));
      transaction.onabort = () => reject(transaction.error ?? new Error('The transaction was aborted.'));

      Promise.resolve(work(transaction.objectStore(store)))
        .then(value => { result = value; })
        .catch(e => { try { transaction.abort(); } catch {} reject(e); });
    } catch (e) {
      reject(e);
    }
  });
}

/// The shape every function answers with: whether it worked, what it produced, and
/// why not. The .NET side reads `ok` and decides whether to fall back to memory.
const ok = value => ({ ok: true, value: value ?? null, error: null });
const failed = e => ({ ok: false, value: null, error: String(e && e.message ? e.message : e) });

async function attempt(work) {
  try {
    return ok(await work());
  } catch (e) {
    return failed(e);
  }
}

window.clrStore = {
  /// Whether the store can be opened at all. The page asks once, at startup, so it can
  /// say `not persisted` before the user has typed anything rather than after their
  /// first write silently went nowhere.
  available() {
    return attempt(async () => { await open(); return true; });
  },

  /// Appends one transaction. `json` is the stored shape, which the .NET side owns;
  /// this only needs the sequence number to key it by.
  appendTransaction(seq, json) {
    return attempt(() => within(TRANSACTIONS, 'readwrite', store => request(store.put({ seq, json }))));
  },

  /// Every transaction, oldest first. IndexedDB returns keys in ascending order, and
  /// the key is the sequence number, so this is already the replay order.
  readAllTransactions() {
    return attempt(async () => {
      const rows = await within(TRANSACTIONS, 'readonly', store => request(store.getAll()));
      return (rows ?? []).sort((a, b) => a.seq - b.seq).map(row => row.json);
    });
  },

  putBlob(hash, text) {
    return attempt(() => within(BLOBS, 'readwrite', store => request(store.put({ hash, text }))));
  },

  getBlob(hash) {
    return attempt(async () => {
      const row = await within(BLOBS, 'readonly', store => request(store.get(hash)));
      return row ? row.text : null;
    });
  },

  /// Throws everything away, for `reset`. Both stores in one transaction, so a failure
  /// half way cannot leave content without the transactions that referenced it.
  clear() {
    return attempt(async () => {
      const db = await open();

      return new Promise((resolve, reject) => {
        const transaction = db.transaction([TRANSACTIONS, BLOBS], 'readwrite');
        transaction.oncomplete = () => resolve(true);
        transaction.onerror = () => reject(transaction.error ?? new Error('The transaction failed.'));
        transaction.onabort = () => reject(transaction.error ?? new Error('The transaction was aborted.'));
        transaction.objectStore(TRANSACTIONS).clear();
        transaction.objectStore(BLOBS).clear();
      });
    });
  },
};
