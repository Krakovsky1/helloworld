// Unity: Tools > Tropical Resort > Setup Guest Arrival
// Vytvorí (alebo nanovo vytvorí) objekt GuestArrival s manažérom vlny, trasou lode a cestou hostí.
// Súradnice sú pre scénu StarterIsland: loď pripláva zo západu popri južnom pobreží a zakotví
// vo vnútornom rohu móla Pier_L (bokom k L-ramenu), hostia idú po móle a popri kamienkovom chodníku k hotelu.
// Ak mólo alebo hotel presunieš, posuň aj body trás (deti objektov BoatPath, Dock, BoatLeavePath, GuestPath).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GuestArrivalSetup
{
    const string Buildings = "Assets/Buildings/TropicalResort_Buildings/";
    const string Characters = "Assets/TropicalResort_Characters/";

    static readonly Vector3[] BoatIn =
    {
        new Vector3(-19.8f, 0f, -5.39f),
        new Vector3(-8.35f, 0f, -1.81f),
        new Vector3(-5.95f, 0f, -1.06f),
        new Vector3(-3.95f, 0.1f, -0.92f),
        new Vector3(-1.95f, 0.15f, -1.88f),
        new Vector3(0f, 0.16f, -3.35f),
        new Vector3(1.6f, 0.13f, -4.6f),
        new Vector3(3.2f, 0.1f, -6.3f),
    };

    // Kotvisko: tu sa zastaví predok lode; loď mieri na východ (+X) a mólo má po pravej ruke.
    static readonly Vector3 Dock = new Vector3(9.35f, 0.2f, -7.4f);
    static readonly Quaternion DockRotation = Quaternion.Euler(0f, 90f, 0f);

    static readonly Vector3[] BoatLeave =
    {
        new Vector3(2.5f, 0f, -6.64f),
        new Vector3(4.5f, 0f, -9.8f),
        new Vector3(11.5f, 0f, -10.6f),
        new Vector3(24f, 0f, -9f),
    };

    static readonly Vector3[] GuestWalk =
    {
        new Vector3(7.75f, 1.02f, -7.6f),
        new Vector3(8.1f, 1.02f, -7.96f),
        new Vector3(9f, 1.02f, -8.1f),
        new Vector3(10.06f, 1.02f, -8.3f),
        new Vector3(10.08f, 1.02f, -6.5f),
        new Vector3(10.08f, 1.02f, -5.2f),
        new Vector3(10.08f, 1.02f, -4.75f),
        new Vector3(10.1f, 0.63f, -4.3f),
        new Vector3(10.14f, 0.8f, -3.71f),
        new Vector3(10.22f, 0.82f, -2.53f),
        new Vector3(9.98f, 0.83f, -1.81f),
        new Vector3(10.29f, 0.82f, 1.21f),
        new Vector3(10.08f, 0.83f, 4.56f),
        new Vector3(10.28f, 0.82f, 6.2f),
        new Vector3(10.05f, 0.82f, 7.2f),
    };

    static readonly string[] GuestNames =
    {
        "Father", "Mother", "Boy", "Girl", "YoungMan", "YoungWoman", "SeniorMan", "SeniorWoman", "BusinessMan", "BusinessWoman", "WealthyMan", "WealthyWoman"
    };

    [MenuItem("Tools/Tropical Resort/Setup Guest Arrival")]
    public static void Setup()
    {
        var old = GameObject.Find("GuestArrival");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("GuestArrival");
        Undo.RegisterCreatedObjectUndo(root, "Setup Guest Arrival");
        var manager = root.AddComponent<GuestWaveManager>();

        var boatPath = Group(root.transform, "BoatPath", "Boat", BoatIn);
        var dock = new GameObject("Dock").transform;
        dock.SetParent(root.transform, false);
        dock.position = Dock;
        dock.rotation = DockRotation;
        var boatLeavePath = Group(root.transform, "BoatLeavePath", "Leave", BoatLeave);
        var guestPath = Group(root.transform, "GuestPath", "Walk", GuestWalk);

        var so = new SerializedObject(manager);

        var boats = so.FindProperty("boats");
        boats.arraySize = 3;
        Boat(boats.GetArrayElementAtIndex(0), "Boat L1", Buildings + "Transport/Boat_L1.obj", 3, 0.15f);
        Boat(boats.GetArrayElementAtIndex(1), "Boat L2", Buildings + "Transport/Boat_L2.obj", 5, 0.23f);
        Boat(boats.GetArrayElementAtIndex(2), "Boat L3", Buildings + "Transport/Boat_L3.obj", 8, 0.45f);

        var map = so.FindProperty("boatForHotelLevel");
        int[] levels = { 0, 1, 1, 2 };
        map.arraySize = levels.Length;
        for (int i = 0; i < levels.Length; i++) map.GetArrayElementAtIndex(i).intValue = levels[i];
        so.FindProperty("pierOnRightSide").boolValue = true;

        var guests = so.FindProperty("guestModels");
        guests.arraySize = GuestNames.Length;
        for (int i = 0; i < GuestNames.Length; i++)
        {
            var g = guests.GetArrayElementAtIndex(i);
            g.FindPropertyRelative("model").objectReferenceValue = Load<GameObject>(Characters + "Guests/Guest_" + GuestNames[i] + ".obj");
            g.FindPropertyRelative("rig").objectReferenceValue = Load<TextAsset>(Characters + "Rigs/Guest_" + GuestNames[i] + ".rig.txt");
        }

        so.FindProperty("boatPath").objectReferenceValue = boatPath;
        so.FindProperty("dock").objectReferenceValue = dock;
        so.FindProperty("boatLeavePath").objectReferenceValue = boatLeavePath;
        so.FindProperty("guestPath").objectReferenceValue = guestPath;
        so.FindProperty("hotelEntryLocal").vector3Value = new Vector3(0f, 0.1f, 2.15f);
        so.FindProperty("hotelFrontLocal").vector3Value = new Vector3(0f, 0.1f, 2f);
        so.FindProperty("hotelDoorLocal").vector3Value = new Vector3(0f, 0.1f, 1.7f);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Guest arrival: GuestArrival je pripravený (3 lode, " + GuestNames.Length + " typov hostí). Ulož scénu.");
    }

    static Transform Group(Transform parent, string name, string pointName, Vector3[] points)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        for (int i = 0; i < points.Length; i++)
        {
            var p = new GameObject(pointName + "_" + i.ToString("00")).transform;
            p.SetParent(group, false);
            p.position = points[i];
        }
        return group;
    }

    static void Boat(SerializedProperty boat, string label, string modelPath, int guests, float deckHeight)
    {
        boat.FindPropertyRelative("label").stringValue = label;
        boat.FindPropertyRelative("model").objectReferenceValue = Load<GameObject>(modelPath);
        boat.FindPropertyRelative("guestCount").intValue = guests;
        boat.FindPropertyRelative("deckHeight").floatValue = deckHeight;
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError("Chýba asset: " + path);
        return asset;
    }
}
