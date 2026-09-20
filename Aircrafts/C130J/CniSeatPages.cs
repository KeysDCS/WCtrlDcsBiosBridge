namespace WCtrlDcsBiosBridge.Aircrafts.C130J;

/// <summary>
/// One seat's screen: the page it draws on, the layout its last packet matched, and what has
/// been worked out about its highlights.
///
/// All three are the seat's own and none of them travels. The session map is keyed on element
/// identifiers the sim regenerates per indicator, so the pilot's answers mean nothing on the
/// copilot's page even where the two show the same page.
/// </summary>
internal sealed class CniSeatView
{
    public CniSeatView(string name, string pageName)
    {
        Name = name;
        PageName = pageName;
    }

    /// <summary>The seat as wctrl-export.lua names it, for the log and the placeholder.</summary>
    public string Name { get; }

    /// <summary>The compositor page this seat is drawn on.</summary>
    public string PageName { get; }

    public CniSessionMap Session { get; } = new();

    /// <summary>
    /// Last page identified, so an unrecognised packet does not blank a screen that was
    /// showing something valid a moment ago.
    /// </summary>
    public CniPage? Page { get; set; }
}

/// <summary>
/// The seats one panel holds, and the order it turns through them.
///
/// The aircraft cannot say where the crew is sitting — pilot and copilot share a single camera
/// point in the module's Views-30.lua and no cockpit state moves between them — so which seat a
/// panel shows is never read, only decided. With a second CDU present it is decided once, at
/// the seat selection screen, and each panel then holds the one seat it was given. A lone panel
/// holds every seat the feed carries and turns through them on a key: the sim cannot say where
/// the crew is, but the crew can, and that hand is already on the keyboard.
///
/// Turning is a cycle rather than a two-way switch, which costs nothing here and keeps a third
/// seat out of the logic: the export carries the pilot's CNI and the copilot's, and adding the
/// augmented crew's would be a matter of what is handed to <see cref="ForEverySeat"/>.
/// </summary>
internal sealed class CniSeatPages
{
    /// <summary>The seat names wctrl-export.lua puts on a packet.</summary>
    public const string Pilot = "pilot";
    public const string Copilot = "copilot";

    private readonly Dictionary<string, CniSeatView> _bySeat =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Turn order, which is the order the seats were given.</summary>
    private readonly List<CniSeatView> _order = new();

    private CniSeatPages(IEnumerable<CniSeatView> views)
    {
        foreach (var view in views)
        {
            _bySeat[view.Name] = view;
            _order.Add(view);
        }
    }

    /// <summary>
    /// Every seat on its own page, turned through by key in the order given. The first is the
    /// one a panel starts on, and the caller names it the default page so that a panel showing
    /// the pilot's CNI renders exactly what it rendered before a second page existed.
    /// </summary>
    public static CniSeatPages ForEverySeat(params (string Seat, string Page)[] seats) =>
        new(seats.Select(s => new CniSeatView(s.Seat, s.Page)));

    /// <summary>
    /// The one seat this panel was given, on the default page. The other seat's packets are
    /// then another panel's and are dropped.
    /// </summary>
    public static CniSeatPages ForOneSeat(bool pilot, string page) =>
        new(new[] { new CniSeatView(pilot ? Pilot : Copilot, page) });

    /// <summary>Whether this panel has more than one seat to turn between.</summary>
    public bool CanTurn => _order.Count > 1;

    public IReadOnlyList<CniSeatView> Views => _order;

    /// <summary>
    /// The seat a packet belongs to, or false when this panel does not hold it — which is the
    /// whole of the seat filter. A packet naming a seat nobody asked for is dropped the same
    /// way, so an export that grew a seat cannot draw it over one of these.
    /// </summary>
    public bool TryGet(string? seat, out CniSeatView view)
    {
        view = null!;
        return seat is not null && _bySeat.TryGetValue(seat, out view!);
    }

    /// <summary>
    /// The page after the one named, wrapping round. An unknown page — nothing has been shown
    /// yet, or the panel holds one seat — answers with the first, so a key press always lands
    /// somewhere this panel draws.
    /// </summary>
    public string Next(string currentPage)
    {
        var at = _order.FindIndex(v => v.PageName == currentPage);
        if (at < 0) return _order[0].PageName;
        return _order[(at + 1) % _order.Count].PageName;
    }
}
