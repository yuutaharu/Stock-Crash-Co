# **『Stock Crash Co.』基本設計書（バージョン 1.1）**

## **1\. 概要 & コンセプト**

『Stock Crash Co.』は、プレイヤーが悪徳トレーダー兼現場の工作員となり、3Dマップ上で物理的な妨害や宣伝を行うことでリアルタイムに株価を乱高下させ、目標金額の達成を目指すリアルタイム協力アクション（Co-op）ゲームである。

> * **タイトル名：** Stock Crash Co.  
> * **ジャンル：** リアルタイム物理介入型株取引アクション（1～4人協力 / ソロ対応）  
> * **プラットフォーム：** PC（Steam想定）  
> * **開発環境：** Unity (C\#) / Visual Studio Code / Mac Mini  
> * **アートスタイル：** ポップな3Dローポリゴン（Low-Poly）

## **2\. コアループ & ゲームサイクル**

> 1. **ブリーフィング：** 目標金額の確認と、自分たちが取引する担当企業（対象2社のうちどちらか1つ）を選択。選ばなければ開始できない。  
> 2. **リアルタイム工作＆トレーディング：** 指示役と現場工作員に分かれ、3Dマップ上でゴミ撒きや掃除等の工作を行いながら、拠点内のトレーディングPCまで移動して担当企業の株を売買（現物買い / 売却のみ、空売りはなし）。  
> 3. **市場反映：** 工作結果に応じてリアルタイムに株価が上下し、利益を獲得（何も工作しなければ株価は変動しない）。  
> 4. **決着：** 目標金額に到達すると、選ばなかった側の企業（相手企業）の店舗前にロケット砲が出現。Eキー長押しで発射し、相手の店舗を爆破すると勝利。制限時間切れは敗北。  
> 5. **リザルト & スキル強化：** 獲得した資金やポイントを使って、プレイヤーの移動速度や工作ツールの強化を実施。


## 3. Steam P2P ネットワーク実装コード (C#)

### 3.1 Steamロビー作成 & フレンド招待 (`SteamLobbyManager.cs`)
```csharp
using UnityEngine;
using Steamworks;

public class SteamLobbyManager : MonoBehaviour
{
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEntered;

    private const string HostAddressKey = "HostAddress";

    private void Start()
    {
        if (!SteamManager.Initialized) return;

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    // ロビー作成（ホスト）
    public void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(lobbyID, HostAddressKey, SteamUser.GetSteamID().ToString());
        Debug.Log("ロビーが正常に作成されました。");
    }

    // Steamフレンドからの参加リクエスト
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        // ロビー入室後のゲームシーン遷移処理
        Debug.Log("ロビーに入室しました。");
    }
}

## **4\. チュートリアル / 第1マップ仕様（バーガーショップ vs ピザ屋）**

1画面に収まるシンプルな街角マップ。拠点（事務所）内に「トレーディングPC」が設置されており、屋外に「バーガー・キングダム」と「ピザ・パレス」が配置されている。

### **4.1 トレーディングPC仕様**

常時HUD上に取引画面を表示するのではなく、**「マップ上の拠点にあるPCの前まで物理的に移動してインタラクトする（Eキー）」**ことで株取引UIが開く仕様。物理的な移動とワンテンポのタイムラグが発生することで、プレイヤー同士のリアルタイムな情報共有とドタバタ感を演出する。

### **4.2 工作・介入アクション仕様**

| 種別 | アクション内容 | 市場への影響   |
| :---- | :---- | :---- |
| **暴落工作** | Eキーでゴミを前方に投げる。物理で飛び、実際に店舗の壁に命中した場所にそのゴミが張り付く（外すと数秒で消える） | 命中した分だけ汚れ度（dirtiness）上昇 → 株価下落 |
| **高騰工作** | 店舗の近くでFキーを押して清掃（常時携帯しているモップを使う想定）。命中して張り付いているゴミも1つ取り除かれる | 人気度（popularity）上昇 → 株価上昇 |
| **終盤兵器** | 目標金額達成後に相手企業の店舗前に出現するロケット砲を、Eキー長押しで発射 | 相手企業の株価が即座に0ドル（破産）へ。これが勝利条件 |

暴落工作は「近づいてキーを押せば即座に汚れる」方式ではなく、実際に狙って投げて命中させる必要がある。ゴミは命中地点にそのまま残るため、汚れの蓄積が視覚的にも分かりやすい。

株価は `dirtiness` と `popularity` だけで決まる（ランダムなノイズは持たない）ため、誰も工作していない間は変動しない。
さらに、自社の評判と競合企業の評判の"差"で価格が決まるため、相手企業を汚すと自社の株価がその分だけ相対的に上がる（2社の価格変動分の合計が常に一定になるゼロサム関係）。

### **4.3 企業選択とロケット砲による勝利条件**

ブリーフィングで、対象2社（バーガー・キングダム／ピザ・パレス）のうちどちらか一方を「担当企業」として選ぶ。選んだ瞬間、選ばなかった方は自動的に「相手企業」になる。

- トレーディングPCでは担当企業の株だけを売買できる（相手企業の株は取引できない）。
- チーム共有資金が目標金額に到達すると、相手企業の店舗の近くにロケット砲が出現する（それまでは存在しない）。
- ロケット砲の前でEキーを2秒間長押しすると発射され、相手企業を即座に破産させる。この瞬間が勝利条件。
- 制限時間内に相手企業を破壊できなければ敗北。

## **5\. プロトタイプ（MVP）実装コード構造**

### **5.1 店舗ステータス管理（Company.cs）**

using UnityEngine;

public class Company : MonoBehaviour  
{  
    \[Header("基本情報")\]  
    public string companyName \= "Burger Co.";  
    public float basePrice \= 100f;  
    public float currentPrice \= 100f;

    \[Header("店舗パラメータ")\]  
    public float dirtiness \= 0f;  
    public float popularity \= 0f;

    \[Header("競合企業")\]  
    public Company rival; // 価格は自社と相手の評判の差で決まる

    void Update()  
    {  
        CalculateStockPrice();  
    }

    void CalculateStockPrice()  
    {  
        // 自社と競合、それぞれの評判(popularity/dirtiness由来)の差で決まる。
        // 相手を汚す/自分を掃除すると、その分だけ自分の株価が相対的に上がる(ゼロサム)。
        float ownScore \= (popularity \* 2.0f) \- (dirtiness \* 3.0f);  
        float rivalScore \= rival \!= null ? (rival.popularity \* 2.0f) \- (rival.dirtiness \* 3.0f) : 0f;  
        float priceModifier \= ownScore \- rivalScore;  
        currentPrice \= Mathf.Max(1.0f, basePrice \+ priceModifier);  
    }

    public void AddDirt(float amount) { dirtiness \+= amount; }  
    public void CleanDirt(float amount) { dirtiness \= Mathf.Max(0f, dirtiness \- amount); }  
    public void AddPopularity(float amount) { popularity \+= amount; }  
}

### **5.2 市場・資金管理（MarketManager.cs）**

using UnityEngine;

public class MarketManager : MonoBehaviour  
{  
    \[Header("チーム共有資金")\]  
    public float playerMoney \= 1000f;

    \[Header("保有ポジション（担当企業のみ）")\]  
    public int boughtSharesCount \= 0;

    public void BuyStock(Company company, int amount)  
    {  
        float cost \= company.currentPrice \* amount;  
        if (playerMoney \>= cost)  
        {  
            playerMoney \-= cost;  
            boughtSharesCount \+= amount;  
        }  
    }

    public void SellStock(Company company, int amount)  
    {  
        int sellAmount \= Mathf.Min(amount, boughtSharesCount);  
        if (sellAmount \<= 0\) return;

        boughtSharesCount \-= sellAmount;  
        playerMoney \+= company.currentPrice \* sellAmount;  
    }  
}

### **5.3 拠点トレーディングPC操作（TradingPC.cs）**

using UnityEngine;

public class TradingPC : MonoBehaviour  
{  
    \[Header("参照")\]  
    public MarketManager marketManager;  
    public Company targetCompany;  
    public GameObject pcScreenUI;

    private bool isPlayerNearby \= false;

    void Start()  
    {  
        if (pcScreenUI \!= null) pcScreenUI.SetActive(false);  
    }

    void Update()  
    {  
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))  
        {  
            TogglePCScreen();  
        }  
    }

    void TogglePCScreen()  
    {  
        bool currentState \= pcScreenUI.activeSelf;  
        pcScreenUI.SetActive(\!currentState);  
    }

    private void OnTriggerEnter(Collider other)  
    {  
        if (other.CompareTag("Player"))  
        {  
            isPlayerNearby \= true;  
        }  
    }

    private void OnTriggerExit(Collider other)  
    {  
        if (other.CompareTag("Player"))  
        {  
            isPlayerNearby \= false;  
            if (pcScreenUI \!= null) pcScreenUI.SetActive(false);  
        }  
    }  
}

### **5.4 プレイヤー操作（PlayerController.cs）**

using UnityEngine;

public class PlayerController : MonoBehaviour  
{  
    public float moveSpeed \= 6.0f;  
    private Rigidbody rb;  
    public Company targetCompany;

    void Start()  
    {  
        rb \= GetComponent\<Rigidbody\>();  
        if (rb \!= null) rb.freezeRotation \= true;  
    }

    void Update()  
    {  
        HandleMovement();  
        HandleActions();  
    }

    void HandleMovement()  
    {  
        float moveX \= Input.GetAxisRaw("Horizontal");  
        float moveZ \= Input.GetAxisRaw("Vertical");  
        Vector3 moveDirection \= new Vector3(moveX, 0, moveZ).normalized;  
          
        if (moveDirection.magnitude \> 0.1f)  
        {  
            transform.forward \= moveDirection;  
            transform.Translate(moveDirection \* moveSpeed \* Time.deltaTime, Space.World);  
        }  
    }

    void HandleActions()  
    {  
        if (targetCompany \== null) return;

        if (Input.GetKeyDown(KeyCode.E)) { targetCompany.AddDirt(10f); }  
        if (Input.GetKeyDown(KeyCode.F)) { targetCompany.CleanDirt(10f); }  
    }

    private void OnTriggerEnter(Collider other)  
    {  
        Company company \= other.GetComponent\<Company\>();  
        if (company \!= null) targetCompany \= company;  
    }

    private void OnTriggerExit(Collider other)  
    {  
        Company company \= other.GetComponent\<Company\>();  
        if (company \!= null && company \== targetCompany) targetCompany \= null;  
    }  
}  
