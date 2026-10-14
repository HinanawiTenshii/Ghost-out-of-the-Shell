"""Original one-second fear roar: voiced growl, breath and restrained CRT grit.

Deterministic Python stdlib synthesis only; no third-party audio samples.
Run --check to validate the installed WAV without replacing it.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import sys
import wave

from GenerateSandwormAudio import RATE, TAU, finish, metrics, noise

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Resources/Audio/FearRoar.wav"
SECONDS = 1.0


def synthesize():
    n = round(SECONDS * RATE)
    breath = noise(n, 13001, 11025, 3100)
    throat = noise(n, 13003, 1350, 270)
    jitter = noise(n, 13007, 83, 33)
    phase = 0.0
    signal = []
    weights = []
    for i in range(n):
        t = i / RATE
        # A rough vocal source with slowly closing formants, not a clean laser
        # sweep or explosion. Fixed seeds preserve reproducibility and provenance.
        fundamental = 94 + 19 * math.exp(-t * 14) - 19 * t + 4 * jitter[i]
        phase += TAU * fundamental / RATE
        if i % 64 == 0:
            formants = [(365 - 100 * t, 95, .9), (810 - 190 * t, 160, .6),
                        (1440 - 240 * t, 220, .25)]
            weights = [(h, (.1 + sum(a * math.exp(-.5 * ((h * fundamental - f) / bw) ** 2)
                        for f, bw, a in formants)) / math.sqrt(h)) for h in range(1, 23)]
        voiced = sum(math.sin(phase * h + .11 * h) * a for h, a in weights)
        flutter = .80 + .15 * math.sin(TAU * 18.7 * t) + .05 * math.sin(TAU * 31.3 * t)
        growl = voiced * flutter * .32 + math.sin(phase * .5) * .18
        air = breath[i] * (.24 + .1 * abs(math.sin(phase * .5))) + throat[i] * .27
        crack = breath[i] * .38 * math.exp(-t * 60)
        raw = math.tanh((growl + air + crack) * 1.7)
        # Mild amplitude steps, not a harsh 8-bit square wave.
        raw = raw * .8 + round(raw * 127) / 127 * .2
        attack = min(1, i / (RATE * .0015))
        release = min(1, (n - 1 - i) / (RATE * .16)) ** .8
        body = .85 + .15 * math.sin(math.pi * t)
        signal.append(raw * attack * release * body)
    pcm = finish(signal, .18, .8)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != 'little':
        pcm.byteswap()
    return pcm.tobytes()


def validate(raw):
    with wave.open(str(OUT), 'rb') as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()) == (1, 2, RATE, RATE)
        assert wav.readframes(wav.getnframes()) == raw, 'Installed roar differs from deterministic synthesis'
    stats = metrics(OUT)
    assert stats['durationSeconds'] == SECONDS
    assert stats['onsetMs'] < 3 and stats['peak'] <= .801 and .12 < stats['rms'] <= .181
    samples = array.array('h', raw)
    if sys.byteorder != 'little': samples.byteswap()
    assert samples[0] == samples[-1] == 0
    assert abs(sum(samples) / len(samples) / 32768) < .003
    tail = samples[round(.9 * RATE):round(.97 * RATE)]
    assert math.sqrt(sum(v * v for v in tail) / len(tail)) / 32768 > .015
    return dict(path=OUT.relative_to(ROOT).as_posix(), sourceId='S13',
                sha256=hashlib.sha256(OUT.read_bytes()).hexdigest().upper(),
                bytes=OUT.stat().st_size, durationSeconds=SECONDS, metrics=stats)


if __name__ == '__main__':
    raw = synthesize()
    if '--check' not in sys.argv:
        with wave.open(str(OUT), 'wb') as wav:
            wav.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
            wav.writeframes(raw)
    print(json.dumps(validate(raw), indent=2))
