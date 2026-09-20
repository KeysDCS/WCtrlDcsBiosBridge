using WCtrlDcsBiosBridge.Services;
using WwDevicesDotNet;

namespace WCtrlDcsBiosBridge.Aircrafts.C130J;

/// <summary>
/// C-130J CNI-MU repeater.
///
/// DCS-BIOS carries the aircraft but not its CNI-MU — the module declares every switch and
/// lamp and not one line of the display. What is shown here therefore comes from the
/// wctrl-export.lua UDP feed, which scrapes the pilot's and the copilot's CNI, and the CNI is
/// the only page this listener offers.
///
/// Which seat reaches this panel is <see cref="CniSeatPages"/>'s to say: one seat when the
/// selection screen gave it one, every seat when this is the only CDU, turned through with
/// <see cref="Config.C130JOptions.SeatToggleKey"/> — SP by default, the one key a CNI page
/// never needs.
///
/// Every seat is drawn whether or not anyone is looking at it, so the page behind the one on
/// screen is current the moment it is turned to. It costs nothing worth counting — the feed
/// sends one seat per packet either way — and the alternative is a panel that stays on the old
/// seat's picture until that seat next has news, which the export throttles to a 2 s heartbeat.
///
/// The indication carries element names and values and nothing else — no position, no font
/// size, no highlight. The layout comes from <c>Resources/c130j-cni-pages.json</c>, extracted
/// offline from the module's own page scripts by <c>tools/cni-schema</c>.
///
/// One lamp comes off the same feed: the CNI-MU's EXEC annunciator, which nothing readable
/// reports. DCS-BIOS declares it as PLT_CNI_EXEC_LED on cockpit argument 3390, but that
/// argument never leaves zero — tried, and the lamp stayed dark. <see cref="CniExecLamp"/>
/// works it out from the page title and the EXEC keypresses instead, and holds it — the marker
/// is only on the modified page, so a lamp recomputed from whatever page is on screen would go
/// out the moment the crew turned away.
/// That lamp is the aircraft's rather than a seat's, so it is fed every packet the feed
/// carries, whichever seat is on screen.
/// </summary>
internal sealed class C130J_Listener : AircraftListener
{
    private const string SchemaFile = "Resources/c130j-cni-pages.json";

    /// <summary>
    /// The copilot's screen. The pilot's keeps DEFAULT_PAGE, so a panel that was given a seat
    /// rather than the toggle renders exactly what it rendered before this page existed.
    /// </summary>
    private const string COPILOT_PAGE = "COPILOT";

    private readonly CniSchema? _schema;
    private readonly CniPageResolver? _resolver;

    private readonly CniSeatPages _seats;
    private readonly Key _seatToggleKey;

    private SimExportReceiver? _exportReceiver;

    /// <summary>
    /// The EXEC annunciator, which the aircraft holds once for all of its CNIs: a change entered
    /// at either station lights both, and executing it at either puts both out. One instance per
    /// listener all the same, because it is fed every packet the feed carries rather than one
    /// seat's alone — two lamps reading the same evidence stay in step, and neither owns state
    /// the other has to be told about.
    /// </summary>
    private readonly CniExecLamp _execLamp = new();

    /// <summary>
    /// The CNI is 25 characters across. It was squeezed into 24 for as long as that was
    /// thought to be the panel's grid, at the cost of every column of figures on the
    /// display; the panel takes 25 without complaint.
    /// </summary>
    protected override (int Lines, int Columns)? ScreenSize => (CniGrid.Lines, CniGrid.Columns);

    /// <summary>
    /// The module's own phosphor, which it names green_blue:
    /// <c>materials["green_blue"] = {5, 255, 63, 255}</c> in the C-130J's materials.lua,
    /// and <c>fonts["cni_font_green"]</c> is built from it.
    /// </summary>
    protected override string? DisplayGreenRgb => "05FF3F";

    public C130J_Listener(UserOptions options, bool pilot = true, bool switchWithSeat = false)
        : base(AircraftRegistry.C130J, options)
    {
        _seats = switchWithSeat
            ? CniSeatPages.ForEverySeat((CniSeatPages.Pilot, DEFAULT_PAGE),
                                        (CniSeatPages.Copilot, COPILOT_PAGE))
            : CniSeatPages.ForOneSeat(pilot, DEFAULT_PAGE);

        _seatToggleKey = Enum.TryParse<Key>(options.C130J.SeatToggleKey, out var toggleKey)
            ? toggleKey : Key.Space;

        try
        {
            _schema = CniSchema.Load(Path.Combine(AppContext.BaseDirectory, SchemaFile));
            _resolver = new CniPageResolver(_schema);
            App.Logger.Info($"C-130J CNI schema loaded: {_schema.Pages.Count} pages");
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, $"Failed to load CNI schema: {SchemaFile}");
        }

