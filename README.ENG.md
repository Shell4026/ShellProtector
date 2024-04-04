# ShellProtector

[![Downloads](https://img.shields.io/github/downloads/Shell4026/ShellProtector/total?color=6451f1)](https://github.com/Shell4026/ShellProtector/releases/latest)
[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FShell4026%2FShellProtector&count_bg=%2379C83D&title_bg=%23555555&icon=&icon_color=%23E7E7E7&title=hits&edge_flat=false)](https://hits.seeyoufarm.com)

## | [한국어](./README.md) | English | [日本語](./README.JP.md) |

### **Texture encryption using shaders available in VRChat**.

After encrypting the texture, a shader is used to decrypt the texture.

This prevents your avatar from being copied and prevents people from modifying your avatar's texture through ripping.

You can easily enter the password with the OSC program.

Source code of OSC: https://github.com/Shell4026/ShellProtectorOSC

## Supported shaders
- Poiyomi 7.3(Unstable), 8.0, 8.1, 8.2
- lilToon (1.3.8 ~ 1.4.0)

## Supported texture formats
- RGB24, RGBA32
- DXT1, DXT5
- The Crunch Compression format will auto-convert to DXT1 or DXT5.
  
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

### Parameter-multiplexing
Detailed principle:https://github.com/seanedwards/vrc-worldobject/blob/main/docs/parameter-multiplexing.md

This is a parameter-saving technique. After checking, OSC must always be turned on and Parameter-multiplexing must be checked in the OSC program.

This will slightly increase the time it takes to get back to your original avatar appearance in-game.

16 digits are not recommended because there may be VRChat security issues related to parameters, so 12 digits are recommended.

## Troubleshooting
**[is not supported texture format! Error]**

Select the texture and change the compression format to either DXT1 or DXT5 in the Inspector. (DXT5 for textures with transparency)

![texture](https://github.com/Shell4026/ShellProtector/assets/104874910/872f9d15-7b89-4381-b940-00514bd60638)

**[liltoon)At testor component, it doesn't come back to normal]**.

It's a bug in lilToon, so ignore it and upload it, or try one of three ways

1. Delete your own avatar folder inside the ShellProtect folder and re-encrypt it.
2. Try restarting Unity.
3. Click Assets - liltoon - Refresh Shader (It takes a while!)

**[If certain areas look monochromatic]**

For lilToon, check if the encrypted texture is missing from the main color part of the material and the Encrypted texture part of the custom properties and add it.

For Poiyomi, try encrypting it again.

**[If not decryption when viewed by others in-game]**

The other person needs to turn off shaders and animation safety or Show Avatar to you.

If they do, this is a VRChat synchronization of parameters bug, **please change your password and re-upload.**

**[When a Texture that was present in a Material is missing]**

When using a texture such as a main color/texture, it will be stripped for security reasons.

The exceptions are the limlight and outline textures, which will not be removed and will still use the encrypted texture.
   
## How it works
Encrypt the texture using an XXTEA algorithm after transforming the key with SHA-256.

After encrypting the texture itself, it is uploaded to the VRChat server. The texture is then decrypted in the game via shaders.

Shaders and Materials are copied, so the original is not affected.

Only the MainTexture is encrypted, so if you use the same texture elsewhere in the Material as the MainTexture, it will automatically be removed for security purposes. Fill in the gaps with the appropriate texture.

The exception to this is when the Rimlight texture and Outline texture are the same texture as the MainTexture, they use the same encrypted texture as the MainTexture.

## Is there any performance impact?
It takes up a little more memory than the original. It's about 1mb larger for a 2K DXT1 image.

On average, it's about 0.2ms~0.8ms slower based on the same 50 materials. Poiyomi performed better than lilToon.

## How secure is it?
By default, it has 16 bytes of keys, split between keys stored inside the shader and keys that the user can enter using VRC parameters. (I'll call these user keys.)

A user key of 0 bytes can be figured out by simply turning the compiled shader into an assembler and analyzing it.

A 4-byte user key can be figured out if someone takes the time to do so. (4-byte key = 32 parameters / 11 parameters(using parameter-multiplexing))

An 8-byte user key would take a long time to crack on a personal computer. (8-byte key = 64 parameters / 12 parameters)

User keys starting at 12 bytes are impossible to crack on a modern computer. (12-byte key = 96 parameter / 13 parameters)

It is safe to increase the number of user keys by using a minimum of 96 parameter spaces(13 when using parameter multiplexing). Please be mindful of your parameter space when setting your keys.

A 0-byte user key is a minimal defense, and should be effective against toolkiddies using simple tools.

## feature
- Support BC7
