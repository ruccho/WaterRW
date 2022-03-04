# WaterRW

[English version](README.md)

WaterRW は Unity で 2D の水表現を実現するシステムです。

![001](https://user-images.githubusercontent.com/16096562/73915969-d07dff00-48ff-11ea-8049-35ed87a50215.gif)

# 要件

- Unity 2020.3.12f1 でテストされています。
- デフォルトでは`Burst`パッケージが必要です。

# 試してみる

1. [releases](https://github.com/ruccho/WaterRW/releases) ページから `WaterRW.unitypackage` をダウンロードします。
2. Package Manager から `Burst` パッケージをダウンロードします。
3. `Ruccho/Water-RW/Prefabs/Water-RW (Compute).prefab` をシーンに配置します。

この Prefab は `WaterRWCompute` スクリプト、 `MeshFilter`、 `MeshRenderer` (`Water-RW/With Compute` マテリアル)から構成されています。

WebGL や古いモバイル端末など、一部のプラットフォームは Compute Shader をサポートしていません。事前に使用するプラットフォームが Compute Shader をサポートしていることをご確認ください。

(C# Job System を使用した旧実装がパッケージに含まれていますが、現在サポートしていません。)

# マテリアル設定

WaterRW は `Water-RW/With Compute` シェーダーを使用します。

![image](https://user-images.githubusercontent.com/16096562/73915083-e68ac000-48fd-11ea-84b7-42de766e5da0.png)

| Property                        | Type        |                                                                |
| ------------------------------- | ----------- | -------------------------------------------------------------- |
| Tint                            | `Color `    | Sprites-Default シェーダーで使用されているのと同一             |
| Pixel Snap                      | `Float `    | Sprites-Default シェーダーで使用されているのと同一             |
| Normal A                        | `Texture2D` | ノーマルマップ A。タイリング設定でスケールを設定できます       |
| Normal A Intensity              | `Float `    | ノーマルマップ A の適用される強さ                              |
| Normal A Speed                  | `Vector `   | ノーマルマップ A のスクロール速度です。X と Y だけを使用します |
| Normal B                        | `Texture2D` | ノーマルマップ A と同じ                                        |
| Normal B Intensity              | `Float `    | ノーマルマップ A と同じ                                        |
| Normal B Speed                  | `Vector `   | ノーマルマップ A と同じ                                        |
| Background Blend                | `Float `    | 反射する像の強さ                                               |
| Transparency                    | `Float `    | 反射像の暗い部分を透明にする効果の強さ                         |
| Multiplier                      | `Color `    | 乗算色                                                         |
| Addend                          | `Color `    | 加算色                                                         |
| Wave Size in Viewport Space     | `Float `    | 水面付近の変位の強さ                                           |
| Wave Distance in Viewport       | `Float `    | 水面付近の変位の広さ                                           |
| Wave Frequency by Position      | `Float `    | 水面付近の変位の周波数 (位置による)                            |
| Wave Frequency by Time          | `Float `    | 水面付近の変位の周波数 (時間による)                            |
| Surface Color                   | `Color `    | 水面のライン色                                                 |
| Surface Width in Pixel          | `Color `    | 水面のラインの太さ (ピクセル)                                  |
| Fade Distance in Viewport Space | `Float `    | 反射像の外側へのフェード範囲                                   |

# インタラクション

![image](https://user-images.githubusercontent.com/16096562/156744259-b001ac3c-68a5-4a62-8d82-b08660f6a596.gif)

WaterRW は Rigidbody2D とのラフなインタラクションを行えます。(複雑な形状の Collider は正しく処理されないことがあります)

`WaterRWCompute`のインスペクタから、`Layers To Interact With`プロパティにインタラクトさせたい Layer をセットしてください。

## `WaterRWCompute` Settings

![image](https://user-images.githubusercontent.com/16096562/142718932-c7c4274f-6a46-46f7-83e9-bf3b20c54ea2.png)

| Property                    | Type                 |                                                                                  |
| --------------------------- | -------------------- | -------------------------------------------------------------------------------- |
| Mesh segments Per Unit      | `float `             | メッシュの分割数 (per unit)                                                      |
| Update Mode                 | FixedUpdate / Update | 波動計算のタイミング。 インタラクションを正しく処理するために`FixedUpdate`を推奨 |
| Override Fixed Time Step    | `bool`               | カスタムタイムステップを使用するかどうか                                         |
| Fixed Time Step             | `float`              | カスタムタイムステップ                                                           |
| C                           | `float `             | 波動計算で使用する定数。波の伝搬速度を決定する                                   |
| Decay                       | `float `             | 減衰                                                                             |
| Enable Interaction          | `float `             | インタラクションを使用するかどうか                                               |
| Layers To Interact With     | `LayerMask`          | インタラクトするレイヤー                                                         |
| Spatial Scale               | `float `             | 波動計算で使用する水平方向スケール                                               |
| Max Interaction Items       | `float `             | インタラクトできる Rigidbody2D の最大数                                          |
| Wave Buffer Pixels Per Unit | `float `             | 波動計算に使用するバッファの解像度                                               |
| Scroll To Main Camera       | `bool `              | 波動計算の範囲をメインカメラの座標に追従させる                                   |
| Max Surface Width           | `float `             | 波動計算する範囲 (World Space)                                                   |

## 発散を防ぐ

`Fixed Time Step`, `C`, `Spatial Scale`, `Wave Buffer Pixels Per Unit`の値によっては、波が発散してしまうことがあります。

発散を防ぐためには、それぞれの値が以下の条件を満たしている必要があります：

---

`dt` = time step (`Override Fixed Time Step`が true のときは`Fixed Time Step`、それ以外の場合は `Time.deltaTime` または `Time.fixedDeltaTime`が使用されます)

`dx` = `Spatial Scale` / `Wave Buffer Pixels Per Unit`

として、

`0 ≤ (C * dt / dx) ≤ 1`

---


## バッファスクロール

波動計算に使用されるバッファのサイズは有限ですが、波動計算の範囲をカメラ位置に追従させることで、見かけ上無限の広さの水面のインタラクションを実現できます。
`Scroll To Main Camera`がtrueの場合は自動的に`Camera.main`に追従します。
手動で設定する場合は`float WaterRWCompute.WavePosition`にX座標をセットしてください。