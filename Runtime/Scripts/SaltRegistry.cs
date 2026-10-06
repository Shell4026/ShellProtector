#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Shell.Protector
{
    // Lists the salts of built avatars in LocalLow/ShellProtector/salts.txt, one "SP_SALT_<salt>" per line.
    // The OSC app reads it next to VRChat's OSC configs, so avatars that VRChat has never loaded
    // (for example in Gesture Manager before the first upload) still get their key. Salts are not secret.
    public static class SaltRegistry
    {
        static readonly Guid LocalAppDataLow = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);

        // Overrides the file builds write to. Tests point it away from the user's real file.
        public static string FileOverride { get; set; }

        public static string GetDefaultFile()
        {
            return Path.Combine(GetLocalLowDir(), "ShellProtector", "salts.txt");
        }

        public static void Register(string salt)
        {
            try
            {
                Register(salt, FileOverride ?? GetDefaultFile());
            }
            catch (Exception e)
            {
                // The avatar still works in VRChat, so this must not fail the build
                Debug.LogWarning("[ShellProtector] Failed to record the parameter salt for the OSC app: " + e.Message);
            }
        }


        public static void Register(string salt, string file)
        {
            if (!UserKey.IsValidSalt(salt))
                throw new ArgumentException("Invalid salt.", nameof(salt));

            string line = UserKey.SaltParameterPrefix + salt;
            if (File.Exists(file) && File.ReadAllLines(file).Any(l => l.Trim() == line))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.AppendAllText(file, line + Environment.NewLine);
        }

        static string GetLocalLowDir()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor &&
                SHGetKnownFolderPath(LocalAppDataLow, 0, IntPtr.Zero, out IntPtr path) == 0)
            {
                try
                {
                    return Marshal.PtrToStringUni(path);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(path);
                }
            }
            // LocalLow sits next to Local
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(Path.GetDirectoryName(local), "LocalLow");
        }
    }
}
#endif
