using System.Collections.Generic;
using TMPro;
using UnityEngine;

// トレーディングPCのUI画面全体。開いた時に対象企業ぶんの行を並べ、
// チーム共有資金を表示し続ける。TradingPC.csから開閉される。
public class TradingUIController : MonoBehaviour
{
    [Header("UI参照")]
    public TMP_Text walletText;
    public Transform rowContainer;
    public CompanyTradeRow rowPrefab;

    private readonly List<CompanyTradeRow> spawnedRows = new List<CompanyTradeRow>();

    // FirstPersonLook側がカーソルのロック可否を判断するために参照する。
    // (取引画面が開いている間はプレイ中でもカーソルをロックしない)
    public static bool IsOpen { get; private set; } = false;

    public void Open(IReadOnlyList<Company> companies)
    {
        gameObject.SetActive(true);
        RebuildRows(companies);
        IsOpen = true;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        IsOpen = false;
    }

    private void RebuildRows(IReadOnlyList<Company> companies)
    {
        foreach (CompanyTradeRow row in spawnedRows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        spawnedRows.Clear();

        if (rowPrefab == null || rowContainer == null || companies == null) return;

        for (int i = 0; i < companies.Count; i++)
        {
            CompanyTradeRow row = Instantiate(rowPrefab, rowContainer);
            row.Setup(companies[i]);
            spawnedRows.Add(row);
        }
    }

    void Update()
    {
        if (walletText != null && MarketManager.Instance != null)
        {
            walletText.text = $"チーム資金: ${MarketManager.Instance.playerMoney:F0}";
        }
    }
}
