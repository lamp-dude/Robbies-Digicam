# Robbie's Digicam

[![Stripe](https://img.shields.io/badge/Donate-Stripe-635BFF?logo=stripe&logoColor=white)](https://buy.stripe.com/5kQcN4fyB5Jp0p8aY91VK03)

**A configurable early 2000s digital camera look for Unity URP.** Robbie's Digicam combines a low resolution 4:3 display with digital zoom, center ray autofocus, CCD style color noise, simulated video compression, washed highlights, and convolution directional bloom. A first person playground scene shows the effects together. Since this is open-source, a donation would be much appreciated! ^^

> **Project status:** This is a Unity project and demo, not a packaged Unity package. The original code and documentation are [MIT licensed](LICENSE).

## Contents

- [Overview](#overview)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Demo controls](#demo-controls)
- [How it works](#how-it-works)
- [Rendering internals](#rendering-internals)
- [Performance and limitations](#performance-and-limitations)
- [Contributing](#contributing-and-publishing)

## Overview

<img width="340" height="178" alt="image" src="https://github.com/user-attachments/assets/29ae63f8-895d-43a3-82fc-a40eea69e0ea" />
<img width="340" height="178" alt="image" src="https://github.com/user-attachments/assets/c8f8f0dd-2478-41a7-8fb4-f827abaec1e1" />
<img width="340" height="178" alt="image" src="https://github.com/user-attachments/assets/42793b58-6197-4314-b447-aced5d14a2e5" />



The project is intended for those that are making found footage games, nostalgic camera views, in world camcorder displays, retro photography scenes, and experiments with low bitrate imagery. Each custom image effect has a URP renderer feature and a Volume override, so every part of the look can be tuned or disabled individually. I wanted to make this open source because many of the other free or even paid options for this effect is rarely good and most of the time just looks like a cheap overlay. 

| Feature | Visible result | Main control |
| --- | --- | --- |
| 640×480 display | Point filtered 4:3 picture with black bars on wider windows | Camera Render Texture and overlay Canvas |
| Digital zoom | Enlarged center crop without changing camera field of view | `Digicam Digital Zoom` Volume override or mouse wheel |
| Autofocus | Depth of field follows the nearest collider under the center of the image | `DigicamCameraControls` and URP `Depth Of Field` |
| CCD color noise | Colored sensor grain, stronger in shadows, with a partly fixed pattern | `CCD Color Noise` Volume override |
| Video compression | Block detail loss, color bleed, quantization, and crawling edges | `Digicam Video Compression` Volume override |
| Washed range | Raised shadows and earlier white clipping | `Digicam Washed Range` Volume override |
|  Convolution bloom | Kernel shaped purple vertical streak and soft halo around highlights | `Digicam Convolution Bloom` Volume override |
| Camera motion | Breathing, walking motion, sideways lean, and landing dip | `ProceduralCameraShake` component |

The demo profile also uses built in URP color adjustments, white balance, vignette, motion blur, chromatic aberration, tonemapping, and bloom. Those settings contribute to the supplied look but are separate from the custom effects

## Requirements

| Dependency | Version in this project | Purpose |
| --- | --- | --- |
| Unity Editor | **6000.3.16f1** | Project serialization and Unity 6 Render Graph APIs |
| Universal Render Pipeline | **17.3.0** | Renderer features, Volumes, post processing, and shaders |

## Quick start

1. Open the project in **Unity 6000.3.16f1**.
2. Open [`DIGICAM PLAYGROUND.unity`](Assets/Robbie's%20Digicam/Demo/DIGICAM%20PLAYGROUND.unity).
3. Enter **Play** mode.
4. Move through the scene, point at objects at different distances, and scroll to zoom.
5. Select **Global Volume** to edit the look in its profile. Inspect the custom renderer features in [`PC_Renderer.asset`](Assets/Robbie's%20Digicam/Demo/PC_Renderer.asset).

The demo already contains the camera, first person player, Global Volume, 640×480 display, and Digicam renderer features. 

## Demo controls

| Input | Action |
| --- | --- |
| `W` `A` `S` `D` | Move |
| Mouse | Look    |
| Left or right `Shift` | Sprint |
| `Space` | Jump |
| Mouse wheel | Digital zoom in 0.25× steps, from 1× to 3× |
| `Escape` | Release cursor |
| Left click | Lock cursor  |

The player uses a `CharacterController`. The camera motion component adds an idle breath, movement waves, sprint response, sideways lean, and landing dip. Zoom and autofocus run on the camera.

## How it works
The `Main Camera` renders to [`Digicam640x480.renderTexture`](Assets/Robbie's%20Digicam/DigicamDisplay/Digicam640x480.renderTexture), an HDR 640×480 texture with point filtering. A screen space Canvas displays it in a `RawImage` with a 4:3 aspect fitter. A black Image fills the remaining screen area. Changing the Game view size changes the displayed picture size, not the 640×480 source.

Each custom image effect is a `ScriptableRendererFeature` that reads a matching Volume component, creates a material from a `Hidden/Memories/...` shader, and records passes into URP Render Graph. Most operate after URP post processing. Convolution bloom extracts HDR highlights **before** post processing and composites its streak and halo **after** post processing. Its highlight, streak, and halo textures use one eighth of the source width and height. The demo renderer also contains URP screen space ambient occlusion for a more realistic view.

`DigicamCameraControls` changes a runtime Volume profile. Mouse wheel input eases the image space zoom value. A center of view `Physics.RaycastNonAlloc` finds the nearest hit, ignores colliders under the player root, and eases URP Bokeh Depth of Field's focus distance toward it. If nothing is hit, focus returns to a fallback distance. Digital zoom crops the rendered image, it does not move the camera or alter its field of view.

| Stage | Implementation |
| --- | --- |
| Bloom source | Thresholds HDR highlights and can add a sun impulse when the directional light is in frame and its depth sample indicates visible sky |
| Bloom shape | Convolves highlights with a point spread kernel for a colored vertical streak; separate horizontal and vertical blurs form the halo |
| Washed range | Maps black to a gray floor and scales values toward white at a configurable clip point |
| Compression | Approximates 2×2 chroma subsampling, block detail loss, luma and chroma quantization, block drift, and edge local crawling noise |
| CCD noise | Hashes color cells for independent RGB noise, mixes fixed and changing patterns, and weights noise toward shadows |
| Zoom | Remaps UVs around the image center before sampling the rendered image |

These are **visual simulations**. Soooo not the most accurate by gets the job done :)

## Rendering internals

Each custom effect reads a Volume component and records a URP Render Graph pass. The image passes sample the active color texture into an intermediate texture, then copy the result back. 

### Convolution bloom

[`DirectionalBloom.shader`](Assets/Robbie's%20Digicam/DirectionalBloom/DirectionalBloom.shader) first extracts HDR highlights with a soft threshold. It can add a sun impulse when the directional light is on screen and the depth buffer shows visible sky. The renderer feature stores highlights, streak, and halo in half float textures at one eighth of the camera width and height (80×60 for the demo).

The streak is a **direct two dimensional convolution** over 9 horizontal by 129 vertical samples. At image coordinate $u$, the shader computes:

$$
S(u)=T\odot\frac{\displaystyle\sum_{i=-4}^{4}\sum_{j=-64}^{64} B(u+\Delta_{ij})K_{ij}}{\displaystyle\max\!\left(\sum_{i=-4}^{4}\sum_{j=-64}^{64} w\cdot K_{ij},\,10^{-4}\right)},
\qquad
\Delta_{ij}=\left(\frac{12(i/4)}{W},\frac{R(j/64)}{H}\right).
$$

Here $B$ is the bright texture, $K_{ij}$ is the RGB kernel sample **including the shader's end taper**, $w=(0.2126,0.7152,0.0722)$ is the luminance weight vector, $T$ is `Stripe Tint`, $R$ is `Vertical Radius`, and $W,H$ are source dimensions. The narrow horizontal span and long vertical span give the light its streak. The denominator normalizes by kernel luminance. This sampled convolution is neither an FFT nor a separable blur.

A separate halo uses a 17 tap horizontal blur followed by a 17 tap vertical blur. After URP post processing, the shader adds both results to the image:

$$C_{\mathrm{out}}=C_{\mathrm{post}}+I_s S+I_h H_{\mathrm{halo}}.$$

`Intensity` supplies $I_s$ and `Halo Intensity` supplies $I_h$. A kernel texture is required for the bloom effect to run, it can be any but for the best digicam effect, I made one that looks like purple light bleeding in early digicams. See [`DirectionalBloomRendererFeature.cs`](Assets/Robbie's%20Digicam/DirectionalBloom/DirectionalBloomRendererFeature.cs) for pass allocation and timing.

### Compression and CCD noise

[`VideoCompression.shader`](Assets/Robbie's%20Digicam/VideoCompression/VideoCompression.shader) divides the image into blocks, blends each pixel's luma with four block samples, averages chroma over 2×2 pixel footprints, and quantizes luma and chroma. A block hash changes brightness every five frames; edge weighted pixel noise changes every three frames. It reconstructs RGB and blends the result with the original using `Intensity`. Looks pretty nice depending on the vaules!

[`CCDColorNoise.shader`](Assets/Robbie's%20Digicam/CCDNoise/CCDColorNoise.shader) hashes pixel cells independently for RGB, blends fixed and frame changing patterns, and adds fine pixel noise. It removes much of the noise's luma component. The amplitude is weighted by $1+\text{ShadowBoost}(1-L)^2$, then suppressed near white, so grain is strongest in shadows, which I think is how it works in older cameras? There is no noise texture.

### Range and zoom

[`DigicamRange.shader`](Assets/Robbie's%20Digicam/DigicamRange/DigicamRange.shader) maps the black point toward `Low` and clips colors at `High` toward white. A luminance smoothstep adds further highlight clipping, and `Strength` blends with the original image. Allowung for that often washed out look.

[`DigitalZoom.shader`](Assets/Robbie's%20Digicam/DigitalZoom/DigitalZoom.shader) crops around the image center with the exact sample transform:

$$u_{\mathrm{sample}}=\frac{u-0.5}{\max(\text{Zoom},1)}+0.5.$$

DigicamCameraControls` eases the zoom value and separately uses a center raycast to set URP Bokeh focus distance from scene colliders. Bloom extraction runs before post processing; bloom composite and the other custom image effects run afterward. The active renderer's feature order affects their final layering.

## Use it in another scene

1. Duplicate the demo scene or copy its **Main Camera**, **Global Volume**, and **Digicam Screen** hierarchy into your scene.
2. Keep the camera tagged `MainCamera`. Enable **Post Processing** on its URP camera data. Use a URP asset whose renderer contains the Digicam features. The demo pair is [`PC_RPAsset.asset`](Assets/Robbie's%20Digicam/Demo/PC_RPAsset.asset) and [`PC_Renderer.asset`](Assets/Robbie's%20Digicam/Demo/PC_Renderer.asset).
3. Add the desired Digicam Volume overrides. You can use the supplied [demo profile](Assets/Robbie's%20Digicam/Demo/SampleSceneProfile.asset) as a starting point. Assign a bloom kernel for directional streaks.
4. For the 4:3 presentation, assign the 640×480 Render Texture to both the camera's **Target Texture** and the screen `RawImage`. Use a 4:3 `AspectRatioFitter` and black backing Image.
5. Add `DigicamCameraControls` to the camera for interactive zoom and autofocus. Give focus targets colliders. Add `DigicamFrameRate` only if you want its frame rate behavior.
6. Optionally add `FirstPersonController` to a parent with a `CharacterController`, and `ProceduralCameraShake` to its camera. The image effects do not require the demo player.

### Editor setup commands

The **Tools → Digicam** menu contains these conveniences:

| Command | What it changes | Important detail |
| --- | --- | --- |
| `Create First Person Player` | Creates or updates a player, adds a `CharacterController`, and reparents the Main Camera | Requires a `MainCamera` tagged camera; saves the scene |
| `Set Up 640x480 Display` | Assigns the Render Texture, adds `DigicamFrameRate`, and rebuilds the `Digicam Screen` Canvas | Replaces a GameObject named `Digicam Screen`; saves the scene |
| `Add Digital Zoom and Autofocus` | Adds camera controls and Zoom/Depth of Field Volume overrides | Requires a Global Volume with a profile; saves scene and assets |

**Renderer asset distinction:** `Add Digital Zoom and Autofocus` installs its renderer feature into [`Assets/Settings/PC_Renderer.asset`](Assets/Settings/PC_Renderer.asset). The demo uses the separate [`Demo/PC_Renderer.asset`](Assets/Robbie's%20Digicam/Demo/PC_Renderer.asset), where the feature is already present. With another URP renderer, add the custom features in its **Renderer Features** inspector. A Volume override alone is insufficient.

`DigicamFrameRate` sets `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 90` at runtime. It does **not** emulate a low camera frame rate. Remove or disable it if the host game controls frame timing.

## Performance and limitations

- Each active effect adds Render Graph passes and intermediate textures. The 640×480 target limits pixel count, but kernel convolution bloom is comparatively expensive because it samples a 2D neighborhood at one eighth resolution. Profile on target hardware, though overall the effect doesn't effect too much preformance issues.
- Bloom, CCD noise, compression, and washed range accept Game and Scene View cameras. Digital zoom is restricted to Game cameras by its renderer feature.
- Effects skip their pass if URP's active target is the back buffer. Use the supplied Render Texture arrangement or an equivalent intermediate target configuration.
- Autofocus uses physics colliders and the center ray. Uncollidable geometry, transparent surfaces without a suitable collider, and objects beyond `maximumFocusDistance` cannot become focus targets.
  
## Contributing and publishing

For effect changes, update the shader, Volume component, and renderer feature together. Keep ranges and tooltips aligned. Exercise the change in the playground scene with the effect enabled and disabled. Bug reports should include Unity and URP versions, platform, active renderer asset, relevant Volume values, and reproduction steps.

Commit `Assets`, `Packages`, `ProjectSettings`, this README, `LICENSE`, and Unity `.meta` files. The supplied `.gitignore` excludes caches, logs, temporary files, and generated IDE projects.

### Licensing

The original source code and documentation are licensed under the [MIT License](LICENSE), with copyright credited to **Robbie Ma**. Redistributors must keep the copyright and permission notice.

### AI Usage

All main functions and major things were 100% done by me, a human. But code polishing, debugging, and editing this readme was by AI.

