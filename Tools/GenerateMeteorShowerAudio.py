"""S18: original meteor descent, impact and seamless magic-fire audio.

Standard library synthesis, no recorded samples. --check verifies assets;
--preview also renders the upgraded sequence into .codex-temp for listening.
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
    ("MeteorDescent", .8, .13, .66, "a3b1c3f985174d709454c657ad9570b6"),
    ("MeteorImpact", .82, .17, .78, "abcda0aec05543dd87f9e9e983ed1184"),
    ("MeteorFireLoop", 1.6, .13, .60, "948b565b51b64f8fbca74f752a18c97a"),
)


def synthesize(name, seconds, rms, peak):
    looping = name.endswith("Loop")
    overlap = round(.12 * RATE) if looping else 0
    n = round(seconds * RATE) + overlap
    air = noise(n, 18001, 14700, 2900)
    grit = noise(n, 18007, 2600, 740)
    earth = noise(n, 18013, 520, 170)
    result, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        if looping:
            breath = .78 + .15 * math.sin(TAU * 2.5 * t) + .07 * math.sin(TAU * 4.375 * t)
            crackle = max(0, math.sin(TAU * 19.375 * t + 1.9 * math.sin(TAU * 3.125 * t))) ** 18
            raw = earth[i] * .25 + grit[i] * .23 * breath + air[i] * (.075 + .4 * crackle)
            raw += .027 * math.sin(TAU * 83.125 * t + .6 * math.sin(TAU * 1.875 * t))
        elif name.endswith("Descent"):
            u = t / seconds
            # Accelerating air and rock vibration, not an isolated laser whistle.
            phase += TAU * (98 + 38 * u + 9 * math.sin(TAU * 4 * t)) / RATE
            rumble = .1 * math.sin(phase) + .038 * math.sin(phase * 2.17)
            rush = air[i] * (.07 + .66 * u ** 2) + grit[i] * (.08 + .15 * u)
            raw = (rush + rumble + earth[i] * .19) * (.2 + .8 * u ** 1.3)
            raw *= min(1, t / .018) * min(1, (seconds - t) / .026)
        else:
            phase += TAU * (39 + 114 * math.exp(-t * 27)) / RATE
            boom = .72 * math.sin(phase) * math.exp(-t * 9)
            crack = air[i] * (1.4 * math.exp(-t * 70) + .32 * math.exp(-t * 11))
            rubble = earth[i] * .7 * math.exp(-t * 6) + grit[i] * .42 * math.exp(-t * 8)
            shards = sum(a * math.sin(TAU * f * t) * math.exp(-t * decay)
                         for f, a, decay in ((213, .065, 7), (371, .038, 10), (683, .019, 14)))
            raw = boom + crack + rubble + shards
            raw *= min(1, t / .001) * min(1, (seconds - t) / .09)
        raw = math.tanh(raw * 1.45)
        result.append(raw * .88 + round(raw * 255) / 255 * .12)
    if looping:
        source = result
        result = source[overlap:]
        for i in range(overlap):
            blend = i / (overlap - 1)
            result[-overlap + i] = source[-overlap + i] * (1 - blend) + source[i] * blend
    pcm = finish(result, rms, peak, looping=looping)
    if looping:
        # Preserve the continuous overlap-add texture, smoothing only the last
        # sub-millisecond sample jump so compressed device output cannot click.
        midpoint = (pcm[0] + pcm[-1]) / 2
        first_delta, last_delta = midpoint - pcm[0], midpoint - pcm[-1]
        for i in range(32):
            weight = .5 + .5 * math.cos(math.pi * i / 31)
            pcm[i] += round(first_delta * weight)
            pcm[-1-i] += round(last_delta * weight)
    else:
        pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def write_wav(path, pcm):
    with wave.open(str(path), "wb") as wav:
        wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        wav.writeframes(pcm)


def preview(cues):
    signals = []
    for pcm in cues:
        samples = array.array("h", pcm)
        if sys.byteorder != "little":
            samples.byteswap()
        signals.append([v / 32768 for v in samples])
    mixed = array.array("h")
    for i in range(round(10.5 * RATE)):
        t = i / RATE
        falls, hits, fires = [], [], []
        for meteor in range(15):
            age = t - meteor * .28
            if 0 <= age < .8:
                falls.append(signals[0][min(len(signals[0]) - 1, int(age * RATE))])
            impact = age - .8
            if 0 <= impact < .82:
                hits.append(signals[1][min(len(signals[1]) - 1, int(impact * RATE))])
            if 0 <= impact < 5:
                fires.append(min(1, impact / .18, (5 - impact) / .3))
        value = .32 * sum(falls) / max(1, len(falls)) + .6 * sum(hits) / max(1, len(hits))
        value += .27 * max(fires, default=0) * signals[2][i % len(signals[2])]
        mixed.append(round(max(-1, min(1, value)) * 32767))
    if sys.byteorder != "little":
        mixed.byteswap()
    write_wav(ROOT / ".codex-temp/MeteorShower-audio-preview.wav", mixed.tobytes())


def main():
    assets, cues = [], []
    for name, seconds, rms, peak, guid in SPECS:
        path = OUT / (name + ".wav")
        pcm = synthesize(name, seconds, rms, peak)
        cues.append(pcm)
        if "--check" not in sys.argv:
            write_wav(path, pcm)
        with wave.open(str(path), "rb") as wav:
            assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
            assert wav.readframes(wav.getnframes()) == pcm, "Installed audio differs from deterministic synthesis"
        stats = metrics(path)
        samples = array.array("h", pcm)
        if sys.byteorder != "little":
            samples.byteswap()
        assert stats["durationSeconds"] == seconds and stats["peak"] <= peak + .001
        assert abs(sum(samples) / len(samples) / 32768) < .002
        if name.endswith("Loop"):
            assert stats["seamDelta"] < .025, "Fire loop seam must be quiet"
            assert stats["rms"] >= .1
        else:
            assert samples[0] == samples[-1] == 0
            if name.endswith("Impact"):
                assert stats["onsetMs"] < 3
            else:
                early = samples[round(.1 * RATE):round(.2 * RATE)]
                late = samples[round(.6 * RATE):round(.7 * RATE)]
                assert sum(v * v for v in late) > 3 * sum(v * v for v in early)
        assets.append(dict(path=path.relative_to(ROOT).as_posix(), sourceId="S18", status="original-procedural",
                           guid=guid, sha256=hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                           bytes=path.stat().st_size, durationSeconds=seconds,
                           generator="Tools/GenerateMeteorShowerAudio.py", metrics=stats))
    if "--preview" in sys.argv:
        preview(cues)
    print(json.dumps(assets, indent=2))


if __name__ == "__main__":
    main()
