"""S16: original deterministic holy-light cues, no recordings or third-party samples.

Each cue includes the 0.16-second descent, followed by the impact and glow tail.
Python standard library only. --check validates installed assets without writing.
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
DESCENT = .16
SPECS = (
    ("HolyLightCentral", 2.0, .15, .76, "68f9d2e31bf74a30acdb2ea29e8264e1"),
    ("HolyLightWave", .65, .13, .68, "2b614173557a44ed9564eefed9b2be22"),
)


def synthesize(hold, target_rms, peak):
    small = hold < 1
    n = round((DESCENT + hold) * RATE)
    air = noise(n, 16003 if small else 16001, 14700, 3800)
    earth = noise(n, 16009 if small else 16007, 2800, 430)
    result = []
    low_phase = 0.0
    for i in range(n):
        t = i / RATE
        age = t - DESCENT
        if age < 0:
            # Air rushing down, not a pitched laser sweep. Slight glint anticipates impact.
            progress = t / DESCENT
            ring = math.sin(TAU * 784 * t) * .014 * progress ** 2
            raw = air[i] * (.025 + .21 * progress ** 2) + ring
        else:
            attack = min(1, age / .001)
            low_phase += TAU * ((115 if small else 73) + 55 * math.exp(-age * 28)) / RATE
            body = math.sin(low_phase) * math.exp(-age * (22 if small else 13))
            crack = air[i] * math.exp(-age * 43) * .8
            dust = earth[i] * math.exp(-age * (14 if small else 8)) * .32
            # Warm, harmonically related resonances suggest sacred light, not gunfire.
            pitch = 1.25 if small else 1.0
            chime = sum(a * math.sin(TAU * f * pitch * age) * math.exp(-age * decay)
                        for f, a, decay in ((261.63, .18, 2.4), (392, .13, 2.7),
                                            (523.25, .1, 3.4), (784, .045, 4.8), (1046.5, .028, 6.5)))
            shimmer = .08 * air[i] * (.6 + .4 * math.sin(TAU * 6 * age) ** 2) * math.exp(-age * 1.7)
            raw = attack * (body * (.54 if small else .72) + crack + dust + chime + shimmer)
        # Retain a small amount of the project's stepped CRT texture, without harsh fizz.
        raw = math.tanh(raw * 1.45)
        raw = raw * .85 + round(raw * 255) / 255 * .15
        fade_in = min(1, i / (RATE * .001))
        fade_out = min(1, (n - 1 - i) / (RATE * (.09 if small else .22)))
        result.append(raw * fade_in * fade_out)
    pcm = finish(result, target_rms, peak)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def main():
    assets = []
    for name, hold, rms, peak, guid in SPECS:
        path = OUT / (name + ".wav")
        pcm = synthesize(hold, rms, peak)
        if "--check" not in sys.argv:
            with wave.open(str(path), "wb") as wav:
                wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
                wav.writeframes(pcm)
        with wave.open(str(path), "rb") as wav:
            assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
            assert wav.readframes(wav.getnframes()) == pcm, "Synthesis differs from installed cue"
        stats = metrics(path)
        samples = array.array("h", pcm)
        if sys.byteorder != "little":
            samples.byteswap()
        assert samples[0] == samples[-1] == 0
        assert stats["durationSeconds"] == round(DESCENT + hold, 6)
        assert stats["peak"] <= peak + .001 and stats["onsetMs"] < 5
        assert abs(sum(samples) / len(samples) / 32768) < .002
        impact = samples[round(DESCENT * RATE):round((DESCENT + .08) * RATE)]
        lead = samples[:round(.08 * RATE)]
        assert sum(v*v for v in impact) > 4 * sum(v*v for v in lead), "Distinct impact at landing"
        assets.append(dict(path=path.relative_to(ROOT).as_posix(), sourceId="S16", guid=guid,
                           sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                           bytes=path.stat().st_size, durationSeconds=stats["durationSeconds"],
                           generator="Tools/GenerateHolyLightAudio.py", metrics=stats))
    print(json.dumps(assets, indent=2))


if __name__ == "__main__":
    main()
