# WaterRW

[日本語版](<README (ja).md>)

WaterRW is 2D interactive water system for Unity.

![001](https://user-images.githubusercontent.com/16096562/73915969-d07dff00-48ff-11ea-8049-35ed87a50215.gif)

# Requirements

- This project is made with Unity 2020.3.12f1.
- By default, `Burst` package is required.

# Quick Start

1. Download `WaterRW.unitypackage` from [releases](https://github.com/ruccho/WaterRW/releases) page.
2. Install `Burst` package from Package Manager.
3. Add `Ruccho/Water-RW/Prefabs/Water-RW (Compute).prefab` to your scene.

Water-RW prefab is composed of `WaterRWCompute` script, `MeshFilter`, and `MeshRenderer` with `Water-RW/With Compute` material.

Some platforms such as WebGL or old mobile devices don't support Compute Shader. Make sure your target platform does before using WaterRW.

(Legacy implementation with C# Job System is also included in the package but it is no longer supported.)

# Material Settings Guide

WaterRW uses the shader `Water-RW/With Compute`.

![image](https://user-images.githubusercontent.com/16096562/73915083-e68ac000-48fd-11ea-84b7-42de766e5da0.png)

| Property                        | Type        |                                                                               |
| ------------------------------- | ----------- | ----------------------------------------------------------------------------- |
| Tint                            | `Color `    | Tint color.                                                                   |
| Pixel Snap                      | `Float `    | Same as one in Sprites-Default shader.                                        |
| Normal A                        | `Texture2D` | Normal map A. Use tiling properties to scale a map.                           |
| Normal A Intensity              | `Float `    | Amount of distortion of normal map A.                                         |
| Normal A Speed                  | `Vector `   | Scroll speed of normal map A. Only X and Y works.                             |
| Normal B                        | `Texture2D` | Same as normal A.                                                             |
| Normal B Intensity              | `Float `    | Same as normal A.                                                             |
| Normal B Speed                  | `Vector `   | Same as normal A.                                                             |
| Background Blend                | `Float `    | Rate of reflection blend.                                                     |
| Transparency                    | `Float `    | Dark reflection areas goes transparent.                                       |
| Multiplier                      | `Color `    | Color multiplier.                                                             |
| Addend                          | `Color `    | Color addend.                                                                 |
| Wave Size in Viewport Space     | `Float `    | Horizontal size of near-surface distortion.                                   |
| Wave Distance in Viewport       | `Float `    | Vertical size of near-surface distortion.                                     |
| Wave Frequency by Position      | `Float `    | Frequency of sin curve used for near-surface distortion by vertical position. |
| Wave Frequency by Time          | `Float `    | Frequency of sin curve used for near-surface distortion by vertical time.     |
| Surface Color                   | `Color `    | Color of surface line.                                                        |
| Surface Width in Pixel          | `Color `    | Width of surface line in pixels.                                              |
| Fade Distance in Viewport Space | `Float `    | Vertical size of fade to avoid display reflection areas out of GrabPass.      |

# Enable Interactions

![image](https://user-images.githubusercontent.com/16096562/156744259-b001ac3c-68a5-4a62-8d82-b08660f6a596.gif)

WaterRW supports rough interation with rigidbodies. (Colliders with complex shapes may not be handled correctly!)

In inspector of `WaterRWCompute` script, select layers to interact with in `Layers To Interact With` property.

## `WaterRWCompute` Settings

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
| Max Surface width           | `float `             | Max width of the wave in world scale.                                                        |

## Avoid Divergence

Values of `Fixed Time Step`, `C`, `Spatial Scale` and `Wave Buffer Pixels Per Unit` may cause divergence.

To avoid divergence, keep `0 ≤ (C * dt / dx) ≤ 1`.

---

`dt` = time step (`Fixed Time Step` when `Override Fixed Time Step` is true, otherwise `Time.deltaTime` or `Time.fixedDeltaTime` is used)

`dx` = `Spatial Scale` / `Wave Buffer Pixels Per Unit`

---


## Buffer Scrolling

Although the size of the buffer used for wave calculation is finite, interaction with the seemingly infinite surface of the water can be achieved by making the wave calculation range follow the camera position.
If `Scroll To Main Camera` is true, it will automatically follow `Camera.main`.
To set it manually, set the X coordinate to `float WaterRWCompute.WavePosition`.