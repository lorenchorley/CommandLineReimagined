# 0057. The terminal is English only, for now

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

Every message, command name, keyword and guide file is English, and the browser client
is published with invariant globalisation. The design doc and the specification's known
deviations described this as "intended for now", with nothing recording who decided it.

## Options

1. **Plan for other languages now**: message catalogues, translated keywords, culture-aware
   formatting.
2. **English only, for now.**

## Decision

Option 2, chosen by the owner. Messages, commands, keywords and the guide are English,
numbers and dates are formatted invariantly, and no work is planned on translation.

## Consequences

The known deviation becomes a decision, and the design doc's internationalisation
section cites it. Revisiting it is a new decision.
