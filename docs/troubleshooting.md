# Troubleshooting

Symptoms that are not error messages, and what is behind them. For messages, see the
[error reference](errors.md).

## The page says `starting…` and the input stays disabled

The .NET runtime has not finished loading. A first visit fetches about 10 MB; later
visits come from the browser cache. If the status turns red and reads `failed to load`,
the runtime did not start within about forty seconds, so its files are missing or
blocked: reload, and if it persists check that your host serves `.wasm` files with the
`application/wasm` content type.

If the status reads `restoring…` for a moment and then `failed to restore`, the runtime
loaded but replaying the stored log failed. The scrollback says why. The input stays
disabled, because showing an empty filesystem that may not be empty would be worse.

## My files disappeared

They should not, so it is worth reading the status line at the top of the page.

If it says **`not persisted`**, this browser is not keeping the log and the session
lasted only as long as the tab. A private or incognito window is the usual reason:
IndexedDB is unavailable there. Site-data settings that block storage for this origin
do the same. Hover the tag to see what the browser said.

The status is set once, when the page loads. If storage goes away while the page is
open, the session carries on in memory without the status changing, and the next load
is the one that shows `not persisted` or the missing lines.

Otherwise, the filesystem is stored by your browser for this site, so anything that
clears site data for it takes the filesystem with it: clearing browsing data, a
"clear cookies and site data" setting, or some privacy extensions. There is no copy
anywhere else, because nothing is uploaded.

If the page reports that stored lines could not be read and were skipped, a log was
written by a different build of the terminal than the one loading it. The unreadable
lines are skipped rather than stopping the page; `reset` starts over cleanly.

## `undo` said there was nothing to undo

Undo works on lines that changed something. A line that changed nothing, such as `ls`
or `pwd`, is not recorded, so it is never what undo reaches for — and the filesystem
you started with is recorded but is not yours to take back, so undo stops before it.
Moving is a change: where you are is kept in the log, so `undo` after `cd documents`
takes you back.

If you have only run read-only lines in a fresh session, there is genuinely nothing to
undo, and that is what it says.

## `redo` will not go any further

Redo puts back what undo took away, and stops once everything is back. It does not
carry on and take it away again.

## I want to start over

```
$ reset
Reset. 9 files restored.
```

It empties the log and seeds the filesystem again. It cannot be undone, which is why it
is not one of the suggestion keys.

## A file name with a space is split into two arguments

Quote it: `write "my notes.txt" hello`. Unquoted words end at a space. Tapping a chip or
a cell inserts the file's path for you, but as it is, without quotes, so a name with a
space in it still needs quotes added around it.

## `cd ..` shows an odd path

It should not. Paths are normalised, so `cd ..` reports the parent's real name. If you
see a path containing `..`, the build predates that fix.

## Tab does nothing

Tab applies a completion, and there is nothing to apply when the word is already
complete or nothing matches. The completion row above the suggestions shows what is
available; when it is empty, Tab has nothing to do. Phone keyboards have no Tab key,
so tapping a chip is the intended path there.

## The Run button says Stop and will not run my line

A command is still running. Press Stop, Enter or Escape, and it stops at its next
checkpoint. Commands that do not check for cancellation between steps finish first.

## `download` fails in the browser but the URL works elsewhere

The page fetches over the network like any other browser request, so the remote host
must allow cross-origin reads. Hosts that do not send permissive CORS headers fail no
matter how valid the URL is. The default URL is chosen because it does allow them.

## `progress` finishes instantly

You gave it a small `steps` or `delay`. `progress 4 5` takes four steps of five
milliseconds. Plain `progress` is a hundred steps of a hundred milliseconds, about ten
seconds.

## `echo -5` says there is no argument named `5`

You are on a build from before the negative-number rule. A `-` in front of a digit now
starts a word, so `echo -5` writes minus five; a `-` in front of anything else is still
a flag.

## `< thing` is a syntax error

A `<` opens a tag only when a name, a `$` or a `/` comes straight after it, with no
space. Comparisons are words, such as `lt`, so a `<` means nothing else. Write
`<thing/>`.

## The desktop application will not build on Linux

It is a Windows Presentation Foundation application and needs Windows. Everything else
in the solution builds anywhere. See [Building and testing](building.md).

## Something behaves differently from these docs

The examples in these pages are pasted from real output, but if you find a gap, the
[specification](spec/README.md) states the intended behaviour normatively and the
conformance section maps each requirement to the test that proves it.
