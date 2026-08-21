using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CapFrameX.PMD.Benchlab
{
    internal sealed class ChildProcessJob : IDisposable
    {
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9;

        private readonly SafeFileHandle _jobHandle;

        private ChildProcessJob(SafeFileHandle jobHandle)
        {
            _jobHandle = jobHandle;
        }

        public static ChildProcessJob Attach(Process process)
        {
            var jobHandle = CreateJobObject(IntPtr.Zero, null);
            if (jobHandle == null || jobHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                ConfigureKillOnClose(jobHandle);

                if (!AssignProcessToJobObject(jobHandle, process.Handle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return new ChildProcessJob(jobHandle);
            }
            catch
            {
                jobHandle.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _jobHandle.Dispose();
        }

        private static void ConfigureKillOnClose(SafeFileHandle jobHandle)
        {
            var information = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            var informationLength = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
            var informationPointer = Marshal.AllocHGlobal(informationLength);
            try
            {
                Marshal.StructureToPtr(information, informationPointer, false);
                if (!SetInformationJobObject(
                    jobHandle,
                    JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
                    informationPointer,
                    (uint)informationLength))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(informationPointer);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr jobAttributes, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeFileHandle jobHandle,
            int jobObjectInformationClass,
            IntPtr jobObjectInformation,
            uint jobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(
            SafeFileHandle jobHandle,
            IntPtr processHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
