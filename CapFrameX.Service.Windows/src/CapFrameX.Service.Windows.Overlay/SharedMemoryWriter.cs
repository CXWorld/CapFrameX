using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using CapFrameX.Service.Overlay.Interop;

namespace CapFrameX.Service.Overlay;

/// <summary>
/// Writes frame data to the RTSS OverlayEditor shared memory
/// </summary>
public sealed class SharedMemoryWriter : IDisposable
{
    private MemoryMappedFile? _memoryMappedFile;
    private MemoryMappedViewAccessor? _accessor;
    private bool _disposed;
    private uint _frameCount;
    private uint _framePos;

    private static readonly int HeaderSize = Marshal.SizeOf<PmdpSharedMemoryHeader>();
    private static readonly int FrameDataSize = Marshal.SizeOf<PmFrameData>();
    private static readonly int TotalSize = HeaderSize + (FrameDataSize * PmdpConstants.FrameArraySize);

    public bool IsInitialized => _memoryMappedFile != null;

    public void Initialize()
    {
        if (_memoryMappedFile != null)
            return;

        try
        {
            // Create the shared memory section
            _memoryMappedFile = MemoryMappedFile.CreateOrOpen(
                PmdpConstants.SharedMemoryName,
                TotalSize,
                MemoryMappedFileAccess.ReadWrite);

            _accessor = _memoryMappedFile.CreateViewAccessor(0, TotalSize, MemoryMappedFileAccess.ReadWrite);

            // Initialize the header
            var header = new PmdpSharedMemoryHeader
            {
                Signature = PmdpSignature.Valid,
                Version = PmdpVersion.V1_0,
                FrameArrEntrySize = (uint)FrameDataSize,
                FrameArrOffset = (uint)HeaderSize,
                FrameArrSize = PmdpConstants.FrameArraySize,
                FrameCount = 0,
                FramePos = 0,
                Status = PmdpStatus.Ok
            };

            _accessor.Write(0, ref header);

            _frameCount = 0;
            _framePos = 0;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void WriteFrame(ref PmFrameData frameData)
    {
        if (_accessor == null || _disposed)
            return;

        // Write frame data to the ring buffer
        var frameOffset = HeaderSize + ((int)_framePos * FrameDataSize);
        _accessor.Write(frameOffset, ref frameData);

        // Update position (ring buffer wraps around)
        _framePos = (_framePos + 1) % PmdpConstants.FrameArraySize;
        _frameCount++;

        // Update header with new counts
        _accessor.Write(
            (long)Marshal.OffsetOf<PmdpSharedMemoryHeader>(nameof(PmdpSharedMemoryHeader.FrameCount)),
            _frameCount);
        _accessor.Write(
            (long)Marshal.OffsetOf<PmdpSharedMemoryHeader>(nameof(PmdpSharedMemoryHeader.FramePos)),
            _framePos);
    }

    public void SetStatus(uint status)
    {
        if (_accessor == null || _disposed)
            return;

        _accessor.Write(
            (long)Marshal.OffsetOf<PmdpSharedMemoryHeader>(nameof(PmdpSharedMemoryHeader.Status)),
            status);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Mark memory as dead before disposing
        if (_accessor != null)
        {
            try
            {
                _accessor.Write(
                    (long)Marshal.OffsetOf<PmdpSharedMemoryHeader>(nameof(PmdpSharedMemoryHeader.Signature)),
                    PmdpSignature.Dead);
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

        _accessor?.Dispose();
        _memoryMappedFile?.Dispose();
        _accessor = null;
        _memoryMappedFile = null;
    }
}
