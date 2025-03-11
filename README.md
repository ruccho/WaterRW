# WaterRW

WaterRW is 2D interactive water system for Unity.

![image](https://user-images.githubusercontent.com/16096562/156744259-b001ac3c-68a5-4a62-8d82-b08660f6a596.gif)

## 📜 Table of Contents

<!-- TOC -->
- [WaterRW](#waterrw)
  - [📜 Table of Contents](#-table-of-contents)
  - [🔥 Installation](#-installation)
    - [Requirements](#requirements)
    - [Install via UPM git dependency](#install-via-upm-git-dependency)
  - [👉 Quick Start](#-quick-start)
    - [1. Import Prefabs and Materials](#1-import-prefabs-and-materials)
    - [2. Place a prefab](#2-place-a-prefab)
    - [3. Enable reflection and refraction (optional)](#3-enable-reflection-and-refraction-optional)
  - [☑️ Material Settings Guide](#️-material-settings-guide)
  - [🏄 Enable Interactions](#-enable-interactions)
    - [`WaterRWCompute` Settings](#waterrwcompute-settings)
    - [Avoid Divergence](#avoid-divergence)
    - [Buffer Scrolling](#buffer-scrolling)
<!-- TOC -->

## 🔥 Installation

### Requirements

- Unity 2022.3 or later
- Compute shader compatible platform
- Universal Render Pipeline enabled
  - **2D Renderer** is also required to enable reflection and refraction.

### Install via UPM git dependency

Add git URL from Package Manager:

```
https://github.com/ruccho/WaterRW.git?path=/Packages/com.ruccho.water-rw
```

## 👉 Quick Start

### 1. Import Prefabs and Materials

- Import `Prefabs & Samples` from `Samples` page in Package Manager.

![](https://github.com/user-attachments/assets/c985c884-aebc-46f0-ab23-1fd8201bb03d)

### 2. Place a prefab

- Place `Prefab/Water-RW (Compute).prefab` to the scene.

### 3. Enable reflection and refraction (optional)

- To enable reflection and refraction, you have to use 2D Renderer.
  - https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/2DRendererData-overview.html

- Enable **Camera Sorting Layer Texture** at the layer you want to reflect and refract.

  - <img src="https://github.com/user-attachments/assets/ab5b7b92-2efc-4fce-bd0a-1616f670b871" height="700">
  
- Water-RW has to be rendered after Camera Sorting Layer Texture. Use **Mesh Renderer Sorting** component to configure the sorting layer of a Water-RW instance.

  - <img src="https://github.com/user-attachments/assets/5795dc20-d8c6-4f4b-94c5-cdb8b5565477" height="700">

## ☑️ Material Settings Guide

WaterRW uses the shader `Water-RW/With Compute`.

- <img src="https://github.com/user-attachments/assets/2b1ca942-90f3-4501-bf06-5c4d0977e604" height="700">

| Property                          | Type        |                                                                                              |
| --------------------------------- | ----------- | -------------------------------------------------------------------------------------------- |
| Tint                              | `Color `    | Tint color.                                                                                  |
| Pixel Snap                        | `Float `    | Same as one in Sprites-Default shader.                                                       |
| Normal A                          | `Texture2D` | Normal map A. Use tiling properties to scale a map.                                          |
| Normal A Intensity                | `Float `    | Amount of distortion of normal map A.                                                        |
| Normal A Speed                    | `Vector `   | Scroll speed of normal map A. Only X and Y works.                                            |
| Normal B                          | `Texture2D` | Same as normal A.                                                                            |
| Normal B Intensity                | `Float `    | Same as normal A.                                                                            |
| Normal B Speed                    | `Vector `   | Same as normal A.                                                                            |
| Background Blend                  | `Float `    | Rate of reflection blend.                                                                    |
| Transparency                      | `Float `    | Dark reflection areas goes transparent.                                                      |
| Multiplier                        | `Color `    | Color multiplier.                                                                            |
| Addend                            | `Color `    | Color addend.                                                                                |
| Wave Size in Viewport Space       | `Float `    | Horizontal size of near-surface distortion.                                                  |
| Wave Distance in Viewport Space   | `Float `    | Vertical size of near-surface distortion.                                                    |
| Wave Frequency by Position        | `Float `    | Frequency of sin curve used for near-surface distortion by vertical position.                |
| Wave Frequency by Time            | `Float `    | Frequency of sin curve used for near-surface distortion by vertical time.                    |
| Surface Color                     | `Color `    | Color of surface line.                                                                       |
| Surface Width in Pixel            | `Color `    | Width of surface line in pixels.                                                             |
| Fade Distance in Viewport Space   | `Float `    | Vertical size of fade to avoid display reflection areas out of Camera Sorting Layer Texture. |
| Smooth Buffer Edge in World Space | `Float `    | Size of smoothing area in world space at the edge of wave buffer.                            |
| Reflection Intensity              | `Float`     | Intensity of reflection.                                                                     |

## 🏄 Enable Interactions

WaterRW supports rough interation with rigidbodies. (Colliders with complex shapes may not be handled correctly!)

In inspector of `WaterRWCompute` script, select layers to interact with in `Layers To Interact With` property.

### `WaterRWCompute` Settings

![image](https://user-images.githubusercontent.com/16096562/142718932-c7c4274f-6a46-46f7-83e9-bf3b20c54ea2.png)

| Property                    | Type                 |                                                                                              |
| --------------------------- | -------------------- | -------------------------------------------------------------------------------------------- |
| Mesh segments Per Unit      | `float `             | Numbers of mesh divisions per unit.                                                          |
| Update Mode                 | FixedUpdate / Update | Timing to calculate wave. Use `FixedUpdate` to work interactions correctly.                  |
| Override Fixed Time Step    | `bool`               | Determine whether to use custom timestep.                                                    |
| Fixed Time Step             | `float`              | Custom time step.                                                                            |
| C                           | `float `             | The constant used in wave calculation. Increasing this will increase the speed of the waves. |
| Decay                       | `float `             | Coefficient for damping waves.                                                               |
| Enable Interaction          | `float `             | Determine whether to use interaction.                                                        |
| Layers To Interact With     | `LayerMask`          | Layers to interact with.                                                                     |
| Spatial Scale               | `float `             | Horizontal scale used in wave calculation.                                                   |
| Max Interaction Items       | `float `             | Max number of rigidbodies to interact with.                                                  |
| Wave Buffer Pixels Per Unit | `float `             | Resolution of buffers used to wave calculation.                                              |
| Scroll To Main Camera       | `bool `              | Track the range of the wave calculation to the position of the main camera.                  |
| Flow Velocity               | `float `             | Velocity of the flow.                                                                        |
| Max Surface width           | `float `             | Max width of the wave in world scale.                                                        |

### Avoid Divergence

Values of `Fixed Time Step`, `C`, `Spatial Scale` and `Wave Buffer Pixels Per Unit` may cause divergence.

To avoid divergence, keep: **`0 ≤ (C * dt / dx) ≤ 1`**.

`dt` = time step (`Fixed Time Step` when `Override Fixed Time Step` is true, otherwise `Time.deltaTime` or `Time.fixedDeltaTime` is used)

`dx` = `Spatial Scale` / `Wave Buffer Pixels Per Unit`


### Buffer Scrolling

Although the size of the buffer used for wave calculation is finite, interaction with the seemingly infinite surface of the water can be achieved by making the wave calculation range follow the camera position.
If `Scroll To Main Camera` is true, it will automatically follow `Camera.main`.
To set it manually, set the X coordinate to `float WaterRWCompute.WavePosition`.