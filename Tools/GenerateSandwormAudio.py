"""Deterministic original synthesis, Python standard library only.

No source recordings are sampled. Existing feedback WAVs are measured solely
for onset, level and transient length. Output: mono PCM16/44100 Hz for Unity.
"""
import array
import hashlib
import json
import math
from pathlib import Path
import random
import re
import sys
import uuid
import wave

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Resources/Audio/Sandworm"
RATE = 44100
TAU = math.tau


def noise(n, seed, hold_rate, cutoff):
    rng = random.Random(seed)
    a = 1 - math.exp(-TAU * cutoff / RATE)
    hold = max(1, round(RATE / hold_rate))
    value = low = 0.0
    result = []
    for i in range(n):
        if i % hold == 0:
            value = round(rng.uniform(-1, 1) * 31) / 31
        low += a * (value - low)
        result.append(low)
    return result


def loop(seconds, seed, roughness):
    overlap = int(.12 * RATE)
    n = round(seconds * RATE) + overlap
    grit = noise(n, seed, 2100 + roughness * 1500, 850 + roughness * 1200)
    earth = noise(n, seed + 1, 360, 145)
    x = []
    for i in range(n):
        t = i / RATE
        pulse = (.5 + .5 * math.sin(TAU * 5 * t + .6 * math.sin(TAU * 1.7 * t))) ** 4
        sub = math.sin(TAU * 61 * t + .9 * math.sin(TAU * 3.3 * t))
        value = earth[i] * .56 + grit[i] * (.14 + .36 * pulse) + sub * .075
        x.append(math.tanh(value * 1.6))
    # Overlap-add joins the next sample of the opening, not two unrelated ends.
    result = x[overlap:]
    for i in range(overlap):
        t = i / (overlap - 1)
        result[-overlap + i] = x[-overlap + i] * (1 - t) + x[i] * t
    return result


def impact(seconds, seed, weight, grit_rate, falling=False):
    n = round(seconds * RATE)
    grit = noise(n, seed, grit_rate, 2600)
    earth = noise(n, seed + 1, 560, 210)
    x = []
    phase = 0.0
    for i in range(n):
        t = i / RATE
        phase += TAU * (46 + weight * 140 * math.exp(-t * 26)) / RATE
        body = math.sin(phase) * math.exp(-t * (10 if falling else 15))
        crack = grit[i] * math.exp(-t * 40) * .62
        crumble = earth[i] * math.exp(-t * 5.5) * .52
        grain = grit[i] * math.exp(-t * 8) * (.18 + .15 * math.sin(TAU * 19 * t) ** 4)
        # A descending, noise-heavy tumble, not a clean laser pitch sweep.
        fall = grit[i] * .15 * math.sin(math.pi * t / seconds) ** 2 if falling else 0
        attack = min(1.0, (i + 1) / (RATE * .001))
        release = min(1.0, (n - 1 - i) / (RATE * .035))
        x.append(math.tanh((body * .32 + crack + crumble + grain + fall) * 1.5) * attack * release)
    return x


def death_burst(head=False):
    # Dry shell fracture for each segment; a deeper, broader final collapse for the head.
    seconds = .72 if head else .23
    n = round(seconds * RATE)
    grit = noise(n, 883 if head else 877, 9200 if head else 7100, 3500)
    debris = noise(n, 887 if head else 881, 800, 410)
    x, phase = [], 0.0
    for i in range(n):
        t = i / RATE
        phase += TAU * ((48 if head else 92) + 170 * math.exp(-t * 36)) / RATE
        crack = grit[i] * (.65 * math.exp(-t * 65) + .3 * math.exp(-t * (12 if head else 24)))
        rattle = debris[i] * math.exp(-t * (7 if head else 21)) * (.65 if head else .4)
        body = math.sin(phase) * math.exp(-t * (9 if head else 28)) * (.5 if head else .23)
        tail = grit[i] * .12 * math.exp(-t * 8) * math.sin(TAU * 27 * t) ** 6 if head else 0
        envelope = min(1, (i + 1) / (RATE * .0007)) * min(1, (n - 1 - i) / (RATE * .025))
        x.append(math.tanh((crack + rattle + body + tail) * 1.7) * envelope)
    return x


def finish(x, target_rms, peak, looping=False):
    dc = sum(x) / len(x)
    x = [v - dc for v in x]
    if not looping:
        # DC removal must not leave a discontinuity at the end of one-shots.
        fade = round(RATE * .004)
        for i in range(fade):
            x[-1-i] *= i / fade
    rms = math.sqrt(sum(v * v for v in x) / len(x))
    gain = min(target_rms / max(rms, 1e-9), peak / max(abs(v) for v in x))
    return array.array("h", (round(max(-.98, min(.98, v * gain)) * 32767) for v in x))


def metrics(path):
    with wave.open(str(path), "rb") as wav:
        rate = wav.getframerate()
        x = array.array("h", wav.readframes(wav.getnframes()))
        if sys.byteorder != "little": x.byteswap()
    peak = max(abs(v) for v in x) / 32768
    rms = math.sqrt(sum((v / 32768) ** 2 for v in x) / len(x))
    first = next((i for i, v in enumerate(x) if abs(v) > 327), len(x))
    return {"durationSeconds": round(len(x) / rate, 6), "peak": round(peak, 4),
            "rms": round(rms, 4), "onsetMs": round(first / rate * 1000, 3),
            "seamDelta": round(abs(x[-1] - x[0]) / 32768, 5)}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    specs = [
        ("MovementLoop", loop(2.4, 817, .15), .155, .70),
        ("EmergenceLoop", loop(1.8, 823, .9), .185, .74),
        ("BurrowLoop", loop(2.1, 829, .4), .17, .72),
        ("Breach", impact(.62, 839, 1.0, 5800), .20, .88),
        ("BurrowImpact", impact(.48, 853, .55, 3400), .19, .84),
        ("BlastFall", impact(.9, 857, 1.4, 6500, True), .205, .90),
        ("GroundImpact", impact(.58, 859, .8, 2300), .20, .86),
        ("DeathSegment", death_burst(), .17, .78),
        ("DeathHead", death_burst(True), .20, .90),
    ]
    assets = []
    for name, signal, rms, peak in specs:
        path = OUT / (name + ".wav")
        pcm = finish(signal, rms, peak, name.endswith("Loop"))
        if sys.byteorder != "little": pcm.byteswap()
        with wave.open(str(path), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(pcm.tobytes())
        stats = metrics(path)
        assert stats["peak"] < .98 and stats["rms"] > .1
        assert stats["onsetMs"] < 3
        if name.endswith("Loop"): assert stats["seamDelta"] < .045
        relative = path.relative_to(ROOT).as_posix()
        meta = Path(str(path) + ".meta")
        guid = re.search(r"^guid: (\w+)", meta.read_text(), re.M).group(1) if meta.exists() else uuid.uuid5(uuid.NAMESPACE_URL, relative).hex
        assets.append({"path": relative, "sourceId": "S10", "guid": guid,
                       "sha256": hashlib.sha256(path.read_bytes()).hexdigest().upper(),
                       "bytes": path.stat().st_size, "durationSeconds": stats["durationSeconds"],
                       "generator": "Tools/GenerateSandwormAudio.py", "metrics": stats})
    references = {p: metrics(ROOT / "Assets/Resources/Audio" / (p + ".wav"))
                  for p in ("CharacterDamage", "CharacterDeath", "BombExplosion")}
    print(json.dumps({"references": references, "assets": assets}, indent=2))


if __name__ == "__main__": main()
