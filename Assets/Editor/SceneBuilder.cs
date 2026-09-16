using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// MVP検証用の最小ステージ(床・店舗2つ・トレーディングPC・プレイヤー・簡易UI)を
// コードから組み立てるためのビルダー。手作業のシーン編集の代わりに、
// バッチモード(-executeMethod)からも、エディタのメニューからも実行できる。
public static class SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainStage.unity";
    private const string RowPrefabPath = "Assets/Prefabs/CompanyTradeRow.prefab";
    private const string FloatingTextPrefabPath = "Assets/Prefabs/FloatingText.prefab";

    // フィールドの一辺の半分。壁/床/建物配置は全てここを基準にする。
    private const float MapHalfSize = 20f;
    // TMP標準の同梱フォント(Liberation Sans)は日本語グリフを含まないため、
    // Sawarabi Gothic(Assets/Fonts/Source, SIL Open Font License)から生成したTMPフォントアセットを
    // ここから読み込み、全UIに割り当てる。無ければCreateJapaneseFontAssetで自動生成する
    // (TMP_FontAsset.clearDynamicDataOnBuildをfalseにしないと、シーン保存後に焼き込んだ文字が
    // 消える既知の不具合があるため、生成時に明示的にfalseへ固定している)。
    private const string JapaneseFontAssetPath = "Assets/Fonts/UI JP SDF.asset";

    private static TMP_FontAsset s_JapaneseFont;


    [MenuItem("Tools/Stock Crash Co/1. Import TMP Essential Resources")]
    public static void ImportTMPEssentials()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string cacheDir = Path.Combine(projectRoot, "Library", "PackageCache");
        string[] candidates = Directory.Exists(cacheDir)
            ? Directory.GetDirectories(cacheDir, "com.unity.ugui@*")
            : new string[0];

        if (candidates.Length == 0)
        {
            Debug.LogError("SceneBuilder: com.unity.ugui のPackageCacheが見つかりません。");
            FinishBatch(1);
            return;
        }

        string packagePath = Path.Combine(candidates[0], "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(packagePath))
        {
            Debug.LogError($"SceneBuilder: TMPパッケージが見つかりません: {packagePath}");
            FinishBatch(1);
            return;
        }

        AssetDatabase.importPackageCompleted += OnImportCompleted;
        AssetDatabase.importPackageFailed += OnImportFailed;
        AssetDatabase.ImportPackage(packagePath, false);
    }

    private static void OnImportCompleted(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnImportCompleted;
        AssetDatabase.importPackageFailed -= OnImportFailed;
        AssetDatabase.SaveAssets();
        Debug.Log("SceneBuilder: TMP Essential Resources のインポートが完了しました。");
        FinishBatch(0);
    }

    private static void OnImportFailed(string packageName, string errorMessage)
    {
        AssetDatabase.importPackageCompleted -= OnImportCompleted;
        AssetDatabase.importPackageFailed -= OnImportFailed;
        Debug.LogError($"SceneBuilder: TMPインポート失敗: {errorMessage}");
        FinishBatch(1);
    }

    // City People (DenysAlmaral) はデフォルトでURP用シェーダーのマテリアルになっており、
    // このプロジェクト(Built-in Render Pipeline)だとシェーダーが見つからずピンク色(missing shader)になる。
    // アセット同梱の変換パッケージを適用してBuilt-in用マテリアルに直す(一度実行すれば恒久的に直る)。
    [MenuItem("Tools/Stock Crash Co/3. Convert City People To Built-in")]
    public static void ConvertCityPeopleToBuiltIn()
    {
        string packagePath = "Assets/DenysAlmaral/CityPeople/URP&Built-in/convert-to-BUILT-IN.unitypackage";
        if (!File.Exists(packagePath))
        {
            Debug.LogError($"SceneBuilder: 変換パッケージが見つかりません: {packagePath}");
            FinishBatch(1);
            return;
        }

        AssetDatabase.importPackageCompleted += OnImportCompleted;
        AssetDatabase.importPackageFailed += OnImportFailed;
        AssetDatabase.ImportPackage(packagePath, false);
    }

    // "Casual Game Sounds U6" (Dustyroom) は各ファイルが番号だけで内容が分からなかったため、
    // ffmpegでスペクトログラムを目視して用途に合いそうなものを選んでいる。
    private const string SfxFolder = "Assets/Casual Game Sounds U6/CasualGameSounds";
    private const string BgmPath = "Assets/ArcadeGameBGM#17/ArcadeGameBGM#17.wav";

    private class SfxSet
    {
        public AudioClip Dirt;
        public AudioClip Clean;
        public AudioClip Popularity;
        public AudioClip Bankrupt;
        public AudioClip RocketCharge;
        public AudioClip RocketFire;
        public AudioClip Win;
        public AudioClip Lose;
        public AudioClip Click;
        public AudioClip Bgm;
    }

    private static SfxSet LoadSfx()
    {
        AudioClip Load(string fileName) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxFolder}/{fileName}.wav");

        return new SfxSet
        {
            Dirt = Load("DM-CGS-37"),
            Clean = Load("DM-CGS-07"),
            Popularity = Load("DM-CGS-14"),
            Bankrupt = Load("DM-CGS-39"),
            RocketCharge = Load("DM-CGS-43"),
            RocketFire = Load("DM-CGS-39"),
            Win = Load("DM-CGS-33"),
            Lose = Load("DM-CGS-22"),
            Click = Load("DM-CGS-20"),
            Bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath),
        };
    }

    [MenuItem("Tools/Stock Crash Co/2. Build MVP Stage")]
    public static void BuildStage()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        s_JapaneseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontAssetPath);
        if (s_JapaneseFont == null || s_JapaneseFont.characterTable == null || s_JapaneseFont.characterTable.Count == 0)
        {
            s_JapaneseFont = CreateJapaneseFontAsset();
        }

        SfxSet sfx = LoadSfx();

        CreateLight();
        CreateFloor();
        CreateBoundaryWalls();
        CreateMusicPlayer(sfx.Bgm);

        // マップの奥(北側, +Z)で向かい合わせ。手前(南側, -Z)には取引PCとスタート地点を置く。
        Vector3 burgerPos = new Vector3(-8f, 0f, 13f);
        Vector3 pizzaPos = new Vector3(8f, 0f, 13f);
        Vector3 burgerLauncherPos = new Vector3(-14f, 1f, 13f);
        Vector3 pizzaLauncherPos = new Vector3(14f, 1f, 13f);
        Vector3 tradingPCPos = new Vector3(0f, 1f, -13f);

        CreateCityBackdrop(
            new ExclusionZone(tradingPCPos, 6f),
            new ExclusionZone(burgerPos, 6f),
            new ExclusionZone(pizzaPos, 6f),
            new ExclusionZone(burgerLauncherPos, 4f),
            new ExclusionZone(pizzaLauncherPos, 4f));

        MarketManager marketManager = CreateMarketManager();

        Company burger = CreateCompany("Burger Kingdom", 0, burgerPos, "Building_Fast Food", pizzaPos);
        Company pizza = CreateCompany("Pizza Palace", 1, pizzaPos, "Building_Pizza", burgerPos);
        burger.rival = pizza;
        pizza.rival = burger;

        FloatingTextSpawner floatingTextSpawner = CreateFloatingTextSpawner();
        AddVisualFeedback(burger, floatingTextSpawner, sfx);
        AddVisualFeedback(pizza, floatingTextSpawner, sfx);

        CreateRocketLauncher(burgerLauncherPos, burger, sfx);
        CreateRocketLauncher(pizzaLauncherPos, pizza, sfx);

        CreateGameSessionManager(burger, pizza);

        Transform canvasTransform = CreateUIRoot();
        TradingUIController tradingUI = CreateTradingUI(canvasTransform, sfx);
        CreateBriefingAndResultUI(canvasTransform, sfx, burger, pizza);
        CreateTradingPC(tradingPCPos, tradingUI, burger, pizza);
        GameObject player = CreatePlayer(new Vector3(0f, 1f, tradingPCPos.z + 1.5f));
        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 取引PC(手前)の方を向いた状態でスタート
        AddPlayerCharacterModel(player);
        GameObject fpsCamera = CreateFirstPersonCamera(player.transform);

        GameObject trashPrefab = CreateTrashPrefab(sfx);
        PlayerController playerController = player.GetComponent<PlayerController>();
        playerController.trashPrefab = trashPrefab;
        playerController.throwOrigin = fpsCamera.transform;

        CreateNPCs(6);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(saved
            ? "SceneBuilder: MVPステージの構築が完了しました。"
            : "SceneBuilder: シーンの保存に失敗しました。");
        FinishBatch(saved ? 0 : 1);
    }

    private static void CreateLight()
    {
        GameObject lightGO = new GameObject("Directional Light");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void CreateMusicPlayer(AudioClip bgm)
    {
        GameObject go = new GameObject("MusicPlayer");
        MusicPlayer player = go.AddComponent<MusicPlayer>();
        player.musicClip = bgm;
        player.volume = 0.4f;
    }

    private static void CreateFloor()
    {
        // "Road Tile"は無地の舗装だけで道路標示が無かったため、実際に白線などが描かれた
        // "Road Lane_01〜04"(いずれも20x20)を交互に敷いて道路らしく見せる。
        const float roadTileSize = 20f;
        string[] roadPrefabNames =
        {
            "Road Lane_01", "Road Lane_02", "Road Lane_03", "Road Lane_04",
        };
        string folder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Roads";

        GameObject firstAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{roadPrefabNames[0]}.prefab");
        if (firstAsset != null)
        {
            int tilesPerSide = Mathf.Max(1, Mathf.RoundToInt((MapHalfSize * 2f) / roadTileSize));
            int tileIndex = 0;
            for (int ix = 0; ix < tilesPerSide; ix++)
            {
                for (int iz = 0; iz < tilesPerSide; iz++)
                {
                    string prefabName = roadPrefabNames[tileIndex % roadPrefabNames.Length];
                    tileIndex++;
                    GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{prefabName}.prefab");
                    if (prefabAsset == null) continue;

                    GameObject tile = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                    tile.name = $"RoadTile_{ix}_{iz}";
                    float x = -MapHalfSize + roadTileSize * 0.5f + roadTileSize * ix;
                    float z = -MapHalfSize + roadTileSize * 0.5f + roadTileSize * iz;
                    tile.transform.position = new Vector3(x, 0f, z);
                }
            }
        }
        else
        {
            Debug.LogWarning($"SceneBuilder: 道路タイルが見つかりません ({folder}/{roadPrefabNames[0]}.prefab)。代わりに床プリミティブを使います。");
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fallback.name = "Floor";
            fallback.transform.localScale = new Vector3(MapHalfSize / 5f, 1f, MapHalfSize / 5f); // Plane既定10x10
            ApplyColor(fallback, new Color(0.5f, 0.55f, 0.5f));
        }

        // タイル1枚ごとにコライダーは付いていないため、床全体を覆う共通のコライダーを1つ用意する。
        GameObject floorCollider = new GameObject("FloorCollider");
        BoxCollider bc = floorCollider.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, -0.05f, 0f);
        bc.size = new Vector3(MapHalfSize * 2f, 0.1f, MapHalfSize * 2f);
    }

    // 床の外周を壁で囲み、プレイヤーがフィールド外へ歩いて落下しないようにする。
    private static void CreateBoundaryWalls()
    {
        const float wallHeight = 5f;
        const float wallThickness = 1f;
        float half = MapHalfSize;
        Color wallColor = new Color(0.3f, 0.32f, 0.35f);

        CreateWall("Wall_North", new Vector3(0f, wallHeight / 2f, half), new Vector3(half * 2f + wallThickness, wallHeight, wallThickness), wallColor);
        CreateWall("Wall_South", new Vector3(0f, wallHeight / 2f, -half), new Vector3(half * 2f + wallThickness, wallHeight, wallThickness), wallColor);
        CreateWall("Wall_East", new Vector3(half, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, half * 2f + wallThickness), wallColor);
        CreateWall("Wall_West", new Vector3(-half, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, half * 2f + wallThickness), wallColor);
    }

    private static void CreateWall(string name, Vector3 position, Vector3 size, Color color)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = size;
        ApplyColor(wall, color);
    }

    // 単なる壁だけだと味気ないので、フィールドの外周をマンション(住宅)だけで隙間なく囲む。
    // 店舗/ロケット砲/取引PCの近くだけは除外ゾーンとして避ける。
    // (壁自体のコライダーは既にCreateBoundaryWallsで用意済みなので、こちらは見た目だけ・衝突防止の保険を兼ねる)
    private static readonly string[] BackdropBuildingNames =
    {
        "Building_Residential_color01", "Building_Residential_color02", "Building_Residential_color03",
    };
    private const float BackdropTargetFootprint = 4f;

    private readonly struct ExclusionZone
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public ExclusionZone(Vector3 center, float radius) { Center = center; Radius = radius; }
    }

    private static void CreateCityBackdrop(params ExclusionZone[] exclusionZones)
    {
        const float wallInset = 1.2f; // 壁のすぐ内側
        const float footprint = BackdropTargetFootprint;
        const float step = footprint * 1.02f; // わずかな余白だけ持たせてほぼ隙間なく並べる
        float ringPos = MapHalfSize - wallInset;

        int countPerSide = Mathf.Max(1, Mathf.FloorToInt((MapHalfSize * 2f) / step));
        int index = 0;
        for (int i = 0; i < countPerSide; i++)
        {
            float coord = -MapHalfSize + step * 0.5f + step * i;
            TryPlaceBackdropBuilding(new Vector3(coord, 0f, ringPos), 180f, footprint, exclusionZones, ref index);   // 北
            TryPlaceBackdropBuilding(new Vector3(coord, 0f, -ringPos), 0f, footprint, exclusionZones, ref index);    // 南
            TryPlaceBackdropBuilding(new Vector3(ringPos, 0f, coord), -90f, footprint, exclusionZones, ref index);   // 東
            TryPlaceBackdropBuilding(new Vector3(-ringPos, 0f, coord), 90f, footprint, exclusionZones, ref index);   // 西
        }
    }

    private static void TryPlaceBackdropBuilding(Vector3 position, float yRotation, float footprint, ExclusionZone[] exclusionZones, ref int index)
    {
        foreach (ExclusionZone zone in exclusionZones)
        {
            float dist = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(zone.Center.x, zone.Center.z));
            if (dist < zone.Radius + footprint * 0.5f) return; // 除外ゾーンに掛かるので置かない
        }

        string prefabName = BackdropBuildingNames[index % BackdropBuildingNames.Length];
        index++;

        GameObject building = InstantiateBuilding(prefabName, $"Backdrop_{prefabName}_{index}", position, footprint);
        building.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        Bounds b = GetLocalBounds(building);
        BoxCollider collider = building.AddComponent<BoxCollider>();
        collider.center = b.center;
        collider.size = b.size;
    }

    private static MarketManager CreateMarketManager()
    {
        GameObject go = new GameObject("MarketManager");
        return go.AddComponent<MarketManager>();
    }

    private static GameSessionManager CreateGameSessionManager(params Company[] targetCompanies)
    {
        GameObject go = new GameObject("GameSessionManager");
        GameSessionManager session = go.AddComponent<GameSessionManager>();
        session.targetAmount = 3000f;
        session.targetCompanies = targetCompanies;
        session.matchDurationSeconds = 180f;
        return session;
    }

    private const string BuildingsFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Buildings";
    // 店舗として使う建物モデルの、横幅(x/zの大きい方)の目標サイズ。実物大(10〜15ユニット)だと
    // このゲームの20x20マップに対して大きすぎるため、これを基準に自動スケールダウンする。
    private const float ShopTargetFootprint = 4.5f;

    // モデルの正面が作者依存でどの向きに作られているか分からないため、実際に見て合わなければ
    // ここを90度単位で調整する(0/90/180/270)。両店舗で共通のモデル群を使っている前提。
    private const float ShopFrontOffsetDegrees = 0f;

    private static Company CreateCompany(string companyName, int id, Vector3 position, string buildingPrefabFileName, Vector3 facePosition)
    {
        GameObject shop = InstantiateBuilding(buildingPrefabFileName, companyName, position, ShopTargetFootprint);

        // 正面が向かい合うよう、もう一方の店がある方向を向かせる。
        Vector3 faceDir = facePosition - position;
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            shop.transform.rotation = Quaternion.LookRotation(faceDir.normalized) * Quaternion.Euler(0f, ShopFrontOffsetDegrees, 0f);
        }

        Bounds localBounds = GetLocalBounds(shop);

        // 見た目通りの大きさで物理的に塞ぐ実体コライダー(プレイヤーがめり込まないようにする)と、
        // それより一回り大きい検知専用のトリガーコライダー(近づいた判定用)を分けて持たせる。
        // モデルごとに大きさが違うため、実寸(メッシュのbounds)から自動的にサイズを決める。
        BoxCollider solidCollider = shop.AddComponent<BoxCollider>();
        solidCollider.center = localBounds.center;
        solidCollider.size = localBounds.size;
        solidCollider.isTrigger = false;

        BoxCollider triggerCollider = shop.AddComponent<BoxCollider>();
        triggerCollider.center = localBounds.center;
        triggerCollider.size = localBounds.size * 1.6f;
        triggerCollider.isTrigger = true;

        Company company = shop.AddComponent<Company>();
        company.companyId = id;
        company.companyName = companyName;
        company.basePrice = 100f;
        company.currentPrice = 100f;

        AddNameLabel(shop.transform, companyName, localBounds);
        return company;
    }

    // SimplePoly City パック内の建物プレハブをインスタンス化する。見つからなければ箱で代用する。
    // targetFootprint: 横幅(x/zの大きい方)がこのサイズになるよう自動スケールする。
    private static GameObject InstantiateBuilding(string prefabFileName, string objectName, Vector3 position, float targetFootprint)
    {
        string path = $"{BuildingsFolder}/{prefabFileName}.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        GameObject instance;
        if (prefabAsset != null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        }
        else
        {
            Debug.LogWarning($"SceneBuilder: 建物プレハブが見つかりません ({path})。代わりに箱を使います。");
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }
        instance.name = objectName;

        Bounds localBounds = GetLocalBounds(instance);
        float footprint = Mathf.Max(localBounds.size.x, localBounds.size.z);
        float scale = footprint > 0.0001f ? targetFootprint / footprint : 1f;
        instance.transform.localScale = Vector3.one * scale;

        // モデルのピボット位置(中心/接地面など)に関わらず、必ず底面がpositionのY座標に接地するようにする。
        // localBoundsは無スケール(メッシュ本来)の値なので、実際のワールド上のズレはscale倍する必要がある。
        float bottomWorldOffset = localBounds.min.y * scale;
        instance.transform.position = new Vector3(position.x, position.y - bottomWorldOffset, position.z);

        return instance;
    }

    // オブジェクト(の子を含む)が持つメッシュ全体のローカル空間でのbounds。
    private static Bounds GetLocalBounds(GameObject go)
    {
        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            return mf.sharedMesh.bounds;
        }
        return new Bounds(Vector3.zero, Vector3.one); // プリミティブCube等のフォールバック
    }

    // 店名を建物の真上に浮かべて表示する(モデルの向きに依存しないよう正面ではなく真上に置く)。
    private static void AddNameLabel(Transform shop, string companyName, Bounds localBounds)
    {
        GameObject nameLabel = new GameObject("NameLabel");
        nameLabel.transform.SetParent(shop, false);
        nameLabel.transform.localPosition = new Vector3(localBounds.center.x, localBounds.max.y + 0.6f, localBounds.center.z);
        TextMeshPro nameTMP = nameLabel.AddComponent<TextMeshPro>();
        nameTMP.text = companyName;
        nameTMP.fontSize = 3.2f;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.color = Color.black;
        if (s_JapaneseFont != null) nameTMP.font = s_JapaneseFont;
        RectTransform nameRT = nameLabel.GetComponent<RectTransform>();
        if (nameRT != null) nameRT.sizeDelta = new Vector2(3f, 0.8f);
    }

    // 数値ポップアップ(FloatingText)を生成するだけの共有スポナー。全店舗から参照される。
    private static FloatingTextSpawner CreateFloatingTextSpawner()
    {
        GameObject go = new GameObject("FloatingTextSpawner");
        FloatingTextSpawner spawner = go.AddComponent<FloatingTextSpawner>();
        spawner.spawnOffset = new Vector3(0f, 2f, 0f);
        spawner.floatingTextPrefab = CreateFloatingTextPrefab();
        return spawner;
    }

    private static FloatingText CreateFloatingTextPrefab()
    {
        GameObject go = new GameObject("FloatingText");
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        if (s_JapaneseFont != null) tmp.font = s_JapaneseFont;
        FloatingText floatingText = go.AddComponent<FloatingText>();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, FloatingTextPrefabPath);
        Object.DestroyImmediate(go);
        return prefabAsset.GetComponent<FloatingText>();
    }

    // dirtiness/popularity/破産を見た目に変換するCompanyVisualFeedbackを組み立てて店舗に付与する。
    private static void AddVisualFeedback(Company company, FloatingTextSpawner spawner, SfxSet sfx)
    {
        GameObject shop = company.gameObject;
        Bounds b = GetLocalBounds(shop);
        float sideRadius = Mathf.Max(b.extents.x, b.extents.z);
        Vector3 midHeight = new Vector3(b.center.x, b.center.y, b.center.z);

        CompanyVisualFeedback feedback = shop.AddComponent<CompanyVisualFeedback>();
        feedback.company = company;
        feedback.floatingTextSpawner = spawner;
        feedback.popupOrigin = shop.transform;
        feedback.dirtSound = sfx.Dirt;
        feedback.cleanSound = sfx.Clean;
        feedback.popularitySound = sfx.Popularity;
        feedback.bankruptSound = sfx.Bankrupt;

        // 汚れは店の足元にゴミが積み上がっていくイメージ、人気は屋根の上に星が増えていくイメージ。
        // モデルごとに大きさが違うため、実寸(bounds)を基準に配置する。
        feedback.dirtStageProps = CreateStageProps(shop.transform, "Dirt", 4, new Color(0.35f, 0.25f, 0.12f),
            baseY: b.min.y + 0.2f, radius: sideRadius * 0.9f, itemSize: 0.4f);
        feedback.dirtStageThresholds = new float[] { 10f, 30f, 60f, 100f };

        feedback.popularityStageProps = CreateStageProps(shop.transform, "Popularity", 4, new Color(1f, 0.85f, 0.2f),
            baseY: b.max.y + 0.3f, radius: sideRadius * 0.5f, itemSize: 0.35f);
        feedback.popularityStageThresholds = new float[] { 10f, 30f, 60f, 100f };

        feedback.dirtBurstEffect = CreateBurstParticles(shop.transform, "DirtBurst", new Color(0.4f, 0.25f, 0.1f), midHeight);
        feedback.cleanBurstEffect = CreateBurstParticles(shop.transform, "CleanBurst", new Color(0.4f, 0.9f, 1f), midHeight);
        feedback.popularityBurstEffect = CreateBurstParticles(shop.transform, "PopularityBurst", new Color(1f, 0.9f, 0.2f), midHeight);

        feedback.bankruptEffect = CreateBankruptOverlay(shop.transform, b);
    }

    private static GameObject[] CreateStageProps(Transform parent, string namePrefix, int count, Color color, float baseY, float radius, float itemSize)
    {
        GameObject[] props = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{namePrefix}Stage_{i}";
            go.transform.SetParent(parent, false);

            float angle = (360f / count) * i * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, baseY, Mathf.Sin(angle) * radius);
            go.transform.localScale = Vector3.one * itemSize;

            // 演出専用の見た目だけのオブジェクトなので、物理判定に干渉しないようコライダーは外す。
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            ApplyColor(go, color);
            go.SetActive(false);
            props[i] = go;
        }
        return props;
    }

    private static ParticleSystem CreateBurstParticles(Transform parent, string name, Color color, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 2.5f;
        main.startSize = 0.3f;
        main.startColor = color;
        main.duration = 0.5f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        return ps;
    }

    private static GameObject CreateBankruptOverlay(Transform parent, Bounds localBounds)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "BankruptOverlay";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localBounds.center;
        go.transform.localScale = localBounds.size * 1.05f; // 本体より一回り大きく覆う
        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        ApplyColor(go, new Color(0.05f, 0.05f, 0.05f));
        go.SetActive(false);
        return go;
    }

    // 終盤兵器。目標金額を達成すると相手企業側だけ出現し、Eキー長押しで即破産させられる
    // (出現条件・長押し判定はRocketLauncher.cs側。ここでは見た目とコライダーを組み立てるだけ)。
    private static void CreateRocketLauncher(Vector3 position, Company targetCompany, SfxSet sfx)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"RocketLauncher_{targetCompany.companyName}";
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
        ApplyColor(go, new Color(0.25f, 0.2f, 0.2f));

        // Cylinderプリミティブは既定でCapsuleColliderが付く。これを実体(物理)側として使い、
        // 別途もう一回り大きいBoxColliderを検知用トリガーとして追加する。
        CapsuleCollider solidCollider = go.GetComponent<CapsuleCollider>();
        solidCollider.isTrigger = false;

        BoxCollider triggerCollider = go.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(4f, 4f, 4f);

        RocketLauncher launcher = go.AddComponent<RocketLauncher>();
        launcher.targetCompany = targetCompany;
        launcher.holdSecondsToFire = 2f;
        launcher.fireEffect = CreateBurstParticles(go.transform, "FireBurst", new Color(1f, 0.5f, 0.1f), new Vector3(0f, 0.6f, 0f));
        launcher.chargeLoopSound = sfx.RocketCharge;
        launcher.fireSound = sfx.RocketFire;

        DecorateRocketLauncher(go.transform);
    }

    // 円柱1本だけだと発射台に見えないので、発射台・弾頭・フィンを追加してロケット砲らしく見せる。
    // RocketLauncher側でGetComponentsInChildrenを使って一括で表示/非表示を切り替えるため、
    // ここで付けたコライダーは全て外しておく(実体/検知コライダーの邪魔をしないため)。
    private static void DecorateRocketLauncher(Transform launcher)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "LaunchPad";
        pad.transform.SetParent(launcher, false);
        pad.transform.localPosition = new Vector3(0f, -0.55f, 0f);
        pad.transform.localScale = new Vector3(2.4f, 0.15f, 2.4f);
        Object.DestroyImmediate(pad.GetComponent<CapsuleCollider>());
        ApplyColor(pad, new Color(0.15f, 0.15f, 0.16f));

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nose.name = "NoseCone";
        nose.transform.SetParent(launcher, false);
        nose.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        nose.transform.localScale = new Vector3(1.05f, 0.55f, 1.05f);
        Object.DestroyImmediate(nose.GetComponent<SphereCollider>());
        ApplyColor(nose, new Color(0.8f, 0.15f, 0.1f));

        for (int i = 0; i < 3; i++)
        {
            GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = $"Fin_{i}";
            fin.transform.SetParent(launcher, false);
            Quaternion rot = Quaternion.Euler(0f, i * 120f, 0f);
            fin.transform.localRotation = rot;
            fin.transform.localPosition = rot * new Vector3(0.65f, -0.35f, 0f);
            fin.transform.localScale = new Vector3(0.55f, 0.5f, 0.06f);
            Object.DestroyImmediate(fin.GetComponent<BoxCollider>());
            ApplyColor(fin, new Color(0.3f, 0.3f, 0.32f));
        }
    }

    private static void CreateTradingPC(Vector3 position, TradingUIController tradingUI, params Company[] companies)
    {
        GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.name = "TradingPC";
        desk.transform.position = position;
        desk.transform.localScale = new Vector3(1.2f, 1f, 0.8f);
        desk.GetComponent<MeshRenderer>().enabled = false; // 見た目は子パーツ(デスク/モニター/椅子等)で構成する

        // 見た目通りの大きさの実体コライダー(めり込み防止)と、操作可能距離を広げる検知用トリガーを分ける。
        BoxCollider solidCollider = desk.GetComponent<BoxCollider>();
        solidCollider.isTrigger = false;

        BoxCollider triggerCollider = desk.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(4f, 4f, 4f); // 見た目より広い範囲で反応させる

        DecorateTradingPC(desk.transform);

        TradingPC tradingPC = desk.AddComponent<TradingPC>();
        tradingPC.tradableCompanies = companies;
        tradingPC.tradingUI = tradingUI;
    }

    // 適切な既製アセットが手持ちのパック内に無かったため、デスク・モニター・キーボード・椅子を
    // プリミティブの組み合わせで作る。ルート(TradingPC)はワールドY=positionYが中心で、
    // 床(ワールドY=0)を基準にした高さになるようlocalPositionを逆算している。
    private static void DecorateTradingPC(Transform root)
    {
        float rootWorldY = root.position.y;
        float LocalY(float worldY) => worldY - rootWorldY; // root.localScale.y==1前提

        CreatePcPart(root, "DeskLeg_FL", new Vector3(0.42f, LocalY(0.36f), 0.42f), new Vector3(0.06f, 0.72f, 0.06f), new Color(0.08f, 0.08f, 0.08f));
        CreatePcPart(root, "DeskLeg_FR", new Vector3(-0.42f, LocalY(0.36f), 0.42f), new Vector3(0.06f, 0.72f, 0.06f), new Color(0.08f, 0.08f, 0.08f));
        CreatePcPart(root, "DeskLeg_BL", new Vector3(0.42f, LocalY(0.36f), -0.42f), new Vector3(0.06f, 0.72f, 0.06f), new Color(0.08f, 0.08f, 0.08f));
        CreatePcPart(root, "DeskLeg_BR", new Vector3(-0.42f, LocalY(0.36f), -0.42f), new Vector3(0.06f, 0.72f, 0.06f), new Color(0.08f, 0.08f, 0.08f));

        CreatePcPart(root, "DeskTop", new Vector3(0f, LocalY(0.75f), 0f), new Vector3(1f, 0.06f, 1f), new Color(0.35f, 0.22f, 0.12f));

        CreatePcPart(root, "Monitor", new Vector3(0f, LocalY(1.02f), -0.25f), new Vector3(0.55f, 0.35f, 0.04f), new Color(0.05f, 0.05f, 0.05f));
        CreatePcPart(root, "MonitorScreen", new Vector3(0f, LocalY(1.02f), -0.22f), new Vector3(0.48f, 0.28f, 0.01f), new Color(0.2f, 0.9f, 0.5f));
        CreatePcPart(root, "MonitorStand", new Vector3(0f, LocalY(0.815f), -0.25f), new Vector3(0.06f, 0.07f, 0.06f), new Color(0.08f, 0.08f, 0.08f));

        CreatePcPart(root, "Keyboard", new Vector3(0f, LocalY(0.79f), 0.15f), new Vector3(0.4f, 0.02f, 0.15f), new Color(0.15f, 0.15f, 0.15f));

        CreatePcPart(root, "ChairSeat", new Vector3(0f, LocalY(0.45f), 0.85f), new Vector3(0.4f, 0.06f, 0.4f), new Color(0.2f, 0.2f, 0.25f));
        CreatePcPart(root, "ChairBack", new Vector3(0f, LocalY(0.75f), 1.05f), new Vector3(0.4f, 0.5f, 0.06f), new Color(0.2f, 0.2f, 0.25f));
        CreatePcPart(root, "ChairLeg", new Vector3(0f, LocalY(0.21f), 0.85f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.08f, 0.08f, 0.08f));
    }

    private static void CreatePcPart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Object.DestroyImmediate(part.GetComponent<BoxCollider>());
        ApplyColor(part, color);
    }

    private static GameObject CreatePlayer(Vector3 position)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = position;
        player.tag = "Player";
        ApplyColor(player, new Color(0.2f, 0.6f, 0.9f));
        // 一人称視点では自分の体はカメラの視界に入らないため、見た目だけ隠す
        // (コライダー/Rigidbody/PlayerControllerはそのまま機能させる)。
        player.GetComponent<MeshRenderer>().enabled = false;

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;

        player.AddComponent<PlayerController>();
        return player;
    }

    private static GameObject CreateFirstPersonCamera(Transform playerTransform)
    {
        GameObject camGO = new GameObject("FPSCamera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(playerTransform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.6f, 0.2f); // カプセル中心から目の高さぶん上
        camGO.transform.localRotation = Quaternion.identity;
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<FirstPersonLook>();

        CreateMopViewModel(camGO.transform);
        return camGO;
    }

    // モップは常時持っている道具として、一人称カメラの右下に固定表示する(掃除の演出は数値/ゴミ除去側で行う)。
    private static void CreateMopViewModel(Transform camera)
    {
        GameObject mop = new GameObject("MopViewModel");
        mop.transform.SetParent(camera, false);
        mop.transform.localPosition = new Vector3(0.35f, -0.35f, 0.6f);
        mop.transform.localRotation = Quaternion.Euler(15f, 0f, -10f);

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Handle";
        handle.transform.SetParent(mop.transform, false);
        handle.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        handle.transform.localScale = new Vector3(0.03f, 0.5f, 0.03f);
        Object.DestroyImmediate(handle.GetComponent<CapsuleCollider>());
        ApplyColor(handle, new Color(0.4f, 0.28f, 0.15f));

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(mop.transform, false);
        head.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        head.transform.localScale = new Vector3(0.14f, 0.12f, 0.08f);
        Object.DestroyImmediate(head.GetComponent<BoxCollider>());
        ApplyColor(head, new Color(0.85f, 0.82f, 0.7f));
    }

    // 投げるゴミ本体。プレハブとして保存し、PlayerControllerがInstantiateする。
    private const string TrashPrefabPath = "Assets/Prefabs/Trash.prefab";

    private static GameObject CreateTrashPrefab(SfxSet sfx)
    {
        GameObject trash = new GameObject("Trash");
        Rigidbody rb = trash.AddComponent<Rigidbody>();
        rb.mass = 0.3f;

        SphereCollider col = trash.AddComponent<SphereCollider>();
        col.radius = 0.15f;

        TrashProjectile projectile = trash.AddComponent<TrashProjectile>();
        projectile.dirtAmount = 5f;
        projectile.splatSound = sfx.Dirt;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Visual";
        core.transform.SetParent(trash.transform, false);
        core.transform.localScale = Vector3.one * 0.3f;
        Object.DestroyImmediate(core.GetComponent<SphereCollider>());
        ApplyColor(core, new Color(0.35f, 0.28f, 0.15f));

        for (int i = 0; i < 3; i++)
        {
            GameObject fleck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fleck.name = $"Fleck_{i}";
            fleck.transform.SetParent(trash.transform, false);
            fleck.transform.localPosition = Random.insideUnitSphere * 0.12f;
            fleck.transform.localRotation = Random.rotation;
            fleck.transform.localScale = Vector3.one * 0.12f;
            Object.DestroyImmediate(fleck.GetComponent<BoxCollider>());
            ApplyColor(fleck, new Color(0.3f, 0.4f, 0.15f));
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(trash, TrashPrefabPath);
        Object.DestroyImmediate(trash);
        return prefabAsset;
    }

    // City People (DenysAlmaral) アセットのキャラクターを、路上を歩き回るだけのNPCとして配置する。
    private const string CityPeopleFolder = "Assets/DenysAlmaral/CityPeople/Prefabs";
    private static readonly string[] NPCPrefabRelativePaths =
    {
        "city/casual_Male_G", "city/casual_Female_G",
        "downtown/casual_Male_K", "downtown/casual_Female_K",
    };

    private static void CreateNPCs(int count)
    {
        // 取引PC・店舗・ロケット砲の手前/奥を避けた、マップ中央の通り部分を徘徊エリアにする。
        Vector2 areaMin = new Vector2(-15f, -8f);
        Vector2 areaMax = new Vector2(15f, 8f);

        for (int i = 0; i < count; i++)
        {
            string relPath = NPCPrefabRelativePaths[i % NPCPrefabRelativePaths.Length];
            string path = $"{CityPeopleFolder}/{relPath}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"SceneBuilder: NPCプレハブが見つかりません ({path})。");
                continue;
            }

            GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            npc.name = $"NPC_{i}_{prefabAsset.name}";
            npc.transform.position = new Vector3(
                Random.Range(areaMin.x, areaMax.x), 0f, Random.Range(areaMin.y, areaMax.y));
            npc.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            NPCWanderer wanderer = npc.AddComponent<NPCWanderer>();
            wanderer.areaMinXZ = areaMin;
            wanderer.areaMaxXZ = areaMax;
            wanderer.moveSpeed = Random.Range(1.0f, 1.6f);
        }
    }

    // プレイヤー自身の見た目(一人称なので自分のカメラには映さない)をCity Peopleのキャラクターにする。
    private static void AddPlayerCharacterModel(GameObject player)
    {
        string path = $"{CityPeopleFolder}/city/casual_Male_G.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"SceneBuilder: プレイヤー用モデルが見つかりません ({path})。");
            return;
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, player.transform);
        model.name = "CharacterModel";
        model.transform.localPosition = new Vector3(0f, -1f, 0f); // カプセル中心(y=1)から見た足元
        model.transform.localRotation = Quaternion.identity;

        // 一人称視点では自分の姿は見えなくてよい(将来的にマルチプレイ等で他人から見える用に残す)。
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
        }
    }

    private static Transform CreateUIRoot()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject canvasGO = new GameObject("Canvas", typeof(RectTransform));
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasGO.AddComponent<GraphicRaycaster>();

        canvasGO.AddComponent<GamePhaseUIRouter>();

        return canvasGO.transform;
    }

    private static TradingUIController CreateTradingUI(Transform canvasTransform, SfxSet sfx)
    {
        GameObject panelGO = new GameObject("TradingPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvasTransform, false);
        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(560f, 460f);
        panelRT.anchoredPosition = Vector2.zero;
        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.85f);

        TradingUIController tradingUI = panelGO.AddComponent<TradingUIController>();

        GameObject walletGO = CreateTMPText("WalletText", panelGO.transform, "チーム資金: $1000", 24f, TextAlignmentOptions.Center);
        RectTransform walletRT = walletGO.GetComponent<RectTransform>();
        walletRT.anchorMin = new Vector2(0f, 1f);
        walletRT.anchorMax = new Vector2(1f, 1f);
        walletRT.pivot = new Vector2(0.5f, 1f);
        walletRT.anchoredPosition = new Vector2(0f, -12f);
        walletRT.sizeDelta = new Vector2(-20f, 36f);
        tradingUI.walletText = walletGO.GetComponent<TextMeshProUGUI>();

        GameObject rowContainerGO = new GameObject("RowContainer", typeof(RectTransform));
        rowContainerGO.transform.SetParent(panelGO.transform, false);
        RectTransform rowContainerRT = rowContainerGO.GetComponent<RectTransform>();
        rowContainerRT.anchorMin = new Vector2(0f, 0f);
        rowContainerRT.anchorMax = new Vector2(1f, 1f);
        rowContainerRT.offsetMin = new Vector2(10f, 10f);
        rowContainerRT.offsetMax = new Vector2(-10f, -56f);
        VerticalLayoutGroup vlg = rowContainerGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        // falseのままだと各行の高さがRectTransformの初期値(100)のまま使われ、行同士が重なって表示が崩れるため、
        // LayoutElementで指定したpreferredHeightを実際に適用させる。
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        tradingUI.rowContainer = rowContainerGO.transform;

        tradingUI.rowPrefab = CreateRowPrefab(sfx);

        panelGO.SetActive(false);
        return tradingUI;
    }

    private static void CreateBriefingAndResultUI(Transform canvasTransform, SfxSet sfx, params Company[] selectableCompanies)
    {
        GamePhaseUIRouter router = canvasTransform.GetComponent<GamePhaseUIRouter>();

        // --- ブリーフィングパネル ---
        GameObject briefingGO = new GameObject("BriefingPanel", typeof(RectTransform));
        briefingGO.transform.SetParent(canvasTransform, false);
        RectTransform briefingRT = briefingGO.GetComponent<RectTransform>();
        briefingRT.anchorMin = new Vector2(0.5f, 0.5f);
        briefingRT.anchorMax = new Vector2(0.5f, 0.5f);
        briefingRT.sizeDelta = new Vector2(560f, 440f);
        Image briefingBg = briefingGO.AddComponent<Image>();
        briefingBg.color = new Color(0f, 0f, 0f, 0.85f);

        BriefingUIController briefing = briefingGO.AddComponent<BriefingUIController>();
        briefing.clickSound = sfx.Click;

        PositionTop(CreateTMPText("Title", briefingGO.transform, "ブリーフィング", 26f, TextAlignmentOptions.Center), 16f, 36f);

        GameObject targetAmountGO = CreateTMPText("TargetAmountText", briefingGO.transform, "", 20f, TextAlignmentOptions.Center);
        PositionTop(targetAmountGO, 64f, 30f);
        briefing.targetAmountText = targetAmountGO.GetComponent<TextMeshProUGUI>();

        GameObject targetCompaniesGO = CreateTMPText("TargetCompaniesText", briefingGO.transform, "", 18f, TextAlignmentOptions.Center);
        PositionTop(targetCompaniesGO, 100f, 28f);
        briefing.targetCompaniesText = targetCompaniesGO.GetComponent<TextMeshProUGUI>();

        GameObject timeLimitGO = CreateTMPText("TimeLimitText", briefingGO.transform, "", 18f, TextAlignmentOptions.Center);
        PositionTop(timeLimitGO, 132f, 28f);
        briefing.timeLimitText = timeLimitGO.GetComponent<TextMeshProUGUI>();

        // --- 担当企業の選択ボタン ---
        GameObject companyButtonsRow = new GameObject("CompanyButtons", typeof(RectTransform));
        companyButtonsRow.transform.SetParent(briefingGO.transform, false);
        RectTransform companyButtonsRT = companyButtonsRow.GetComponent<RectTransform>();
        companyButtonsRT.anchorMin = new Vector2(0f, 1f);
        companyButtonsRT.anchorMax = new Vector2(1f, 1f);
        companyButtonsRT.pivot = new Vector2(0.5f, 1f);
        companyButtonsRT.anchoredPosition = new Vector2(0f, -172f);
        companyButtonsRT.sizeDelta = new Vector2(-40f, 44f);
        HorizontalLayoutGroup companyButtonsHLG = companyButtonsRow.AddComponent<HorizontalLayoutGroup>();
        companyButtonsHLG.spacing = 12f;
        companyButtonsHLG.childControlWidth = true;
        companyButtonsHLG.childForceExpandWidth = true;
        companyButtonsHLG.childControlHeight = true;
        companyButtonsHLG.childForceExpandHeight = true;

        Button[] companyButtons = new Button[selectableCompanies.Length];
        for (int i = 0; i < selectableCompanies.Length; i++)
        {
            Color color = i == 0 ? new Color(0.7f, 0.35f, 0.2f) : new Color(0.8f, 0.65f, 0.2f);
            companyButtons[i] = CreateButton($"CompanyButton_{i}", companyButtonsRow.transform, selectableCompanies[i].companyName, color);
        }
        briefing.companyButtons = companyButtons;

        GameObject selectedCompanyGO = CreateTMPText("SelectedCompanyText", briefingGO.transform, "担当企業を選んでください", 18f, TextAlignmentOptions.Center);
        PositionTop(selectedCompanyGO, 224f, 28f);
        briefing.selectedCompanyText = selectedCompanyGO.GetComponent<TextMeshProUGUI>();

        Button startBtn = CreateButton("StartButton", briefingGO.transform, "開始", new Color(0.2f, 0.6f, 0.3f));
        RectTransform startBtnRT = startBtn.GetComponent<RectTransform>();
        startBtnRT.anchorMin = new Vector2(0.5f, 0f);
        startBtnRT.anchorMax = new Vector2(0.5f, 0f);
        startBtnRT.pivot = new Vector2(0.5f, 0f);
        startBtnRT.sizeDelta = new Vector2(160f, 44f);
        startBtnRT.anchoredPosition = new Vector2(0f, 24f);
        briefing.startButton = startBtn;

        GameObject waitingGO = CreateTMPText("WaitingForHostLabel", briefingGO.transform, "ホストの開始を待っています…", 16f, TextAlignmentOptions.Center);
        RectTransform waitingRT = waitingGO.GetComponent<RectTransform>();
        waitingRT.anchorMin = new Vector2(0.5f, 0f);
        waitingRT.anchorMax = new Vector2(0.5f, 0f);
        waitingRT.pivot = new Vector2(0.5f, 0f);
        waitingRT.sizeDelta = new Vector2(400f, 30f);
        waitingRT.anchoredPosition = new Vector2(0f, 24f);
        waitingGO.SetActive(false);
        briefing.waitingForHostLabel = waitingGO;

        if (router != null) router.briefingPanel = briefingGO;

        // --- リザルトパネル ---
        GameObject resultGO = new GameObject("ResultPanel", typeof(RectTransform));
        resultGO.transform.SetParent(canvasTransform, false);
        RectTransform resultRT = resultGO.GetComponent<RectTransform>();
        resultRT.anchorMin = new Vector2(0.5f, 0.5f);
        resultRT.anchorMax = new Vector2(0.5f, 0.5f);
        resultRT.sizeDelta = new Vector2(480f, 220f);
        Image resultBg = resultGO.AddComponent<Image>();
        resultBg.color = new Color(0f, 0f, 0f, 0.85f);

        ResultUIController result = resultGO.AddComponent<ResultUIController>();
        result.winSound = sfx.Win;
        result.loseSound = sfx.Lose;

        GameObject resultTitleGO = CreateTMPText("ResultTitleText", resultGO.transform, "", 28f, TextAlignmentOptions.Center);
        PositionTop(resultTitleGO, 30f, 40f);
        result.resultTitleText = resultTitleGO.GetComponent<TextMeshProUGUI>();

        GameObject finalMoneyGO = CreateTMPText("FinalMoneyText", resultGO.transform, "", 18f, TextAlignmentOptions.Center);
        PositionTop(finalMoneyGO, 90f, 30f);
        result.finalMoneyText = finalMoneyGO.GetComponent<TextMeshProUGUI>();

        resultGO.SetActive(false);
        if (router != null) router.resultPanel = resultGO;

        // --- 残り時間HUD ---
        // MatchTimerHUD自身は常時アクティブなオブジェクトに付け、表示/非表示は子のPanelだけ切り替える
        // (スクリプト自身を無効化するとUpdateが止まり、二度と再表示できなくなるため)。
        GameObject timerHudGO = new GameObject("MatchTimerHUD", typeof(RectTransform));
        timerHudGO.transform.SetParent(canvasTransform, false);
        RectTransform timerHudRT = timerHudGO.GetComponent<RectTransform>();
        timerHudRT.anchorMin = new Vector2(0.5f, 1f);
        timerHudRT.anchorMax = new Vector2(0.5f, 1f);
        timerHudRT.pivot = new Vector2(0.5f, 1f);
        timerHudRT.sizeDelta = new Vector2(140f, 44f);
        timerHudRT.anchoredPosition = new Vector2(0f, -12f);
        MatchTimerHUD timerHud = timerHudGO.AddComponent<MatchTimerHUD>();

        GameObject timerPanelGO = new GameObject("Panel", typeof(RectTransform));
        timerPanelGO.transform.SetParent(timerHudGO.transform, false);
        RectTransform timerPanelRT = timerPanelGO.GetComponent<RectTransform>();
        timerPanelRT.anchorMin = Vector2.zero;
        timerPanelRT.anchorMax = Vector2.one;
        timerPanelRT.offsetMin = Vector2.zero;
        timerPanelRT.offsetMax = Vector2.zero;
        Image timerBg = timerPanelGO.AddComponent<Image>();
        timerBg.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject timerTextGO = CreateTMPText("TimerText", timerPanelGO.transform, "00:00", 24f, TextAlignmentOptions.Center);
        RectTransform timerTextRT = timerTextGO.GetComponent<RectTransform>();
        timerTextRT.anchorMin = Vector2.zero;
        timerTextRT.anchorMax = Vector2.one;
        timerTextRT.offsetMin = Vector2.zero;
        timerTextRT.offsetMax = Vector2.zero;

        timerHud.panel = timerPanelGO;
        timerHud.timerText = timerTextGO.GetComponent<TextMeshProUGUI>();
        timerPanelGO.SetActive(false);

        // --- ロケット砲 チャージゲージ ---
        // 同じ理由でRocketChargeGaugeHUD自身は常時アクティブなオブジェクトに付け、Panelだけ切り替える。
        GameObject gaugeHudGO = new GameObject("RocketChargeGaugeHUD", typeof(RectTransform));
        gaugeHudGO.transform.SetParent(canvasTransform, false);
        RectTransform gaugeHudRT = gaugeHudGO.GetComponent<RectTransform>();
        gaugeHudRT.anchorMin = new Vector2(0.5f, 0f);
        gaugeHudRT.anchorMax = new Vector2(0.5f, 0f);
        gaugeHudRT.pivot = new Vector2(0.5f, 0f);
        gaugeHudRT.sizeDelta = new Vector2(320f, 32f);
        gaugeHudRT.anchoredPosition = new Vector2(0f, 90f);
        RocketChargeGaugeHUD gaugeHud = gaugeHudGO.AddComponent<RocketChargeGaugeHUD>();

        GameObject gaugePanelGO = new GameObject("Panel", typeof(RectTransform));
        gaugePanelGO.transform.SetParent(gaugeHudGO.transform, false);
        RectTransform gaugePanelRT = gaugePanelGO.GetComponent<RectTransform>();
        gaugePanelRT.anchorMin = Vector2.zero;
        gaugePanelRT.anchorMax = Vector2.one;
        gaugePanelRT.offsetMin = Vector2.zero;
        gaugePanelRT.offsetMax = Vector2.zero;
        Image gaugeBg = gaugePanelGO.AddComponent<Image>();
        gaugeBg.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject gaugeFillBgGO = new GameObject("FillBackground", typeof(RectTransform));
        gaugeFillBgGO.transform.SetParent(gaugePanelGO.transform, false);
        RectTransform gaugeFillBgRT = gaugeFillBgGO.GetComponent<RectTransform>();
        gaugeFillBgRT.anchorMin = Vector2.zero;
        gaugeFillBgRT.anchorMax = Vector2.one;
        gaugeFillBgRT.offsetMin = new Vector2(4f, 4f);
        gaugeFillBgRT.offsetMax = new Vector2(-4f, -4f);
        Image gaugeFillBg = gaugeFillBgGO.AddComponent<Image>();
        gaugeFillBg.color = new Color(1f, 1f, 1f, 0.15f);

        GameObject gaugeFillGO = new GameObject("Fill", typeof(RectTransform));
        gaugeFillGO.transform.SetParent(gaugeFillBgGO.transform, false);
        RectTransform gaugeFillRT = gaugeFillGO.GetComponent<RectTransform>();
        gaugeFillRT.anchorMin = Vector2.zero;
        gaugeFillRT.anchorMax = Vector2.one;
        gaugeFillRT.offsetMin = Vector2.zero;
        gaugeFillRT.offsetMax = Vector2.zero;
        Image gaugeFill = gaugeFillGO.AddComponent<Image>();
        gaugeFill.color = new Color(1f, 0.5f, 0.1f, 1f);
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        gaugeFill.fillAmount = 0f;

        GameObject gaugeLabelGO = CreateTMPText("Label", gaugePanelGO.transform, "発射準備中…", 14f, TextAlignmentOptions.Center);
        RectTransform gaugeLabelRT = gaugeLabelGO.GetComponent<RectTransform>();
        gaugeLabelRT.anchorMin = Vector2.zero;
        gaugeLabelRT.anchorMax = Vector2.one;
        gaugeLabelRT.offsetMin = Vector2.zero;
        gaugeLabelRT.offsetMax = Vector2.zero;

        gaugeHud.panel = gaugePanelGO;
        gaugeHud.fillImage = gaugeFill;
        gaugePanelGO.SetActive(false);
    }

    private static void PositionTop(GameObject go, float topOffset, float height)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -topOffset);
        rt.sizeDelta = new Vector2(-40f, height);
    }

    private static CompanyTradeRow CreateRowPrefab(SfxSet sfx)
    {
        GameObject rowGO = new GameObject("CompanyTradeRow", typeof(RectTransform));
        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 150f;
        Image rowBg = rowGO.AddComponent<Image>();
        rowBg.color = new Color(1f, 1f, 1f, 0.08f);

        VerticalLayoutGroup rowVLG = rowGO.AddComponent<VerticalLayoutGroup>();
        rowVLG.padding = new RectOffset(8, 8, 6, 6);
        rowVLG.spacing = 4f;
        rowVLG.childControlWidth = true;
        rowVLG.childForceExpandWidth = true;
        rowVLG.childControlHeight = true;
        rowVLG.childForceExpandHeight = false;

        CompanyTradeRow rowScript = rowGO.AddComponent<CompanyTradeRow>();
        rowScript.clickSound = sfx.Click;

        GameObject nameGO = CreateTMPText("NameText", rowGO.transform, "Company", 20f, TextAlignmentOptions.Left);
        rowScript.companyNameText = nameGO.GetComponent<TextMeshProUGUI>();

        GameObject priceGO = CreateTMPText("PriceText", rowGO.transform, "$100.00", 18f, TextAlignmentOptions.Left);
        rowScript.priceText = priceGO.GetComponent<TextMeshProUGUI>();

        GameObject posGO = CreateTMPText("PositionText", rowGO.transform, "保有: 0株", 16f, TextAlignmentOptions.Left);
        rowScript.positionText = posGO.GetComponent<TextMeshProUGUI>();

        GameObject buttonsRow = new GameObject("Buttons", typeof(RectTransform));
        buttonsRow.transform.SetParent(rowGO.transform, false);
        HorizontalLayoutGroup hlg = buttonsRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandHeight = true;
        LayoutElement buttonsLE = buttonsRow.AddComponent<LayoutElement>();
        buttonsLE.preferredHeight = 32f;

        rowScript.buyButton = CreateButton("BuyButton", buttonsRow.transform, "買う", new Color(0.2f, 0.55f, 0.9f));
        rowScript.sellButton = CreateButton("SellButton", buttonsRow.transform, "売る", new Color(0.3f, 0.7f, 0.4f));

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(rowGO, RowPrefabPath);
        Object.DestroyImmediate(rowGO);
        return prefabAsset.GetComponent<CompanyTradeRow>();
    }

    private static GameObject CreateTMPText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        if (s_JapaneseFont != null) tmp.font = s_JapaneseFont;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8f;
        return go;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (s_JapaneseFont != null) tmp.font = s_JapaneseFont;
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        return btn;
    }

    // Sawarabi Gothic (Assets/Fonts/Source, SIL Open Font License) からTMPフォントアセットを生成する。
    // 以前は「TryAddCharactersで文字を焼き込んでからCreateAssetで保存」の順序で試して、
    // 保存後に中身が空になる不具合が再現したため、順序を「先にCreateAssetで資産登録してから
    // TryAddCharactersで文字を焼き込み、最後にもう一度SaveAssets」に変更して対策する。
    private static TMP_FontAsset CreateJapaneseFontAsset()
    {
        const string sourceFontPath = "Assets/Fonts/Source/SawarabiGothic-Regular.ttf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"SceneBuilder: 日本語フォントファイルが見つかりません: {sourceFontPath}");
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(JapaneseFontAssetPath) != null)
        {
            AssetDatabase.DeleteAsset(JapaneseFontAssetPath);
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null) return null;

        // TMP_FontAsset.clearDynamicDataOnBuild(既定でtrue)が原因で、この後のシーン保存等の
        // タイミングで焼き込んだ動的アトラスデータが消えていた(バッチモードでのみ再現)。
        // internalプロパティで直接は触れないため、SerializedObject経由でfalseに固定する。
        {
            SerializedObject so = new SerializedObject(fontAsset);
            SerializedProperty prop = so.FindProperty("m_ClearDynamicDataOnBuild");
            if (prop != null)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Fonts"))
        {
            AssetDatabase.CreateFolder("Assets", "Fonts");
        }

        // 先にアセットとして登録する(まだ中身は初期状態の1x1プレースホルダ)。
        AssetDatabase.CreateAsset(fontAsset, JapaneseFontAssetPath);
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D tex in fontAsset.atlasTextures)
            {
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }
        if (fontAsset.material != null)
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        AssetDatabase.SaveAssets();

        // 資産として登録済みの状態になってから文字を焼き込む。
        string charSet = BuildJapaneseCharacterSet();
        fontAsset.TryAddCharacters(charSet, out string missingCharacters);
        if (!string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning($"SceneBuilder: アトラスに入りきらなかった文字があります: {missingCharacters}");
        }
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D tex in fontAsset.atlasTextures)
            {
                if (tex != null) tex.Apply(false, false);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        if (fontAsset.material != null) EditorUtility.SetDirty(fontAsset.material);
        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D tex in fontAsset.atlasTextures)
            {
                if (tex != null) EditorUtility.SetDirty(tex);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TMP_FontAsset reloaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontAssetPath);
        int count = reloaded != null && reloaded.characterTable != null ? reloaded.characterTable.Count : -1;
        Debug.Log($"SceneBuilder: 日本語フォントアセットを生成しました(文字数={count})。");
        return reloaded;
    }

    // ASCII全般 + ひらがな/カタカナ全域 + 現在および今後のUI文言で使いそうな主要漢字。
    private static string BuildJapaneseCharacterSet()
    {
        var sb = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) sb.Append(c);
        for (char c = 'ぁ'; c <= 'ゖ'; c++) sb.Append(c); // ひらがな
        for (char c = 'ァ'; c <= 'ー'; c++) sb.Append(c); // カタカナ
        sb.Append("資金保有株空売買返済破産円商店舗価格暴落高騰工作員指示役目標達成時間終了勝利敗北");
        return sb.ToString();
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        Material mat = new Material(shader) { color = color };
        renderer.sharedMaterial = mat;
    }

    private static void AddSceneToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void FinishBatch(int exitCode)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
        }
    }
}
