using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Parpa.Services;

/// <summary>
/// Sonidos sintetizados de Data/sounds.json. Cada tono: oscilador (sine/triangle/square) con rampa
/// exponencial f0→f1; ganancia 0 → nivel en 5 ms lineal y luego exponencial a 0,0001 en <c>dur</c>.
/// Se pre-renderizan al arrancar. La salida de audio solo está abierta mientras suena algo.
/// Silencio con <c>sounds</c> desactivado, No molestar o pantalla completa.
/// </summary>
public static class SoundService
{
    private const int SampleRate = 48000;
    private const double Attack = 0.005;
    private const double Floor = 0.0001;
    private static readonly TimeSpan IdleClose = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RingEvery = TimeSpan.FromSeconds(1.2);

    private static readonly Dictionary<string, float[]> Sounds = new();
    private static readonly object Gate = new();
    private static ParpaSettings _s = null!;
    private static DispatcherQueueTimer? _closeTimer;
    private static DispatcherQueueTimer? _ringTimer;
    private static MixingSampleProvider? _mixer;
    private static WasapiOut? _output;

    /// <summary>Pantalla completa: el notch está apartado y no suena nada.</summary>
    public static bool Suppressed { get; set; }

    public static void Initialize(ParpaSettings settings)
    {
        _s = settings;
        var root = DataFiles.ReadData("sounds.json")["sounds"]!.AsObject();
        foreach (var (name, tones) in root)
            Sounds[name] = Render(tones!.AsArray().Select(t => (
                Wave: t!["wave"]!.GetValue<string>(),
                F0: t["f0"]!.GetValue<double>(),
                F1: t["f1"]!.GetValue<double>(),
                Start: t["start"]!.GetValue<double>(),
                Dur: t["dur"]!.GetValue<double>(),
                Level: t["level"]!.GetValue<double>())));

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _closeTimer = dispatcher.CreateTimer();
        _closeTimer.IsRepeating = false;
        _closeTimer.Interval = IdleClose;
        _closeTimer.Tick += (_, _) => CloseOutput();
        _ringTimer = dispatcher.CreateTimer();
        _ringTimer.Interval = RingEvery;
        _ringTimer.Tick += (_, _) => Play("ring");
    }

    /// <summary>click, tick, page, open, close, notify, ring, confirm, send, decline.</summary>
    public static void Play(string name)
    {
        if (_s is null || !_s.Sounds || _s.DndActive || Suppressed) return;
        if (!Sounds.TryGetValue(name, out var samples)) return;
        // Volumen maestro (vol, 60 % por defecto).
        var gain = (float)Math.Clamp(_s.Vol / 100.0, 0, 1);
        if (gain <= 0) return;
        try
        {
            lock (Gate)
            {
                EnsureOutput();
                _mixer!.AddMixerInput(new BufferProvider(samples, gain));
            }
            _closeTimer?.Stop();
            _closeTimer?.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Sonido: no se pudo reproducir", ex);
            CloseOutput();
        }
    }

    /// <summary>Llamada entrante: "ring" cada 1,2 s hasta que se responde o desaparece.</summary>
    public static void SetRinging(bool ringing)
    {
        if (_ringTimer is null) return;
        if (ringing == _ringTimer.IsRunning) return;
        if (ringing)
        {
            Play("ring");
            _ringTimer.Start();
        }
        else _ringTimer.Stop();
    }

    private static void EnsureOutput()
    {
        if (_output is not null) return;
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2)) { ReadFully = true };
        _output = new WasapiOut(AudioClientShareMode.Shared, 60);
        _output.Init(_mixer);
        _output.Play();
    }

    private static void CloseOutput()
    {
        lock (Gate)
        {
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _mixer = null;
        }
    }

    public static void Dispose()
    {
        _ringTimer?.Stop();
        CloseOutput();
    }

    /// <summary>Suma de tonos en un búfer mono a 48 kHz.</summary>
    private static float[] Render(IEnumerable<(string Wave, double F0, double F1, double Start, double Dur, double Level)> tones)
    {
        var list = tones.ToList();
        var length = (int)Math.Ceiling((list.Max(t => t.Start + t.Dur) + 0.02) * SampleRate);
        var buffer = new float[length];
        foreach (var tone in list)
        {
            var phase = 0.0;
            var first = (int)(tone.Start * SampleRate);
            var count = (int)(tone.Dur * SampleRate);
            for (var i = 0; i < count && first + i < length; i++)
            {
                var t = i / (double)SampleRate;
                var progress = t / tone.Dur;
                var frequency = tone.F0 * Math.Pow(tone.F1 / tone.F0, progress);
                phase += frequency / SampleRate;
                phase -= Math.Floor(phase);

                var wave = tone.Wave switch
                {
                    "square" => phase < 0.5 ? 1.0 : -1.0,
                    "triangle" => 1 - 4 * Math.Abs(phase - 0.5),
                    _ => Math.Sin(2 * Math.PI * phase),
                };
                var envelope = t < Attack
                    ? tone.Level * t / Attack
                    : tone.Level * Math.Pow(Floor / tone.Level, (t - Attack) / (tone.Dur - Attack));
                buffer[first + i] += (float)(wave * envelope);
            }
        }
        return buffer;
    }

    /// <summary>Reproduce un búfer mono en estéreo con ganancia.</summary>
    private sealed class BufferProvider(float[] samples, float gain) : ISampleProvider
    {
        private int _position;

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);

        public int Read(float[] buffer, int offset, int count)
        {
            var frames = Math.Min(count / 2, samples.Length - _position);
            for (var i = 0; i < frames; i++)
            {
                var value = samples[_position + i] * gain;
                buffer[offset + 2 * i] = value;
                buffer[offset + 2 * i + 1] = value;
            }
            _position += frames;
            return frames * 2;
        }
    }
}
