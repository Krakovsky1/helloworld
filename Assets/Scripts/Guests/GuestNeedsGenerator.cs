using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GuestNeeds))]
public class GuestNeedsGenerator : MonoBehaviour
{
    [Header("Generation interval - normal guests")]
    [SerializeField] private float minIntervalSeconds = 20f;
    [SerializeField] private float maxIntervalSeconds = 60f;

    [Header("Generation interval - VIP guests")]
    [SerializeField] private float vipMinIntervalSeconds = 10f;
    [SerializeField] private float vipMaxIntervalSeconds = 25f;

    [Header("VIP special needs")]
    [Range(0f, 1f)]
    [SerializeField] private float vipDoubleNeedChance = 0.6f;

    private GuestNeeds guestNeeds;
    private Coroutine generationRoutine;
    private float nextNeedAt = -1f;

    // FR-39: o koľko sekúnd príde ďalšia potreba (-1 = generátor nebeží).
    public float SecondsToNextNeed =>
        generationRoutine != null && nextNeedAt >= 0f
            ? Mathf.Max(0f, nextNeedAt - Time.time)
            : -1f;

    private readonly GuestNeedType[] availableNeeds =
    {
        GuestNeedType.Room,
        GuestNeedType.Drink,
        GuestNeedType.Food,
        GuestNeedType.Relax
    };

    private void Awake()
    {
        guestNeeds = GetComponent<GuestNeeds>();
    }

    // Volá sa až v momente, keď je hosť ubytovaný.
    // Generátor sa nespúšťa automaticky pri vytvorení hosťa na móle.
    public void StartGenerating()
    {
        if (generationRoutine != null)
            return;

        GenerateRandomNeed();
        generationRoutine = StartCoroutine(GenerateNeedsOverTime());
    }

    // FR-39: pokračovanie po načítaní uloženej hry - bez okamžitej novej potreby,
    // prvá ďalšia príde o firstDelay sekúnd (ako pred uložením).
    public void ResumeGenerating(float firstDelay)
    {
        if (generationRoutine != null)
            return;

        if (guestNeeds == null)
            guestNeeds = GetComponent<GuestNeeds>();

        generationRoutine = StartCoroutine(GenerateNeedsOverTime(firstDelay));
    }

    public void StopGenerating()
    {
        if (generationRoutine == null)
            return;

        StopCoroutine(generationRoutine);
        generationRoutine = null;
        nextNeedAt = -1f;
    }

    private IEnumerator GenerateNeedsOverTime(float firstDelay = -1f)
    {
        while (true)
        {
            bool isVip = IsVipGuest();

            float min = isVip ? vipMinIntervalSeconds : minIntervalSeconds;
            float max = isVip ? vipMaxIntervalSeconds : maxIntervalSeconds;

            min = Mathf.Max(1f, min);
            max = Mathf.Max(min, max);

            float interval = firstDelay >= 0f
                ? firstDelay
                : Random.Range(min, max);

            firstDelay = -1f;
            nextNeedAt = Time.time + interval;

            yield return new WaitForSeconds(interval);
            GenerateRandomNeed();
        }
    }

    private bool IsVipGuest()
    {
        // Marker môže byť na tom istom objekte alebo na nadradenom objekte hosťa.
        return GetComponent<VipGuestMarker>() != null ||
               GetComponentInParent<VipGuestMarker>() != null;
    }

    private void GenerateRandomNeed()
    {
        if (guestNeeds == null || availableNeeds.Length == 0)
            return;

        // VIP má častejšie viacero potrieb naraz.
        // Každá potreba sa pridá samostatne, takže GuestServiceManager
        // vytvorí samostatnú požiadavku pre každú službu.
        if (IsVipGuest() && Random.value < vipDoubleNeedChance)
        {
            guestNeeds.AddNeed(GuestNeedType.Food);
            guestNeeds.AddNeed(GuestNeedType.Drink);

            Debug.Log($"VIP {gameObject.name} dostal súčasne potreby Food + Drink.");
            return;
        }

        int randomIndex = Random.Range(0, availableNeeds.Length);
        GuestNeedType selectedNeed = availableNeeds[randomIndex];
        guestNeeds.AddNeed(selectedNeed);
    }

    private void OnDestroy()
    {
        StopGenerating();
    }
}
