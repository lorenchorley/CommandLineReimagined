#!/usr/bin/env node
/**
 * Runs the published browser client in a real browser and checks what it shows.
 *
 * The test suites prove the core. This proves the page: that the runtime boots, that
 * the bridge is wired to it, and that what a person sees at phone size is what the
 * acceptance list says. Those are exactly the failures a unit test cannot have —
 * a missing interop name, a renderer that drops a field, a console error nobody reads.
 *
 *   node tools/browser-check.mjs publish/wwwroot          a local folder, served here
 *   node tools/browser-check.mjs https://host/path/       a deployed site, as it is
 *
 * The second form is what the pages workflow runs after deploying: the same session,
 * against the real thing, over the real network. A deployment that builds and uploads
 * but does not run is not a deployment.
 *
 * Set PLAYWRIGHT_CHROMIUM to an executable to use a specific browser; otherwise
 * Playwright's own is used.
 */

import { spawn } from 'node:child_process';
import { existsSync, renameSync, readdirSync, readFileSync, writeFileSync, statSync,
         mkdtempSync, symlinkSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { chromium } from 'playwright';

// A phone. The whole page is designed for this, so checking it anywhere else would be
// checking something nobody uses.
const VIEWPORT = { width: 390, height: 844 };
const DEVICE_SCALE_FACTOR = 3;

/**
 * The acceptance list from docs/plan/phase-1-functional-core.md, run in order against
 * one session. Each line is submitted and the entry it produces is compared.
 *
 *   expect   the rendered entry must contain this text
 *   absent   the rendered entry must not contain this text
 *   fault    the entry must show a failure with this kind
 *   caught   the entry must show a fault held as a value, with this kind (Phase 5)
 *   hides    an undo: the entry for this line must go, and the undo leave none of its own
 *   shows    a redo: the entry for this line must be back, and the redo leave none
 *
 * Kept in the same order as the plan, and as one session rather than one each, because
 * undo and history only mean anything against what came before them.
 */
const SCRIPT = [
  { line: 'ls', expect: ['documents', 'projects', 'readme.txt'], absent: ['up'] },

  // A failed line leaves nothing behind, whatever its earlier stages managed.
  { line: 'mkdir a | cd nowhere', fault: 'notfound', expect: ['Directory does not exist : nowhere'] },
  { line: 'ls', absent: ['a '] },

  { line: 'mkdir alpha', expect: ['alpha'] },
  { line: 'ls', expect: ['alpha', 'documents', 'readme.txt'] },
  // Undo takes the line back rather than adding one, and redo puts it back.
  { line: 'undo', hides: 'mkdir alpha' },
  { line: 'ls', absent: ['alpha'] },
  { line: 'redo', shows: 'mkdir alpha' },
  { line: 'history', expect: ['seed', 'mkdir alpha'], absent: ['(undone)'] },

  { line: 'write note.txt first', expect: ['note.txt'] },
  { line: 'write note.txt second', expect: ['note.txt'] },
  { line: 'undo', hides: 'write note.txt second' },
  { line: 'cat note.txt', expect: ['first'] },

  { line: 'attr note.txt tag=work', expect: ['note.txt'] },
  // A table of name and value from Phase 3, rather than a line per attribute.
  { line: 'attr note.txt', expect: ['name', 'note.txt', 'kind', 'text', 'folder', 'tag', 'work', 'created', 'modified'] },

  { line: 'save <note name=todo due=2026-10-01/>', expect: ['todo'] },
  { line: 'cat todo', expect: [] },

  // Decision 0007: a path in a tag needs no quotes, and reads back unchanged.
  { line: '<file path=documents/notes.txt/>', expect: ['<file path=documents/notes.txt/>'] },
  { line: 'echo -5', expect: ['-5'] },

  { line: 'set v 1', expect: ['1'] },
  { line: 'set v 2', expect: ['2'] },
  { line: 'undo', hides: 'set v 2' },
  { line: 'undo', hides: 'set v 1' },
  { line: 'echo $v', fault: 'notfound', expect: ['Unknown variable: $v'] },

  { line: 'progress 3 1', expect: ['100'] },

  // A command from Phase 3, not a word the page intercepts.
  { line: 'help', expect: ['undo', 'redo', 'history', 'attr', 'save', 'where', 'select'] },
];

/**
 * Phase 3: listings are tables, and a table can be questioned.
 *
 * Run from a fresh store, so the rows are the seeded ones and the counts are the ones
 * in docs/plan/examples.md rather than whatever the acceptance script left behind.
 */
const PHASE_3 = [
  { line: 'ls | where $row.kind eq folder | count', expect: ['3'] },
  { line: 'ls | sort name desc | first', expect: ['<row name=readme.txt', 'kind=text'] },
  { line: 'ls | select name kind | take 2', expect: ['documents', 'examples'], absent: ['readme.txt'] },
  { line: 'ls | where $row.name like read | count', expect: ['1'] },

  // Decision 0019: a reserved word is never an argument, and quoting is the way round it.
  { line: 'echo eq', fault: 'syntax' },
  { line: 'echo "eq"', expect: ['eq'] },

  { line: 'help | where $row.name eq set | select name description',
    expect: ['Bind a value, or whatever was piped in, to a variable'] },

  // The example program, end to end, in the browser. Each line is echoed as it runs.
  { line: 'run examples/tables.clr', expect: ['> ls', '> vars', 'greeting', 'hello', '42'] },
];

/**
 * Phase 4: a query is a place.
 *
 * Run from a fresh store. The example program first, because it is the phase's proof,
 * and then the four things about a view that only the page can be wrong about: what
 * the prompt says, what a listing shows across folders, and the way back out.
 */
const PHASE_4 = [
  { line: 'run examples/journal.clr', expect: ['> cd $row.mood eq great', '> up', 'Undone: rm tuesday'] },

  // The program leaves you in /journal. The rest of these are about views as such,
  // so they start from the root.
  { line: 'cd /', expect: ['/'] },

  { line: 'cd $row.kind eq folder', expect: ['$row.kind eq folder'] },
  // A view lists across folders, so the seeded tree and the journal appear together.
  { line: 'ls', expect: ['documents', 'examples', 'projects', 'journal'], absent: ['readme.txt'] },
  // Both work notes are back: journal.clr's last act was to undo the `rm`.
  { line: 'find $row.kind eq note and $row.tag eq work | count', expect: ['2'] },
  { line: 'up', expect: ['/'] },
  { line: 'ls', expect: ['readme.txt'] },

  { line: 'save-view weekend $row.mood eq great', expect: ['weekend'] },
  { line: 'cd weekend', expect: ['$row.mood eq great'] },
  { line: 'ls', expect: ['saturday'], absent: ['monday'] },
  { line: 'up', expect: ['/'] },
];

/**
 * Phase 5: failure is a value.
 *
 * Run from a fresh store. The example program first, then the plan's four lines, each
 * as its own entry so the page's two ways of drawing a fault are both seen: `else`
 * never shows one at all, and `try` shows one as a result rather than as a failed line.
 */
const PHASE_5 = [
  { line: 'run examples/resilient.clr',
    expect: ['> try cat nowhere.txt | set problem', '> mkdir today | cd nowhere else echo', 'today.txt'] },
  // The program's point: the folder on the failed side of its last `else` never existed.
  { line: 'find $row.name eq today | count', expect: ['0'] },

  { line: 'cat missing.txt else echo "none"', expect: ['none'] },
  { line: 'try cat missing.txt | set r', caught: 'NotFound', expect: ['File does not exist : /missing.txt'] },
  { line: 'echo $r.kind', expect: ['NotFound'] },
  { line: 'first (ls | where $row.kind eq note) ?? "no notes"', expect: ['no notes'] },
  { line: 'is-fault $r', expect: ['true'] },
];

/**
 * Phase 6: XML and CSV are real files.
 *
 * Run from a fresh store. The example program first: its last line is the bolts row,
 * read back out of the XML file it wrote. Then the CSV it exported, as the file it is,
 * and a document that is not XML, which has to fail as a line rather than as a page.
 */
const PHASE_6 = [
  { line: 'run examples/inventory.clr',
    expect: ['> cat reorder.csv', 'sku,qty', 'C3,0', '<row sku=A1 name=bolts qty=120 min=50/>'] },
  { line: 'from-csv reorder.csv', expect: ['sku', 'qty', 'C3', 'B2'], absent: ['A1'] },
  { line: 'from-xml items.xml | where $row.qty lt $row.min | count', expect: ['2'] },
  { line: 'echo "not xml" | write broken.xml', expect: ['broken.xml'] },
  { line: 'from-xml broken.xml', fault: 'invalid', expect: ['Not well-formed XML : /stock/broken.xml line 1'] },
  { line: 'cd /', expect: ['/'] },
];

/**
 * Phase 2: what a reload is for. Run, reload the page, and check what came back.
 *
 * This cannot be a unit test. Replaying a log is covered in `Core.Tests`; what is only
 * checkable here is that the log reached IndexedDB at all, that the page waits for the
 * replay before enabling the input, and that a second visit finds the first one's work.
 */
/**
 * Phase 8: completion reads the line.
 *
 * Run from a fresh store at `/`, so `ls` is the four seeded rows. The first three lines
 * are the session docs/plan/phase-8-intellisense.md's Acceptance section is written
 * against; the rest are the lines of it that are run rather than typed. What is typed,
 * the chips and the detail line, is checked after this, by hand, in main().
 */
const PHASE_8 = [
  { line: 'set v 5', expect: ['5'] },
  { line: 'ls | set files', expect: ['documents', 'readme.txt'] },
  { line: 'try cat missing.txt | set problem', caught: 'NotFound' },
  { line: '$v', expect: ['5'] },
  { line: '$files | count', expect: ['4'] },
  // A name that is not a command says which one it is near.
  { line: 'lss', fault: 'unknowncommand', expect: ['Unknown command : lss. Did you mean ls?'] },
];

const BEFORE_RELOAD = [
  { line: 'mkdir persisted', expect: ['persisted'] },
  { line: 'write kept.txt remembered', expect: ['kept.txt'] },
  { line: 'set survivor yes', expect: ['yes'] },
];

const AFTER_RELOAD = [
  { line: 'ls', expect: ['persisted', 'kept.txt', 'documents', 'readme.txt'] },
  { line: 'cat kept.txt', expect: ['remembered'] },
  { line: 'echo $survivor', expect: ['yes'] },
  // Undo reaches back across the reload, because the compensation chain is in the log.
  // The line it reverses is not on screen any more, so the undo says what it did.
  { line: 'undo', expect: ['Undone: set survivor yes'] },
];

const AFTER_RESET = [
  { line: 'ls', expect: ['documents', 'projects', 'readme.txt'], absent: ['persisted', 'kept.txt'] },
  { line: 'undo', expect: ['Nothing to undo.'] },
];

/**
 * Makes a published folder servable.
 *
 * Blazor publishes its runtime to `_framework`, and the static host this is deployed
 * to refuses paths beginning with an underscore, so the page asks for `framework`
 * instead and the deployment renames the folder. A folder straight out of `publish`
 * therefore does not run anywhere: the page 404s on its own runtime. Doing the rename
 * here is what makes publish-then-check one step, and it is the same rename
 * documented in docs/building.md.
 */
function prepare(root) {
  if (!existsSync(join(root, '_framework')) || existsSync(join(root, 'framework'))) {
    return;
  }

  renameSync(join(root, '_framework'), join(root, 'framework'));

  const rewritable = new Set(['.js', '.json', '.html']);
  const rewrite = directory => {
    for (const entry of readdirSync(directory)) {
      const path = join(directory, entry);

      if (statSync(path).isDirectory()) {
        rewrite(path);
      } else if (rewritable.has(entry.slice(entry.lastIndexOf('.')))) {
        const before = readFileSync(path, 'utf8');
        const after = before.replaceAll('_framework', 'framework');
        if (after !== before) writeFileSync(path, after);
      }
    }
  };

  rewrite(root);
  console.log('Renamed _framework to framework, as the deployment does.');
}

/**
 * The path the build expects to be served under, from its own base href.
 *
 * A build prepared for a project site on GitHub Pages carries
 * `<base href="/CommandLineReimagined/">`, so serving it at the root would ask for
 * every asset one directory too high and answer 404. Mounting it where it thinks it
 * is means the local check runs against exactly the bytes that get deployed.
 */
function basePathOf(root) {
  const page = readFileSync(join(root, 'index.html'), 'utf8');
  const href = page.match(/<base href="([^"]*)"/)?.[1] ?? '/';
  return href.replace(/^\/+|\/+$/g, '');
}

