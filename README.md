# ShellProtector

[![Downloads](https://img.shields.io/github/downloads/Shell4026/ShellProtector/total?color=6451f1)](https://github.com/Shell4026/ShellProtector/releases/latest)
[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FShell4026%2FShellProtector&count_bg=%2379C83D&title_bg=%23555555&icon=&icon_color=%23E7E7E7&title=hits&edge_flat=false)](https://hits.seeyoufarm.com)

## | 한국어 | [English](./README.ENG.md) | [日本語](./README.JP.md) |

### **VRChat에서 사용 가능한 셰이더를 이용한 텍스쳐 암호화**

텍스쳐를 암호화 시킨 후, 셰이더를 이용하여 텍스쳐를 복호화합니다.

아바타 복사를 막아주고 리핑을 통해 아바타의 텍스쳐를 뜯어가서 수정하는 것을 막을 수 있습니다.

OSC 프로그램으로 간편하게 비밀번호를 입력할 수 있습니다.

OSC 소스 코드: https://github.com/Shell4026/ShellProtectorOSC

## 지원 셰이더
- Poiyomi 7.3(불안정), 8.0, 8.1, 8.2
- lilToon (1.3.8 ~ 1.4.0)

## 지원 텍스쳐 형식
- RGB24, RGBA32
- DXT1, DXT5
- Crunch Compression 포멧은 자동으로 DXT1이나 DXT5로 변환 됩니다.

## 사용법
1. 아바타에 'Shell Protector' 컴포넌트를 추가합니다.
2. 비밀번호를 지정해주고 메테리얼 리스트에 암호화 할 텍스쳐가 존재하는 메테리얼을 넣습니다.
3. Encrypt 버튼을 누르세요.
4. 새로 생긴 아바타에 들어간 Testor컴포넌트를 통해 암호화 여부를 확인하고 완료 버튼을 누르세요.
5. 아바타를 업로드 합니다.

### 자신의 비밀번호가 4자리 이상인 경우 (OSC)
1. Release에 있는 ShellProtectorOSC.zip을 다운 후 압축을 풀고 ShellProtectorOSC.exe를 실행시킵니다. (최초 한 번만 실행하면 됩니다. 리셋 아바타나 파라미터 멀티플렉싱을 사용한다면 계속 켜두세요.)
2. 업로드 한 아바타로 바꾼 후 OSC프로그램에서 사용자 비밀번호를 입력합니다.
3. 만약 비밀번호가 바뀌어도 아바타의 외형에 변화가 없다면 VRChat에서 액션 메뉴 - Options - OSC - Reset Config를 눌러보세요.
4. 그래도 문제가 있다면 C:\Users\유저\AppData\LocalLow\VRChat\VRChat\OSC 폴더를 지워보세요.

### 파라미터 멀티플렉싱
세부 원리:https://github.com/seanedwards/vrc-worldobject/blob/main/docs/parameter-multiplexing.md

파라미터 절약 기술입니다. 체크 후 OSC를 항상 켜둬야하며 OSC프로그램에도 Parameter-multiplexing을 체크 해야합니다.

인게임에서 원래 모습으로 돌아오기까지 시간이 약간 증가합니다.

16자리는 혹시 모를 파라미터 관련 VRChat보안 이슈가 있을 수 있으니 12자리를 권장합니다.

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

**<인게임에서 남이 봤을 때 암호화가 안 풀리는 경우>**

남이 셰이더와 애니메이션 세이프티를 끄거나 당신을 Show Avatar해야합니다.

그랬는데도 그러면 VRChat 파라미터 동기화 버그로, 비밀번호를 변경 후 **다시 업로드 하길 바랍니다.**

**<메테리얼에서 있던 텍스쳐가 빠지는 경우>**

메인 컬러와 같은 텍스쳐 사용시 보안상의 이유로 해당 텍스쳐는 빠집니다.

예외로, 림라이트, 아웃라인 텍스쳐는 빠지지 않고 그대로 암호화된 텍스쳐를 사용합니다.

## 세부 원리
SHA-256으로 키를 변형 후 XXTEA 알고리즘을 사용하여 메테리얼의 MainTexure를 암호화합니다.

압축 텍스쳐는 색만 암호화하여 용량을 줄입니다. 원본 텍스쳐의 형태는 일부 남아있습니다.

텍스쳐 자체를 암호화 한 후 VRChat 서버에 업로드 됩니다. 이 텍스쳐는 게임에서 셰이더를 통해 복호화 시킵니다.

셰이더와 메테리얼은 복사 되기 때문에 원본에 영향이 없습니다.

MainTexture만 암호화 하기 때문에 메테리얼 내 다른 곳에 MainTexture와 동일한 텍스쳐를 쓴다면 보안을 위해 자동으로 빠집니다. 빠진 곳엔 적절한 텍스쳐를 채워 넣으세요.

예외로 림라이트 텍스쳐와 아웃라인 텍스쳐가 메인 텍스쳐와 같은 텍스쳐일 경우 MainTexture와 같은 암호화된 텍스쳐를 사용합니다.

## 성능에 영향은 없나요?
메모리는 원본보다 조금 더 차지합니다. 2K DXT1 이미지 기준 1mb정도 커집니다.

같은 메테리얼 50개를 기준으로 평균 0.2ms ~ 0.8ms정도 느려집니다. 포이요미가 릴툰보다 성능이 좋았습니다.

## 얼마나 안전한가요?
기본적으로 16바이트의 키를 가지며, 셰이더 내부에 저장되는 키와 사용자가 VRC 파라미터를 이용하여 입력할 수 있는 키로 나누어져 있습니다. (사용자 키라고 부르겠습니다.)

0바이트의 사용자 키는 컴파일된 셰이더를 어셈블리어로 바꾸고 분석하기만 하면 알아낼 수 있습니다.

4바이트의 사용자 키는 누군가 시간만 들이면 키를 알아낼 수 있습니다. (4바이트 키 = 파라미터 32칸 / 11칸(파라미터 멀티플렉싱 사용시))

8바이트의 사용자 키는 개인용 컴퓨터로는 알아내는데 시간이 많이 걸릴 것입니다. (8바이트 키 = 파라미터 64칸 / 12칸)

12바이트의 사용자 키부터는 현대 컴퓨터로는 알아낼 수 없습니다. (12바이트 키 = 파라미터 96칸 / 13칸)

최소 파라미터 96칸(파라미터 멀티플렉싱 사용시 13칸)을 써서 사용자 키의 수를 늘린다면 안전합니다. 자신의 파라미터 공간을 생각해서 키를 설정하시길 바랍니다.

0바이트의 사용자 키는 최소한의 방어라고 보면 되고, 단순 툴을 이용한 툴키디들을 막아내는데는 효과적일 것입니다.
 
## 예정
- BC7 지원
