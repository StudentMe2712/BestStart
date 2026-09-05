using System.Runtime.InteropServices;

namespace UniversalMediaPlayer.Playback.Native;

public static unsafe class LibMpvNative
{
    private const string LibName = "libmpv-2.dll";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mpv_create();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(nint handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(nint handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(nint handle, byte** args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command_string(nint handle, byte* args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_option_string(nint handle, byte* name, byte* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_property_string(nint handle, byte* name, byte* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* mpv_get_property_string(nint handle, byte* name);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(void* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte* mpv_error_string(int error);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mpv_wait_event(nint handle, double timeout);
}