/** Starts a static server on a free-ish port and waits for it to answer. */
async function serve(root, port, basePath) {
  let directory = root;

  if (basePath) {
    // A symlink rather than a copy: the same 8 MB, and nothing to clean up that
    // matters if this exits badly.
    directory = mkdtempSync(join(tmpdir(), 'clr-check-'));
    symlinkSync(resolve(root), join(directory, basePath));
  }

  const server = spawn('python3', ['-m', 'http.server', String(port), '--bind', '127.0.0.1'], {
    cwd: directory,
    stdio: 'ignore',
  });

  const address = `http://127.0.0.1:${port}/${basePath ? basePath + '/' : ''}index.html`;

  for (let attempt = 0; attempt < 100; attempt++) {
    try {
      const response = await fetch(address);
      if (response.ok) return server;
    } catch {
      // Not up yet.
    }

    await new Promise(resolve => setTimeout(resolve, 100));
  }

  server.kill();
  throw new Error(`The static server did not start in ${root}.`);
}

/** Waits for the page to finish booting and returns its status line. */
async function boot(page) {
  // The runtime takes a few seconds to download and start, and the page shows
  // "restoring…" while it replays the log. Ready means the input is enabled.
  await page.waitForSelector('#status:has-text("wasm")', { timeout: 120000 });
  await page.waitForFunction(() => !document.getElementById('cmd').disabled, null, { timeout: 30000 });

  return (await page.locator('#status').innerText()).trim();
}