        if (options.EnableLiveExport)
        {
            // Started before subscribing: the receiver lives as long as the process, so a
            // handler attached ahead of a throwing EnsureStarted would outlive this
            // half-built listener and keep being called on it.
            var receiver = SimExportReceiver.Shared;
            receiver.EnsureStarted();
            receiver.DataReceived += OnLiveExportData;
            _exportReceiver = receiver;
        }
    }

    protected override void RegisterCduControls()
    {
        // Built here rather than in the constructor because a page takes the panel's grid at
        // the moment it is made, and the panel is not known until it has been attached. The
        // CNI wants 25 columns where a page built too early would hold the default 24.
        foreach (var seat in _seats.Views)
            AddNewPage(seat.PageName);

        if (_seats.CanTurn && CduDevice != null)
        {
            CduDevice.KeyDown -= HandleKeyDown;
            CduDevice.KeyDown += HandleKeyDown;
        }

        RenderPlaceholder();
    }

    // The gear lights and the master caution are declared in LedDefaults, which registers them.
    protected override void RegisterFrontpanelControls() { }

    /// <summary>
    /// Turns the panel to the next seat. Nothing is redrawn here: every page is kept current as
    /// its packets arrive, and the display tick renders whichever one is named.
    ///
    /// The turn is counted rather than told. DCS holds its own idea of this key — it is the
    /// natural one to also carry a modifier moving the keyboard's own output to the other CNI —
    /// and neither side can see the other's. They agree as long as both see every press; one
    /// that either missed leaves the keyboard on one seat and the screen on another, which is
    /// visible, and which further presses walk back into step.
    /// </summary>
    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != _seatToggleKey) return;

        _currentPage = _seats.Next(_currentPage);
        App.Logger.Debug($"CNI seat shown: {_currentPage}");
    }

    // Runs on the UDP receiver thread, like the A-10C live export path.
    private void OnLiveExportData(SimExportData data)
    {
        // The export only sends a page when it changed, plus a heartbeat. A packet without
        // one carries no news, and clearing on it would make the display flicker.
        if (data.Cni is not { } cni) return;

        // Both annunciators for one lamp: EXEC is the PFP's, silkscreened for exactly this, and
        // RDY stands in on the MCDU, which has no EXEC. A panel ignores the one it does not
        // carry, so each lights a single lamp.
        //
        // Ahead of the seat lookup, and deliberately: the lamp belongs to the aircraft and not
        // to a station. A change entered on either CNI lights both annunciators, and executing
        // it from either puts both out — which is what the aircraft does, checked on a pair of
        // CDUs seated pilot and copilot. Filtering first left each panel lit by its own seat
        // alone, so the copilot's change never reached the pilot's lamp.
        //
        // Fed before the page is resolved, and from the title rather than the layout: a page the
        // schema does not know still carries the marker, and every packet has to reach the lamp
        // for it to know what it has and has not been shown.
        var exec = _execLamp.Update(cni.Title, cni.ExecPresses);
        SetCduLeds(rdy: exec, exec: exec);

        // The screen, unlike the lamp, is a seat's. Every seat arrives on the same feed, one
        // page per packet, and a seat this panel does not hold belongs to another one.
        if (_resolver is null) return;
        if (!_seats.TryGet(cni.Seat, out var seat)) return;

        var page = _resolver.Resolve(cni);
        if (page is null)
        {
            App.Logger.Debug($"CNI page not recognised: '{cni.Title}' ({cni.N} blocks)");
            return;
        }

        if (!ReferenceEquals(page, seat.Page))
        {
            seat.Page = page;
            App.Logger.Debug($"CNI page ({seat.Name}): {page.Name} ('{cni.Title}')");
        }

        var runs = CniGrid.Render(cni, page, seat.Session);

        var c = GetCompositor(seat.PageName);
        c.Clear();

        // Off by default, the compositor renders lowercase as small uppercase. The CNI draws
        // real mixed case on some pages, so ask for it — a fresh compositor each tick means
        // this has to be set every time.
        c.UseLowercaseFont();

        foreach (var run in runs)
        {
            if (run.Invert) c.Black().BGGreen();
            else c.Green().BGBlack();

            c.Line(run.Line)
             .Column(run.Column)
             .Small(run.Small)
             .Write(run.Text);
        }
    }

    private void RenderPlaceholder()
    {
        // Every page this panel holds, not just the one on screen: a page turned to before its
        // seat has sent anything would otherwise be blank rather than saying why.
        foreach (var seat in _seats.Views)
        {
            var c = GetCompositor(seat.PageName);
            c.Clear();

            // Write() establishes column 0 before Centered so it knows the line width.
            if (_schema is null)
            {
                c.Line(4).Small().White().Write("").Centered("CNI SCHEMA MISSING");
                c.Line(5).Small().White().Write("").Centered("REINSTALL THE BRIDGE");
            }
            else if (_exportReceiver is null)
            {
                c.Line(4).Small().White().Write("").Centered("LIVE EXPORT DISABLED");
                c.Line(5).Small().White().Write("").Centered("ENABLE IT IN OPTIONS");
            }
            else
            {
                c.Line(4).Small().White().Write("").Centered("WAITING FOR CNI DATA");
                c.Line(5).Small().White().Write("").Centered("CHECK wctrl-export.lua");
            }

            // Which seat this page is, so the turn is legible before any data has arrived.
            if (_seats.CanTurn)
                c.Line(7).Small().White().Write("").Centered(seat.Name.ToUpperInvariant());
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (CduDevice != null)
                CduDevice.KeyDown -= HandleKeyDown;

            // The receiver is shared across every listener that wants the feed (other CDUs
            // on the same or a different aircraft may still be reading it), so unsubscribe
            // rather than tearing the socket down.
            if (_exportReceiver != null)
                _exportReceiver.DataReceived -= OnLiveExportData;
            _exportReceiver = null;
        }
        base.Dispose(disposing);
    }
}
