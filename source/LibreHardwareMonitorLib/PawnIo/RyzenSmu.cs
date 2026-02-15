using System;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// Provides an interface to interact with the Ryzen System Management Unit (SMU) using PawnIo.
/// </summary>
public class RyzenSmu
{
    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(RyzenSmu).Assembly, $"{nameof(LibreHardwareMonitor)}.Resources.PawnIo.RyzenSMU.bin");

    /// <summary>
    /// Retrieves the version of the Ryzen SMU.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public uint GetSmuVersion()
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        uint version;

        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_get_smu_version", [], 1);
            version = (uint)outArray[0];
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }

        return version;
    }

    /// <summary>
    /// Retrieves the code name of the Ryzen SMU.
    /// </summary>
    /// <returns></returns>
    public long GetCodeName()
    {
        long[] outArray = _pawnIO.Execute("ioctl_get_code_name", [], 1);
        return outArray[0];
    }

    /// <summary>
    /// Reads the Power Management (PM) table from the Ryzen SMU. 
    /// </summary>
    /// <param name="size"></param>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public long[] ReadPmTable(int size)
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_pm_table", [], size);
            return outArray;
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    /// <summary>
    /// Updates the Power Management (PM) table in the Ryzen SMU.
    /// </summary>
    /// <exception cref="TimeoutException"></exception>
    public void UpdatePmTable()
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        try
        {
            _pawnIO.Execute("ioctl_update_pm_table", [], 0);
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    /// <summary>
    /// Resolves the base address of the Power Management (PM) table and retrieves the version of the PM table.
    /// </summary>
    /// <param name="version"></param>
    /// <param name="tableBase"></param>
    /// <exception cref="TimeoutException"></exception>
    public void ResolvePmTable(out uint version, out uint tableBase)
    {
        if (!Mutexes.WaitPciBus(5000))
            throw new TimeoutException("Timeout waiting for PCI bus mutex");

        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_resolve_pm_table", [], 2);
            version = (uint)outArray[0];
            tableBase = (uint)outArray[1];
        }
        finally
        {
            Mutexes.ReleasePciBus();
        }
    }

    /// <summary>
    /// Closes the PawnIo module and releases any resources associated with it.
    /// </summary>
    public void Close() => _pawnIO.Close();
}
