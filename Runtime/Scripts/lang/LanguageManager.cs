﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shell.Protector
{
    public class LanguageManager
    {
        static LanguageManager instance = null;

        private Dictionary<string, Dictionary<string, string>> languageMap;

        public static LanguageManager GetInstance()
        {
            if (instance == null)
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
            { "Password", "비밀번호" },
            { "A password that you don't need to memorize. (max:", "이 비밀번호는 외울 필요 없습니다. (최대 자릿수:" },
            { "This password should be memorized. (max:", "이 비밀번호는 외워야 합니다. (최대 자릿수:" },
            { "Generate", "자동 생성" },
            { "Options", "옵션" },
            { "Encrytion algorithm", "암호 알고리즘" },
            { "Default texture filter", "기본 텍스쳐 필터" },
            { "Encrypt!", "암호화 시작!" },
            { "Debug", "디버그" },
            { "XXTEA test", "XXTEA 테스트" },
            { "Encrypt", "암호화" },
            { "Languages: ", "언어: "},
            { "If it looks like its original appearance when pressed, it's a success.", "눌렀을 때 원본과 같으면 성공입니다." },
            { "Check encryption success", "암호화 성공 체크" },
            { "Press it before uploading.", "업로드 전에 누르세요."},
            { "Done & Reset", "완료 & 리셋"},
            { "Max password length", "최대 비밀번호 길이" },
            { "0 (Minimal security)", "0 (최소한의 보안)" },
            { "4 (Low security)", "4 (낮은 보안)" },
            { "8 (Middle security)", "8 (중간 보안)" },
            { "12 (Hight security)", "12 (높은 보안)" },
            { "16 (Unbreakable security)", "16 (뚫을 수 없는 보안)" },
            { "Parameters to be used:", "사용 예정 파라미터:" },
            { "Free parameter:", "여유 파라미터:" },
            { "Not enough parameter space!", "파라미터 공간이 부족합니다!" },
            { "It's okay for the 0-digit password to be the same as the original.", "0자리 비밀번호는 원본과 외형이 같은 것이 정상입니다." },
            { "Show", "보기" },
            { "Initial animation speed", "초기 애니메이션 속도" },
            { "Avatar first load animation speed", "아바타 로드시 나오는 애니메이션 속도" },
            { "Current version: ", "현재 버전: " },
            { "Lastest version: ", "최신 버전: " },
            { "Delete folders that already exists when at creation time", "생성시 이미 존재하는 폴더 삭제" },
            { "parameter-multiplexing", "파라미터 멀티플렉싱"},
            { "The OSC program must always be on, but it consumes fewer parameters.", "OSC프로그램을 상시로 켜둬야하지만, 더 적은 파라미터만 사용합니다." },
            { "Rounds", "라운드 수"},
            { "Number of encryption iterations. Higher values provide better security, but at the expense of performance.", "암호화 반복 횟수. 값이 높을 수록 보안이 좋아지나 성능이 저하 됩니다." },
            { "Setting it to 'Point' may result in aliasing, but performance is better.", "Point로 설정시 계단현상이 생길 수 있으나 성능이 좋아집니다." },
            { "Small mip texture", "작은 밉 텍스쳐" },
            { "It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side.", "작은 밉 텍스쳐를 사용하여 메모리 사용량을 줄이고 성능을 개선합니다. 옆에서 봤을 때 원본과 약간 다르게 보일 수 있습니다."},
            { "Object list", "오브젝트 목록" },
            { "Manual Encrypt!", "수동 암호화 시작!" },
            { "Modular avatars exist. It is automatically encrypted on upload.", "모듈러 아바타가 존재합니다. 업로드 시 자동으로 암호화됩니다." },
            { "Force progress", "강제 진행" },
            { "Obfustactor Options", "난독화 옵션" },
            { "BlendShape obfuscation", "쉐이프키 난독화" },
            { "Preserve MMD BlendShapes", "MMD 쉐이프키 보존" },
            { "Off fallback", "폴백 끄기" },
            { "On fallback", "폴백 켜기" },
            { "Fallback Options", "폴백 옵션" },
            { "Fallback wait time", "폴백 대기 시간" },
            { "After this time, the fallback is turned off. (Only who is Safety OFF)", "이 시간이 지나면 폴백 기능이 꺼집니다. (세이프티 꺼진 상대만)" },
            { "Change all Safety Fallback settings of shader to Unlit.", "셰이더의 모든 Safety Fallback설정을 Unlit으로 바꿉니다." },
            { "It looks strange, try restarting Unity and checking back.", "이상하게 보인다면 유니티를 재시작하고 다시 확인해보세요."},
            { "Delete previously encrypted files", "이전에 암호화 했던 파일들 삭제" },
            { "Material advanced settings", "메테리얼 상세 설정" },
            { "The main texture is empty.", "메인 텍스쳐가 비어있습니다." },
            { "The main texture is not supported format.", "메인 텍스쳐가 지원하지 않는 포멧입니다." },
            { "Not supported shader", "지원하지 않는 셰이더" },
            { "Encrypting too many objects can cause lag when loading avatars in-game.", "너무 많은 오브젝트를 암호화 하면 인게임에서 아바타 로딩 시 렉이 걸릴 수 있습니다." },
            { "Encrypted shader", "암호화된 셰이더" },
            { "New shader", "새로운 셰이더" },
            { "The main texture is not Texture2D.", "메인 텍스쳐가 Texture2D가 아닙니다." },
            { "Opponents with Safety option turned on will see degraded textures instead of noise.", "세이프티를 켜둔 상대방은 노이즈 대신 저하된 텍스처를 보게 됩니다."},
            { "Default fallback texture", "기본 폴백 텍스쳐" }
        };

            var jpStrings = new Dictionary<string, string>()
        {
            { "Material List", "マテリアル一覧" },
            { "Texture List", "テクスチャ一覧" },
            { "Directory", "アセットパス" },
            { "Decteced shaders:", "検出されたシェーダー:" },
            { "Password", "パスワード" },
            { "A password that you don't need to memorize. (max:", "このパスワードは暗記する必要はありません（最大桁数：" },
            { "This password should be memorized. (max:", "このパスワードは暗記する必要があります（最大桁数：" },
            { "Generate", "自動生成" },
            { "Options", "オプション" },
            { "Encrytion algorithm", "パスワードアルゴリズム" },
            { "Default texture filter", "基本テクスチャフィルター" },
            { "Encrypt!", "暗号化開始！" },
            { "Debug", "デバッグ" },
            { "XXTEA test", "XXTEA test" },
            { "Encrypt", "暗号化" },
            { "Languages: ", "言語: "},
            { "If it looks like its original appearance when pressed, it's a success.", "押したときにオリジナルと同じなら成功です。" },
            { "Check encryption success", "暗号化成功チェック" },
            { "Press it before uploading.", "アップロードする前に押してください。"},
            { "Done & Reset", "完了＆リセット"},
            { "Max password length", "最大パスワードの長さ" },
            { "0 (Minimal security)", "0 (最小限のセキュリティ)" },
            { "4 (Low security)", "4 (低セキュリティ)" },
            { "8 (Middle security)", "8 (中程度のセキュリティ)" },
            { "12 (Hight security)", "12 (高いセキュリティ)" },
            { "16 (Unbreakable security)", "16 (侵入不可能なセキュリティ)" },
            { "Parameters to be used:", "使用予定パラメータ:" },
            { "Free parameter:", "余裕パラメータ:" },
            { "Not enough parameter space!", "パラメータスペースが不足しています！" },
            { "It's okay for the 0-digit password to be the same as the original.", "0桁のパスワードは、オリジナルと見た目が同じであることが正常です。" },
            { "Show", "見る" },
            { "Initial animation speed", "初期アニメーション速度" },
            { "Avatar first load animation speed", "アバター読み込み時のアニメーション速度" },
            { "Current version: ", "現在のバージョン: " },
            { "Lastest version: ", "最新バージョン: " },
            { "Delete folders that already exists when at creation time", "作成時に既に存在するフォルダを削除" },
            { "parameter-multiplexing", "パラメータマルチプレクシング"},
            { "The OSC program must always be on, but it consumes fewer parameters.", "OSCプログラムを常時オンにしておく必要がありますが、より少ないパラメータを使用します。" },
            { "Rounds", "라운드 수"},
            { "Number of encryption iterations. Higher values provide better security, but at the expense of performance.", "暗号化の繰り返し回数。値が高いほどセキュリティは向上しますが、性能が低下します。" },
            { "Setting it to 'Point' may result in aliasing, but performance is better.", "Pointに設定すると階段現象が発生する可能性がありますが、性能が良くなります。" },
            { "Small mip texture", "小さなミップテクスチャ" },
            { "It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side.", "小さなミップテクスチャを使用して、メモリ使用量を減らし、パフォーマンスを向上させます。 横から見ると、オリジナルと少し違って見えるかもしれません。"},
            { "Object list", "オブジェクト一覧"},
            { "Manual Encrypt!", "手動暗号化開始！" },
            { "Modular avatars exist. it is automatically encrypted on upload.", "Modular Avatarが存在します。アップロード時に自動的に暗号化されます。" },
            { "Force progress", "強制的に進行" },
            { "Obfustactor Options", "難読化オプション" },
            { "BlendShape obfuscation", "BlendShape難読化" },
            { "Preserve MMD BlendShapes", "MMD BlendShapeを保存" },
            { "Off fallback", "フォールバックをオフにする" },
            { "On fallback", "フォールバックをオンにする" },
            { "Fallback Options", "フォールバックオプション" },
            { "Fallback wait time", "フォールバック待機時間" },
            { "After this time, the fallback is turned off. (Only who is Safety OFF)", "この時間が過ぎると、フォールバック機能がオフになります。( Safety off 相手のみ)" },
            { "Change all Safety Fallback settings of shader to Unlit.", "シェーダーのすべての Safety Fallback 設定を Unlit に変更します。" },
            { "It looks strange, try restarting Unity and checking back.", "おかしい場合は、Unityを再起動してもう一度確認してください。" },
            { "Delete previously encrypted files", "過去に暗号化されたファイルを削除する" },
            { "Material advanced settings", "マテリアルの詳細設定" },
            { "The main texture is empty.", "mainTextureが空です。" },
            { "The main texture is not supported format.", "mainTextureがサポートしていないフォーマットです。" },
            { "Not supported shader", "対応していないシェーダー" },
            { "Encrypting too many objects can cause lag when loading avatars in-game.", "あまりにも多くのオブジェクトを暗号化すると、ゲーム内でアバターをロードする際にラグが発生する可能性があります。" },
            { "Encrypted shader", "暗号化されたシェーダー" },
            { "New shader", "新しいシェーダー" },
            {"The main texture is not Texture2D.", "メインテクスチャがTexture2Dではありません。" },
            { "Opponents with Safety option turned on will see degraded textures instead of noise.", "Safetyオプションをオンにした相手には、ノイズの代わりに劣化したテクスチャが表示されます。"},
            { "Default fallback texture", "デフォルトのフォールバックテクスチャ" }
        };

            languageMap.Add("kor", koreanStrings);
            languageMap.Add("jp", jpStrings);
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
}
