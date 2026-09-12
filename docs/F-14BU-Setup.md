# F-14B(U) CDNU — in a hurry

Everything on the F-14B(U) except the CDNU comes off DCS-BIOS like any other aircraft's, from
**v0.11.7 onwards** — that release is the one whose F-14 module covers the variant. On an
older DCS-BIOS the aircraft is detected but nothing is exported for it.

The CDNU itself is not in that module, so the bridge reads it from a small export script that
scrapes it straight from the cockpit and sends it over UDP. Three steps.

> The script and the app talk a versioned protocol. Take both from the **same release** —
> a mismatched script is ignored and the page stays empty.

## 1. Install the export script

Extract the `wctrl-export-scripts-<version>.zip` asset of the release (the folder also ships
inside the application zip) into your DCS saved games, so you end up with:

```
%USERPROFILE%\Saved Games\DCS\Scripts\wctrl-export\
```

Full details, including a DCS running on another PC and where to look when nothing shows up:
[Lua export script setup](Export-Script-Setup.md).

## 2. Chain it from Export.lua

Open (or create) `%USERPROFILE%\Saved Games\DCS\Scripts\Export.lua` and **add** this line:

```lua
dofile(lfs.writedir() .. [[Scripts\wctrl-export\wctrl-export.lua]])
```

Add it, do not replace what is already there — that file is usually shared with DCS-BIOS
and SRS, and overwriting it breaks them.

## 3. Turn it on

In the app, under **GENERAL**, tick **Use DCS live data export**, then restart the bridge.
Load the F-14B(U) in DCS and the CDNU appears on the CDU.

## What the F-14B(U) shows

Everything the plain F-14B shows, plus the CDNU:

| Page | Key | From |
| --- | --- | --- |
| CDNU | **DATA** — the page the aircraft opens on | the export script |
| RIO CAP | **PREV PAGE** | DCS-BIOS |
| Radio / IFF | **NEXT PAGE** | DCS-BIOS |

The gear lights and the clock reach the front panels too, same as on the F-14B. All three keys
are yours to change, in the **F-14B / F-14B(U)** section of the options.

## If the page stays empty

It reads `WAITING FOR CDNU DATA` when the socket is up but nothing is arriving. Check that
step 2 was applied to the `Export.lua` of the DCS install you actually fly, and that DCS
has been restarted since.

`Scripts\wctrl-export\test_client.py` prints the raw feed on UDP 31090, which tells you
whether DCS is sending before you start suspecting the bridge. The app's log reports a
protocol version mismatch if the script is older than the app.

If the CDNU is fine but the gear lights, clock, RIO or radio pages are dead, that is
DCS-BIOS rather than the script: check you are on v0.11.7 or later.

## Also in this release

The same export script pre-fills live wind and field elevation on the A-10C takeoff
performance page. Values you type over are left alone. See
[Lua export script setup](Export-Script-Setup.md#what-uses-the-feed).
