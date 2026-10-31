"""Original crisp D.A.T.A crystal fracture; deterministic synthesis, no recordings."""
import argparse
import math
import random
import sys
import wave
from pathlib import Path
from GenerateSandwormAudio import RATE, finish, noise, metrics
from GenerateDestructionAudio import add_grain

OUT = Path(__file__).resolve().parent.parent / "Assets/Resources/Audio/DataPickupCrystal.wav"


def synthesize():
    signal = [0.] * round(.64 * RATE)
    # A compact, dry fracture followed by separate small crystal shards.
    for at, length, level, seed in [(0, .055, .8, 30901), (.009, .048, .36, 30907), (.027, .042, .22, 30911)]:
        add_grain(signal, at, length, seed, level, True)
    rng = random.Random(30917)
    for shard in range(11):
        start = .045 + shard * .027 + rng.uniform(-.007, .007)
        freq = rng.uniform(1900, 3600)
        duration = rng.uniform(.08, .19)
        level = .12 * (1 - start / .55)
        offset = round(start * RATE)
        for i in range(min(round(duration * RATE), len(signal) - offset)):
            t = i / RATE
            envelope = min(1, t / .0015) * math.exp(-t * 7 / duration)
            # Inharmonic resonances imply crystal pieces, not an ascending UI melody.
            ring = math.sin(math.tau * freq * t) + .3 * math.sin(math.tau * freq * 1.51 * t)
            signal[offset + i] += level * envelope * ring
    grit = noise(len(signal), 30931, 14700, 5500)
    low = 0.
    for i in range(len(signal)):
        t = i / RATE
        ring = sum(level * math.sin(math.tau * freq * t) * math.exp(-t * decay)
                   for freq, level, decay in [(1283, .12, 18), (2213, .18, 15), (3467, .09, 21), (5087, .025, 34)])
        value = signal[i] + ring + grit[i] * .13 * math.exp(-t * 75)
        # Light retro texture, without the heavy saturation of combat explosions.
        value = round(math.tanh(value) * 1023) / 1023
        low += .58 * (value - low)
        signal[i] = low * min(1, t / .0007) * min(1, (len(signal) - 1 - i) / (RATE * .04))
    pcm = finish(signal, .11, .7)
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
    assert stats["durationSeconds"] == .64 and stats["peak"] <= .701
    assert .04 < stats["rms"] <= .111 and stats["onsetMs"] < 3
    print("DATA_PICKUP_AUDIO_PASS", stats)
