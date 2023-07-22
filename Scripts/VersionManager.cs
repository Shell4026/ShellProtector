#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class VersionManager : MonoBehaviour
{
    string version = "";
    string download_uri = "";
    string github_version = "";

    static VersionManager instance;
    const string github_url = "https://github.com/Shell4026/ShellProtector";
    const string version_uri = github_url + "/raw/main/version.json";
    string raw_data;
    
    public static VersionManager GetInstance()
    {
        if(instance == null)
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
        string version = "", download_uri = "";
        if (match.Success)
        {
            version = match.Groups[1].Value;
            download_uri = match.Groups[2].Value;
        }
        return new[]{ version, download_uri };
    }
    public string GetVersion()
    {
        MonoScript monoScript = MonoScript.FromMonoBehaviour(this);
        string script_path = AssetDatabase.GetAssetPath(monoScript);
        string dir = Path.GetDirectoryName(Path.GetDirectoryName(script_path));

        string data = File.ReadAllText(Path.Combine(dir, "version.json"));
        string[] parse = ParseVersionJson(data);
        return parse[0];
    }
    public string GetGithubVersion()
    {
        return github_version;
    }
    public void Refresh()
    {
        StartCoroutine(LoadData());
    }
    private IEnumerator LoadData()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(version_uri))
        {
            yield return www.SendWebRequest();

            if (!www.isNetworkError)
            {
                string json_data = www.downloadHandler.text;
                string[] parse = ParseVersionJson(json_data);

                github_version = parse[0];
                download_uri = parse[1];
            }
            else
            {
                Debug.Log("[ShellProtector]version checking error: " + www.error);
            }
        }
    }
}
#endif