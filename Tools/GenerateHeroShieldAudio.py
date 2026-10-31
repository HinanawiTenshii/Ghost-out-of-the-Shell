"""Original short synthetic shield clang; standard library only, no sampled recordings."""
import argparse
import math
import sys
import wave
from pathlib import Path
from GenerateSandwormAudio import RATE, noise, finish, metrics

OUT = Path(__file__).resolve().parent.parent / "Assets/Resources/Audio/HeroShieldBlock.wav"


def synthesize():
    samples = round(.24 * RATE)
    grit = noise(samples, 2849, 15000, 5900)
    signal = []
    for i in range(samples):
        t = i / RATE
        # Inharmonic plate modes with a dry impact, not a pitched bell or explosion.
        metal = sum(g * math.sin(math.tau * f * t) * math.exp(-decay * t)
                    for f, g, decay in [(790, .42, 23), (1351, .28, 30), (2263, .18, 39), (3511, .09, 50)])
        hit = grit[i] * .5 * math.exp(-t * 145)
        body = .14 * math.sin(math.tau * 195 * t) * math.exp(-t * 75)
        envelope = min(1, i / (RATE * .001)) * min(1, (samples - 1 - i) / (RATE * .025))
        signal.append((metal + hit + body) * envelope)
    pcm = finish(signal, .1, .6)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little": pcm.byteswap()
    return pcm.tobytes()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    raw = synthesize()
    if not args.check:
        with wave.open(str(OUT), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(raw)
    with wave.open(str(OUT), "rb") as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
        assert wav.readframes(wav.getnframes()) == raw
    stats = metrics(OUT)
    assert stats["durationSeconds"] == .24 and stats["peak"] <= .601
    assert .03 < stats["rms"] <= .101 and stats["onsetMs"] < 3
    print("HERO_SHIELD_AUDIO_PASS", stats)
