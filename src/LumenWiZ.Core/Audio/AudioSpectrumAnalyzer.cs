namespace LumenWiZ.Core.Audio;

public class AudioSpectrumAnalyzer
{
    private readonly int _sampleRate;

    public AudioSpectrumAnalyzer(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
    }

    /// <summary>
    /// Analyzes raw PCM float samples and calculates normalized energy in 3 frequency bands:
    /// Bass (20-150 Hz), Mids (150-2500 Hz), Treble (2500-12000 Hz).
    /// </summary>
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
        rms = (float)Math.Sqrt(rms / n);
        if (rms < 0.0001f)
            return (0f, 0f, 0f);

        // Target sample frequencies spanning the spectrum
        float[] bassFreqs = [30f, 50f, 60f, 80f, 100f, 120f, 150f];
        float[] midFreqs = [200f, 400f, 600f, 800f, 1000f, 1500f, 2000f];
        float[] trebleFreqs = [3000f, 4000f, 5000f, 6000f, 7000f, 8000f, 10000f];

        float bassEnergy = CalculateMaxOrMeanEnergy(samples, bassFreqs);
        float midEnergy = CalculateMaxOrMeanEnergy(samples, midFreqs);
        float trebleEnergy = CalculateMaxOrMeanEnergy(samples, trebleFreqs);

        return (
            Math.Clamp(bassEnergy * 2.0f, 0f, 1f),
            Math.Clamp(midEnergy * 2.5f, 0f, 1f),
            Math.Clamp(trebleEnergy * 3.0f, 0f, 1f)
        );
    }

    private float CalculateMaxOrMeanEnergy(float[] samples, float[] targetFreqs)
    {
        float maxVal = 0f;
        float sum = 0f;
        foreach (float freq in targetFreqs)
        {
            float mag = GoertzelMagnitude(samples, freq, _sampleRate);
            sum += mag;
            if (mag > maxVal) maxVal = mag;
        }

        // Combine peak presence with band density
        return (maxVal * 0.7f) + ((sum / targetFreqs.Length) * 0.3f);
    }

    private static float GoertzelMagnitude(float[] samples, float targetFreq, int sampleRate)
    {
        int n = samples.Length;
        float k = (int)(0.5f + ((n * targetFreq) / sampleRate));
        float omega = (float)(2.0 * Math.PI * k / n);
        float cosine = (float)Math.Cos(omega);
        float coeff = 2.0f * cosine;

        float q0 = 0f, q1 = 0f, q2 = 0f;

        for (int i = 0; i < n; i++)
        {
            q0 = coeff * q1 - q2 + samples[i];
            q2 = q1;
            q1 = q0;
        }

        float real = q1 - q2 * cosine;
        float imag = q2 * (float)Math.Sin(omega);
        float magnitude = (float)Math.Sqrt(real * real + imag * imag) / (n / 2f);

        return magnitude;
    }
}
