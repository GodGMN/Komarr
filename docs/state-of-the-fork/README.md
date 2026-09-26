# State of the fork — periodic writeups

This directory holds occasional posts on what Librarr's last stretch
looked like, so a reader can find that out without reading commit logs.

**These are not a commitment.** They used to be: `docs/governance.md`
promised one per quarter, on a 14-day deadline, with a skipped quarter
counting as a bus-factor warning. That document was deleted 2026-08-03
because it described a multi-maintainer project that has never existed
here. The writeups survived the deletion because they turned out to be
worth doing on their own merits — the 2026-Q2 post is what surfaced
that the project moves in four-day bursts and that the planning docs
had drifted out of sync with the code.

So: write one when there is something worth saying. A missed one is
not a failure of anything.

## Filename convention

`YYYY-Q1.md`, `YYYY-Q2.md`, `YYYY-Q3.md`, `YYYY-Q4.md`.

Quarters are calendar quarters (Q1 = Jan–Mar, etc.), not fiscal.

## Cadence

Quarterly is the natural rhythm and the filename convention assumes
it, but nothing enforces it. The old rule — published within 14 days
of the quarter's end, with a skipped quarter reading as a bus-factor
warning — went with `governance.md` on 2026-08-03.

### Partial quarters (decided 2026-08-02)

**The first writeup owed is 2026-Q2, not 2026-Q1.** Librarr's first
commit is 2026-05-16 and the repository was created 2026-05-19, so Q1
2026 predates the project entirely. No Q1 post is owed and none is
missing.

Q2 is therefore a 45-day quarter for this project, and a thin one.
Three options were weighed when it came up 19 days late:

1. **Publish it thin** — chosen. A short honest post costs an hour and
   keeps the cadence's record continuous.
2. Merge Q2 into Q3 and publish one combined post in October —
   rejected. A quarter that silently absorbs into the next one is
   precisely the drift a cadence exists to catch. Skipping a quarter
   to save an hour's writing is how the habit stops.
3. Amend the cadence to begin at the first *full* quarter (Q3) —
   rejected for the same reason, plus it would mean the release of
   1.0.0-beta went unrecorded in the only place that tracks quarters.

The rule that falls out, for anyone hitting this again: **a quarter in
which the project existed at all gets a post, however short.** State
the real window in the opening line when it isn't a full quarter.

## Template

Copy this into each new `YYYY-Qn.md`:

```markdown
# State of the fork — YYYY Qn

_Published: YYYY-MM-DD_

## Numbers

- Open PRs at quarter end: N
- PRs closed this quarter: N (merged: N, declined: N)
- Recent contributors: @alice, @bob, @carol
- Contributors this period, if any beyond the maintainer: @alice

## Roadmap delta

What moved between buckets in `docs/roadmap.md` this quarter:

- Now → Done: ...
- Soon → Now: ...
- Later → Soon: ...
- New entries: ...

## Retro: what didn't ship

Items that were planned for this quarter and slipped. One sentence
each on why — useful context for next quarter's planning.

- ...

## Operating costs

What the project actually costs to run:

- CI runtime: $X (or "GHA free tier, $0")
- Code-signing certs: $X
- Docker Hub: $X
- Error-tracking subscription: $X
- Domain: $X
- **Total this quarter:** $X

## Asks

Where help is most useful right now — review bandwidth, testing on a
specific platform, infra access, anything that unblocks the next
quarter's roadmap.

- ...
```

## Backlinks

- [`docs/roadmap.md`](../roadmap.md) — the rolling priority list this
  writeup reports deltas against.
- [`docs/release-checklist.md`](../release-checklist.md) — what each
  release tag requires.
