using UnityEngine;

// Oznaèuje hosa ako VIP. Generátor potrieb pod¾a tejto znaèky zapína
// špeciálne správanie VIP hostí.
public class VipGuestMarker : MonoBehaviour
{
    public VipPersonality Personality { get; private set; }

    public void Initialize(VipPersonality personality)
    {
        Personality = personality;
    }
}
