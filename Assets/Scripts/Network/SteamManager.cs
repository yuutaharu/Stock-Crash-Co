using System;
using UnityEngine;
using Steamworks;

// SteamAPIの初期化とRunCallbacks()の呼び出しを担当する。
// シーンの最初期に1つだけ存在させる（DontDestroyOnLoadでシーンを跨いで生存させる）。
[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour
{
    private static SteamManager instance;

    protected static SteamManager Instance
    {
        get
        {
            if (instance == null) return new GameObject("SteamManager").AddComponent<SteamManager>();
            return instance;
        }
    }

    private static bool everInitialized = false;

    public static bool Initialized => Instance.initializedInternal;

    private bool initializedInternal = false;

    protected virtual void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (everInitialized)
        {
            throw new Exception("SteamManagerを複数回初期化しようとしました。SteamManagerはアプリ全体で1つだけにしてください。");
        }

        if (!Packsize.Test())
        {
            Debug.LogError("[Steamworks.NET] Packsize Test が失敗しました。32/64bitのビルド設定が一致しているか確認してください。");
        }

        try
        {
            // Steam経由(steam://run/<appid>)で起動していない場合、必要ならSteamクライアント経由で
            // 再起動させる。SteamのAppIdが未登録の開発中は steam_appid.txt を使う。
            if (SteamAPI.RestartAppIfNecessary(AppId_t.Invalid))
            {
                Application.Quit();
                return;
            }
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError(
                $"[Steamworks.NET] ネイティブライブラリが見つかりません。{e}", this);
            Application.Quit();
            return;
        }

        initializedInternal = SteamAPI.Init();
        if (!initializedInternal)
        {
            Debug.LogError(
                "[Steamworks.NET] SteamAPI_Init() に失敗しました。Steamクライアントが起動しているか、steam_appid.txt がプロジェクト直下にあるか確認してください。");
            return;
        }

        everInitialized = true;
    }

    protected virtual void OnEnable()
    {
        if (instance == null) instance = this;
    }

    protected virtual void OnDestroy()
    {
        if (instance != this) return;
        instance = null;

        if (!initializedInternal) return;
        SteamAPI.Shutdown();
    }

    protected virtual void Update()
    {
        if (!initializedInternal) return;
        SteamAPI.RunCallbacks();
    }
}
