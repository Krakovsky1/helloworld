using UnityEngine;

public enum PlotType { Hotel, Pool, Restaurant, Bar, GolfCourse, Free }

/// <summary>
/// Plocha na ostrove, kam hrac postavi budovu. Vytvara ju Level1IslandBuilder.
/// Pozicia objektu = stred plochy na urovni terenu. Rozmery su v metroch.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public class BuildPlot : MonoBehaviour
{
    public PlotType type;
    public string displayName;
    [Tooltip("Rozmer plochy v metroch: x = sirka, y = dlzka (os Z)")]
    public Vector2 size = new Vector2(8, 8);
    public bool occupied;

    /// <summary>Svetova pozicia, kam umiestnit stred budovy.</summary>
    public Vector3 SnapPoint { get { return transform.position; } }

    static Color ColorFor(PlotType t)
    {
        switch (t)
        {
            case PlotType.Hotel: return new Color(0.92f, 0.25f, 0.20f);
            case PlotType.Pool: return new Color(0.16f, 0.47f, 1f);
            case PlotType.Restaurant: return new Color(1f, 0.6f, 0f);
            case PlotType.Bar: return new Color(0.67f, 0.28f, 0.74f);
            case PlotType.GolfCourse: return new Color(0.18f, 0.63f, 0.26f);
            default: return new Color(1f, 0.92f, 0.23f);
        }
    }

    void Reset() { EnsureCollider(); }
    void OnValidate() { EnsureCollider(); }

    public void EnsureCollider()
    {
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        bc.isTrigger = true;
        bc.center = new Vector3(0, 1f, 0);
        bc.size = new Vector3(size.x, 2f, size.y);
    }

    void OnDrawGizmos()
    {
        var c = ColorFor(type);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(c.r, c.g, c.b, occupied ? 0.05f : 0.3f);
        Gizmos.DrawCube(new Vector3(0, 0.05f, 0), new Vector3(size.x, 0.1f, size.y));
        Gizmos.color = c;
        Gizmos.DrawWireCube(new Vector3(0, 0.05f, 0), new Vector3(size.x, 0.1f, size.y));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, displayName + "  " + size.x + "x" + size.y + " m");
#endif
    }
}
