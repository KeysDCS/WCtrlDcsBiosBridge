using WCtrlDcsBiosBridge.Aircrafts;
using Xunit;

namespace WCtrlDcsBiosBridge.Tests;

/// <summary>
/// The F-14B(U) is the F-14B plus a CDNU, and DCS-BIOS' F-14 module has covered the variant
/// since v0.11.7. What that leaves to pin down is the split: which module the descriptor
/// loads, that the variant is matched before the aircraft it is a variant of, and that it
/// lights what the F-14B lights rather than nothing.
/// </summary>
public class F14BURegistryTests
{
    [Fact]
    public void DetectedByItsDcsBiosName()
    {
        Assert.Same(AircraftRegistry.F14BU, AircraftRegistry.FindByDcsBiosName("F-14BU"));
    }

    /// <summary>
    /// Matching is on prefix and "F-14BU" also starts with "F-14B", so registry order decides
    /// this one: put the F-14B first and the variant is never detected at all.
    /// </summary>
    [Fact]
    public void MatchedBeforeThePlainF14B()
    {
        var all = AircraftRegistry.All.ToList();

        Assert.True(all.IndexOf(AircraftRegistry.F14BU) < all.IndexOf(AircraftRegistry.F14B));
        Assert.Same(AircraftRegistry.F14B, AircraftRegistry.FindByDcsBiosName("F-14B"));
    }

    [Fact]
    public void IsInTheRegistryUnderAnIdOfItsOwn()
    {
        Assert.Contains(AircraftRegistry.F14BU, AircraftRegistry.All);
        Assert.Same(AircraftRegistry.F14BU, AircraftRegistry.Find(AircraftRegistry.F14BU.ModuleId));
        Assert.NotEqual(AircraftRegistry.F14B.ModuleId, AircraftRegistry.F14BU.ModuleId);
    }

    /// <summary>
    /// DCS-BIOS files the variant under the F-14 module, so that is the module whose controls
    /// the locator has to load — the registry id is the bridge's own.
    /// </summary>
    [Fact]
    public void LoadsTheF14DcsBiosModule()
    {
        Assert.Equal(AircraftRegistry.F14B.ModuleId, AircraftRegistry.F14BU.EffectiveDcsBiosModuleId);
        Assert.Equal(AircraftRegistry.F14B.JsonFile, AircraftRegistry.F14BU.JsonFile);
        Assert.Contains(AircraftRegistry.F14BU.JsonFile, AircraftRegistry.ExpectedJsonFiles);
    }

    /// <summary>
    /// The one thing the variant does not share: the CDNU is written in mixed case, so it
    /// needs the font carrying real lowercase rather than the shared A-10C one.
    /// </summary>
    [Fact]
    public void CarriesItsOwnFont()
    {
        Assert.NotEqual(AircraftRegistry.F14B.FontFile, AircraftRegistry.F14BU.FontFile);
        Assert.Contains("f14bu", AircraftRegistry.F14BU.FontFile);
    }

    /// <summary>
    /// The defaults are declared once, against the DCS-BIOS module. A variant reading them off
    /// its own registry id would light nothing — which is what the F-14B(U) did for as long as
    /// DCS-BIOS exported nothing for it.
    /// </summary>
    [Fact]
    public void LightsWhatTheF14BLights()
    {
        var variant = LedDefaults.For(AircraftRegistry.F14BU);

        Assert.NotEmpty(variant.Signals);
        Assert.Equal(
            LedDefaults.For(AircraftRegistry.F14B).Signals.Select(s => s.Signal),
            variant.Signals.Select(s => s.Signal));
    }
}
