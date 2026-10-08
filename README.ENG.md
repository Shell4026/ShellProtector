# ShellProtector

[![Downloads](https://img.shields.io/github/downloads/Shell4026/ShellProtector/total?color=6451f1)](https://github.com/Shell4026/ShellProtector/releases/latest)
[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FShell4026%2FShellProtector&count_bg=%2379C83D&title_bg=%23555555&icon=&icon_color=%23E7E7E7&title=hits&edge_flat=false)](https://hits.seeyoufarm.com)

[한국어](./README.md) | **English** | [日本語](./README.JP.md)

ShellProtector encrypts a VRChat avatar's textures before upload, and a shader decrypts them in real time in game.

If someone rips the avatar from the cache, its textures come out as noise, so the avatar is hard to copy as is or to edit and redistribute. Part of the decryption key can only be derived from a password, which you enter in game with the accompanying OSC program.

- VCC repository: https://shell4026.github.io/VCC/
- OSC program: [ShellProtectorOSC](https://github.com/Shell4026/ShellProtectorOSC)

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Password and OSC](#password-and-osc)
- [Material advanced settings](#material-advanced-settings)
- [How it works](#how-it-works)
- [Performance](#performance)
- [Security and limitations](#security-and-limitations)
- [Troubleshooting](#troubleshooting)

## Features

- Main texture encryption. The texture shows as noise until the password is correct.
- Optional emission map encryption. Selected maps emit no light until the password is correct.
- Fallback textures. Viewers whose Safety settings block shaders, or who see the avatar before it is decrypted, get a low-resolution texture instead of noise.
- BlendShape obfuscation. BlendShape names on the selected meshes are made unreadable.
- With Modular Avatar (NDMF), the avatar is encrypted automatically on upload, and the original avatar stays untouched.

## Requirements

| | Supported |
| --- | --- |
| Unity | 2022.3 |
| VRChat SDK | Avatars 3.1 or later |
| Shaders | Poiyomi 8.x, 9.x, 10.x (including Pro), PCSS4Poi (not recommended)<br>lilToon 1.3.8 – 2.3.4 |
| Texture formats | RGB24, RGBA32, DXT1, DXT5, BC7 |

- Crunch-compressed textures are converted to DXT1/DXT5 automatically.
- BC7 costs about twice as much GPU time and memory as DXT. Use it only for textures that need its quality. ([Cost by texture format](#cost-by-texture-format))

## Installation

Install it with VCC (VRChat Creator Companion).

<img width="1260" height="284" alt="VCC" src="https://github.com/user-attachments/assets/986aebcd-4b7f-4af0-99ba-6ce0cf9da117" />

1. Click **Add to VCC** on https://shell4026.github.io/VCC/, or add this URL in VCC under Settings → Packages → Add Repository:
   ```
   https://Shell4026.github.io/VCC/index.json
   ```
2. Add **Shell Protector** to your project in Manage Project.

## Usage

1. Right-click the avatar in the Hierarchy and select **ShellProtector**. The component is added under the avatar, and the `Body` object (the one with the face) is added as a target.
2. Add the objects or materials to encrypt.
3. Set a user password (up to 16 characters). See [Password and OSC](#password-and-osc).
4. Upload the avatar.

**If Modular Avatar is installed, you're done.** The avatar is encrypted on upload.

Without Modular Avatar, encrypt it yourself before uploading:

1. Click **Encrypt!**. The avatar is duplicated and the original is deactivated.
2. On the duplicate's Shell Protector Tester component, click **Check encryption success**. If the avatar looks like the original, it worked.
3. Click **Done & Reset**, then upload the duplicated avatar.

> Encrypting too many objects can cause lag when the avatar loads in game. Encrypt only the textures you really need to protect.

## Password and OSC

You enter the password in game through the OSC program to make the avatar look normal.

> **ShellProtector 3.0 requires ShellProtectorOSC 1.7 or later.** Older OSC versions can't unlock the avatar. The **Download latest OSC** button in the inspector gets the latest one.

1. Download ShellProtectorOSC, unzip it and run `ShellProtectorOSC.exe`.
2. Switch to the uploaded avatar in VRChat.
3. Enter your user password in the OSC program.

### Password

The password is up to 16 characters, and the whole 16-byte key comes from it.

### Sync speed

The number of key bytes synced at once.

- **1**: Uses the fewest parameters. The key isn't saved in the avatar, so the OSC program has to keep running while you play.
- **2, 4**: The key syncs faster and is saved in the avatar. You only need to run the OSC program once.

Parameter cost in bits:

| Speed 1 | Speed 2 | Speed 4 |
| --- | --- | --- |
| 13 | 20 | 35 |

The inspector shows how many bits are used and how many are free.

## Material advanced settings

The **Material advanced settings** window lets you change these per material:

- Whether to encrypt it
- Texture filter (Point / Bilinear). Point can alias but is cheaper.
- Fallback texture (white, black, 4x4 to 128x128)
- Which emission slots to encrypt

### Fallback

Viewers whose Safety settings disable shaders see a small fallback texture instead of noise. So does everyone before the password is entered. **Unlit safety fallback** in the advanced options sets every shader's Safety Fallback to Unlit.

![fallback](https://github.com/user-attachments/assets/d3ca69b0-ff08-4793-a4e4-73269bc8efd3)

### Emission encryption

Off by default. Only the checked slots are encrypted; unchecked maps stay as they are. An unchecked slot that holds an encrypted main texture gets the fallback texture instead, so the original isn't exposed.

- Slots: Emission 0–3 for Poiyomi, Emission 1–2 for lilToon.
- An emission map may differ from the main texture in size and format. It must be a power-of-two RGB24, RGBA32, DXT1 or DXT5 texture, at least 8x4 for DXT.
- An emission map that is the main texture at the same UV (UV channel, tiling and offset) is not encrypted again: it uses the main texture's decrypted color. It adds almost no size or GPU cost and takes any format. Settings that make the two UVs differ, such as panning only the emission, Poiyomi's Center Out, or lilToon's parallax and UV scrolling, make it encrypted on its own.
- UV transforms, emission color, masks and blending keep working. Masks and gradient textures themselves are not encrypted.
- Filtering and wrapping follow the main texture on Poiyomi and the emission map's own settings on lilToon.
- Anisotropic filtering is not supported.

## How it works

Textures are encrypted with ChaCha before upload, and a shader decrypts them per pixel in game. Part of the 16-byte key lives in the material and the shader; the rest is derived from the password with PBKDF2 and delivered through OSC and avatar parameters. DXT textures keep their compressed format because only the color endpoints are encrypted.

The key layout, texture layout, shader injection and GPU optimizations are covered in the [technical write-up](./HOW_IT_WORKS.md) (Korean only).

## Performance

Decryption runs per pixel while the avatar is drawn, so the extra GPU time grows with **how much of the screen the avatar covers** and **how many materials are encrypted**.

### Measurements

When only the face and body are encrypted, the typical setup, the added time stays at **about 1–3% for Poiyomi and 2.5–7% for lilToon** of a 90 fps VR frame budget (11.1 ms), even with emission encrypted too.

**Face + body encrypted** (ms per frame, increase over the original in parentheses)

| Shader | View | Original | Encrypted | Encrypted + emission |
| --- | --- | --- | --- | --- |
| Poiyomi | Full body | 0.155 | 0.202 (+0.05) | 0.276 (+0.12) |
| Poiyomi | Close-up | 0.310 | 0.480 (+0.17) | 0.619 (+0.31) |
| lilToon | Full body | 0.592 | 0.719 (+0.13) | 0.865 (+0.27) |
| lilToon | Close-up | 1.475 | 1.835 (+0.36) | 2.268 (+0.79) |

**All 23 materials encrypted** (stress test)

| Shader | View | Original | Encrypted | Encrypted + emission |
| --- | --- | --- | --- | --- |
| Poiyomi | Full body | 0.147 | 0.848 (+0.70) | 1.639 (+1.49) |
| Poiyomi | Close-up | 0.299 | 2.074 (+1.78) | 3.895 (+3.60) |
| lilToon | Full body | 0.581 | 1.279 (+0.70) | 2.176 (+1.60) |
| lilToon | Close-up | 1.466 | 3.239 (+1.77) | 5.447 (+3.98) |

- Main texture encryption costs about the same on Poiyomi and lilToon. It looks larger on Poiyomi as a percentage only because the unencrypted Poiyomi material is much lighter.
- Emission encryption costs 1.1–3× more on lilToon than on Poiyomi, and the gap widens as the avatar fills more of the screen.
- Encrypting every material multiplies the cost. Encrypt only the materials you need to protect, and only the emission slots you need.

<details>
<summary>Test setup</summary>

- GPU: NVIDIA GeForce RTX 3060 Ti (Direct3D 11), CPU: Intel Core i5-12400F
- Unity 2022.3.22f1 editor, ShellProtector 2.8.0
- Avatar: Manuka, with new Poiyomi Toon (locked) and lilToon materials using the original main textures
  - Face 2048 RGB24, body and costume 2048 DXT1, hair 4096 DXT1
  - Emission map: the same 2048 DXT1 texture on every material, bilinear
- Encryption: ChaCha, bilinear filter
- Screen: 2880x2880, no MSAA, one directional light, front view, T-pose
  - Full body: the avatar covers about 18% of the screen (about 1.5 M pixels)
  - Close-up: about 48% (about 4 M pixels)
- 30 warm-up frames, then 7 runs of 100 frames per case, interleaved; the median is reported

</details>

These are editor measurements. In VRChat, with other avatars, post-processing and stereo VR rendering, the absolute numbers will differ, so read them as a comparison under equal conditions. AMD GPUs haven't been measured yet; they are more sensitive to integer division and divergent branches and may show higher costs than NVIDIA.

### Cost by texture format

On another avatar, the face and body materials were encrypted with their main textures converted to each format. (Poiyomi, ms per frame; in parentheses, the increase: over the original avatar for full body and face close-up, and over the unencrypted material of the same format for the full screen.) The avatar and screen size differ from the measurements above, so the absolute numbers can't be compared with them.

| Format | Full body | Face close-up | One material filling the screen | Memory (2 textures) |
| --- | --- | --- | --- | --- |
| Original | 0.934 | 2.152 | 2.673 | |
| DXT1 | 0.994 (+0.06) | 2.661 (+0.51) | 6.182 (+3.51) | 5.3 MB → 8.0 MB |
| DXT5 | 1.001 (+0.07) | 2.667 (+0.52) | 6.173 (+3.49) | 10.7 MB → 13.4 MB |
| RGB24 | 1.019 (+0.09) | 2.880 (+0.73) | 7.267 (+4.54) | 32.0 MB → 32.0 MB |
| RGBA32 | 1.029 (+0.10) | 2.867 (+0.72) | 7.335 (+4.66) | 42.7 MB → 42.7 MB |
| BC7 | 1.121 (+0.19) | 3.268 (+1.12) | 10.082 (+7.37) | 10.7 MB → 21.4 MB |

- DXT1/DXT5 are the lightest and grow the least in memory. Use DXT where you can.
- RGB24/RGBA32 cost about 30% more than DXT, and take a lot of memory because they are uncompressed to begin with.
- BC7 costs about twice as much as DXT and doubles in memory. Its blocks have different modes, so the shader has to decode them itself.

<details>
<summary>Test setup</summary>

- GPU: NVIDIA GeForce RTX 3060 Ti (Direct3D 11)
- Unity 2022.3.22f1 (batch mode), ShellProtector 3.0.0
- Avatar: Poiyomi Pro (locked). Only the face and body materials are encrypted, with both 2048 main textures converted to each format (with mipmaps). The originals are DXT1 (body) and BC7 (face).
- Encryption: ChaCha, bilinear filter, no emission
- Screen: 3840x2160, no MSAA, one directional light, front view, T-pose
  - Full body: the whole avatar fits the screen height
  - Face close-up: the face fills the screen height
  - One material filling the screen: the body material drawn on a full-screen quad
- 9 runs of 40 frames per case, interleaved; the median is reported, without the cost of an empty camera render (about 0.11 ms)

</details>

### Memory

Slightly more than the original: about 1 MB for a 2K DXT1 texture. RGB24/RGBA32 barely grow, and BC7 about doubles. An encrypted emission map takes about 1.5× (DXT1) or 1.25× (DXT5) its original size.

### Reducing the cost

- Encrypt only the materials and emission slots you really need.
- Turn on **Small mip texture** in the advanced options. It saves memory and improves performance, though the avatar may look slightly different at steep angles.

## Security and limitations

No protection is perfect. ShellProtector is meant to stop **indiscriminate ripping and sharing of avatars**, and with a password that is hard to guess it does.

- Someone in the same world who persistently analyzes network packets can capture the synced key values.
- The user key is derived from the password, so a short or guessable password weakens it. Use all 16 characters and a password that is hard to guess.

## Troubleshooting

Please report problems in [Issues](https://github.com/Shell4026/ShellProtector/issues).

### `is not supported texture format!`

Select the texture and change its format to DXT1/RGB24 or DXT5/RGBA32 in the Inspector. Use DXT5 or RGBA32 for textures with transparency.

![texture](https://github.com/Shell4026/ShellProtector/assets/104874910/872f9d15-7b89-4381-b940-00514bd60638)

### The avatar doesn't change after entering the password

1. Make sure the OSC program is 1.7 or later.
2. In VRChat, open the Action Menu and click Options → OSC → Reset Config.
3. If that doesn't help, delete `C:\Users\<user>\AppData\LocalLow\VRChat\VRChat\OSC`.

### Others still see the encrypted avatar

- They need to turn off shader and animation Safety, or use Show Avatar on you.
- At sync speed 1, values may not reach others depending on server or network conditions. Raise the Refresh rate in the OSC program a little, or use sync speed 2 or higher.
- Parameter compression assets can also cause this.
- If nothing else works, it may be a VRChat parameter sync issue. Change the password and upload again.

### lilToon: the avatar doesn't return to normal in the tester

This is a lilToon issue, and the uploaded avatar is often fine. Try one of these:

1. Delete your avatar's folder under `Assets/ShellProtector/Generated` and encrypt again.
2. Restart Unity.
3. Click Assets → lilToon → Refresh Shaders. (This takes a while.)

### Some parts are a single color

Encrypt again and restart Unity.

### A texture disappeared from the material

If the main texture is also used in another slot, it's removed from that slot so the original doesn't leak through it. Put a different texture in the empty slot. Rim light and outline textures are the exception: they use the decrypted main texture.

## License

[MIT](./LICENSE)
