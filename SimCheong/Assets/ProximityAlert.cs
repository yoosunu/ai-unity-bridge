using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class ProximityAlert : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private Transform player;

    [Header("거리 구간 (m)")]
    [SerializeField] private float farThreshold = 5f;
    [SerializeField] private float midThreshold = 3f;
    [SerializeField] private float continuousThreshold = 1.5f;

    [Header("소리")]
    [SerializeField] private float frequency = 880f;
    [SerializeField] private float maxVolume = 1f;
    [SerializeField] private float transitionTime = 0.15f; // 구간 전환 시 크로스페이드 시간

    // 클립은 인스턴스마다 새로 만들 필요 없음 - 정적 캐시로 공유 (성능, 메모리 절약)
    private static AudioClip continuousClip;
    private static AudioClip fastClip;
    private static AudioClip slowClip;

    private AudioSource source;
    private enum Zone { Silent, Slow, Fast, Continuous }
    private Zone currentZone = Zone.Silent;
    private Coroutine transitionRoutine;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;
        source.volume = 0f;
        source.loop = true;

        EnsureClipsGenerated();
    }

    private void EnsureClipsGenerated()
    {
        if (continuousClip == null)
        {
            continuousClip = BeepGenerator.GenerateContinuousTone(frequency);
            fastClip = BeepGenerator.GeneratePulseLoop(frequency, period: 0.25f, beepDuration: 0.1f, beepCountInLoop: 4);
            slowClip = BeepGenerator.GeneratePulseLoop(frequency, period: 1.0f, beepDuration: 0.15f, beepCountInLoop: 2);
        }
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        Zone newZone = GetZone(distance);

        if (newZone != currentZone)
        {
            currentZone = newZone;
            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(TransitionToZone(newZone));
        }
    }

    private Zone GetZone(float distance)
    {
        const float hysteresis = 0.3f; // 여유폭 (m)

        switch (currentZone)
        {
            case Zone.Silent:
                if (distance <= farThreshold) return Zone.Slow;
                return Zone.Silent;

            case Zone.Slow:
                if (distance > farThreshold + hysteresis) return Zone.Silent;
                if (distance <= midThreshold) return Zone.Fast;
                return Zone.Slow;

            case Zone.Fast:
                if (distance > midThreshold + hysteresis) return Zone.Slow;
                if (distance <= continuousThreshold) return Zone.Continuous;
                return Zone.Fast;

            case Zone.Continuous:
                if (distance > continuousThreshold + hysteresis) return Zone.Fast;
                return Zone.Continuous;

            default:
                return Zone.Silent;
        }
    }

    private IEnumerator TransitionToZone(Zone zone)
    {
        // 1. 현재 소리를 짧게 페이드아웃
        float startVolume = source.volume;
        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, t / transitionTime);
            yield return null;
        }
        source.volume = 0f;
        source.Stop();

        if (zone == Zone.Silent)
        {
            yield break; // 무음 구간이면 여기서 끝, 아무것도 재생 안 함
        }

        // 2. 새 클립으로 교체하고 처음부터 재생 시작
        source.clip = zone switch
        {
            Zone.Slow => slowClip,
            Zone.Fast => fastClip,
            Zone.Continuous => continuousClip,
            _ => null
        };
        source.Play();

        // 3. 페이드인
        t = 0f;
        while (t < transitionTime)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, maxVolume, t / transitionTime);
            yield return null;
        }
        source.volume = maxVolume;
    }

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform;
    }
}