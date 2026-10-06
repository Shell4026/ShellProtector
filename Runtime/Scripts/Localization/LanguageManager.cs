using System.Collections;
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
            { "Encrypt emission", "이미션 암호화" },
            { "Unchecked materials are not encrypted. Emission maps are encrypted only for the checked slots.", "체크하지 않은 메테리얼은 암호화하지 않습니다. 이미션 맵은 체크한 슬롯만 암호화합니다." },
            { "Encrypt this material", "이 메테리얼을 암호화합니다" },
            { "A shader injected before is reused.", "이전에 만든 셰이더를 재사용합니다." },
            { "The shader is injected and compiled on this build.", "이번 빌드에서 셰이더를 새로 만들고 컴파일합니다." },
            { "Material", "메테리얼" },
            { "Main texture", "메인 텍스쳐" },
            { "Texture filter", "텍스쳐 필터" },
            { "Fallback texture", "폴백 텍스쳐" },
            { "Selected emission maps emit no light until the correct password is entered. Masks and gradients are not encrypted.", "선택한 이미션 맵은 올바른 비밀번호를 입력하기 전까지 발광하지 않습니다. 마스크와 그라데이션은 암호화하지 않습니다." },
            { "No texture in this slot.", "이 슬롯에 텍스쳐가 없습니다." },
            { "Emission maps must be power-of-two RGB24, RGBA32, DXT1 or DXT5 textures (DXT: at least 8x4).", "이미션 맵은 2의 거듭제곱 크기의 RGB24, RGBA32, DXT1, DXT5 텍스쳐여야 합니다 (DXT는 최소 8x4)." },
            { "Reset", "초기화" },
            { "Filter, fallback and emission encryption per material.", "메테리얼별 필터, 폴백, 이미션 암호화 설정입니다." },
            { "Emission encryption: {0} maps in {1} materials", "이미션 암호화: 메테리얼 {1}개, 맵 {0}개" },
            { "Texture List", "텍스쳐 목록" },
            { "Password", "비밀번호" },
            { "Default texture filter", "기본 텍스쳐 필터" },
            { "Encrypt!", "암호화 시작!" },
            { "Debug", "디버그" },
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
            { "Not enough parameter space!", "파라미터 공간이 부족합니다!" },
            { "It's okay for the 0-digit password to be the same as the original.", "0자리 비밀번호는 원본과 외형이 같은 것이 정상입니다." },
            { "Show", "보기" },
            { "Releases page", "릴리즈 페이지" },
            { "Download latest OSC", "최신 OSC 다운로드" },
            { "Save ShellProtectorOSC", "ShellProtectorOSC 저장" },
            { "Downloading...", "다운로드 중..." },
            { "Failed to download the OSC program. Open the releases page instead?", "OSC 프로그램 다운로드에 실패했습니다. 대신 릴리즈 페이지를 열까요?" },
            { "Open", "열기" },
            { "Cancel", "취소" },
            { "Delete folders that already exists when at creation time", "생성시 이미 존재하는 폴더 삭제" },
            { "Setting it to 'Point' may result in aliasing, but performance is better.", "Point로 설정시 계단현상이 생길 수 있으나 성능이 좋아집니다." },
            { "Small mip texture", "작은 밉 텍스쳐" },
            { "It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side.", "작은 밉 텍스쳐를 사용하여 메모리 사용량을 줄이고 성능을 개선합니다. 옆에서 봤을 때 원본과 약간 다르게 보일 수 있습니다."},
            { "Object list", "오브젝트 목록" },
            { "Manual Encrypt! (for testing)", "수동 암호화 시작! (테스트용)" },
            { "Modular avatars exist. It is automatically encrypted on upload.", "모듈러 아바타가 존재합니다. 업로드 시 자동으로 암호화됩니다." },
            { "Force progress", "강제 진행" },
            { "BlendShape obfuscation", "쉐이프키 난독화" },
            { "Preserve MMD BlendShapes", "MMD 쉐이프키 보존" },
            { "Fallback Options", "폴백 옵션" },
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
            { "Default fallback texture", "기본 폴백 텍스쳐" },
            { "Number of key bytes synced at once. At 2 or higher the key syncs faster and is saved in the avatar, so the OSC program only has to run once, but more parameters are used.", "한 번에 동기화하는 키 바이트 수입니다. 2 이상이면 키가 더 빨리 동기화되고 아바타에 저장되어 OSC 프로그램을 한 번만 실행하면 되지만, 파라미터를 더 사용합니다." },
            { "A new version is available: ", "새 버전이 있습니다: " },
            { "Avatar", "아바타" },
            { "Assign the avatar to encrypt.", "암호화할 아바타를 지정해주세요." },
            { "No supported shader (lilToon, Poiyomi) was found in the project.", "프로젝트에서 지원하는 셰이더(lilToon, Poiyomi)를 찾을 수 없습니다." },
            { "Detected: ", "감지됨: " },
            { "Encryption targets", "암호화 대상" },
            { "Add the objects or materials to encrypt.", "암호화할 오브젝트나 메테리얼을 추가해주세요." },
            { "Add Body", "Body 추가" },
            { "User password", "사용자 비밀번호" },
            { "Sync speed", "동기화 속도" },
            { "Cannot find VRCExpressionParameters in your avatar!", "아바타에서 VRCExpressionParameters를 찾을 수 없습니다!" },
            { "Parameters: {0} bits used, {1} bits free", "파라미터: {0}비트 사용, {1}비트 남음" },
            { "OSC program", "OSC 프로그램" },
            { "The key is saved in the avatar, so the OSC program only has to run once. Uses more parameters.", "키가 아바타에 저장되어 OSC 프로그램을 한 번만 실행하면 됩니다. 파라미터를 더 사용합니다." },
            { "The OSC program must keep running while you play. Uses the fewest parameters.", "플레이하는 동안 OSC 프로그램을 계속 켜둬야 합니다. 파라미터를 가장 적게 사용합니다." },
            { "Run ShellProtectorOSC once while playing VRChat and enter the user password. The key stays saved in the avatar after that.", "VRChat을 플레이하는 동안 ShellProtectorOSC를 한 번 실행해 사용자 비밀번호를 입력하세요. 그 뒤로는 키가 아바타에 저장되어 있습니다." },
            { "Keep ShellProtectorOSC running while playing VRChat and enter the user password. Set the sync speed to 2 or higher to run it only once.", "VRChat을 플레이하는 동안 ShellProtectorOSC를 켜두고 사용자 비밀번호를 입력하세요. 동기화 속도를 2 이상으로 하면 한 번만 실행하면 됩니다." },
            { "This version requires ShellProtectorOSC 1.7 or later. Older OSC versions can't unlock the avatar, so make sure to update the OSC program to the latest version.", "이 버전은 ShellProtectorOSC 1.7 이상이 필요합니다. 이전 버전의 OSC로는 아바타의 암호화를 풀 수 없으니 OSC 프로그램을 반드시 최신 버전으로 업데이트하세요." },
            { "Advanced options", "고급 옵션" },
            { "Texture", "텍스쳐" },
            { "Unlit safety fallback", "Safety 폴백을 Unlit으로" },
            { "Obfuscated meshes", "난독화할 메쉬" },
            { "Output", "출력" },
            { "Clean the output folder", "출력 폴더 비우기" },
            { "Chacha8 test", "Chacha8 테스트" }
        };

            var jpStrings = new Dictionary<string, string>()
        {
            { "Material List", "マテリアル一覧" },
            { "Encrypt emission", "Emissionを暗号化" },
            { "Unchecked materials are not encrypted. Emission maps are encrypted only for the checked slots.", "チェックしていないマテリアルは暗号化されません。Emissionマップはチェックしたスロットのみ暗号化されます。" },
            { "Encrypt this material", "このマテリアルを暗号化します" },
            { "A shader injected before is reused.", "以前に作成したシェーダーを再利用します。" },
            { "The shader is injected and compiled on this build.", "今回のビルドでシェーダーを新しく作成し、コンパイルします。" },
            { "Material", "マテリアル" },
            { "Main texture", "メインテクスチャ" },
            { "Texture filter", "テクスチャフィルター" },
            { "Fallback texture", "フォールバックテクスチャ" },
            { "Selected emission maps emit no light until the correct password is entered. Masks and gradients are not encrypted.", "選択したEmissionマップは、正しいパスワードが入力されるまで発光しません。マスクとグラデーションは暗号化されません。" },
            { "No texture in this slot.", "このスロットにテクスチャがありません。" },
            { "Emission maps must be power-of-two RGB24, RGBA32, DXT1 or DXT5 textures (DXT: at least 8x4).", "Emissionマップは2のべき乗サイズのRGB24、RGBA32、DXT1、DXT5テクスチャである必要があります（DXTは最小8x4）。" },
            { "Reset", "リセット" },
            { "Filter, fallback and emission encryption per material.", "マテリアルごとのフィルター、フォールバック、Emission暗号化の設定です。" },
            { "Emission encryption: {0} maps in {1} materials", "Emission暗号化: マテリアル{1}個、マップ{0}個" },
            { "Texture List", "テクスチャ一覧" },
            { "Password", "パスワード" },
            { "Default texture filter", "基本テクスチャフィルター" },
            { "Encrypt!", "暗号化開始！" },
            { "Debug", "デバッグ" },
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
            { "Not enough parameter space!", "パラメータスペースが不足しています！" },
            { "It's okay for the 0-digit password to be the same as the original.", "0桁のパスワードは、オリジナルと見た目が同じであることが正常です。" },
            { "Show", "見る" },
            { "Releases page", "リリースページ" },
            { "Download latest OSC", "最新OSCをダウンロード" },
            { "Save ShellProtectorOSC", "ShellProtectorOSCを保存" },
            { "Downloading...", "ダウンロード中..." },
            { "Failed to download the OSC program. Open the releases page instead?", "OSCプログラムのダウンロードに失敗しました。代わりにリリースページを開きますか？" },
            { "Open", "開く" },
            { "Cancel", "キャンセル" },
            { "Delete folders that already exists when at creation time", "作成時に既に存在するフォルダを削除" },
            { "Setting it to 'Point' may result in aliasing, but performance is better.", "Pointに設定すると階段現象が発生する可能性がありますが、性能が良くなります。" },
            { "Small mip texture", "小さなミップテクスチャ" },
            { "It uses a smaller mipTexture to reduce memory usage and improve performance. It may look slightly different from the original when viewed from the side.", "小さなミップテクスチャを使用して、メモリ使用量を減らし、パフォーマンスを向上させます。 横から見ると、オリジナルと少し違って見えるかもしれません。"},
            { "Object list", "オブジェクト一覧"},
            { "Manual Encrypt! (for testing)", "手動暗号化開始！(テスト用)" },
            { "Modular avatars exist. It is automatically encrypted on upload.", "Modular Avatarが存在します。アップロード時に自動的に暗号化されます。" },
            { "Force progress", "強制的に進行" },
            { "BlendShape obfuscation", "BlendShape難読化" },
            { "Preserve MMD BlendShapes", "MMD BlendShapeを保存" },
            { "Fallback Options", "フォールバックオプション" },
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
            { "Default fallback texture", "デフォルトのフォールバックテクスチャ" },
            { "Number of key bytes synced at once. At 2 or higher the key syncs faster and is saved in the avatar, so the OSC program only has to run once, but more parameters are used.", "一度に同期するキーのバイト数です。2以上にするとキーの同期が速くなり、アバターに保存されるためOSCプログラムは一度実行するだけで済みますが、より多くのパラメータを使用します。" },
            { "A new version is available: ", "新しいバージョンがあります: " },
            { "Avatar", "アバター" },
            { "Assign the avatar to encrypt.", "暗号化するアバターを指定してください。" },
            { "No supported shader (lilToon, Poiyomi) was found in the project.", "プロジェクトに対応シェーダー（lilToon、Poiyomi）が見つかりません。" },
            { "Detected: ", "検出: " },
            { "Encryption targets", "暗号化対象" },
            { "Add the objects or materials to encrypt.", "暗号化するオブジェクトまたはマテリアルを追加してください。" },
            { "Add Body", "Bodyを追加" },
            { "User password", "ユーザーパスワード" },
            { "Sync speed", "同期速度" },
            { "Cannot find VRCExpressionParameters in your avatar!", "アバターにVRCExpressionParametersが見つかりません！" },
            { "Parameters: {0} bits used, {1} bits free", "パラメータ: {0}ビット使用、{1}ビット空き" },
            { "OSC program", "OSCプログラム" },
            { "The key is saved in the avatar, so the OSC program only has to run once. Uses more parameters.", "キーがアバターに保存されるため、OSCプログラムは一度実行するだけで済みます。より多くのパラメータを使用します。" },
            { "The OSC program must keep running while you play. Uses the fewest parameters.", "プレイ中はOSCプログラムを起動したままにする必要があります。使用するパラメータは最も少なくなります。" },
            { "Run ShellProtectorOSC once while playing VRChat and enter the user password. The key stays saved in the avatar after that.", "VRChatのプレイ中にShellProtectorOSCを一度実行し、ユーザーパスワードを入力してください。その後はキーがアバターに保存されます。" },
            { "Keep ShellProtectorOSC running while playing VRChat and enter the user password. Set the sync speed to 2 or higher to run it only once.", "VRChatのプレイ中はShellProtectorOSCを起動したままにし、ユーザーパスワードを入力してください。同期速度を2以上にすると、一度実行するだけで済みます。" },
            { "This version requires ShellProtectorOSC 1.7 or later. Older OSC versions can't unlock the avatar, so make sure to update the OSC program to the latest version.", "このバージョンにはShellProtectorOSC 1.7以降が必要です。古いOSCではアバターの暗号化を解除できないため、必ずOSCプログラムを最新バージョンに更新してください。" },
            { "Advanced options", "詳細オプション" },
            { "Texture", "テクスチャ" },
            { "Unlit safety fallback", "SafetyフォールバックをUnlitに" },
            { "Obfuscated meshes", "難読化するメッシュ" },
            { "Output", "出力" },
            { "Clean the output folder", "出力フォルダーを空にする" },
            { "Chacha8 test", "Chacha8テスト" }
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
