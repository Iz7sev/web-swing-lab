# Web Swing Lab

Meta Quest 3のハンドトラッキングで、両手からウェブを発射するUnity製VR / MRプロトタイプです。

[体験動画を見る（13秒 / MP4）](demo.mp4)

[Unityプロジェクト一式をダウンロード](UnityProject.zip) · [C#ソースコードを読む](src/)

<img src="WebSwingIcon.png" alt="Web Swing Lab icon" width="128">

## できること

- 左右の手からそれぞれウェブを発射し、片手ずつ解除できます。
- 建物・地面・屋上に付けたウェブは自動で巻き取り、重力を受けながら移動します。
- 木箱に付けたウェブは、手を体の方へ引いた分だけ巻き取ります。箱の質量に応じて動きます。
- VRでは、低い位置で両腕を交互に振ると、見ている方向へ進みます（最高4.5m/秒）。
- 指と手のひらの表示、繊維を束ねたウェブ、街の練習場を備えています。
- MRでは現実の部屋を背景に小さな木箱4個を出し、ウェブで引っ張れます。

## 操作

| 操作 | 動作 |
| --- | --- |
| 人差し指と小指を伸ばし、中指と薬指を曲げる | その手から発射 |
| 手をパーにする | その手のウェブを解除 |
| 糸が付いた箱から手を手前に引く | 箱を引っ張る |
| 左右どちらかの手で親指だけ上に立て、約1秒保つ | メニューを開く |
| メニューを人差し指で狙い、親指と人差し指をつまむ | ボタンを選択 |
| メニューの RESET | 初期位置と木箱をリセット |
| メニューの VR CITY / MR ROOM | モード切り替え |

追跡が途切れた後やメニューを閉じた後は、一度手をパーにしてから発射してください。
MRでは腕振りによる仮想歩行とスイング移動を停止します。実際の家具や壁を認識した衝突・遮蔽は未実装です。

## Unityで開く

1. Unity HubからUnity **6000.6.4f1** をインストールし、Android Build Support（SDK / NDK / OpenJDK）を追加します。
2. `UnityProject.zip` をダウンロードして展開し、中の `WebSwingQuest` フォルダーをUnity Hubへ追加して開きます。Package Managerの復元を待ちます。
3. Androidをビルド対象にし、必要なら `WebSwing > Setup Quest Project` を実行します。
4. Quest 3の開発者モードを有効にしてUSB接続し、ヘッドセット内でUSBデバッグを許可します。
5. `WebSwing > Build And Run Quest` でビルド・転送します。

WindowsでAndroidをビルドする場合は、日本語を含まないパスへクローンしてください。ビルド処理はプロジェクト内のBuildCacheにGradleと一時ファイルを置きます。
シーンは起動時にコードから生成します。シースルー映像はQuest実機で確認します。

主な依存パッケージはXR Hands 1.9.0、OpenXR 1.18.0、OpenXR Meta 2.4.1、Input System 1.20.1です。詳細は `UnityProject.zip 内の Packages/manifest.json` とロックファイルにあります。

## コードの構成

| ファイル | 役割 |
| --- | --- |
| `WebInput.cs` | 手の形、両手入力、腕振り、サムズアップメニュー |
| `WebShooter.cs` | 命中判定と手の動きによる物体の牽引 |
| `SwingMotor.cs` / `WebPhysics.cs` | 重力、2本のロープ制約と巻き取り |
| `WebRopeVisual.cs` / `TrackedHandVisual.cs` | ウェブと手の描画 |
| `CityEnvironment.cs` / `DemoWorld.cs` | 街・木箱・プレイヤーの生成 |
| `WorldModes.cs` | VR / MR切り替えとメニュー |
| `Editor/ProjectSetup.cs` | Quest向け設定、物理・地面判定チェックとビルド |

C#コードは [`src/`](src/) で閲覧できます。元のUnityフォルダー構成・画像・XR設定・パッケージ設定を含む完全なプロジェクトは `UnityProject.zip` にあります。メニュー操作の詳細は [操作ガイド](OPERATIONS_JA.md)、生成テクスチャの記録は [ASSET_NOTES.txt](ASSET_NOTES.txt) を参照してください。
ハプティック機器の制御はまだ含まれていません。
