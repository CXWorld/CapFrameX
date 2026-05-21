using CapFrameX.Service.Monitoring.Hardware;

namespace CapFrameX.Service.Monitoring.PawnIo;

/// <summary>
/// Provides methods to read Intel Model-Specific Registers (MSRs) using PawnIo.
/// </summary>
public class IntelMsr
{
    private readonly long[] _inArray = new long[1];
    private readonly long[] _writeArray = new long[2];
    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(IntelMsr).Assembly, "CapFrameX.Service.Monitoring.Resources.PawnIo.IntelMSR.bin");

    /// <summary>
    /// Reads the value of a specified MSR.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool ReadMsr(uint index, out ulong value)
    {
        _inArray[0] = index;
        value = 0;
        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_msr", _inArray, 1);
            value = (ulong)outArray[0];
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the value of a specified MSR.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="eax"></param>
    /// <param name="edx"></param>
    /// <returns></returns>
    public bool ReadMsr(uint index, out uint eax, out uint edx)
    {
        _inArray[0] = index;
        eax = 0;
        edx = 0;
        try
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_msr", _inArray, 1);
            eax = (uint)outArray[0];
            edx = (uint)(outArray[0] >> 32);
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the value of a specified MSR on a specific processor group and index.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="eax"></param>
    /// <param name="edx"></param>
    /// <param name="affinity"></param>
    /// <returns></returns>
    public bool ReadMsr(uint index, out uint eax, out uint edx, GroupAffinity affinity)
    {
        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
        bool result = ReadMsr(index, out eax, out edx);
        ThreadAffinity.Set(previousAffinity);
        return result;
    }

    /// <summary>
    /// Writes a 64-bit value to the specified MSR. The MSR must be on the
    /// underlying PawnIo module's write allow-list (see IntelMSR.p
    /// is_allowed_msr_write).
    /// </summary>
    /// <param name="index">MSR index.</param>
    /// <param name="value">64-bit value to write.</param>
    /// <returns>true on success.</returns>
    public bool WriteMsr(uint index, ulong value)
    {
        _writeArray[0] = index;
        _writeArray[1] = unchecked((long)value);
        try
        {
            _pawnIO.Execute("ioctl_write_msr", _writeArray, 0);
        }
        catch
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes a 64-bit value to the specified MSR on a specific processor.
    /// </summary>
    public bool WriteMsr(uint index, ulong value, GroupAffinity affinity)
    {
        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
        bool result = WriteMsr(index, value);
        ThreadAffinity.Set(previousAffinity);
        return result;
    }

    /// <summary>
    /// Closes the PawnIo module.
    /// </summary>
    public void Close() => _pawnIO.Close();
}
