using UnityEngine;

namespace HotelParadiseStyle
{
    /// <summary>Marks an empty build plot on the island. Add your own game logic here.</summary>
    public class BuildSlot : MonoBehaviour
    {
        public string slotId;
        public float sizeUnits = 5.4f;
        public bool occupied;
        public string buildingId;
    }
}
