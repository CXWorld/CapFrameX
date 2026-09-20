using CapFrameX.Service.Capture;

namespace CapFrameX.Service.Capture.Tests.Configuration;

/// <summary>
/// The command line is the whole contract with PresentMon: a missing switch silently removes a
/// column, and a malformed one makes PresentMon refuse to start at all. Both failures look like
/// "capture is broken" from the outside, so the arguments are pinned here.
/// </summary>
public sealed class PresentMonArgumentsTests
{
    private static string Build(params string[] excluded)
    {
        var configuration = new PresentMonServiceConfiguration
        {
            ExcludeProcesses = [.. excluded],
        };

        configuration.BuildArguments();
        return configuration.Arguments;
    }

    [Theory]
    [InlineData("--stop_existing_session")]
    [InlineData("--output_stdout")]
    [InlineData("--no_track_input")]
    [InlineData("--qpc_time_ms")]
    [InlineData("--track_pc_latency")]
    public void Carries_the_switches_the_capture_pipeline_relies_on(string expected)
    {
        Assert.Contains(expected, Build());
    }

    [Fact]
    public void Tracks_frame_type_so_generated_frames_can_be_told_apart()
    {
        // Without this PresentMon omits the FrameType column entirely, and frame generation
        // becomes invisible to the analysis.
        Assert.Contains("--track_frame_type", Build());
    }

    [Fact]
    public void Tracks_app_timing()
    {
        Assert.Contains("--track_app_timing", Build());
    }

    [Fact]
    public void Sets_the_present_event_buffer_to_the_size_CapFrameX_has_always_used()
    {
        // PresentMon's own default is 2048; CapFrameX runs 4096. The value must be a power of two
        // or PresentMon rejects the whole command line.
        Assert.Contains("--set_circular_buffer_size 4096", Build());
    }

    [Fact]
    public void Does_not_ask_PresentMon_to_restart_itself_elevated()
    {
        // The service is always elevated, so the switch buys nothing - and a PresentMon that
        // re-executes itself takes its redirected stdout with it, which is the capture stream.
        Assert.DoesNotContain("--restart_as_admin", Build());
    }

    [Fact]
    public void Excluded_processes_are_passed_with_an_exe_suffix()
    {
        var arguments = Build("dwm", "explorer.exe");

        Assert.Contains("--exclude dwm.exe", arguments);
        Assert.Contains("--exclude explorer.exe", arguments);
    }

    [Fact]
    public void Process_name_with_a_space_is_skipped_rather_than_breaking_the_command_line()
    {
        var arguments = Build("Some Game");

        Assert.DoesNotContain("Some Game", arguments);
    }

    [Fact]
    public void Arguments_are_separated_by_single_spaces()
    {
        var arguments = Build("dwm");

        Assert.DoesNotContain("  ", arguments);
        Assert.Equal(arguments.Trim(), arguments);
    }

    [Fact]
    public void Without_the_output_stream_there_are_no_arguments()
    {
        var configuration = new PresentMonServiceConfiguration { EnableOutputStream = false };

        configuration.BuildArguments();

        Assert.Equal(string.Empty, configuration.Arguments);
    }
}
