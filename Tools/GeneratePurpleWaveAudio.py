"""S17: two original purple energy-wave cues; no sampled recordings.

Python standard library only. --check validates installed assets without writing.
Release is a continuous hand-fed surge, followed by a separate terminal breakup.
"""
import array
import hashlib
import json
import math
import sys
import wave
from pathlib import Path

from GenerateSandwormAudio import RATE, TAU, finish, metrics, noise

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Resources/Audio"
SPECS = (
    ("PurpleWaveRelease", .51, .15, .70, "61740816e8bb42deb6f5847bc2f13f6f"),
    ("PurpleWaveBurst", .48, .16, .76, "133536a58b544f489c916a9637e5203f"),
)


def synthesize(name, seconds, rms, peak):
    n = round(seconds * RATE)
    burst = name.endswith("Burst")
    air = noise(n, 17011 if burst else 17001, 13200, 3100 if burst else 1750)
    grit = noise(n, 17013 if burst else 17003, 2200, 600)
    result, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        u = t / seconds
        if burst:
            phase += TAU * (47 + 92 * math.exp(-t * 35)) / RATE
            body = .60 * math.sin(phase) * math.exp(-t * 17)
            crack = air[i] * (1.4 * math.exp(-t * 60) + .3 * math.exp(-t * 11))
            # Brittle, non-harmonic magical residue, not metallic gunfire.
            splinters = sum(a * math.sin(TAU * f * t) * math.exp(-t * d)
                            for f, a, d in ((412, .10, 13), (673, .065, 17), (1097, .04, 22)))
            flecks = 0
            for at in (.035, .082, .137):
                if t > at:
                    flecks += air[i] * .14 * math.exp(-(t - at) * 100)
            raw = body + crack + splinters + flecks + grit[i] * .2 * math.exp(-t * 10)
        else:
            # Sustained rough exhalation with hollow, slightly discordant magic.
            # Avoid an isolated pitched 'pew': energy keeps pouring from the hand.
            phase += TAU * (124 + 14 * math.sin(TAU * 2.3 * t)) / RATE
            pulse = .8 + .2 * math.sin(TAU * 26 * t)
            hollow = .15 * math.sin(phase + 1.2 * math.sin(TAU * 43 * t))
            hollow += .075 * math.sin(phase * 2.13) + .04 * math.sin(phase * 3.47)
            rush = air[i] * (.4 + .3 * u) + grit[i] * .27
            onset = .26 * air[i] * math.exp(-t * 75)
            raw = (hollow + rush) * pulse * (.9 + .1 * math.sin(math.pi * u)) + onset
        raw = math.tanh(raw * 1.55)
        raw = raw * .84 + round(raw * 127) / 127 * .16
        fade_in = min(1, i / (RATE * .001))
        fade_out = min(1, (n - 1 - i) / (RATE * (.065 if burst else .025)))
        result.append(raw * fade_in * fade_out)
    pcm = finish(result, rms, peak)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def main():
    assets = []
    for name, seconds, rms, peak, guid in SPECS:
        path = OUT / (name + ".wav")
        pcm = synthesize(name, seconds, rms, peak)
        if "--check" not in sys.argv:
            with wave.open(str(path), "wb") as wav:
                wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
                wav.writeframes(pcm)
        with wave.open(str(path), "rb") as wav:
            assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
            assert wav.readframes(wav.getnframes()) == pcm, "Installed cue differs from synthesis"
        stats = metrics(path)
        samples = array.array("h", pcm)
        if sys.byteorder != "little":
            samples.byteswap()
        assert samples[0] == samples[-1] == 0
        assert stats["durationSeconds"] == seconds
        assert stats["peak"] <= peak + .001 and stats["onsetMs"] < 3
        assert abs(sum(samples) / len(samples) / 32768) < .002
        if name.endswith("Release"):
            tail = samples[round(.3 * RATE):round(.45 * RATE)]
            assert math.sqrt(sum((v / 32768) ** 2 for v in tail) / len(tail)) > .1, "Continuous surge, not a short shot"
        assets.append(dict(path=path.relative_to(ROOT).as_posix(), sourceId="S17", status="original-procedural",
                           guid=guid, sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                           bytes=path.stat().st_size, durationSeconds=stats["durationSeconds"],
                           generator="Tools/GeneratePurpleWaveAudio.py", metrics=stats))
    print(json.dumps(assets, indent=2))


if __name__ == "__main__":
    main()
