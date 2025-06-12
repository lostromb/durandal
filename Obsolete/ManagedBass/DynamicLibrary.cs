#if !WINDOWS_PHONE_APP

using System;
using System.Runtime.InteropServices;
using System.IO;

namespace ManagedBass
{
    static class DynamicLibrary
    {
        const string LinuxDllName = "libdl.so",
            MacDllName = "/usr/lib/libSystem.dylib",
            WinDllName = "kernel32.dll";

        const int RtldNow = 2;

        [DllImport(LinuxDllName, EntryPoint = "dlopen")]
        static extern IntPtr LoadLinux(string FileName, int Flags = RtldNow);

        [DllImport(LinuxDllName, EntryPoint = "dlclose")]
        static extern bool UnloadLinux(IntPtr hLib);

        [DllImport(MacDllName, EntryPoint = "dlopen")]
        static extern IntPtr LoadMac(string FileName, int Flags = RtldNow);
        
        [DllImport(MacDllName, EntryPoint = "dlclose")]
        static extern bool UnloadMac(IntPtr hLib);
        
        [DllImport(WinDllName, EntryPoint = "LoadLibrary")]
        static extern IntPtr LoadWin(string DllToLoad);

        [DllImport(WinDllName, EntryPoint = "FreeLibrary")]
        static extern bool UnloadWin(IntPtr hLib);

        static string GetPath(string FileName, string Folder)
        {
            return !string.IsNullOrWhiteSpace(Folder) ? Path.Combine(Folder, FileName) : FileName;
        }

        public static IntPtr Load(string DllName, string Folder)
        {
            try { return LoadWin(GetPath($"{DllName}.dll", Folder)); }
            catch
            {
                try { return LoadLinux(GetPath($"lib{DllName}.so", Folder)); }
                catch
                {
                    try { return LoadMac(GetPath($"lib{DllName}.dylib", Folder)); }
                    catch { return IntPtr.Zero; }
                }
            }
        }

        public static bool Unload(IntPtr hLib)
        {
            try { return UnloadWin(hLib); }
            catch
            {
                try { return UnloadLinux(hLib); }
                catch
                {
                    try { return UnloadMac(hLib); }
                    catch { return false; }
                }
            }
        }
    }
}

#endif