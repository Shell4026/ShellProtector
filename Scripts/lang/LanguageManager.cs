using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LanguageManager
{
    static LanguageManager instance = null;

    private Dictionary<string, Dictionary<string, string>> languageMap;

    public static LanguageManager GetInstance()
    {
        if(instance == null)
            instance = new LanguageManager();
        return instance;
    }

    private LanguageManager()
    {
        languageMap = new Dictionary<string, Dictionary<string, string>>();
        var koreanStrings = new Dictionary<string, string>()
        {
            { "Material List", "메테리얼 목록" },
            { "Texture List", "텍스쳐 목록" },
            { "Directory", "에셋 경로" },
            { "Decteced shaders:", "검출된 셰이더:" },
            { "Password (max:12)", "비밀번호 (최대12)" },
            { "You don't need to memorize your password.", "비밀번호는 외울 필요 없습니다." },
            { "Options", "옵션" },
            { "Encrytion algorithm", "암호 알고리즘" },
            { "Texture filter", "텍스쳐 필터" },
            { "Encrypt!", "암호화 시작!" },
            { "Debug", "디버그" },
            { "XXTEA test", "XXTEA 테스트" },
            { "Encrypt", "암호화" },
            { "Languages: ", "언어: "}
        };
        languageMap.Add("kor", koreanStrings);
    }

    public string GetLang(string lang, string word)
    {
        if (lang == "eng")
            return word;
        if (languageMap.ContainsKey(lang))
        {
            var languageStrings = languageMap[lang];
            if (languageStrings.ContainsKey(word))
            {
                return languageStrings[word];
            }
        }

        return word;
    }
}