/** Runs a table of lines and reports any mismatch through `note`. */
async function runScript(page, script, note) {
  for (const step of script) {
    const { text, fault, caught, left } = await submit(page, step.line);

    for (const [reversed, visible] of [[step.hides, false], [step.shows, true]]) {
      if (reversed === undefined) continue;

      if (left) {
        note(`'${step.line}' left an entry of its own: ${JSON.stringify(text)}`);
      }

      const shown = await page.evaluate(
        line => [...document.querySelectorAll('.entry:not(.undone) .echo')]
          .some(echo => echo.textContent.replace(/\u00a0/g, ' ').trim() === `$ ${line}`),
        reversed);

      if (shown !== visible) {
        note(`after '${step.line}' the entry for '${reversed}' is ${shown ? 'still on screen' : 'not on screen'}`);
      }
    }

    for (const expected of step.expect ?? []) {
      if (!text.includes(expected)) {
        note(`'${step.line}' did not show '${expected}'. It showed: ${JSON.stringify(text)}`);
      }
    }

    for (const unexpected of step.absent ?? []) {
      if (text.includes(unexpected)) {
        note(`'${step.line}' showed '${unexpected}', which it should not. It showed: ${JSON.stringify(text)}`);
      }
    }

    if (step.caught && caught !== step.caught) {
      note(`'${step.line}' showed a caught fault of kind ${JSON.stringify(caught)}, expected '${step.caught}'`);
    }

    if (!step.caught && caught !== null) {
      note(`'${step.line}' showed a caught fault (${caught}) it should not have: ${JSON.stringify(text)}`);
    }

    if (step.fault && fault !== step.fault) {
      note(`'${step.line}' showed fault kind ${JSON.stringify(fault)}, expected '${step.fault}'`);
    }

    if (!step.fault && fault !== null) {
      note(`'${step.line}' failed unexpectedly (${fault}): ${JSON.stringify(text)}`);
    }
  }
}

