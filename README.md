# ShellProtector

## 한국어

### **VRChat에서 사용 가능한 셰이더를 이용한 텍스쳐 암호화**

텍스쳐를 암호화 시킨 후, 셰이더를 이용하여 텍스쳐를 복호화합니다.

아바타 복사는 막지 못하지만, 리핑을 통해 아바타의 텍스쳐를 뜯어가서 수정하는 것을 막을 수 있습니다.

복사를 막고 싶다면 비밀번호 에셋과 함께 쓰십시오.

### 사용법
1. 아바타에 'Shell Protector' 컴포넌트를 추가합니다.
2. 비밀번호를 지정해주고 메테리얼 리스트에 암호화 할 텍스쳐가 존재하는 메테리얼을 넣습니다.
3. Encrypt 버튼을 누르세요.

### 세부 원리
XXTEA 알고리즘을 사용하여 메테리얼의 MainTexure를 암호화합니다.

압축 텍스쳐는 색만 암호화하여 용량을 줄입니다. 원본 텍스쳐의 형태는 일부 남아있습니다.

텍스쳐 자체를 암호화 한 후 VRChat 서버에 업로드 됩니다. 이 텍스쳐는 게임에서 셰이더를 통해 복호화 시킵니다.

셰이더와 메테리얼은 복사 되기 때문에 원본에 영향이 없습니다.

MainTexture만 암호화 하기 때문에 메테리얼 내 다른 곳에 MainTexture와 동일한 텍스쳐를 쓴다면 보안을 위해 자동으로 빠집니다. 빠진 곳엔 적절한 텍스쳐를 채워 넣으세요.

### 성능에 영향은 없나요?
메모리는 원본보다 조금 더 차지합니다. 2K DXT1 이미지 기준 2mb정도 커집니다.

같은 메테리얼 50개를 기준으로 평균 0.2ms ~ 0.8ms정도 느려집니다. 포이요미가 릴툰보다 성능이 좋았습니다.

### 얼마나 안전한가요?
기본적으로 16바이트의 키를 가지며, 12바이트의 키는 셰이더 내부에 저장돼 있으며 4바이트의 키는 사용자가 VRC 파라미터를 이용하여 입력할 수 있는 구조입니다. (사용자 키라고 부르겠습니다.)

4바이트의 사용자 키는 누군가 시간만 들이면 키를 알아낼 수 있습니다. (4바이트 키 = 파라미터 32칸)

12바이트의 키는 컴파일된 셰이더를 어셈블리어로 바꾸고 분석하면 알아낼 수 있습니다.

최소 파라미터 96칸을 써서 사용자 키의 수를 늘린다면 웬만한 컴퓨터 연산으로는 뚫을 수 없습니다. 하지만 그러기엔 VRChat의 파라미터는 너무 작습니다.

기본 세팅은 최소한의 방어선이라고 보면 되고, 단순 툴을 이용한 툴키디들을 막아내는데는 매우 효과적일 것입니다.

아직은 4바이트 키 설정만 가능하지만, 파라미터 칸이 넉넉하고 더 안전한 보안을 원하는 사용자를 위해 늘릴 수 있게 개발하겠습니다.

### 지원 셰이더
- Poiyomi 7.3, 8.0, 8.1, 8.2
- lilToon

### 지원 텍스쳐 형식
- RGB24, RGBA32
- DXT1, DXT5
- Crunch Compression 포멧은 자동으로 DXT1이나 DXT5로 변환 됩니다.
 
### 예정
- OSC를 이용한 인게임 패스워드
- 가변 사용자키 길이

## English

### **Texture encryption using shaders available in VRChat**.

After encrypting the texture, the shader is used to decrypt the MainTexure of materials.

A compressed texture reduces its size by encrypting only the colors. Some of the original texture's shape remains.

This does not prevent copying of the avatar, but it does prevent ripping and modifying the avatar's texture.

If you want to prevent copying, use it in conjunction with the password assets.

### Usage
1. Add an 'Shell Protector' Component to your avatar.
2. Enter a password and add the material that contains the texture you want to encrypt to the Material List.
3. Click the Encrypt button.

### How it works
Encrypt the texture using a XXTEA algorithm.

After encrypting the texture itself, it is uploaded to the VRChat server. The texture is then decrypted in the game via shaders.

Shaders and Materials are copied, so the original is not affected.

Only the MainTexture is encrypted, so if you use the same texture elsewhere in the Material as the MainTexture, it will automatically be removed for security purposes. Fill in the gaps with the appropriate texture.

### Is there any performance impact?
It takes up a little more memory than the original. It's about 2mb larger for a 2K DXT1 image.

On average, it's about 0.2ms~0.8ms slower based on the same 50 materials. Poiyomi performed better than lilToon.

### How secure is it?
By default, it has a 16-byte key, 12 bytes of which are stored inside the shader, and 4 bytes of which can be entered by the user using VRC parameters. (Let's call it the user key.)

A 4-byte user key can be broken if someone takes the time to figure it out (4-byte key = 32 parameter spaces).

A 12-byte key can be figured out by turning the compiled shader into an assembler and analyzing it.

You can increase the number of user keys by using at least 96 parameter spaces to make them unbreakable for most computers, but VRChat's parameters are too small for that.

The default settings are the first line of defense, and should be very effective against toolkiddies using simple tools.

For now, you can only set a 4-byte key, but I'll work on increasing that for users who want more security by using more parameter space.

### Supported shaders
- Poiyomi 7.3, 8.0, 8.1, 8.2
- lilToon

### Supported texture formats
- RGB24, RGBA32
- DXT1, DXT5
- The Crunch Compression format will auto-convert to DXT1 or DXT5.
### feature
- In-game passwords with OSC
- Variable user key length
