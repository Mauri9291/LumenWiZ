using Xunit;
using LumenWiZ.Core.Audio;

namespace LumenWiZ.Tests;

public class AudioAnalyzerTests
{
    [Fact]
    public void ProcessSamples_LowFrequencySignal_YieldsDominantBass()
    {
        var analyzer = new AudioSpectrumAnalyzer(sampleRate: 44100);
        int sampleCount = 1024;
        float[] samples = new float[sampleCount];

        // 60 Hz sine wave (Bass)
        float freq = 60f;
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * freq * i / 44100);
        }

        var (bass, mid, treble) = analyzer.AnalyzeBandEnergies(samples);

        Assert.True(bass > mid, $"Expected bass ({bass}) > mid ({mid})");
        Assert.True(bass > treble, $"Expected bass ({bass}) > treble ({treble})");
        Assert.True(bass > 0.1f, "Expected significant bass energy");
    }

    [Fact]
    public void ProcessSamples_HighFrequencySignal_YieldsDominantTreble()
    {
        var analyzer = new AudioSpectrumAnalyzer(sampleRate: 44100);
        int sampleCount = 1024;
        float[] samples = new float[sampleCount];

        // 6000 Hz sine wave (Treble)
        float freq = 6000f;
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(2 * Math.PI * freq * i / 44100);
        }

        var (bass, mid, treble) = analyzer.AnalyzeBandEnergies(samples);

        Assert.True(treble > bass, $"Expected treble ({treble}) > bass ({bass})");
        Assert.True(treble > 0.05f, "Expected significant treble energy");
    }

    [Fact]
    public void ProcessSamples_Silence_YieldsZeroEnergies()
    {
        var analyzer = new AudioSpectrumAnalyzer(sampleRate: 44100);
        float[] silentSamples = new float[1024];

        var (bass, mid, treble) = analyzer.AnalyzeBandEnergies(silentSamples);

        Assert.Equal(0f, bass);
        Assert.Equal(0f, mid);
        Assert.Equal(0f, treble);
    }
}
