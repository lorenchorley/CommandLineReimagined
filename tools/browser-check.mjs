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
  { line: 'undo', expect: ['Undone: mkdir alpha'] },
  { line: 'ls', absent: ['alpha'] },
  { line: 'redo', expect: ['Redone: mkdir alpha'] },
  { line: 'history', expect: ['seed', 'mkdir alpha'], absent: ['(undone)'] },

  { line: 'write note.txt first', expect: ['note.txt'] },
  { line: 'write note.txt second', expect: ['note.txt'] },
  { line: 'undo', expect: ['Undone: write note.txt second'] },
  { line: 'cat note.txt', expect: ['first'] },

  { line: 'attr note.txt tag=work', expect: ['note.txt'] },
  { line: 'attr note.txt', expect: ['name = note.txt', 'kind = text', 'folder = /', 'tag = work', 'created = ', 'modified = '] },

  { line: 'save <note name=todo due=2026-10-01/>', expect: ['todo'] },
  { line: 'cat todo', expect: [] },

  // Decision 0007: a path in a tag needs no quotes, and reads back unchanged.
  { line: '<file path=documents/notes.txt/>', expect: ['<file path=documents/notes.txt/>'] },
  { line: 'echo -5', expect: ['-5'] },

  { line: 'set v 1', expect: ['1'] },
  { line: 'set v 2', expect: ['2'] },
  { line: 'undo', expect: ['Undone: set v 2'] },
  { line: 'undo', expect: ['Undone: set v 1'] },
  { line: 'echo $v', fault: 'notfound', expect: ['Unknown variable : $v'] },

  { line: 'progress 3 1', expect: ['100'] },

  // The page's own words, which never reach the evaluator.
  { line: 'help', expect: ['undo', 'redo', 'history', 'attr', 'save'] },
];

/**
 * Phase 2: what a reload is for. Run, reload the page, and check what came back.
 *
 * This cannot be a unit test. Replaying a log is covered in `Core.Tests`; what is only
 * checkable here is that the log reached IndexedDB at all, that the page waits for the
 * replay before enabling the input, and that a second visit finds the first one's work.
 */
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
    const { text, fault } = await submit(page, step.line);

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

    if (step.fault && fault !== step.fault) {
      note(`'${step.line}' showed fault kind ${JSON.stringify(fault)}, expected '${step.fault}'`);
    }

    if (!step.fault && fault !== null) {
      note(`'${step.line}' failed unexpectedly (${fault}): ${JSON.stringify(text)}`);
    }
  }
}

/** Submits one line and returns the text of the entry it produced. */
async function submit(page, line) {
  const before = await page.locator('.entry').count();

  await page.fill('#cmd', line);
  await page.press('#cmd', 'Enter');

  // The entry appears as soon as the line is submitted; it is finished when it stops
  // being marked as running. `progress` takes a moment, so this waits rather than polls.
  await page.waitForFunction(
    count => document.querySelectorAll('.entry').length > count,
    before,
    { timeout: 15000 });

  await page.waitForFunction(
    () => document.querySelectorAll('.entry.running').length === 0,
    null,
    { timeout: 30000 });

  const entry = page.locator('.entry').last();
  return {
    text: (await entry.innerText()).replace(/ /g, ' '),
    fault: await entry.locator('.kind').count() > 0
      ? (await entry.locator('.kind').first().innerText()).trim()
      : null,
  };
}

async function main() {
  const target = process.argv[2];
  const deployed = /^https?:\/\//.test(target ?? '');

  if (!target || (!deployed && !existsSync(`${target}/index.html`))) {
    console.error('usage: node tools/browser-check.mjs (<published wwwroot> | <url>)');
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

    // The chips have to be tappable. 44 pixels is the smallest target a finger hits
    // reliably, and the whole page is built for a phone.
    await submit(page, 'ls');
    const chip = page.locator('.entry').last().locator('.item').first();
    const box = await chip.boundingBox();

    if (!box || box.height < 44) {
      note(`a result chip is ${box ? box.height : 'not'} pixels tall; 44 is the minimum tap target`);
    }

    await runScript(page, SCRIPT, note);
    console.log(`Ran ${SCRIPT.length} lines.`);

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
