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

텍스쳐 자체를 암호화 한 후 VRChat 서버에 업로드 됩니다. 이 텍스쳐는 게임에서 셰이더를 통해 복호화 시킵니다.

셰이더와 메테리얼은 복사 되기 때문에 원본에 영향이 없습니다.

MainTexture만 암호화 하기 때문에 메테리얼 내 다른 곳에 MainTexture와 동일한 텍스쳐를 쓴다면 보안을 위해 자동으로 빠집니다. 빠진 곳엔 적절한 텍스쳐를 채워 넣으세요.

### 성능에 영향은 없나요?
메모리는 원본보다 조금 더 차지합니다. 2K DXT1 이미지 기준 2mb정도 커집니다.

같은 메테리얼 50개를 기준으로 평균 0.2ms ~ 0.8ms정도 느려집니다. 포이요미가 릴툰보다 성능이 좋았습니다.

### 지원 셰이더
- Poiyomi 7.3, 8.0, 8.1, 8.2
- lilToon

### 지원 텍스쳐 형식
- RGB24, RGBA32
- DXT1, DXT5
- Crunch Compression 포멧은 자동으로 DXT1이나 DXT5로 변환 됩니다.
 
### 예정
- OSC를 이용한 인게임 패스워드

## English

### **Texture encryption using shaders available in VRChat**.

After encrypting the texture, the shader is used to decrypt the MainTexure of materials.

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

### Supported shaders
- Poiyomi 7.3, 8.0, 8.1, 8.2
- lilToon

### Supported texture formats
- RGB24, RGBA32
- DXT1, DXT5
- The Crunch Compression format will auto-convert to DXT1 or DXT5.
### feature
- In-game passwords with OSC
