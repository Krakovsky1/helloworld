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

    public void StopGenerating()
    {
        if (generationRoutine == null)
            return;

        StopCoroutine(generationRoutine);
        generationRoutine = null;
    }

    private IEnumerator GenerateNeedsOverTime()
    {
        while (true)
        {
            float min = Mathf.Max(1f, minIntervalSeconds);
            float max = Mathf.Max(min, maxIntervalSeconds);
            float interval = Random.Range(min, max);

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
