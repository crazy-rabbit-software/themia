using Themia.Core.SystemData;
using Xunit;

namespace Themia.Core.Tests;

public sealed class GpuDriverTests
{
    private const string Windows = @"C:\Windows";
    private const string System32 = @"C:\Windows\System32";
    private const string ProgramFiles = @"C:\Program Files";
    private const string Store = @"C:\Windows\System32\DriverStore\FileRepository\nv_dispig.inf_amd64_cc526ccc3287f5cd";

    // ---- [S-437] the library only while a GPU widget is shown ----------------------------------

    [Fact]
    public void S437_a_gpu_widget_only_in_a_hidden_view_never_opens_the_library()
    {
        var demand = new GpuDriverDemand();

        for (var sample = 0; sample < 5; sample++)
        {
            Assert.Equal(DriverStep.None, demand.Before(shown: false));
        }

        Assert.False(demand.IsOpen);
        Assert.Equal(DriverStep.None, demand.End());
    }

    [Fact]
    public void S437_a_shown_gpu_widget_opens_it_once_and_the_last_one_leaving_closes_it()
    {
        var demand = new GpuDriverDemand();

        Assert.Equal(DriverStep.Open, demand.Before(shown: true));
        demand.Opened(true);
        Assert.Equal(DriverStep.None, demand.Before(shown: true));
        Assert.Equal(DriverStep.None, demand.Before(shown: true));

        Assert.Equal(DriverStep.Close, demand.Before(shown: false));
        demand.Closed();
        Assert.Equal(DriverStep.None, demand.Before(shown: false));

        // Back on screen: opened again.
        Assert.Equal(DriverStep.Open, demand.Before(shown: true));
    }

    [Fact]
    public void S437_a_library_that_would_not_open_is_not_tried_every_sample_but_again_on_the_next_showing()
    {
        var demand = new GpuDriverDemand();

        Assert.Equal(DriverStep.Open, demand.Before(shown: true));
        demand.Opened(false);
        Assert.False(demand.IsOpen);
        Assert.Equal(DriverStep.None, demand.Before(shown: true));
        Assert.Equal(DriverStep.None, demand.Before(shown: true));

        // Nothing to close: it never opened.
        Assert.Equal(DriverStep.None, demand.Before(shown: false));
        Assert.Equal(DriverStep.Open, demand.Before(shown: true));
    }

    [Fact]
    public void S437_the_session_ending_closes_an_open_library_and_only_that()
    {
        var demand = new GpuDriverDemand();
        Assert.Equal(DriverStep.None, demand.End());

        demand.Before(shown: true);
        demand.Opened(true);
        Assert.Equal(DriverStep.Close, demand.End());
        demand.Closed();
        Assert.Equal(DriverStep.None, demand.End());
    }

    // ---- [S-437] from the driver's own folder, so freeing it unloads it ------------------------

    [Theory]
    [InlineData(@"\SystemRoot\System32\DriverStore\FileRepository\nv_dispig.inf_amd64_cc526ccc3287f5cd", Store)]
    [InlineData(@"\systemroot\System32\DriverStore\FileRepository\nv_dispig.inf_amd64_cc526ccc3287f5cd\", Store)]
    [InlineData(@"\??\C:\Windows\System32\DriverStore\FileRepository\nv_dispig.inf_amd64_cc526ccc3287f5cd", Store)]
    [InlineData(@"C:\Windows\System32\DriverStore\FileRepository\nv_dispig.inf_amd64_cc526ccc3287f5cd", Store)]
    public void S437_the_kernels_driver_store_path_becomes_a_win32_path(string kernel, string expected)
        => Assert.Equal(expected, GpuDriverLibrary.Win32Path(kernel, Windows + @"\"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"System32\DriverStore\FileRepository\nv")]
    [InlineData(@"\Device\HarddiskVolume3\Windows\System32\DriverStore")]
    [InlineData(@"\SystemRoot\System32\DriverStore\..\..\Temp")]
    [InlineData(@"\\server\share\nv")]
    public void S437_any_other_kernel_path_is_refused(string? kernel)
        => Assert.Null(GpuDriverLibrary.Win32Path(kernel, Windows));

    [Fact]
    public void S437_the_driver_store_comes_first_then_the_system32_loader_then_nvsmi()
    {
        var candidates = GpuDriverLibrary.Candidates([Store, Store.ToUpperInvariant(), " "], System32, ProgramFiles);

        Assert.Equal(
            [
                Store + @"\nvml.dll",
                System32 + @"\nvml.dll",
                @"C:\Program Files\NVIDIA Corporation\NVSMI\nvml.dll",
            ],
            candidates);
        Assert.True(GpuDriverLibrary.IsLoader(candidates[1], System32 + @"\"));
        Assert.False(GpuDriverLibrary.IsLoader(candidates[0], System32));
    }

    [Fact]
    public void S437_with_no_driver_store_named_the_loader_is_the_first_candidate()
        => Assert.Equal(System32 + @"\nvml.dll", GpuDriverLibrary.Candidates([], System32, ProgramFiles)[0]);
}
