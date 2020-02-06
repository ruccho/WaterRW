# WaterRW
WaterRW is 2D interactive water optimized by C# Job System.

![001](https://user-images.githubusercontent.com/16096562/73915969-d07dff00-48ff-11ea-8049-35ed87a50215.gif)

# Requirements

- This project is made with Unity 2018.4.15f1.
- By default, `Burst` package is required.

# Quick Start

1. Download `WaterRW.unitypackage` from [releases](https://github.com/ruccho/WaterRW/releases) page.
2. Install `Burst` package from Package Manager.
3. Add `Ruccho/Water-RW/Prefabs/Water-RW.prefab` to your scene.

Water-RW prefab is composed of `WaterRWaver` script, `MeshFilter`, and `MeshRenderer` with `Water-RW/Standard` material.

# Material Settings Guide

WaterRW uses the shader `Water-RW/Standard`.

![image](https://user-images.githubusercontent.com/16096562/73915083-e68ac000-48fd-11ea-84b7-42de766e5da0.png)


| Property                        | Type        |                                                                               |
|---------------------------------|-------------|-------------------------------------------------------------------------------|
| Tint                            | `Color  `   | Tint color.                                                                   |
| Pixel Snap                      | `Float  `   | Same as one in Sprites-Default shader.                                        |
| Normal A                        | `Texture2D` | Normal map A. Use tiling properties to scale a map.                           |
| Normal A Intensity              | `Float  `   | Amount of distortion of normal map A.                                         |
| Normal A Speed                  | `Vector `   | Scroll speed of normal map A. Only X and Y works.                             |
| Normal B                        | `Texture2D` | Same as normal A.                                                             |
| Normal B Intensity              | `Float  `   | Same as normal A.                                                             |
| Normal B Speed                  | `Vector `   | Same as normal A.                                                             |
| Background Blend                | `Float  `   | Rate of reflection blend.                                                     |
| Transparency                    | `Float  `   | Dark reflection areas goes transparent.                                       |
| Multiplier                      | `Color  `   | Color multiplier.                                                             |
| Addend                          | `Color  `   | Color addend.                                                                 |
| Wave Size in Viewport Space     | `Float  `   | Horizontal size of near-surface distortion.                                   |
| Wave Distance in Viewport       | `Float  `   | Vertical size of near-surface distortion.                                     |
| Wave Frequency by Position      | `Float  `   | Frequency of sin curve used for near-surface distortion by vertical position. |
| Wave Frequency by Time          | `Float  `   | Frequency of sin curve used for near-surface distortion by vertical time.     |
| Surface Color                   | `Color  `   | Color of surface line.                                                        |
| Surface Width in Pixel          | `Color  `   | Width of surface line in pixels.                                              |
| Fade Distance in Viewport Space | `Float  `   | Vertical size of fade to avoid display reflection areas out of GrabPass.      |

# Enable interactions

![001](https://user-images.githubusercontent.com/16096562/73915969-d07dff00-48ff-11ea-8049-35ed87a50215.gif)

WaterRW supports rough interation with rigidbodies. (Colliders with complex shapes are not handled correctly!) Wave caluculation based on the wave equation and mesh distortion is executed on worker threads through C# Job System.

In inspector of `WaterRWaver` script, select layers to interact with in `LayersToInteract` property.

## `WaterRWaver` Settings

![image](https://user-images.githubusercontent.com/16096562/73915132-06ba7f00-48fe-11ea-9823-d211fcc445e9.png)

| Property                       | Type        |                                                                                                                                       |
|--------------------------------|-------------|---------------------------------------------------------------------------------------------------------------------------------------|
| Resolution                     | `int      ` | Numbers of mesh divisions per unit.                                                                                                   |
| Wave Constant                  | `float    ` | The constant used in wave calculation. Increasing this will increase the speed of the waves.                                          |
| Interact Multiplier            | `float    ` | Influence of vertical velocity of rigidbodies.                                                                                        |
| Interact Horizontal Multiplier | `float    ` | Influence of horizontal velocity of rigidbodies.                                                                                      |
| Layers To Interact             | `LayerMask` | Layers to interact with.                                                                                                              |
| Wave Update Loop               | `int      ` | Number of wave update processes executed by one `Update()`. This value must be smaller than `WaterRWaver.bufferCount` (default is 4). |
| Decay                          | `float    ` | Coefficient for damping waves.                                                                                                        |

# Performance Tips

- Wave calculations are always performed as long as the script is active, so disable them if you do not need them or if they are off the screen.
- Wave processing time is generally proportional to the product of horizontal scale of Water-RW GameObject and resolution in `WaterRWaver`.
- Wave calculations are executed on worker threads. The number of worker threads depends on the CPU, so a slow CPU or with a small number of threads may reduce FPS.