using System.Diagnostics;
using System.Runtime;

namespace PersonalProvocateur.App.WindowsInterop;

/// <summary>
/// Provides proactive memory management routines to keep background idle working set minimal.
/// Compiles LOH, forces blocking compaction, and releases process working set to Windows.
/// </summary>
public static class MemoryOptimizer
{
    /// <summary>
    /// Forces comprehensive garbage collection with LOH compaction and trims process working set pages.
    /// Safe to call anywhere; swallows exceptions in non-supported or restricted environments.
    /// </summary>
    public static void TrimProcessMemory()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            var process = Process.GetCurrentProcess();
            NativeMethods.EmptyWorkingSet(process.Handle);
            NativeMethods.SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch
        {
            // Игнорируем ошибки при отсутствии прав или вызове в несовместимом окружении
        }
    }
}
