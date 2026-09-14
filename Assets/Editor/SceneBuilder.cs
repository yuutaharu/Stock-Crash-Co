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

    [MenuItem("Tools/Stock Crash Co/2. Build MVP Stage")]
    public static void BuildStage()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        s_JapaneseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JapaneseFontAssetPath);
        if (s_JapaneseFont == null || s_JapaneseFont.characterTable == null || s_JapaneseFont.characterTable.Count == 0)
        {
            s_JapaneseFont = CreateJapaneseFontAsset();
        }

        CreateLight();
        CreateFloor();
        MarketManager marketManager = CreateMarketManager();

        Company burger = CreateCompany("Burger Kingdom", 0, new Vector3(-6f, 1.5f, 5f), new Color(0.85f, 0.35f, 0.2f));
        Company pizza = CreateCompany("Pizza Palace", 1, new Vector3(6f, 1.5f, 5f), new Color(0.95f, 0.75f, 0.2f));

        CreateGameSessionManager(burger, pizza);

        Transform canvasTransform = CreateUIRoot();
        TradingUIController tradingUI = CreateTradingUI(canvasTransform);
        CreateBriefingAndResultUI(canvasTransform);
        CreateTradingPC(new Vector3(0f, 1f, -6f), tradingUI, burger, pizza);
        GameObject player = CreatePlayer(new Vector3(0f, 1f, 0f));
        CreateFirstPersonCamera(player.transform);

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

    private static void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f); // 20x20
        ApplyColor(floor, new Color(0.5f, 0.55f, 0.5f));
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

    private static Company CreateCompany(string companyName, int id, Vector3 position, Color color)
    {
        GameObject shop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shop.name = companyName;
        shop.transform.position = position;
        shop.transform.localScale = new Vector3(3f, 3f, 3f);
        ApplyColor(shop, color);

        // 見た目通りの大きさで物理的に塞ぐ実体コライダー(プレイヤーがめり込まないようにする)と、
        // それより一回り大きい検知専用のトリガーコライダー(近づいた判定用)を分けて持たせる。
        BoxCollider solidCollider = shop.GetComponent<BoxCollider>();
        solidCollider.isTrigger = false;

        BoxCollider triggerCollider = shop.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = solidCollider.size * 1.6f;

        Company company = shop.AddComponent<Company>();
        company.companyId = id;
        company.companyName = companyName;
        company.basePrice = 100f;
        company.currentPrice = 100f;
        return company;
    }

    private static void CreateTradingPC(Vector3 position, TradingUIController tradingUI, params Company[] companies)
    {
        GameObject desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        desk.name = "TradingPC";
        desk.transform.position = position;
        desk.transform.localScale = new Vector3(1.2f, 1f, 0.8f);
        ApplyColor(desk, new Color(0.15f, 0.2f, 0.3f));

        // 見た目通りの大きさの実体コライダー(めり込み防止)と、操作可能距離を広げる検知用トリガーを分ける。
        BoxCollider solidCollider = desk.GetComponent<BoxCollider>();
        solidCollider.isTrigger = false;

        BoxCollider triggerCollider = desk.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(4f, 4f, 4f); // 見た目より広い範囲で反応させる

        TradingPC tradingPC = desk.AddComponent<TradingPC>();
        tradingPC.tradableCompanies = companies;
        tradingPC.tradingUI = tradingUI;
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

    private static void CreateFirstPersonCamera(Transform playerTransform)
    {
        GameObject camGO = new GameObject("FPSCamera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(playerTransform, false);
        camGO.transform.localPosition = new Vector3(0f, 0.6f, 0.2f); // カプセル中心から目の高さぶん上
        camGO.transform.localRotation = Quaternion.identity;
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<FirstPersonLook>();
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

    private static TradingUIController CreateTradingUI(Transform canvasTransform)
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

        tradingUI.rowPrefab = CreateRowPrefab();

        panelGO.SetActive(false);
        return tradingUI;
    }

    private static void CreateBriefingAndResultUI(Transform canvasTransform)
    {
        GamePhaseUIRouter router = canvasTransform.GetComponent<GamePhaseUIRouter>();

        // --- ブリーフィングパネル ---
        GameObject briefingGO = new GameObject("BriefingPanel", typeof(RectTransform));
        briefingGO.transform.SetParent(canvasTransform, false);
        RectTransform briefingRT = briefingGO.GetComponent<RectTransform>();
        briefingRT.anchorMin = new Vector2(0.5f, 0.5f);
        briefingRT.anchorMax = new Vector2(0.5f, 0.5f);
        briefingRT.sizeDelta = new Vector2(560f, 360f);
        Image briefingBg = briefingGO.AddComponent<Image>();
        briefingBg.color = new Color(0f, 0f, 0f, 0.85f);

        BriefingUIController briefing = briefingGO.AddComponent<BriefingUIController>();

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

    private static CompanyTradeRow CreateRowPrefab()
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

        GameObject nameGO = CreateTMPText("NameText", rowGO.transform, "Company", 20f, TextAlignmentOptions.Left);
        rowScript.companyNameText = nameGO.GetComponent<TextMeshProUGUI>();

        GameObject priceGO = CreateTMPText("PriceText", rowGO.transform, "$100.00", 18f, TextAlignmentOptions.Left);
        rowScript.priceText = priceGO.GetComponent<TextMeshProUGUI>();

        GameObject posGO = CreateTMPText("PositionText", rowGO.transform, "保有: 0株 / 空売り: 0株", 16f, TextAlignmentOptions.Left);
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
        rowScript.shortButton = CreateButton("ShortButton", buttonsRow.transform, "空売り", new Color(0.8f, 0.4f, 0.2f));
        rowScript.closeShortButton = CreateButton("CloseShortButton", buttonsRow.transform, "返済", new Color(0.6f, 0.3f, 0.7f));

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