/**
 * Submits one line and returns the text of the entry it produced.
 *
 * An undo or a redo whose line is on screen produces no entry: it hides or restores
 * that line's instead. So a line is known to be done by the run the page says it
 * finished last, not by the number of entries, and `left` says whether it left one.
 */
async function submit(page, line) {
  const finished = () => page.evaluate(() => Number(document.body.dataset.finished || 0));
  const before = await finished();

  await page.fill('#cmd', line);
  await page.press('#cmd', 'Enter');

  // It is finished when the page has recorded a later run than before, and nothing is
  // still running. `progress` takes a moment, so this waits rather than polls.
  await page.waitForFunction(
    count => Number(document.body.dataset.finished || 0) > count &&
             document.querySelectorAll('.entry.running').length === 0,
    before,
    { timeout: 30000 });

  const id = await finished();
  const entry = page.locator(`.entry[data-id="${id}"]`);

  if (await entry.count() === 0) {
    return { text: '', fault: null, caught: null, left: false };
  }

  // A failed line is the red `.err`; a fault that `try` caught is a `.caught` result
  // and not a failure at all, though both carry the same small kind tag. Telling them
  // apart is the whole of what Phase 5 changed on the page.
  return {
    left: true,
    text: (await entry.innerText()).replace(/ /g, ' '),
    fault: await entry.locator('.err .kind').count() > 0
      ? (await entry.locator('.err .kind').first().innerText()).trim()
      : null,
    caught: await entry.locator('.caught').count() > 0
      ? await entry.locator('.caught').first().getAttribute('data-fault-kind')
      : null,
  };
}

/** The chips in the completion row, in order. */
const chips = page => page.locator('#complete .key:not(.more)').allInnerTexts();

/** The chip Tab puts in place next and whose detail is shown, or null. */
const selectedChip = page => page.evaluate(() => document.querySelector('#complete .key.sel')?.textContent ?? null);

/**
 * Types a line with the caret `back` characters before its end, waits until the chip
 * row offers `wanted`, and returns the row; null if it never did.
 *
 * Completion is asked asynchronously from Phase 8, and a slow answer to an earlier
 * keystroke is dropped, so the row is known to answer this line only when it changes
 * to what this line offers. The line is emptied first, which empties the row, so a row
 * left over from the last case cannot be read as this one's. The caret is moved with
 * the arrow keys, as a person moves it, which is what makes the page ask again there.
 */
async function offered(page, line, wanted, back = 0) {
  await page.fill('#cmd', '');
  await page.waitForFunction(() => document.querySelectorAll('#complete .key').length === 0, null, { timeout: 10000 });
  await page.fill('#cmd', line);
  for (let i = 0; i < back; i++) await page.press('#cmd', 'ArrowLeft');

  const arrived = await page.waitForFunction(
    wanted => [...document.querySelectorAll('#complete .key:not(.more)')].some(chip => chip.textContent === wanted),
    wanted, { timeout: 10000 }).then(() => true, () => false);

  return arrived ? chips(page) : null;
}

/** The detail line's text once it contains `needle`, or as it stands after ten seconds. */
async function detailSays(page, needle) {
  await page.waitForFunction(
    needle => document.getElementById('detail').innerText.replace(/ /g, ' ').includes(needle),
    needle, { timeout: 10000 }).catch(() => {});
  return (await page.locator('#detail').innerText()).replace(/ /g, ' ').trim();
}

/**
 * The folder or URL to check, written either way round.
 *
 *   browser-check.mjs publish/wwwroot          browser-check.mjs --dir publish/wwwroot
 *   browser-check.mjs https://host/path/       browser-check.mjs --url https://host/path/
 *
 * Both spellings are accepted because both get written: the positional form by hand,
 * the flag form in a workflow. Reading only the positional one turned a passing live
 * site into a red build, with the script's own usage message as the only clue.
 */
function targetFrom(argv) {
  const flag = name => {
    const at = argv.indexOf(name);
    return at >= 0 && at + 1 < argv.length ? argv[at + 1] : null;
  };

  return flag('--url') ?? flag('--dir') ?? argv.find(argument => !argument.startsWith('--')) ?? null;
}

