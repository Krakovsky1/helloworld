using System.Collections.Generic;
using UnityEngine;

// FR-37: Zoznam všetkých VIP osobností.
// Súbor: Assets/Resources/VIP/VipCatalog.asset. V kóde: VipCatalog.Load().Personalities
// Ak by sa súbor pokazil, znova ho vytvorí Tools > Tropical Resort > Setup VIP Catalog.
[CreateAssetMenu(fileName = "VipCatalog", menuName = "Tropical Resort/VIP Catalog")]
public class VipCatalog : ScriptableObject
{
    public const string ResourcePath = "VIP/VipCatalog";

    [SerializeField] List<VipPersonality> personalities = new List<VipPersonality>();

    [Header("FR-31: Ponuka VIP hostí (tlačidlo VIP)")]
    [Tooltip("Koľko náhodných VIP hostí sa ponúkne naraz")]
    [Min(1)] [SerializeField] int offerSize = 3;
    [Tooltip("Šanca vzácnosti v ponuke (relatívna váha). Bežná sa ukazuje najčastejšie, Legendárna najmenej.")]
    [Min(0f)] [SerializeField] float commonWeight = 55f;
    [Min(0f)] [SerializeField] float rareWeight = 30f;
    [Min(0f)] [SerializeField] float epicWeight = 12f;
    [Min(0f)] [SerializeField] float legendaryWeight = 3f;

    public IReadOnlyList<VipPersonality> Personalities => personalities;
    public int OfferSize => Mathf.Max(1, offerSize);

    // Relatívna váha vzácnosti pri losovaní ponuky (FR-31).
    public float RarityWeight(VipRarity rarity)
    {
        switch (rarity)
        {
            case VipRarity.Common: return Mathf.Max(0f, commonWeight);
            case VipRarity.Rare: return Mathf.Max(0f, rareWeight);
            case VipRarity.Epic: return Mathf.Max(0f, epicWeight);
            default: return Mathf.Max(0f, legendaryWeight);
        }
    }

    static VipCatalog cached;

    public static VipCatalog Load()
    {
        if (cached == null) cached = Resources.Load<VipCatalog>(ResourcePath);
        if (cached == null) Debug.LogWarning("VipCatalog: chýba Resources/" + ResourcePath + ".asset");
        return cached;
    }

    public VipPersonality Find(string id)
    {
        foreach (var vip in personalities)
            if (vip != null && vip.id == id) return vip;
        return null;
    }

    // Osobnosti, ktorých podmienky sú teraz splnené.
    public List<VipPersonality> Available()
    {
        var result = new List<VipPersonality>();
        foreach (var vip in personalities)
            if (vip != null && VipRequirements.CanInvite(vip)) result.Add(vip);
        return result;
    }

#if UNITY_EDITOR
    // Používa len editorový skript VipCatalogSetup.
    public void SetPersonalities(List<VipPersonality> list) { personalities = list; }
#endif
}
