"""S20: deterministic original arcane ritual synthesis, no recordings or samples.

Three synchronized cues: crystal convergence, rising eruption, dispersal.
Python standard library only. --check validates installed WAVs without writing.
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
    ("SummoningGather", 3.0, .115, .60, "4189ff3e78f741e59978eb95ad951d53"),
    ("SummoningEruption", 3.0, .15, .72, "77fc832741de40ebbb0f980394fef563"),
    ("SummoningBurst", 1.4, .16, .78, "3a720da86aa0448387919260ef6c4f86"),
)


def synthesize(name, duration, rms, peak):
    n = round(duration * RATE)
    air = noise(n, 19001, 14700, 3400)
    earth = noise(n, 19007, 1700, 240)
    grain = noise(n, 19013, 7000, 1900)
    result = []
    phase = 0
    for i in range(n):
        t = i / RATE
        p = t / duration
        if name == "SummoningGather":
            # Six repeated crystal modes gather faster into a restrained rising drone.
            swirl = math.sin(TAU * (1.8*t + .7*t*t)) ** 2
            phase += TAU * (76 + 32 * p*p) / RATE
            bed = math.sin(phase) * (.08 + .18*p) + earth[i] * (.1 + .18*p)
            shimmer = 0
            for j, f in enumerate((349, 415, 523, 698, 830, 1046)):
                age = (t - j*.1) % .61
                shimmer += math.sin(TAU*f*t + .3*math.sin(TAU*3*t)) * math.exp(-age*19) * .045
            value = bed + air[i]*(.025 + p*.13)*swirl + shimmer*(.35 + .65*p)
            envelope = min(1, t/.06) * min(1, (duration-t)/.035) * (.3 + .7*p)
        elif name == "SummoningEruption":
            # Continuous rushing energy, with irregular low flutter rather than a gunshot.
            phase += TAU * (64 + 9*math.sin(TAU*.7*t)) / RATE
            flutter = .66 + .18*math.sin(TAU*9*t) + .16*math.sin(TAU*13.7*t)
            body = math.sin(phase)*.25 + math.sin(phase*1.49)*.08
            value = body + earth[i]*.55 + grain[i]*.55*flutter + air[i]*.19
            value += .15*math.sin(TAU*(110*t + 75*t*t))*math.exp(-t*7)
            envelope = min(1, t/.015) * min(1, (duration-t)/.04) * (.85 + .15*p)
        else:
            # A single broad release followed by falling crystalline dust.
            phase += TAU * (52 + 105*math.exp(-t*24)) / RATE
            blast = math.sin(phase)*.6*math.exp(-t*10) + air[i]*.8*math.exp(-t*16)
            tail = earth[i]*.36*math.exp(-t*5) + grain[i]*.14*math.exp(-t*3.8)
            sparkle = sum(a*math.sin(TAU*f*t)*math.exp(-t*d)
                          for f, a, d in ((261,.08,4),(415,.06,4.6),(698,.04,5.2),(1175,.025,6)))
            value = blast + tail + sparkle
            envelope = min(1, t/.001) * min(1, (duration-t)/.14)
        value = math.tanh(value*1.35)
        value = value*.85 + round(value*255)/255*.15
        result.append(value*envelope)
    pcm = finish(result, rms, peak)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def main():
    assets = []
    for name, duration, rms, peak, guid in SPECS:
        path = OUT / (name + ".wav")
        pcm = synthesize(name, duration, rms, peak)
        if "--check" not in sys.argv:
            with wave.open(str(path), "wb") as wav:
                wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
                wav.writeframes(pcm)
        with wave.open(str(path), "rb") as wav:
            assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
            assert wav.readframes(wav.getnframes()) == pcm
        stats = metrics(path)
        samples = array.array("h", pcm)
        if sys.byteorder != "little":
            samples.byteswap()
        assert samples[0] == samples[-1] == 0
        assert stats["peak"] <= peak + .001 and stats["rms"] <= rms + .001
        assert stats["durationSeconds"] == duration
        assert abs(sum(samples)/len(samples)/32768) < .002
        assets.append(dict(path=path.relative_to(ROOT).as_posix(), sourceId="S20", guid=guid,
                           sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                           bytes=path.stat().st_size, durationSeconds=duration,
                           generator="Tools/GenerateSummoningAudio.py", metrics=stats))
    print(json.dumps(assets, indent=2))


if __name__ == "__main__":
    main()
