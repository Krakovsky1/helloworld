using UnityEngine;

// Lahka animacia postav bez Animatora: idle / chôdza / čakanie v rade / šťastný / nespokojný / mávanie.
// Nízka réžia (žiadne kosti, len 5 rotácií) - vhodné pre stovky hostí naraz.
public enum CharState { Idle, Walk, Waiting, Happy, Unhappy, Wave }

public class CharacterAnimator : MonoBehaviour
{
    public Transform body, head, armL, armR, legL, legR;   // naplní Setup skript
    public CharState state = CharState.Idle;
    public float speed = 1f;
    float phase;
    Quaternion hq, alq, arq, llq, lrq; Vector3 bodyPos;

    void Awake()
    {
        if (body == null) body = transform.childCount > 0 ? transform.GetChild(0) : transform;
        if (head) hq = head.localRotation; if (armL) alq = armL.localRotation; if (armR) arq = armR.localRotation;
        if (legL) llq = legL.localRotation; if (legR) lrq = legR.localRotation; bodyPos = body.localPosition;
        phase = Random.value * 10f;   // aby hostia nekmitali synchrónne
    }

    // 0..1 spokojnosť hosťa -> stav (podľa požiadavky na stav spokojnosti)
    public void SetSatisfaction(float s) { if (state == CharState.Walk || state == CharState.Wave) return; state = s < 0.35f ? CharState.Unhappy : (s > 0.75f ? CharState.Happy : CharState.Idle); }

    void LateUpdate()
    {
        float t = (Time.time + phase) * speed;
        float sw = 0, bob = 0, headX = 0, headZ = 0, armUp = 0, armOut = 0;
        switch (state)
        {
            case CharState.Idle:    bob = Mathf.Sin(t * 1.6f) * 0.006f; headZ = Mathf.Sin(t * 0.7f) * 2f; break;
            case CharState.Walk:    sw = Mathf.Sin(t * 7f) * 32f; bob = Mathf.Abs(Mathf.Sin(t * 7f)) * 0.03f; break;
            case CharState.Waiting: bob = Mathf.Sin(t * 1.2f) * 0.004f; sw = Mathf.Max(0, Mathf.Sin(t * 4f)) * 10f; headZ = Mathf.Sin(t * 0.5f) * 5f; headX = 4f; break;
            case CharState.Happy:   bob = Mathf.Abs(Mathf.Sin(t * 5f)) * 0.05f; armUp = 55f + Mathf.Sin(t * 10f) * 15f; break;
            case CharState.Unhappy: headX = 22f; bob = Mathf.Sin(t * 1.2f) * 0.003f; armOut = 6f; break;
            case CharState.Wave:    armUp = 150f + Mathf.Sin(t * 9f) * 18f; bob = Mathf.Sin(t * 1.6f) * 0.005f; break;
        }
        if (body) body.localPosition = bodyPos + Vector3.up * bob;
        if (legL) legL.localRotation = llq * Quaternion.Euler(sw, 0, 0);
        if (legR) legR.localRotation = lrq * Quaternion.Euler(-sw, 0, 0);
        if (armL) armL.localRotation = alq * Quaternion.Euler(state == CharState.Wave ? 0 : -sw * 0.9f - armUp, 0, armOut);
        if (armR) armR.localRotation = arq * Quaternion.Euler(state == CharState.Wave ? -armUp : sw * 0.9f - armUp, 0, -armOut);
        if (head) head.localRotation = hq * Quaternion.Euler(headX, 0, headZ);
    }
}
