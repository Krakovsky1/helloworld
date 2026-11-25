using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GuestNeeds))]
public class GuestNeedsGenerator : MonoBehaviour
{
    [Header("Generation interval")]
    [SerializeField] private float minIntervalSeconds = 20f;
    [SerializeField] private float maxIntervalSeconds = 60f;

    private GuestNeeds guestNeeds;
    private Coroutine generationRoutine;
    private float nextNeedAt = -1f;

    // FR-39: o koľko sekúnd príde ďalšia potreba (-1 = generátor nebeží).
    public float SecondsToNextNeed =>
        generationRoutine != null && nextNeedAt >= 0f ? Mathf.Max(0f, nextNeedAt - Time.time) : -1f;

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
    }

    private IEnumerator GenerateNeedsOverTime(float firstDelay = -1f)
    {
        while (true)
        {
            float min = Mathf.Max(1f, minIntervalSeconds);
            float max = Mathf.Max(min, maxIntervalSeconds);
            float interval = firstDelay >= 0f ? firstDelay : Random.Range(min, max);
            firstDelay = -1f;

            nextNeedAt = Time.time + interval;
            yield return new WaitForSeconds(interval);
            GenerateRandomNeed();
        }
    }

    private void GenerateRandomNeed()
    {
        if (guestNeeds == null || availableNeeds.Length == 0)
            return;

        int randomIndex = Random.Range(0, availableNeeds.Length);
        GuestNeedType selectedNeed = availableNeeds[randomIndex];

        // Opakovanie rovnakého typu potreby je povolené.
        guestNeeds.AddNeed(selectedNeed);
    }

    private void OnDestroy()
    {
        StopGenerating();
    }
}
