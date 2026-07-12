#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Shell.Protector
{
    public class VersionManager : MonoBehaviour
    {
        string githubVersion = "";

        static VersionManager instance;
        const string githubUrl = "https://github.com/Shell4026/ShellProtector";
        const string versionUri = githubUrl + "/raw/main/version.json";

        public static VersionManager GetInstance()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("VersionManager");
                obj.hideFlags = HideFlags.HideAndDontSave;
                instance = obj.AddComponent<VersionManager>();
            }
            return instance;
        }

        private string[] ParseVersionJson(string data)
        {
            Match match = Regex.Match(data, "{ \"latestVersion\": \"(.*?)\", \"downloadPage\": \"(.*?)\" }");
            string version = "", downloadUri = "";
            if (match.Success)
            {
                version = match.Groups[1].Value;
                downloadUri = match.Groups[2].Value;
            }
            return new[] { version, downloadUri };
        }

        public string GetVersion()
        {
            MonoScript monoScript = MonoScript.FromMonoBehaviour(this);
            string scriptPath = Directory.GetParent(AssetDatabase.GetAssetPath(monoScript)).ToString();
            string dir = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath));

            string data = File.ReadAllText(Path.Combine(dir, "version.json"));
            string[] parse = ParseVersionJson(data);
            return parse[0];
        }

        public string GetGithubVersion()
        {
            return githubVersion;
        }

        public void Refresh()
        {
            StartCoroutine(LoadData());
        }

        private IEnumerator LoadData()
        {
            using (UnityWebRequest www = UnityWebRequest.Get(versionUri))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string jsonData = www.downloadHandler.text;
                    string[] parse = ParseVersionJson(jsonData);

                    githubVersion = parse[0];
                }
                else
                {
                    Debug.Log("[ShellProtector]version checking error: " + www.error);
                }
            }
        }
    }
}
#endif
