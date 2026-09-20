using CapFrameX.Service.Application.Settings;
using CapFrameX.Service.Contracts.Settings;

namespace CapFrameX.Service.Application.Tests.Settings;

/// <summary>
/// What a settings patch is allowed to say.
/// </summary>
/// <remarks>
/// Every value here reaches code with no defence of its own, and the failures are quiet rather than
/// loud: rounding digits outside what <c>Math.Round</c> accepts make the statistics provider return
/// nothing at all, and a stuttering factor of one reports an even capture as stuttering throughout.
/// </remarks>
public sealed class SettingsValidatorTests
{
    [Fact]
    public void An_empty_patch_leaves_everything_as_it_was()
    {
        var current = new PersistedSettings { StutteringFactor = 3, Theme = "dark" };

        var validation = SettingsValidator.Validate(current, new AppSettingsPatch());

        Assert.True(validation.IsValid);
        Assert.Equal(3, validation.Result!.StutteringFactor);
        Assert.Equal("dark", validation.Result.Theme);
    }

    [Fact]
    public void A_section_left_out_is_left_alone()
    {
        var current = new PersistedSettings { Theme = "dark" };
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(StutteringFactor: 3));

        var validation = SettingsValidator.Validate(current, patch);

        Assert.Equal("dark", validation.Result!.Theme);
        Assert.Equal(3, validation.Result.StutteringFactor);
    }

    [Fact]
    public void A_rejected_patch_leaves_the_current_settings_untouched()
    {
        // The caller keeps using the object it passed in, so it must not come back half-changed.
        var current = new PersistedSettings { StutteringFactor = 2.5 };
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(
            StutteringThreshold: 60,
            FpsValuesRoundingDigits: 99));

        var validation = SettingsValidator.Validate(current, patch);

        Assert.False(validation.IsValid);
        Assert.Null(validation.Result);
        Assert.Equal(25d, current.StutteringThreshold);
    }

    [Fact]
    public void Everything_wrong_with_a_patch_is_reported_at_once()
    {
        // Otherwise the user finds the faults one request at a time.
        var patch = new AppSettingsPatch(
            Analysis: new AnalysisSettingsPatch(StutteringFactor: 0, FpsValuesRoundingDigits: -1),
            Appearance: new AppearanceSettingsPatch(Theme: "neon"));

        var validation = SettingsValidator.Validate(new PersistedSettings(), patch);

        Assert.Equal(3, validation.Errors.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0.5)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    public void A_stuttering_factor_that_makes_every_frame_a_stutter_is_refused(double factor)
    {
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(StutteringFactor: factor));

        Assert.False(SettingsValidator.Validate(new PersistedSettings(), patch).IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void Rounding_digits_outside_what_Math_Round_accepts_are_refused(int digits)
    {
        // The provider catches the exception and returns NaN, so a metric would simply stop
        // appearing with no reason given.
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(FpsValuesRoundingDigits: digits));

        Assert.False(SettingsValidator.Validate(new PersistedSettings(), patch).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public void The_ends_of_the_rounding_range_are_allowed(int digits)
    {
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(FpsValuesRoundingDigits: digits));

        Assert.True(SettingsValidator.Validate(new PersistedSettings(), patch).IsValid);
    }

    [Fact]
    public void An_outlier_method_that_removes_nothing_is_refused()
    {
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(OutlierMethod: "ThreeSigma"));

        var validation = SettingsValidator.Validate(new PersistedSettings(), patch);

        Assert.False(validation.IsValid);
        Assert.Contains("DeciPercentile", Assert.Single(validation.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void A_metric_the_service_cannot_compute_is_refused()
    {
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(Metrics: ["average", "loudness"]));

        Assert.False(SettingsValidator.Validate(new PersistedSettings(), patch).IsValid);
    }

    [Fact]
    public void Metrics_are_stored_under_the_key_the_catalogue_uses()
    {
        // Whatever casing arrived, so the tile layout reads the same however it was set.
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(Metrics: ["P95", "AVERAGE"]));

        var validation = SettingsValidator.Validate(new PersistedSettings(), patch);

        Assert.Equal(["p95", "average"], validation.Result!.Metrics);
    }

    [Fact]
    public void An_empty_tile_row_means_the_default_one()
    {
        // A view with no tiles is broken rather than configured.
        var current = new PersistedSettings { Metrics = ["p95"] };
        var patch = new AppSettingsPatch(Analysis: new AnalysisSettingsPatch(Metrics: []));

        Assert.Empty(SettingsValidator.Validate(current, patch).Result!.Metrics);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("Light")]
    [InlineData("DARK")]
    public void A_known_theme_is_kept_in_one_casing(string theme)
    {
        var patch = new AppSettingsPatch(Appearance: new AppearanceSettingsPatch(theme));

        Assert.Equal(theme.ToLowerInvariant(), SettingsValidator.Validate(new PersistedSettings(), patch).Result!.Theme);
    }

    [Fact]
    public void A_theme_nobody_implemented_is_refused()
    {
        var patch = new AppSettingsPatch(Appearance: new AppearanceSettingsPatch("neon"));

        Assert.False(SettingsValidator.Validate(new PersistedSettings(), patch).IsValid);
    }

    [Fact]
    public void A_relative_capture_folder_is_refused()
    {
        // It would resolve against the service's working directory, which is not a place the user
        // can see or reason about.
        var patch = new AppSettingsPatch(Paths: new PathSettingsPatch("Captures"));

        Assert.False(SettingsValidator.Validate(new PersistedSettings(), patch).IsValid);
    }

    [Fact]
    public void A_capture_folder_is_created_rather_than_merely_accepted()
    {
        // The user is saying where captures will live; a folder that cannot be made is a setting
        // that would fail silently on every scan.
        var folder = Path.Combine(Path.GetTempPath(), "cfx-settings-" + Guid.NewGuid().ToString("N"), "Captures");

        try
        {
            var patch = new AppSettingsPatch(Paths: new PathSettingsPatch(folder));

            var validation = SettingsValidator.Validate(new PersistedSettings(), patch);

            Assert.True(validation.IsValid);
            Assert.True(Directory.Exists(folder));
            Assert.Equal(folder, validation.Result!.CaptureDirectory);
        }
        finally
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
            }
            catch (IOException)
            {
                // A temp folder that outlives the run is not a test failure.
            }
        }
    }

    [Fact]
    public void Clearing_the_capture_folder_hands_it_back_to_the_platform()
    {
        var current = new PersistedSettings { CaptureDirectory = @"C:\elsewhere" };
        var patch = new AppSettingsPatch(Paths: new PathSettingsPatch(string.Empty));

        Assert.Null(SettingsValidator.Validate(current, patch).Result!.CaptureDirectory);
    }
}
