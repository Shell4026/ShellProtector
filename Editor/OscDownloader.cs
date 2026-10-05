#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Shell.Protector
{
    // Downloads the latest ShellProtectorOSC release zip from GitHub.
    public static class OscDownloader
    {
        public const string ReleasesUrl = "https://github.com/Shell4026/ShellProtectorOSC/releases";
        const string latestReleaseApi = "https://api.github.com/repos/Shell4026/ShellProtectorOSC/releases/latest";

#pragma warning disable 0649 // assigned by JsonUtility
        [Serializable]
        class Release
        {
            public string tag_name;
            public Asset[] assets;
        }

        [Serializable]
        class Asset
        {
            public string name;
            public string browser_download_url;
        }
#pragma warning restore 0649

        static UnityWebRequest request;
        static string language = "eng";

        public static bool IsBusy => request != null;

        static string Lang(string word)
        {
            return LanguageManager.GetInstance().GetLang(language, word);
        }

        public static void DownloadLatest(string lang)
        {
            if (IsBusy)
                return;
            language = lang;

            request = UnityWebRequest.Get(latestReleaseApi);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "ShellProtector");
            request.SendWebRequest().completed += _ => OnReleaseInfo();
        }

        static void OnReleaseInfo()
        {
            UnityWebRequest req = request;
            request = null;
            using (req)
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Fail("[ShellProtector] OSC release check error: " + req.error);
                    return;
                }

                Release release = JsonUtility.FromJson<Release>(req.downloadHandler.text);
                Asset zip = null;
                if (release?.assets != null)
                    zip = Array.Find(release.assets, a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (zip == null)
                {
                    Fail("[ShellProtector] No zip file in the latest OSC release.");
                    return;
                }

                string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string path = EditorUtility.SaveFilePanel(Lang("Save ShellProtectorOSC"), Directory.Exists(downloads) ? downloads : "", zip.name, "zip");
                if (string.IsNullOrEmpty(path))
                    return;

                StartDownload(zip.browser_download_url, path, release.tag_name);
            }
        }

        static void StartDownload(string url, string path, string version)
        {
            request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            request.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
            request.SetRequestHeader("User-Agent", "ShellProtector");
            request.SendWebRequest();

            string title = "ShellProtectorOSC " + version;
            bool aborted = false;
            void Update()
            {
                if (!request.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(title, Lang("Downloading..."), request.downloadProgress))
                    {
                        aborted = true;
                        request.Abort();
                    }
                    return;
                }

                EditorApplication.update -= Update;
                EditorUtility.ClearProgressBar();
                UnityWebRequest req = request;
                request = null;
                using (req)
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log("[ShellProtector] OSC downloaded: " + path);
                        EditorUtility.RevealInFinder(path);
                    }
                    else if (!aborted)
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                        Fail("[ShellProtector] OSC download error: " + req.error);
                    }
                }
            }
            EditorApplication.update += Update;
        }

        static void Fail(string message)
        {
            Debug.LogWarning(message);
            if (EditorUtility.DisplayDialog("ShellProtector", Lang("Failed to download the OSC program. Open the releases page instead?"), Lang("Open"), Lang("Cancel")))
                Application.OpenURL(ReleasesUrl);
        }
    }
}
#endif
