"""Original, restrained terminal refresh cues. No recordings or third-party samples."""
import argparse
import math
import random
import wave
from array import array
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[1] / "Assets/Resources/Audio"


def make(name, length, peak, kind, check):
    samples = []
    rng = random.Random(8231)
    filtered = phase = 0.0
    for i in range(round(length * RATE)):
        t = i / RATE
        p = t / length
        envelope = min(1., t / .005) * min(1., (length - t) / .025)
        if kind == "scan":
            filtered = filtered * .86 + rng.uniform(-1., 1.) * .14
            phase += math.tau * (520. - 290. * p) / RATE
            # Breath-like scan with a faint low-frame electronic texture.
            value = (filtered * .6 + math.sin(phase) * .08) * (.7 + .3 * math.cos(p * math.tau * 3))
        else:
            frequency = 523.25 if kind == "begin" else 1046.5
            phase = math.tau * frequency * t
            value = (math.sin(phase) + .10 * math.sin(phase * 3)) * math.exp(-t * 18)
        samples.append(value * envelope)
    scale = peak / max(abs(x) for x in samples)
    pcm = array("h", (round(x * scale * 32767) for x in samples))
    pcm[0] = pcm[-1] = 0
    path = OUT / (name + ".wav")
    if not check:
        with wave.open(str(path), "wb") as output:
            output.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            output.writeframes(pcm.tobytes())
    with wave.open(str(path), "rb") as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate()) == (1, 2, RATE)
        assert source.readframes(source.getnframes()) == pcm.tobytes()
    print(f"{name}: {length:.3f}s, peak {peak:.3f}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    check = parser.parse_args().check
    make("SceneRefreshBegin", .085, .14, "begin", check)
    make("SceneRefreshScan", .34, .065, "scan", check)
    make("SceneRefreshReady", .11, .13, "ready", check)
    print("SCENE_REFRESH_AUDIO_PASS")
