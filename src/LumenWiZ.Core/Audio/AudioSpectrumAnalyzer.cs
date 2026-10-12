namespace LumenWiZ.Core.Audio;

public class AudioSpectrumAnalyzer
{
    private readonly int _sampleRate;
    private static readonly float[] BassFreqs = [30f, 60f, 100f, 150f];
    private static readonly float[] MidFreqs = [300f, 600f, 1000f, 1800f];
    private static readonly float[] TrebleFreqs = [3500f, 5000f, 6000f, 8000f, 10000f];

    public AudioSpectrumAnalyzer(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
    }

    public (float Bass, float Mid, float Treble) AnalyzeBandEnergies(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return (0f, 0f, 0f);

        int n = samples.Length;
        float rms = 0f;
        for (int i = 0; i < n; i++)
        {
            rms += samples[i] * samples[i];
        }
        rms = MathF.Sqrt(rms / n);
        if (rms < 0.0001f)
            return (0f, 0f, 0f);

        float maxBass = 0f;
        foreach (float f in BassFreqs)
        {
            float m = Goertzel(samples, f);
            if (m > maxBass) maxBass = m;
        }

        float maxMid = 0f;
        foreach (float f in MidFreqs)
        {
            float m = Goertzel(samples, f);
            if (m > maxMid) maxMid = m;
        }

        float maxTreble = 0f;
        foreach (float f in TrebleFreqs)
        {
            float m = Goertzel(samples, f);
            if (m > maxTreble) maxTreble = m;
        }

        return (
            Math.Clamp(maxBass * 1.5f, 0f, 1f),
            Math.Clamp(maxMid * 1.8f, 0f, 1f),
            Math.Clamp(maxTreble * 2.2f, 0f, 1f)
        );
    }

    private float Goertzel(float[] samples, float targetFreq)
    {
        int n = samples.Length;
        float k = (int)(0.5f + ((n * targetFreq) / _sampleRate));
        float omega = (float)(2.0 * Math.PI * k / n);
        float cosine = MathF.Cos(omega);
        float coeff = 2.0f * cosine;

        float q0 = 0f, q1 = 0f, q2 = 0f;

        for (int i = 0; i < n; i++)
        {
            q0 = coeff * q1 - q2 + samples[i];
            q2 = q1;
            q1 = q0;
        }

        float real = q1 - q2 * cosine;
        float imag = q2 * MathF.Sin(omega);
        return MathF.Sqrt(real * real + imag * imag) / (n / 2f);
    }
}
