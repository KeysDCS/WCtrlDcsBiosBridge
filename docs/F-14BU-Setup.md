# F-14B(U) — in a hurry

Nothing to install. The F-14B(U) is the F-14B plus the CDNU, and all of it comes off DCS-BIOS.
The export script is **no longer needed** for this aircraft.

> **DCS-BIOS: a [nightly build](https://github.com/DCS-Skunkworks/dcs-bios/releases/tag/latest)
> dated 2026-09-11 or later.** The variant itself is in v0.11.7, but the CDNU rows
> (`RIO_CDNU_LINE1..8`) landed on 2026-09-08, after that release was cut.

Load the F-14B(U) in DCS and the CDNU appears on the CDU.

## What the F-14B(U) shows

Everything the plain F-14B shows, plus the CDNU:

| Page | Key | Notes |
| --- | --- | --- |
| CDNU | **DATA** — the page the aircraft opens on | 8 rows, mixed case, as the RIO sees them |
| RIO CAP | **PREV PAGE** | |
| Radio / IFF | **NEXT PAGE** | |

The gear lights and the clock reach the front panels too, same as on the F-14B, and LEDs bind
like any other aircraft's — see [LED mapping](LED-Mapping.md).

All three keys are yours to change, in the **F-14B / F-14B(U)** section of the options.

## If the CDNU page is empty

It reads `CDNU NOT IN DCS-BIOS` when the installed module does not declare the rows: take a
nightly dated 2026-09-11 or later. The rest of the aircraft keeps working meanwhile — the page
is disabled, not the listener, and the log says the same thing in one line.

`WAITING FOR CDNU DATA` means the rows are declared but nothing has arrived yet. Check the CDNU
is powered in the cockpit.

## Symbols

The CDNU draws its arrows and markers on control codes, which DCS-BIOS swaps for printable
stand-ins (`«` `»` `{` `}` `®` `©`) before exporting the row. The bridge turns those into the CDU
font's own glyphs. One has no glyph on the device at all — the scratchpad's horizontal
double-headed arrow — and renders blank until the font gains a slot for it.
