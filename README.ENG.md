# ShellProtector

[![Downloads](https://img.shields.io/github/downloads/Shell4026/ShellProtector/total?color=6451f1)](https://github.com/Shell4026/ShellProtector/releases/latest)
[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FShell4026%2FShellProtector&count_bg=%2379C83D&title_bg=%23555555&icon=&icon_color=%23E7E7E7&title=hits&edge_flat=false)](https://hits.seeyoufarm.com)

# VCC : https://shell4026.github.io/VCC/
Require Unity version : 2022

## | [한국어](./README.md) | English | [日本語](./README.JP.md) |

### **Texture encryption using shaders available in VRChat**.

After encrypting the texture, a shader is used to decrypt the texture.

This prevents your avatar from being copied and prevents people from modifying your avatar's texture through ripping.

You can easily enter the password with the OSC program.

Source code of OSC: https://github.com/Shell4026/ShellProtectorOSC

## Supported shaders
- Poiyomi 7.3(Unstable), 8.0, 8.1, 8.2, 9.0, 9.1(pro), PCSS(Need to more testing)
- lilToon (1.3.8 ~ 1.7.3)(VCC)

## Supported texture formats
- RGB24, RGBA32
- DXT1, DXT5
- The Crunch Compression format will auto-convert to DXT1 or DXT5.

## Features
- Texture Encryption
- OSC programs for descryption
- Blendshape obfuscation
- Fallback: the ability to make non-friends see a 16x16 texture instead of encryption noise
  
## Usage

1. Right-click on your avatar and select "Shell Protector" to add the component.
2. Set a password and specify the Material or GameObject to encrypt.

(The steps below are not required when using Modular avatar)

3. Click the Encrypt button.
4. Check the encryption via the Testor component in the new avatar and press the Done button.
5. Upload the avatar.

### If your password is more than 4 digits (OSC)
1. Download ShellProtectorOSC.zip from the release, unzip it, and run ShellProtectorOSC.exe. (You only need to run it once the first time. If you use a reset avatar, keep it on.)
2. Replace your uploaded avatar and enter your user password in the OSC program.
3. If changing the password doesn't change the appearance of your avatar, try going to the Action menu - Options - OSC - Reset Config in VRChat.
4. If you're still having trouble, try clearing the C:\Users\UserName\AppData\LocalLow\VRChat\VRChat\OSC folder.

### Parameter-multiplexing
Detailed principle:https://github.com/seanedwards/vrc-worldobject/blob/main/docs/parameter-multiplexing.md

This is a parameter-saving technique. After checking, OSC must always be turned on and Parameter-multiplexing must be checked in the OSC program.

This will slightly increase the time it takes to get back to your original avatar appearance in-game.

16 digits are not recommended because there may be VRChat security issues related to parameters, so 12 digits are recommended.

When using parameter multiplexing, depending on the server or network conditions, OSC values may not be delivered to other users and decryption may not work.

In this case, try increasing the refresh rate slightly, which was added in OSC 1.5.0.

### Avatar fallback
A feature that allows anyone with Safety On when encryption is in place to appear as a degraded version of themselves when viewing your avatar.
![fallback](https://github.com/user-attachments/assets/d3ca69b0-ff08-4793-a4e4-73269bc8efd3)

## Troubleshooting
If you find the issue, please raise it in Issues.

**[is not supported texture format! Error]**

Select the texture and change the compression format to either DXT1 or DXT5 in the Inspector. (DXT5 for textures with transparency)

![texture](https://github.com/Shell4026/ShellProtector/assets/104874910/872f9d15-7b89-4381-b940-00514bd60638)

**[liltoon)At testor component, it doesn't come back to normal]**.

It's a bug in lilToon, so ignore it and upload it, or try one of three ways

1. Delete your own avatar folder inside the ShellProtect folder and re-encrypt it.
2. Try restarting Unity.
3. Click Assets - liltoon - Refresh Shader (It takes a while!)

**[If certain areas look monochromatic]**

Try encrypting it again, and restart Unity.

**[If not decryption when viewed by others in-game]**

The other person needs to turn off shaders and animation safety or Show Avatar to you.

If they do, this is a VRChat synchronization of parameters bug, **please change your password and re-upload.**

When using parameter multiplexing, depending on the server or network conditions, OSC values may not be delivered to other users and decryption may not work.

In this case, try increasing the refresh rate slightly, which was added in OSC 1.5.0.

**[When a Texture that was present in a Material is missing]**

When using a texture such as a main color/texture, it will be stripped for security reasons.

The exceptions are the limlight and outline textures, which will not be removed and will still use the encrypted texture.
   
## How it works
Encrypt the texture using an XXTEA/Chacha8 algorithm after transforming the key with SHA-256.

After encrypting the texture itself, it is uploaded to the VRChat server. The texture is then decrypted in the game via shaders.

Shaders, Materials and animations are copied, so the original is not affected.

Only the MainTexture is encrypted, so if you use the same texture elsewhere in the Material as the MainTexture, it will automatically be removed for security purposes. Fill in the gaps with the appropriate texture.

The exception to this is when the Rimlight texture and Outline texture are the same texture as the MainTexture, they use the same encrypted texture as the MainTexture.

## Is there any performance impact?
It takes up a little more memory than the original. It's about 1mb larger for a 2K DXT1 image.

Poiyomi performed better than lilToon.

<Approximate Poiyomi GPU measurement results>

Default : 0.1ms

Point filtering: 0.2ms

Bilinear filtering: 0.35ms

This may not seem like a huge difference, but for performance reasons, I recommend only encrypting textures that are absolutely necessary.

## How secure is it?
By default, it has 16 bytes of keys, split between keys stored inside the shader and keys that the user can enter using VRC parameters. (I'll call these user keys.)

A user key of 0 bytes can be figured out by simply turning the compiled shader into an assembler and analyzing it.</br>
A 4-byte user key can be figured out if someone takes the time to do so. (4-byte key = 32 parameters / 11 parameters(using parameter-multiplexing))</br>
An 8-byte user key would take a long time to crack on a personal computer. (8-byte key = 64 parameters / 12 parameters)</br>
User keys starting at 12 bytes are impossible to crack on a modern computer. (12-byte key = 96 parameter / 13 parameters)

It is safe to increase the number of user keys by using a minimum of 96 parameter spaces(13 when using parameter multiplexing). Please be mindful of your parameter space when setting your keys.</br>
A 0-byte user key is a minimal defense, and should be effective against toolkiddies using simple tools.
<br/><br/><br/>
There is no such thing as perfect security, and while this method is not straightforward, there is a possibility that someone persistently analyzing a their network packets within the same world could find keys.<br/> 
However, if you use a sufficiently large key size, it is possible to completely prevent someone from indiscriminately extracting and sharing a user's avatar.

## feature
- Support BC7
