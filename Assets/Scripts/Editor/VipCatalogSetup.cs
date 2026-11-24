// Unity: Tools > Tropical Resort > Setup VIP Catalog
// FR-37: Vytvorí (alebo prepíše) Assets/Resources/VIP/VipCatalog.asset s 10 VIP osobnosťami
// a priradí im portréty (Resources/UI/VIP), modely (TropicalResort_Characters/VIP) a rigy.
// Netreba ho spúšťať - súbor už v projekte je. Použi ho, len ak sa katalóg pokazí alebo chceš vrátiť pôvodné hodnoty.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VipCatalogSetup
{
    const string AssetPath = "Assets/Resources/VIP/VipCatalog.asset";
    const string Characters = "Assets/TropicalResort_Characters/";

    [MenuItem("Tools/Tropical Resort/Setup VIP Catalog")]
    public static void Setup()
    {
        var list = new List<VipPersonality>
        {
            Vip("travel_vlogger", "VIP_TravelVlogger", "Lea Nováková", "Cestovateľská vloggerka",
                "Natáča videá z exotických miest pre pol milióna sledovateľov. Stačí jej útulný hotel a pekný výhľad.",
                VipRarity.Common, 120, 15, 1.2f, Req(VipBuilding.Hotel, 2, false)),
            Vip("nature_photographer", "VIP_Photographer", "Tomáš Horák", "Fotograf prírody",
                "Fotí západy slnka a tropickú prírodu pre cestovné magazíny. Po celodennom fotení si rád dá drink.",
                VipRarity.Common, 160, 25, 1.3f, Req(VipBuilding.Hotel, 2, false), Req(VipBuilding.Bar, 2, false)),
            Vip("fitness_influencer", "VIP_FitnessInfluencer", "Mia Sunny", "Fitness influencerka",
                "Ranné cvičenie, zdravé smoothie a stories pri bazéne. Bez bazéna nepríde.",
                VipRarity.Common, 220, 35, 1.4f, Req(VipBuilding.Pool, 1, false)),
            Vip("food_blogger", "VIP_FoodBlogger", "Kenji Mori", "Food bloger",
                "Hodnotí reštaurácie po celom svete. Jeho recenzia vie rezortu priniesť zástupy hostí.",
                VipRarity.Rare, 350, 60, 1.7f, Req(VipBuilding.Restaurant, 0, true), Req(VipBuilding.Hotel, 3, false)),
            Vip("dj_nova", "VIP_DJ", "DJ Nova", "DJ a hudobný producent",
                "Hrá na najväčších plážových párty. Chce špičkový bar a bazén na after party.",
                VipRarity.Rare, 420, 80, 1.8f, Req(VipBuilding.Bar, 0, true), Req(VipBuilding.Pool, 2, false)),
            Vip("bestseller_writer", "VIP_Writer", "Eva Kráľová", "Spisovateľka bestsellerov",
                "Na ostrov prichádza dopísať novú knihu. Potrebuje pokoj, wellness a pohodlný hotel.",
                VipRarity.Rare, 480, 100, 2.0f, Req(VipBuilding.Wellness, 1, false), Req(VipBuilding.Hotel, 3, false)),
            Vip("football_star", "VIP_FootballStar", "Diego Santos", "Futbalová hviezda",
                "Kapitán reprezentácie si užíva dovolenku po sezóne. Očakáva luxusný hotel a najlepší bazén.",
                VipRarity.Epic, 750, 150, 2.4f, Req(VipBuilding.Hotel, 0, true), Req(VipBuilding.Pool, 0, true)),
            Vip("celebrity_chef", "VIP_CelebrityChef", "Marco Bellini", "Celebrity šéfkuchár",
                "Majiteľ ocenených reštaurácií a hviezda kulinárskych show. Skontroluje každú kuchyňu aj bar.",
                VipRarity.Epic, 850, 180, 2.6f, Req(VipBuilding.Hotel, 0, true), Req(VipBuilding.Restaurant, 0, true), Req(VipBuilding.Bar, 0, true)),
            Vip("movie_star", "VIP_MovieStar", "Isabella Moreau", "Filmová herečka",
                "Hviezda veľkých filmov hľadá súkromie a relax. Prijme len luxusný hotel s najlepším wellness.",
                VipRarity.Epic, 1000, 220, 2.8f, Req(VipBuilding.Hotel, 0, true), Req(VipBuilding.Wellness, 0, true)),
            Vip("pop_star", "VIP_PopStar", "Aurora Star", "Popová hviezda",
                "Najznámejšia speváčka sveta. Jej návšteva preslávi rezort - ale všetko musí byť dokonalé.",
                VipRarity.Legendary, 1600, 350, 3.5f, Req(VipBuilding.Hotel, 0, true), Req(VipBuilding.Restaurant, 0, true), Req(VipBuilding.Bar, 0, true), Req(VipBuilding.Pool, 0, true), Req(VipBuilding.Wellness, 0, true)),
        };

        var catalog = AssetDatabase.LoadAssetAtPath<VipCatalog>(AssetPath);
        if (catalog == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/VIP");
            catalog = ScriptableObject.CreateInstance<VipCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        Undo.RecordObject(catalog, "Setup VIP Catalog");
        catalog.SetPersonalities(list);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
        Debug.Log("VIP katalóg: " + list.Count + " osobností je v " + AssetPath);
    }

    static VipPersonality Vip(string id, string model, string name, string profession, string description,
                              VipRarity rarity, int cost, int resortValue, float multiplier, params VipBuildingRequirement[] reqs)
    {
        var vip = new VipPersonality
        {
            id = id, displayName = name, profession = profession, description = description, rarity = rarity,
            inviteCost = cost, requiredResortValue = resortValue, rewardMultiplier = multiplier,
            buildingRequirements = new List<VipBuildingRequirement>(reqs),
            portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/VIP/vip_" + id + ".png"),
            model = AssetDatabase.LoadAssetAtPath<GameObject>(Characters + "VIP/" + model + ".obj"),
            rig = AssetDatabase.LoadAssetAtPath<TextAsset>(Characters + "Rigs/" + model + ".rig.txt"),
        };
        if (vip.portrait == null) Debug.LogWarning("VIP " + id + ": chýba portrét");
        if (vip.model == null) Debug.LogWarning("VIP " + id + ": chýba model " + model);
        return vip;
    }

    static VipBuildingRequirement Req(VipBuilding building, int level, bool max)
    {
        return new VipBuildingRequirement { building = building, minLevel = Mathf.Max(1, level), requireMaxLevel = max };
    }
}
