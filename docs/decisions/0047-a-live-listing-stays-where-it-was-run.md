# 0047. A live listing stays where it was run

| Field | Value |
| --- | --- |
| Status | Accepted |
| Date | 2026-09-24 |

## Context

A live listing is re-run whenever the store changes (Phase 4, and Phase 9's R5). The
re-run happened wherever the session was at the time, so `ls` at `/`, then
`in documents`, turned the listing of `/` above into a listing of `documents`, and
`out` of a view turned the view's listing into the folder's. The owner reported it:
the listing should keep the view in which it was originally run.

## Options

1. **Re-run where the session is now**, as before. The entry above then says something
   its line never asked.
2. **Freeze a listing when the location changes.** Honest, and it gives up the live
   table whenever you move, which is most of the time.
3. **Re-run it where it was first run.** The store is the current one; only where the
   line stands is the listing's own.

## Decision

Option 3, chosen by the owner. A live listing is asked again from the folder and view
its first response carried. `in`, `out` and `back` afterwards change where the next line
runs, not what a listing above it shows. Everything else about a refresh is unchanged:
it reads the current store, commits nothing, and refuses a line that could write.

## Consequences

The session gains `RefreshAt(source, location)`, and the web adapter and bridge a
refresh that takes the folder and the view's text as the first response gave them. A
listing whose folder has since been removed or renamed fails to refresh and stays as it
was drawn, as any failed refresh does.
