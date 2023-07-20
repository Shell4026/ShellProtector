# ShellProtector

## 한국어

### **VRChat에서 사용 가능한 셰이더를 이용한 텍스쳐 암호화**

[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FShell4026%2FShellProtector&count_bg=%2379C83D&title_bg=%23555555&icon=&icon_color=%23E7E7E7&title=hits&edge_flat=false)](https://hits.seeyoufarm.com)

텍스쳐를 암호화 시킨 후, 셰이더를 이용하여 텍스쳐를 복호화합니다.

아바타 복사를 막아주고 리핑을 통해 아바타의 텍스쳐를 뜯어가서 수정하는 것을 막을 수 있습니다.

OSC 프로그램으로 간편하게 비밀번호를 입력할 수 있습니다.

## 사용법
1. 아바타에 'Shell Protector' 컴포넌트를 추가합니다.
2. 비밀번호를 지정해주고 메테리얼 리스트에 암호화 할 텍스쳐가 존재하는 메테리얼을 넣습니다.
3. Encrypt 버튼을 누르세요.
4. 새로 생긴 아바타에 들어간 Testor컴포넌트를 통해 암호화 여부를 확인하고 완료 버튼을 누르세요.
5. 아바타를 업로드 합니다.

### 자신의 비밀번호가 4자리 이상인 경우 (OSC)
1. Release에 있는 ShellProtectorOSC.zip을 다운 후 압축을 풀고 ShellProtectorOSC.exe를 실행시킵니다. (최초 한 번만 실행하면 됩니다. 리셋 아바타를 사용한다면 계속 켜두세요.)
2. 업로드 한 아바타로 바꾼 후 OSC프로그램에서 사용자 비밀번호를 입력합니다.
3. 만약 비밀번호가 바뀌어도 아바타의 외형에 변화가 없다면 VRChat에서 액션 메뉴 - Options - OSC - Reset Config를 눌러보세요.
4. 그래도 문제가 있다면 C:\Users\유저\AppData\LocalLow\VRChat\VRChat\OSC 폴더를 지워보세요.

## 문제해결
**<is not supported texture format! 에러>**

텍스쳐를 선택 후 Inspector에서 압축 포멧을 DXT1이나 DXT5중 하나로 바꿔주세요. (투명도가 있는 텍스쳐는 DXT5)

![texture](https://github.com/Shell4026/ShellProtector/assets/104874910/872f9d15-7b89-4381-b940-00514bd60638)

**<릴툰)Testor컴포넌트로 테스트 했을 때 원래대로 안 돌아오는 경우>**

릴툰의 버그이므로 무시하고 업로드 하거나 3가지 방법 중 하나를 해보세요.

1. ShellProtect 폴더 안에 생긴 자기 아바타 폴더를 지우고 다시 암호화 하기
2. 유니티를 재실행 해보세요.
3. Assets - liltoon - Refresh Shader를 눌러보기 (오래 걸림!)

**<특정 부위가 단색으로 보이는 경우>**

릴툰의 경우 메테리얼의 메인 컬러 부분과 custom properties의 Encrypted texture부분에 암호화 된 텍스쳐가 빠져 있는지 확인하고 넣어주세요.

포이요미의 경우 다시 암호화 해보세요.

## 세부 원리
SHA-256으로 키를 변형 후 XXTEA 알고리즘을 사용하여 메테리얼의 MainTexure를 암호화합니다.

압축 텍스쳐는 색만 암호화하여 용량을 줄입니다. 원본 텍스쳐의 형태는 일부 남아있습니다.

텍스쳐 자체를 암호화 한 후 VRChat 서버에 업로드 됩니다. 이 텍스쳐는 게임에서 셰이더를 통해 복호화 시킵니다.

셰이더와 메테리얼은 복사 되기 때문에 원본에 영향이 없습니다.

MainTexture만 암호화 하기 때문에 메테리얼 내 다른 곳에 MainTexture와 동일한 텍스쳐를 쓴다면 보안을 위해 자동으로 빠집니다. 빠진 곳엔 적절한 텍스쳐를 채워 넣으세요.

## 성능에 영향은 없나요?
메모리는 원본보다 조금 더 차지합니다. 2K DXT1 이미지 기준 1mb정도 커집니다.

같은 메테리얼 50개를 기준으로 평균 0.2ms ~ 0.8ms정도 느려집니다. 포이요미가 릴툰보다 성능이 좋았습니다.

## 얼마나 안전한가요?
기본적으로 16바이트의 키를 가지며, 셰이더 내부에 저장되는 키와 사용자가 VRC 파라미터를 이용하여 입력할 수 있는 키로 나누어져 있습니다. (사용자 키라고 부르겠습니다.)

0바이트의 사용자 키는 컴파일된 셰이더를 어셈블리어로 바꾸고 분석하기만 하면 알아낼 수 있습니다.

4바이트의 사용자 키는 누군가 시간만 들이면 키를 알아낼 수 있습니다. (4바이트 키 = 파라미터 32칸)

8바이트의 사용자 키는 개인용 컴퓨터로는 알아내는데 시간이 많이 걸릴 것입니다. (8바이트 키 = 파라미터 64칸)

12바이트의 사용자 키부터는 현대 컴퓨터로는 알아낼 수 없습니다. (12바이트 키 = 파라미터 96칸)

최소 파라미터 96칸을 써서 사용자 키의 수를 늘린다면 안전합니다. 자신의 파라미터 공간을 생각해서 키를 설정하시길 바랍니다.

0바이트의 사용자 키는 최소한의 방어라고 보면 되고, 단순 툴을 이용한 툴키디들을 막아내는데는 효과적일 것입니다.

## 지원 셰이더
- Poiyomi 7.3, 8.0, 8.1, 8.2
- lilToon

## 지원 텍스쳐 형식
- RGB24, RGBA32
- DXT1, DXT5
- Crunch Compression 포멧은 자동으로 DXT1이나 DXT5로 변환 됩니다.
 
## 예정
- BC7 지원

## English

### **Texture encryption using shaders available in VRChat**.

After encrypting the texture, a shader is used to decrypt the texture.

This prevents your avatar from being copied and prevents people from modifying your avatar's texture through ripping.

You can easily enter the password with the OSC program.

## Usage

1. Add an 'Shell Protector' Component to your avatar.
2. Enter a password and add the material that contains the texture you want to encrypt to the Material List.
3. Click the Encrypt button.
4. Check the encryption via the Testor component in the new avatar and press the Done button.
5. Upload the avatar.

### If your password is more than 4 digits (OSC)
1. Download ShellProtectorOSC.zip from the release, unzip it, and run ShellProtectorOSC.exe. (You only need to run it once the first time. If you use a reset avatar, keep it on.)
2. Replace your uploaded avatar and enter your user password in the OSC program.
3. If changing the password doesn't change the appearance of your avatar, try going to the Action menu - Options - OSC - Reset Config in VRChat.
4. If you're still having trouble, try clearing the C:\Users\UserName\AppData\LocalLow\VRChat\VRChat\OSC folder.

## Troubleshooting
**<is not supported texture format! Error>**

Select the texture and change the compression format to either DXT1 or DXT5 in the Inspector. (DXT5 for textures with transparency)

![texture](https://github.com/Shell4026/ShellProtector/assets/104874910/872f9d15-7b89-4381-b940-00514bd60638)

**<liltoon)At testor component, it doesn't come back to normal>**.

It's a bug in lilToon, so ignore it and upload it, or try one of three ways

1. Delete your own avatar folder inside the ShellProtect folder and re-encrypt it.
2. Try restarting Unity.
3. Click Assets - liltoon - Refresh Shader (It takes a while!)

**<If certain areas look monochromatic>**

For lilToon, check if the encrypted texture is missing from the main color part of the material and the Encrypted texture part of the custom properties and add it.

For Poiyomi, try encrypting it again.
   
## How it works
Encrypt the texture using an XXTEA algorithm after transforming the key with SHA-256.

After encrypting the texture itself, it is uploaded to the VRChat server. The texture is then decrypted in the game via shaders.

Shaders and Materials are copied, so the original is not affected.

Only the MainTexture is encrypted, so if you use the same texture elsewhere in the Material as the MainTexture, it will automatically be removed for security purposes. Fill in the gaps with the appropriate texture.

## Is there any performance impact?
It takes up a little more memory than the original. It's about 1mb larger for a 2K DXT1 image.

On average, it's about 0.2ms~0.8ms slower based on the same 50 materials. Poiyomi performed better than lilToon.

## How secure is it?
By default, it has 16 bytes of keys, split between keys stored inside the shader and keys that the user can enter using VRC parameters. (I'll call these user keys.)

A user key of 0 bytes can be figured out by simply turning the compiled shader into an assembler and analyzing it.

A 4-byte user key can be figured out if someone takes the time to do so. (A 4-byte key = 32 lines of parameters.)

An 8-byte user key would take a long time to crack on a personal computer. (8-byte key = 64 parameters)

User keys starting at 12 bytes are impossible to crack on a modern computer. (12-byte key = 96 parameter fields)

It is safe to increase the number of user keys by using a minimum of 96 parameter spaces. Please be mindful of your parameter space when setting your keys.

A 0-byte user key is a minimal defense, and should be effective against toolkiddies using simple tools.

## Supported shaders
- Poiyomi 7.3, 8.0, 8.1, 8.2
- lilToon

## Supported texture formats
- RGB24, RGBA32
- DXT1, DXT5
- The Crunch Compression format will auto-convert to DXT1 or DXT5.
## feature
- Support BC7