async function main() {
  const target = targetFrom(process.argv.slice(2));
  const deployed = /^https?:\/\//.test(target ?? '');

  if (!target || (!deployed && !existsSync(`${target}/index.html`))) {
    console.error('usage: node tools/browser-check.mjs (<published wwwroot> | <url>)');
    console.error('       the same thing written --dir <published wwwroot> or --url <url>');
    console.error('  publish it first: dotnet publish WebClient/WebClient.csproj -c Release -o publish');
    process.exit(2);
  }

  // A deployed site is already prepared and already served; there is nothing to do
  // to it but drive it.
  let server = null;
  let address = target;

  if (!deployed) {
    prepare(target);
    const basePath = basePathOf(target);
    const port = 8000 + Math.floor(Math.random() * 1000);
    server = await serve(target, port, basePath);
    address = `http://127.0.0.1:${port}/${basePath ? basePath + '/' : ''}index.html`;
  }

  const browser = await chromium.launch({
    executablePath: process.env.PLAYWRIGHT_CHROMIUM || undefined,
  });

  const context = await browser.newContext({
    viewport: VIEWPORT,
    deviceScaleFactor: DEVICE_SCALE_FACTOR,
    isMobile: true,
    hasTouch: true,
  });

  const page = await context.newPage();

  // A console error is a failure even when the page looks right: it means something
  // threw where nobody was watching, which is how an interop name goes stale.
  const consoleErrors = [];
  page.on('console', message => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('pageerror', error => consoleErrors.push(String(error)));

  const failures = [];
  const note = message => { failures.push(message); console.error(`  FAIL  ${message}`); };

  try {
    console.log(`Checking ${address}`);
    const response = await page.goto(address, { waitUntil: 'domcontentloaded' });

    if (response && !response.ok()) {
      note(`the page answered HTTP ${response.status()}`);
    }

    const status = await boot(page);
    console.log(`Booted at ${VIEWPORT.width}x${VIEWPORT.height}. Status: ${status}`);

    // A headless Chromium has IndexedDB, so a run that reports otherwise means the
    // store failed to open, which the reload cases below would then fail on for a
    // reason that has nothing to do with them.
    if (status.includes('not persisted')) {
      note('the page reports `not persisted`; the log is not reaching IndexedDB');
    }

    // A listing is a real table from Phase 3: a header row from the columns and a cell
    // per value, not a run of chips and not preformatted text.
    await submit(page, 'ls');
    const listing = page.locator('.entry').last();
    const headers = await listing.locator('.grid thead th').allInnerTexts();

    if (headers.join(' ') !== 'name kind folder size modified') {
      note(`a listing's header row is ${JSON.stringify(headers)}`);
    }

    // The cells have to be tappable. 44 pixels is the smallest target a finger hits
    // reliably, and the whole page is built for a phone.
    const cell = listing.locator('.grid tbody td').first();
    const box = await cell.boundingBox();

    if (!box || box.height < 44) {
      note(`a table cell is ${box ? box.height : 'not'} pixels tall; 44 is the minimum tap target`);
    }

    // Tapping a header re-sorts what is on screen without running anything.
    const before = await listing.locator('.grid tbody td').first().innerText();
    await listing.locator('.grid thead th').first().click();
    await listing.locator('.grid thead th').first().click();
    const after = await listing.locator('.grid tbody td').first().innerText();

    if (before === after) {
      note(`sorting a listing by its first column twice did not change the first row (${before})`);
    }

    // Below the root the page offers `up`, which is where the parent row went.
    await submit(page, 'cd documents');

    try {
      // The prompt is refreshed after the entry finishes, so this waits rather than
      // asking once.
      await page.waitForSelector('#prompt .up', { timeout: 10000 });
      await page.locator('#prompt .up').click();
      await page.waitForFunction(
        () => document.getElementById('where').innerText.trim() === '/', null, { timeout: 10000 });
    } catch {
      note('below the root the location line does not offer a working `up`');
      await submit(page, 'up');
    }

    await runScript(page, SCRIPT, note);
    console.log(`Ran ${SCRIPT.length} lines.`);

    // Undo and redo sit beside `up`, at the root too. A tap is the line it stands for,
    // so the entry it takes back goes and comes back, and what was being typed stays.
    await submit(page, 'mkdir tapped');
    await page.fill('#cmd', 'half typed');

    const tap = async id => {
      const before = await page.evaluate(() => Number(document.body.dataset.finished || 0));
      await page.locator(`#prompt #${id}`).click();
      await page.waitForFunction(
        count => Number(document.body.dataset.finished || 0) > count &&
                 document.querySelectorAll('.entry.running').length === 0,
        before, { timeout: 10000 });
    };

    const standing = () => page.evaluate(() =>
      [...document.querySelectorAll('.entry:not(.undone) .echo')]
        .some(echo => echo.innerText.includes('mkdir tapped')));

    try {
      await tap('undo');
      if (await standing()) note('tapping the undo button left `mkdir tapped` on screen');
      await tap('redo');
      if (!await standing()) note('tapping the redo button did not bring `mkdir tapped` back');

      const kept = await page.inputValue('#cmd');
      if (kept !== 'half typed') note(`tapping undo and redo left the input as ${JSON.stringify(kept)}`);
    } catch {
      note('the location line does not offer working undo and redo buttons');
    }

    await page.fill('#cmd', '');

    // ---- Phase 3: tables, predicates and the example program -----------------

    await submit(page, 'reset');
    await runScript(page, PHASE_3, note);
    console.log(`Ran ${PHASE_3.length} more for Phase 3.`);

    // ---- Phase 4: views, the location line and a live listing ---------------

    await submit(page, 'reset');
    await runScript(page, PHASE_4, note);
    console.log(`Ran ${PHASE_4.length} more for Phase 4.`);

    // The location line says which of the two ways of being somewhere this is.
    await submit(page, 'cd $row.kind eq folder');
    const viewing = (await page.locator('#where').innerText()).trim();

    if (!viewing.includes('view:') || !viewing.includes('$row.kind eq folder')) {
      note(`in a view the location line reads ${JSON.stringify(viewing)}`);
    }

    await page.locator('#prompt .up').click();
    await page.waitForFunction(
      () => document.getElementById('where').innerText.trim() === '/', null, { timeout: 10000 });

    // A listing keeps itself up to date: `mkdir` in the next entry adds a row to the
    // table above it, without that entry being re-run by hand.
    await submit(page, 'ls');
    // Pinned by index rather than with `.last()`: more entries follow, and a locator
    // is resolved when it is used, so `.last()` would quietly become the wrong entry.
    const listed = page.locator('.entry').nth(await page.locator('.entry').count() - 1);
    const badge = await listed.locator('.watch .badge').innerText();

    if (badge.trim() !== 'live') {
      note(`the newest listing is not marked live; it says ${JSON.stringify(badge)}`);
    }

    const rowsBefore = await listed.locator('.grid tbody tr').count();
    await submit(page, 'mkdir appeared');

    try {
      await page.waitForFunction(
        ({ index, count }) =>
          document.querySelectorAll('.entry')[index].querySelectorAll('.grid tbody tr').length > count,
        { index: await page.locator('.entry').count() - 2, count: rowsBefore },
        { timeout: 10000 });
    } catch {
      note('the live listing did not gain a row when a folder was created below it');
    }

    // Turning it off freezes it: the next change leaves the table alone.
    await listed.locator('.watch .badge').click();
    const frozenAt = await listed.locator('.grid tbody tr').count();
    await submit(page, 'mkdir ignored');
    await page.waitForTimeout(500);

    if (await listed.locator('.grid tbody tr').count() !== frozenAt) {
      note('a paused listing refreshed anyway');
    }

    // ---- Phase 5: else, try, ?? and nested pipelines -------------------------

    await submit(page, 'reset');
    await runScript(page, PHASE_5, note);
    console.log(`Ran ${PHASE_5.length} more for Phase 5.`);

    // The keywords are coloured as keywords as they are typed, not as arguments.
    await page.fill('#cmd', 'try cat x else echo y');
    try {
      await page.waitForFunction(
        () => document.querySelectorAll('#mirror .t.keyword').length === 2, null, { timeout: 10000 });
    } catch {
      note('`try` and `else` are not coloured as keywords in the input');
    }
    await page.fill('#cmd', '');

    // ---- Phase 6: XML and CSV as real files ---------------------------------

    await submit(page, 'reset');
    await runScript(page, PHASE_6, note);
    console.log(`Ran ${PHASE_6.length} more for Phase 6.`);

    // A listing written out as XML reads back as as many rows as it had. Counted
    // rather than pinned, so the check says what it means whatever the program left.
    // The result is the entry's tail; the rest of the entry is the line echoed back.
    const answer = async line => {
      await submit(page, line);
      return (await page.locator('.entry').last().locator('.tail').innerText()).trim();
    };
    const counted = await answer('ls | count');
    await submit(page, 'ls | to-xml listing.xml');
    const readBack = await answer('from-xml listing.xml | count');

    if (!/^\d+$/.test(counted) || readBack !== counted) {
      note(`\`ls\` counted ${JSON.stringify(counted)} rows and listing.xml read back ${JSON.stringify(readBack)}`);
    }

    // ---- Phase 2: the log survives a reload ---------------------------------

    // Start from a clean store, so what comes back after the reload is what these
    // lines put there rather than whatever the acceptance script left behind.
    await submit(page, 'reset');

    await runScript(page, BEFORE_RELOAD, note);

    await page.reload({ waitUntil: 'domcontentloaded' });
    const restored = await boot(page);
    console.log(`Reloaded. Status: ${restored}`);

    const banner = await page.locator('#banner').innerText();

    if (!/Restored \d+ line/.test(banner)) {
      note(`after a reload the page did not say what it restored. It said: ${JSON.stringify(banner)}`);
    }

    await runScript(page, AFTER_RELOAD, note);

    // ---- Phase 2: reset is the way back -------------------------------------

    const reset = await submit(page, 'reset');

    if (!reset.text.includes('Reset.')) {
      note(`'reset' did not report what it did. It showed: ${JSON.stringify(reset.text)}`);
    }

    await page.reload({ waitUntil: 'domcontentloaded' });
    await boot(page);
    await runScript(page, AFTER_RESET, note);

    console.log(`Ran ${BEFORE_RELOAD.length + AFTER_RELOAD.length + AFTER_RESET.length} more across two reloads.`);

    // ---- Phase 7: what a tap inserts ----------------------------------------

    // A cell inserts what the line would need: a name with a space goes in quoted, or
    // tapping it would hand the next command two arguments.
    await submit(page, 'write "my notes.txt" hi');
    await submit(page, 'ls');
    await page.fill('#cmd', 'cat');
    await page.locator('.entry').last().locator('.grid tbody td', { hasText: 'my notes.txt' }).first().click();
    const tapped = await page.inputValue('#cmd');

    if (tapped !== 'cat "/my notes.txt"') {
      note(`tapping 'my notes.txt' in a listing made the line ${JSON.stringify(tapped)}`);
    }

    // A folder completes to its name and a slash and stops there, so the next tap can
    // go deeper. The core calls it `folder`; the page once waited for `directory`, and
    // added a space after every folder.
    //
    // Completion is asked asynchronously since Phase 8, and Tab waits for the answer to
    // the last keystroke before it fills, so the line is read once it has changed rather
    // than straight after the key: read at once, it is still what was typed.
    await page.fill('#cmd', 'cd doc');
    await page.press('#cmd', 'Tab');
    await page.waitForFunction(
      () => document.getElementById('cmd').value !== 'cd doc', null, { timeout: 10000 }).catch(() => {});
    const completed = await page.inputValue('#cmd');

    if (completed !== 'cd documents/') {
      note(`completing 'cd doc' made the line ${JSON.stringify(completed)}`);
    }

    await page.fill('#cmd', '');

    // ---- Phase 8: completion that reads the line ----------------------------

    await submit(page, 'reset');
    await runScript(page, PHASE_8, note);

    // `$files` alone is a value stage: the table it holds is drawn as a table.
    await submit(page, '$files');
    const drawn = page.locator('.entry').last();
    const drawnHeaders = (await drawn.locator('.grid thead th').allInnerTexts()).join(' ');
    const drawnRows = await drawn.locator('.grid tbody tr').count();

    if (drawnHeaders !== 'name kind folder size modified' || drawnRows !== 4) {
      note(`'$files' drew headers ${JSON.stringify(drawnHeaders)} and ${drawnRows} row(s), not the four-row listing`);
    }

    // What is typed. Completion is asked asynchronously, so each case empties the line
    // first, which empties the chip row, and then waits for the chip it expects to
    // arrive, rather than sleeping and hoping the answer came.

    // `$` offers the variables, and the first chip's detail is the detail line's job
    // when the caret is not in an argument. The detail line is read as `#detail`'s text.
    const dollar = await offered(page, '$', '$files');

    if (!dollar) {
      note(`'$' did not offer '$files'. The chips were ${JSON.stringify(await chips(page))}`);
    } else {
      if (dollar.includes('$row')) note(`'$' offered '$row' outside a predicate: ${JSON.stringify(dollar)}`);

      // Tab steps to `$files` if it is not the chip already selected.
      for (let step = 0; step < dollar.length && await selectedChip(page) !== '$files'; step++) {
        const was = await selectedChip(page);
        await page.press('#cmd', 'Tab');
        await page.waitForFunction(
          was => document.querySelector('#complete .key.sel')?.textContent !== was,
          was, { timeout: 10000 }).catch(() => {});
      }

      const detail = await detailSays(page, 'table · 4 rows');
      if (!detail.includes('$files') || !detail.includes('table · 4 rows')) {
        note(`with '$files' selected after '$', the detail line read ${JSON.stringify(detail)}`);
      }
    }

    // A column is offered where `sort` takes one, and the signature says which it is.
    const sortable = await offered(page, 'ls | sort ', 'name');

    if (!sortable) {
      note(`'ls | sort ' did not offer 'name'. The chips were ${JSON.stringify(await chips(page))}`);
    } else {
      const active = await detailSays(page, '<column>');
      const bold = (await page.locator('#detail .param.on').allInnerTexts()).join(' ');
      if (!active.startsWith('sort') || bold !== '<column>') {
        note(`after 'ls | sort ' the detail line read ${JSON.stringify(active)}, bold ${JSON.stringify(bold)}`);
      }
    }

    // The right of a comparison offers the values the column holds.
    const kinds = await offered(page, 'ls | where $row.kind eq ', 'folder');

    if (!kinds) {
      note(`'ls | where $row.kind eq ' did not offer 'folder'. The chips were ${JSON.stringify(await chips(page))}`);
    }

    // Tab steps through the chips, each put in place in turn; Shift+Tab steps back and
    // Escape puts back what was typed. With nothing typed of the word there is no
    // common prefix to fill, so the first Tab already steps.
    const base = 'ls | where $row.kind eq ';

    if (!kinds || kinds.length < 2) {
      note(`Tab could not be checked: 'ls | where $row.kind eq ' offered ${JSON.stringify(kinds)}`);
    } else {
      const steps = [
        ['Tab', base + kinds[0], kinds[0]],
        ['Tab', base + kinds[1], kinds[1]],
        ['Shift+Tab', base + kinds[0], kinds[0]],
        ['Escape', base, null],
      ];

      for (const [key, line, chip] of steps) {
        await page.press('#cmd', key);
        const reached = await page.waitForFunction(
          ({ line, chip }) => document.getElementById('cmd').value === line &&
            (chip === null || document.querySelector('#complete .key.sel')?.textContent === chip),
          { line, chip }, { timeout: 10000 }).then(() => true, () => false);

        if (!reached) {
          note(`${key} in the chips of ${JSON.stringify(base)} made the line ` +
               `${JSON.stringify(await page.inputValue('#cmd'))} with ${JSON.stringify(await selectedChip(page))} ` +
               `selected; expected ${JSON.stringify(line)}` + (chip ? ` with '${chip}' selected` : ''));
          break;
        }
      }
    }

    // Completing mid-line replaces the word at the caret and keeps the rest of the line.
    const middle = await offered(page, 'cat re documents', 'readme.txt', ' documents'.length);

    if (!middle) {
      note(`'cat re| documents' did not offer 'readme.txt'. The chips were ${JSON.stringify(await chips(page))}`);
    } else {
      await page.locator('#complete .key', { hasText: 'readme.txt' }).first().click();
      const kept = await page.waitForFunction(
        () => document.getElementById('cmd').value === 'cat readme.txt documents', null, { timeout: 10000 })
        .then(() => true, () => false);
      const caret = await page.evaluate(() => document.getElementById('cmd').selectionStart);

      if (!kept || caret !== 'cat readme.txt'.length) {
        note(`applying 'readme.txt' in 'cat re| documents' made the line ` +
             `${JSON.stringify(await page.inputValue('#cmd'))} with the caret at ${caret}`);
      }
    }

    await page.fill('#cmd', '');

    // Tapping a variable in a line already run says what it holds.
    await submit(page, '$files | count');
    const echoed = page.locator('.entry').last().locator('.echo .t', { hasText: 'files' }).first();

    // Running the line empties the input and so the detail line; were it still showing
    // `$files` from the typing above, the tap would prove nothing.
    const beforeTap = (await page.locator('#detail').innerText()).trim();

    if (await echoed.count() === 0) {
      note(`the line '$files | count' has no 'files' token to tap`);
    } else if (beforeTap.includes('table · 4 rows')) {
      note(`before the tap the detail line already read ${JSON.stringify(beforeTap)}`);
    } else {
      await echoed.click();
      const hover = await detailSays(page, 'table · 4 rows');
      if (!hover.includes('$files') || !hover.includes('table · 4 rows')) {
        note(`tapping '$files' in a finished line made the detail line read ${JSON.stringify(hover)}`);
      }
    }

    console.log(`Ran ${PHASE_8.length + 1} more for Phase 8, then checked the chips, the detail line, Tab and a tap.`);

    // ---- On a phone: nothing moves, opens or closes on its own ----------------

    // The keyboard comes with the input's focus, so the focus is what is checked: a
    // blur is the keyboard closing, and a focus the page gave is it opening.
    await page.evaluate(() => {
      window.__blurs = 0;
      document.getElementById('cmd').addEventListener('blur', () => { window.__blurs++; });
    });

    const finishedNow = () => page.evaluate(() => Number(document.body.dataset.finished || 0));
    const afterTap = async (selector, before) => {
      await page.tap(selector);
      await page.waitForFunction(
        count => Number(document.body.dataset.finished || 0) > count &&
                 document.querySelectorAll('.entry.running').length === 0,
        before, { timeout: 30000 });
      await page.waitForTimeout(150);
    };
    const focused = () => page.evaluate(() => document.activeElement === document.getElementById('cmd'));
    const atTheEnd = () => page.evaluate(() => {
      const s = document.getElementById('scroll');
      return s.scrollHeight - s.scrollTop - s.clientHeight < 2;
    });

    // With the keyboard up, running a line by tapping Run keeps it up throughout.
    await page.focus('#cmd');
    await page.fill('#cmd', 'mkdir kept');
    await afterTap('#run', await finishedNow());

    if (await page.evaluate(() => window.__blurs) > 0 || !await focused()) {
      note('tapping Run with the keyboard up closed it, even for a moment');
    }

    // With the keyboard down, undo and redo leave it down.
    await page.evaluate(() => document.getElementById('cmd').blur());
    await afterTap('#prompt #undo', await finishedNow());
    if (await focused()) note('tapping undo with the keyboard down brought it up');
    await afterTap('#prompt #redo', await finishedNow());
    if (await focused()) note('tapping redo with the keyboard down brought it up');

    // A listing is shown to its last row, even when the location line grows an `up`
    // button after the line has finished, which takes height from the scrollback.
    await submit(page, 'cd documents');
    await submit(page, 'ls');
    await page.waitForTimeout(150);
    if (!await atTheEnd()) note('after ls below the root, the scrollback stopped short of its last line');

    // The keyboard opening takes height from the page: the input stays in sight, just
    // above it, and the scrollback stays at its end. Resizing the viewport is how a
    // headless browser shows the keyboard.
    await page.focus('#cmd');
    await page.setViewportSize({ width: VIEWPORT.width, height: 460 });
    await page.waitForTimeout(200);
    await submit(page, 'ls');
    await page.waitForTimeout(150);

    const input = await page.locator('#cmd').boundingBox();
    if (!input || input.y + input.height > 460) {
      note(`with the keyboard up, the input is at ${input ? Math.round(input.y) : 'no'} pixels, below the 460 left above it`);
    }
    if (!await atTheEnd()) note('with the keyboard up, after ls the scrollback stopped short of its last line');
    if (!await focused()) note('with the keyboard up, running ls from it closed it');

    await page.setViewportSize(VIEWPORT);
    console.log('Checked that the keyboard, the input and the scrollback stay put on a phone.');

    if (consoleErrors.length > 0) {
      for (const error of consoleErrors) note(`console error: ${error}`);
    }
  } finally {
    await browser.close();
    server?.kill();
  }

  if (failures.length > 0) {
    console.error(`\n${failures.length} failure(s).`);
    process.exit(1);
  }

  console.log('\nAll checks passed.');
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
