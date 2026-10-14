"""Original deterministic CRT-styled destruction synthesis (stdlib only).

No recordings or existing WAV samples enter the synthesis. The reference clips
are measured only for levels/onset. Unity paths and .meta GUIDs remain unchanged.
Run with --check to validate the installed files without overwriting them.
"""
import argparse
import array
import hashlib
import json
import math
from pathlib import Path
import random
import sys
import wave

from GenerateSandwormAudio import RATE, TAU, finish, metrics, noise

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Resources/Audio"


def add_grain(signal, start, duration, seed, amplitude, glass=False):
    n = round(duration * RATE)
    offset = round(start * RATE)
    rng = random.Random(seed)
    grit = noise(n, seed, 14700 if glass else 6300, 6500 if glass else 2800)
    frequencies = ([rng.uniform(1800, 3500), rng.uniform(3800, 5700)] if glass
                   else [rng.uniform(180, 350), rng.uniform(430, 750)])
    for i in range(min(n, len(signal) - offset)):
        t = i / RATE
        phase_t = (i // 3) * 3 / RATE
        ring = sum(math.sin(TAU * f * phase_t) for f in frequencies) * .5
        envelope = min(1, (i + 1) / 12) * math.exp(-t * 4.5 / duration)
        envelope *= min(1, (n - 1 - i) / (RATE * .004))
        signal[offset + i] += amplitude * envelope * (grit[i] * .82 + ring * .18)


def wood():
    x = [0.0] * round(.52 * RATE)
    # Distinct stepped wooden snaps on top of a low, dry board impact.
    for start, duration, level, seed in [
        (0, .12, 1.0, 10401), (.027, .095, .75, 10403),
        (.063, .105, .60, 10409), (.119, .12, .43, 10411)]:
        add_grain(x, start, duration, seed, level)
    rng = random.Random(10417)
    for j in range(13):
        start = .16 + j * .023 + rng.uniform(-.007, .007)
        add_grain(x, start, rng.uniform(.025, .055), 10500 + j,
                  .21 * (1 - start / .58))
    low = noise(len(x), 10427, 1200, 530)
    for i in range(len(x)):
        t = i / RATE
        # Fixed inharmonic resonances; no laser-like descending oscillator.
        thud = (math.sin(TAU * 127 * t) + .45 * math.sin(TAU * 211 * t))
        x[i] += thud * .19 * math.exp(-t * 27) + low[i] * .4 * math.exp(-t * 13)
    return texture(x)


def glass():
    x = [0.0] * round(.64 * RATE)
    add_grain(x, 0, .10, 10601, 1.0, True)
    add_grain(x, .013, .095, 10607, .76, True)
    add_grain(x, .041, .10, 10609, .52, True)
    rng = random.Random(10613)
    # A scatter of short non-musical shard ticks, not an ascending UI chime.
    for j in range(23):
        start = .068 + j * .021 + rng.uniform(-.006, .006)
        add_grain(x, start, rng.uniform(.026, .073), 10700 + j,
                  .37 * (1 - start / .69), True)
    grit = noise(len(x), 10627, 14700, 6900)
    for i in range(len(x)):
        t = i / RATE
        x[i] += grit[i] * .32 * math.exp(-t * 15)
        x[i] += math.sin(TAU * 290 * t) * .08 * math.exp(-t * 65)
    return texture(x)


def texture(x):
    # Mild stepped amplitude texture, tapered AFTER quantizing so the end
    # decays cleanly instead of ending with a full-strength bit-crusher tick.
    low = highpass = previous = 0.0
    for i, value in enumerate(x):
        highpass = .991 * (highpass + value - previous)
        previous = value
        shaped = round(math.tanh(highpass * 1.8) * 255) / 255
        low += .76 * (shaped - low)
        fade_in = min(1, i / (RATE * .0005))
        fade_out = min(1, (len(x) - 1 - i) / (RATE * .025))
        x[i] = low * fade_in * fade_out
    return x


def encode(signal):
    pcm = finish(signal, .18, .80)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def validate(path, expected, duration):
    with wave.open(str(path), "rb") as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
        raw = wav.readframes(wav.getnframes())
    assert raw == expected, f"{path.name}: deterministic synthesis differs"
    stats = metrics(path)
    assert abs(stats["durationSeconds"] - duration) < .0001
    assert .09 < stats["rms"] <= .181 and stats["peak"] <= .801
    assert stats["onsetMs"] < 3
    samples = array.array("h", raw)
    if sys.byteorder != "little":
        samples.byteswap()
    assert samples[0] == samples[-1] == 0
    assert abs(sum(samples) / len(samples) / 32768) < .003
    # Audible debris after the initial impact, but no long silent padding.
    tail = samples[round(.16 * RATE):round(.32 * RATE)]
    assert math.sqrt(sum(v * v for v in tail) / len(tail)) / 32768 > .012
    return dict(path=path.relative_to(ROOT).as_posix(), sourceId="S11",
                sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                bytes=path.stat().st_size, durationSeconds=duration,
                generator="Tools/GenerateDestructionAudio.py", metrics=stats)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    assets = []
    for name, signal, duration in [("DoorBreak", wood(), .52), ("GlassBreak", glass(), .64)]:
        path = OUT / (name + ".wav")
        raw = encode(signal)
        if not args.check:
            with wave.open(str(path), "wb") as wav:
                wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
                wav.writeframes(raw)
        assets.append(validate(path, raw, duration))
    references = {n: metrics(OUT / (n + ".wav"))
                  for n in ("CharacterDamage", "CharacterDeath", "BombExplosion")}
    print(json.dumps(dict(references=references, assets=assets), indent=2))


if __name__ == "__main__":
    main()
