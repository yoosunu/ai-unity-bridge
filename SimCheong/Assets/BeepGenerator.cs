using UnityEngine;

public static class BeepGenerator
{
    private const int SampleRate = 44100;

    /// <summary>
    /// 끊김 없는 순수 톤 (연속음 구간용). duration은 frequency의 정수배 주기가 되게 맞춰야 이음매가 매끄럽다.
    /// </summary>
    public static AudioClip GenerateContinuousTone(float frequency)
    {
        float duration = 1f; // frequency*1초 = 정수 사이클이 되도록, 아래에서 보정
        int cycleCount = Mathf.RoundToInt(frequency * duration);
        int sampleCount = Mathf.RoundToInt(SampleRate * cycleCount / frequency);

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / SampleRate);
        }

        AudioClip clip = AudioClip.Create("ContinuousTone", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// "삐 ... 삐 ... 삐" 패턴이 통째로 구워진 루프 클립.
    /// period: 한 삐 소리 시작부터 다음 삐 소리 시작까지 걸리는 시간
    /// beepCountInLoop: 이 클립 하나에 삐 소리를 몇 번 넣을지 (루프 길이 = period * beepCountInLoop)
    /// </summary>
    public static AudioClip GeneratePulseLoop(float frequency, float period, float beepDuration, int beepCountInLoop)
    {
        float loopDuration = period * beepCountInLoop;
        int totalSamples = Mathf.RoundToInt(SampleRate * loopDuration);
        float[] samples = new float[totalSamples];

        int beepSamples = Mathf.RoundToInt(SampleRate * beepDuration);
        int fadeSamples = Mathf.Min(beepSamples / 4, Mathf.RoundToInt(SampleRate * 0.008f)); // 약 8ms 페이드, 클릭 방지

        for (int b = 0; b < beepCountInLoop; b++)
        {
            int startSample = Mathf.RoundToInt(SampleRate * period * b);

            for (int i = 0; i < beepSamples && startSample + i < totalSamples; i++)
            {
                float value = Mathf.Sin(2 * Mathf.PI * frequency * i / SampleRate);

                if (i < fadeSamples)
                    value *= (float)i / fadeSamples;
                else if (i > beepSamples - fadeSamples)
                    value *= (float)(beepSamples - i) / fadeSamples;

                samples[startSample + i] = value;
            }
            // 나머지(삐 소리 사이 구간)는 배열 기본값 0f로 이미 무음
        }

        AudioClip clip = AudioClip.Create("PulseLoop", totalSamples, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}