"""Original dry branch snaps and leaf rustle; matches the project's synthetic feedback.

Python standard library only. No recorded audio is sampled. --check verifies without rewriting.
"""
import argparse
import math
from pathlib import Path
import sys
import wave
from GenerateSandwormAudio import RATE, noise, finish, metrics
from GenerateDestructionAudio import add_grain, texture

OUT = Path(__file__).resolve().parent.parent / "Assets/Resources/Audio/BushBreak.wav"


def synthesize():
    signal = [0.0] * round(.78 * RATE)
    # Smaller, higher dry fractures than a door; no explosive sub-bass or glass chime.
    for start, duration, amplitude, seed in [(0,.075,.8,1811), (.024,.065,.55,1817), (.071,.06,.35,1823)]:
        add_grain(signal, start, duration, seed, amplitude)
    leaves = noise(len(signal), 1831, 12000, 4300)
    for i in range(len(signal)):
        t = i / RATE
        rustle = (1 - math.exp(-t*80)) * math.exp(-t*6)
        flutter = .5 + .5 * math.sin(math.tau * 23 * t + .6 * math.sin(math.tau*7*t)) ** 2
        signal[i] += leaves[i] * rustle * flutter * .55
    pcm = finish(texture(signal), .12, .66)
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
            wav.setparams((1,2,RATE,0,"NONE","not compressed")); wav.writeframes(raw)
    with wave.open(str(OUT), "rb") as wav:
        assert (wav.getnchannels(),wav.getsampwidth(),wav.getframerate()) == (1,2,RATE)
        assert wav.readframes(wav.getnframes()) == raw
    stats = metrics(OUT)
    assert stats["durationSeconds"] == .78 and stats["peak"] <= .661
    assert .03 < stats["rms"] <= .121 and stats["onsetMs"] < 3
    print("BUSH_AUDIO_PASS", stats)
