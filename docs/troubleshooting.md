# Troubleshooting

Symptoms that are not error messages, and what is behind them. For messages, see the
[error reference](errors.md).

## The page says `starting…` and the input stays disabled

The .NET runtime has not finished loading. A first visit fetches about 15 MB; later
visits come from the browser cache. If the status turns red and reads `failed to load`,
the runtime files are missing or blocked: reload, and if it persists check that your
host serves `.wasm` files with the `application/wasm` content type.

## My files disappeared

The filesystem lives in the tab's memory. Reloading or closing the tab discards it, and
the seeded tree comes back. There is no persistence yet, by design: nothing is uploaded
and nothing is written to the device.

## `undo` did nothing

It undid the previous command, which had nothing to reverse. The response names what it
undid, so `Undone: ls` means the `ls` came off the history. Press undo again to reach
the command that changed something.

## A file name with a space is split into two arguments

Quote it: `write "my notes.txt" hello`. Unquoted words end at a space. Chips help here:
tapping a result inserts its name for you.

## `cd ..` shows an odd path

It should not. Paths are normalised, so `cd ..` reports the parent's real name. If you
see a path containing `..`, the build predates that fix.

## Tab does nothing

Tab applies a completion, and there is nothing to apply when the word is already
complete or nothing matches. The completion row above the suggestions shows what is
available; when it is empty, Tab has nothing to do. Phone keyboards have no Tab key,
so tapping a chip is the intended path there.

## The Run button says Stop and will not run my line

A command is still running. Press Stop, or Escape, and it stops at its next checkpoint.
Commands that do not check for cancellation between steps finish first.

## `download` fails in the browser but the URL works elsewhere

The page fetches over the network like any other browser request, so the remote host
must allow cross-origin reads. Hosts that do not send permissive CORS headers fail no
matter how valid the URL is. The default URL is chosen because it does allow them.

## `progress` finishes instantly

You gave it a small `steps` or `delay`. `progress 4 5` takes four steps of five
milliseconds. Plain `progress` is a hundred steps of a hundred milliseconds, about ten
seconds.

## A tag attribute will not take my path

Attribute values are identifiers, strings or variables, not bare words, because inside
a tag a `/` closes the tag. Quote it: `<file path="documents/notes.txt"/>`.

## The desktop application will not build on Linux

It is a Windows Presentation Foundation application and needs Windows. Everything else
in the solution builds anywhere. See [Building and testing](building.md).

## Something behaves differently from these docs

The documentation is generated from the same source as the tests, but if you find a
gap, the [specification](spec/README.md) states the intended behaviour normatively and
the conformance section maps each requirement to the test that proves it.
