"""Original D.A.T.A homing/absorption synthesis; no recordings or extra packages."""
import argparse
import math
import random
import sys
import wave
from pathlib import Path
from GenerateSandwormAudio import RATE, finish, metrics

OUT = Path(__file__).resolve().parent.parent / "Assets/Resources/Audio"
TAU = math.tau


def homing():
    seconds = .8
    rng = random.Random(31001)
    # All frequencies and modulations contain whole cycles: a quiet, seamless
    # glass/air shimmer whose envelope is controlled by actual particle flight.
    grains = [(rng.randrange(700, 2400) / seconds, rng.random() * TAU) for _ in range(24)]
    result = []
    for i in range(round(seconds * RATE)):
        t = i / RATE
        breath = sum(math.sin(TAU * f * t + phase) for f, phase in grains) / 24
        pulse = .82 + .18 * math.sin(TAU * 5 * t)
        resonances = sum(level * math.sin(TAU * freq * t + .3 * math.sin(TAU * 2.5 * t))
                         for freq, level in [(540, .13), (813.75, .10), (1215, .055), (1620, .025)])
        # Softer and lower than the initial brittle crystal crack; no low boom.
        result.append((resonances + breath * .19) * pulse)
    return finish(result, .065, .26, looping=True)


def absorption():
    seconds = .14
    result = [0.] * round(seconds * RATE)
    # A tiny inward breath and short crystal tick: readable at many arrivals/sec.
    for i in range(len(result)):
        t = i / RATE
        u = min(1, t / .035)
        phase = TAU * (720*t + 540*t*t/.15)
        result[i] = math.sin(phase) * .06 * math.sin(math.pi * u)**2 if t < .035 else 0
    for onset, frequency, level in [(0, 1280, .21), (.008, 1920, .12), (.016, 2560, .075)]:
        offset = round(onset * RATE)
        for i in range(len(result) - offset):
            t = i / RATE
            env = min(1, t/.002) * math.exp(-t * 48)
            ring = math.sin(TAU * frequency * t) + .19 * math.sin(TAU * frequency * 1.503 * t)
            result[offset + i] += env * level * ring
    for i in range(len(result)):
        t = i / RATE
        result[i] = round(math.tanh(result[i]) * 4095) / 4095
        result[i] *= min(1, t/.002) * min(1, (len(result)-1-i)/(RATE*.025))
    pcm = finish(result, .06, .30)
    pcm[0] = pcm[-1] = 0
    return pcm


def write_or_verify(name, pcm, check):
    if sys.byteorder != "little":
        pcm.byteswap()
    raw = pcm.tobytes()
    path = OUT / (name + ".wav")
    if not check:
        with wave.open(str(path), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(raw)
    with wave.open(str(path), "rb") as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
        assert wav.readframes(wav.getnframes()) == raw
    stats = metrics(path)
    assert stats["peak"] <= .401 and .025 < stats["rms"] <= .076
    if "Homing" in name:
        assert stats["seamDelta"] < .035, stats
    else:
        assert pcm[0] == pcm[-1] == 0
    print(name, stats)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    write_or_verify("DataHomingLoop", homing(), args.check)
    write_or_verify("DataAbsorption", absorption(), args.check)
    print("DATA_ABSORPTION_AUDIO_PASS")
