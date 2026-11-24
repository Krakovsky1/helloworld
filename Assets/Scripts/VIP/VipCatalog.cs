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

    public IReadOnlyList<VipPersonality> Personalities => personalities;

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
