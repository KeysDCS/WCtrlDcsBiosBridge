using WCtrlDcsBiosBridge.Aircrafts.C130J;
using WCtrlDcsBiosBridge.Config;
using WwDevicesDotNet;
using Xunit;

namespace WCtrlDcsBiosBridge.Tests;

/// <summary>
/// Which seat a C-130J panel shows. The aircraft cannot say where the crew is sitting, so this
/// is decided rather than read: given once at the seat selection screen when a second CDU is
/// present, turned through by key when a panel is on its own.
///
/// The page names here stand in for the listener's own — the class is told them rather than
/// knowing them, so that the pilot keeps whatever page the panel rendered before there was a
/// second one.
/// </summary>
public class CniSeatPagesTests
{
    private const string PilotPage = "default";
    private const string CopilotPage = "COPILOT";

    private static CniSeatPages BothSeats() =>
        CniSeatPages.ForEverySeat((CniSeatPages.Pilot, PilotPage),
                                  (CniSeatPages.Copilot, CopilotPage));

    [Fact]
    public void EachSeatDrawsOnItsOwnPage()
    {
        var seats = BothSeats();

        Assert.True(seats.TryGet(CniSeatPages.Pilot, out var pilot));
        Assert.True(seats.TryGet(CniSeatPages.Copilot, out var copilot));
        Assert.Equal(PilotPage, pilot.PageName);
        Assert.Equal(CopilotPage, copilot.PageName);
    }

    /// <summary>
    /// The feed names the seat in lower case, but nothing in the protocol promises it, and the
    /// filter this replaced compared with OrdinalIgnoreCase.
    /// </summary>
    [Theory]
    [InlineData("PILOT")]
    [InlineData("Pilot")]
    [InlineData("pilot")]
    public void TheSeatNameIsMatchedWhateverItsCase(string seat) =>
        Assert.True(BothSeats().TryGet(seat, out _));

    /// <summary>
    /// A packet for a seat this panel does not hold is another panel's. This is the whole of
    /// the seat filter, and it also covers a seat the export might grow later: the augmented
    /// crew's CNI is not carried today, and if it were it must not draw over one of these.
    /// </summary>
    [Theory]
    [InlineData("copilot")]
    [InlineData("augcrew")]
    [InlineData("")]
    [InlineData(null)]
    public void APacketForASeatNotHeldIsDropped(string? seat)
    {
        var seats = CniSeatPages.ForOneSeat(pilot: true, PilotPage);

        Assert.False(seats.TryGet(seat, out _));
    }

    [Fact]
    public void AnAssignedSeatDrawsOnTheDefaultPage()
    {
        var copilotOnly = CniSeatPages.ForOneSeat(pilot: false, PilotPage);

        Assert.True(copilotOnly.TryGet(CniSeatPages.Copilot, out var view));
        Assert.Equal(PilotPage, view.PageName);
        Assert.False(copilotOnly.TryGet(CniSeatPages.Pilot, out _));
    }

    /// <summary>
    /// A panel that was given a seat has nowhere to turn to, so the key is never subscribed
    /// and the other seat stays on the CDU it was given to.
    /// </summary>
    [Fact]
    public void AnAssignedSeatDoesNotTurn()
    {
        Assert.False(CniSeatPages.ForOneSeat(pilot: true, PilotPage).CanTurn);
        Assert.True(BothSeats().CanTurn);
    }

    [Fact]
    public void TurningGoesRoundAndComesBack()
    {
        var seats = BothSeats();

        Assert.Equal(CopilotPage, seats.Next(PilotPage));
        Assert.Equal(PilotPage, seats.Next(CopilotPage));
    }

    /// <summary>
    /// Turning is a cycle rather than a two-way switch, so a third seat would be a matter of
    /// what is handed in rather than of the logic here. Nothing carries one today — the export
    /// resolves the pilot's CNI and the copilot's and drops the augmented crew's — so this
    /// stands for the shape rather than for a feature.
    /// </summary>
    [Fact]
    public void TurningCyclesHoweverManySeatsThereAre()
    {
        var three = CniSeatPages.ForEverySeat((CniSeatPages.Pilot, PilotPage),
                                              (CniSeatPages.Copilot, CopilotPage),
                                              ("augcrew", "AUGCREW"));

        Assert.Equal(CopilotPage, three.Next(PilotPage));
        Assert.Equal("AUGCREW", three.Next(CopilotPage));
        Assert.Equal(PilotPage, three.Next("AUGCREW"));
    }

    /// <summary>
    /// The listener turns from whatever page it is on, and on the very first press that may be
    /// a page no seat owns. Answering with the first seat keeps the press from doing nothing.
    /// </summary>
    [Fact]
    public void TurningFromAPageNoSeatOwnsLandsOnTheFirst() =>
        Assert.Equal(PilotPage, BothSeats().Next("a page from another aircraft"));

    /// <summary>
    /// Every seat keeps its own highlight answers. They are keyed on element identifiers the
    /// sim regenerates per indicator, so one seat's mean nothing on another's page — sharing
    /// a map would let the pilot's answers decide the copilot's highlights.
    /// </summary>
    [Fact]
    public void EachSeatKeepsItsOwnSessionMap()
    {
        var seats = BothSeats();

        seats.TryGet(CniSeatPages.Pilot, out var pilot);
        seats.TryGet(CniSeatPages.Copilot, out var copilot);

        Assert.NotSame(pilot.Session, copilot.Session);
    }

    /// <summary>
    /// The same view comes back every time, so a seat's resolved page and session map survive
    /// from one packet to the next.
    /// </summary>
    [Fact]
    public void ASeatKeepsTheSameViewAcrossLookups()
    {
        var seats = BothSeats();

        seats.TryGet(CniSeatPages.Pilot, out var first);
        seats.TryGet(CniSeatPages.Pilot, out var second);

        Assert.Same(first, second);
    }

    /// <summary>
    /// The default toggle key has to name a real key or the listener falls back to SP without
    /// saying so — and SP is what it is meant to be anyway. It is the one key a CNI page never
    /// needs, which is what makes it free for the DCS modifier that moves the keyboard's own
    /// output to the other CNI.
    /// </summary>
    [Fact]
    public void TheDefaultToggleKeyNamesARealKey()
    {
        Assert.True(Enum.TryParse<Key>(new C130JOptions().SeatToggleKey, out var key));
        Assert.Equal(Key.Space, key);
    }
}
