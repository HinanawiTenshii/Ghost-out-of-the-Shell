"""Original two-strike arena bell, mono PCM16. No recorded samples.

Uses only the Python standard library and the project's existing audio helpers.
--check verifies the installed WAV without rewriting it.
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
PATH = ROOT / "Assets/Resources/Audio/ArenaRoundEndBell.wav"
INTERVAL = .23
LENGTH = .72
GUID = "83fc3ff1e0b04731bcf6a82704111f13"


def synthesize():
    count = round(LENGTH * RATE)
    hammer = noise(count, 19231, 12000, 4700)
    samples = [0.0] * count
    # Damped, inharmonic bronze modes: a bell strike rather than a sine beep.
    modes = ((665, 1.0, 11), (1345, .53, 15), (1795, .31, 21),
             (2760, .15, 32), (3540, .065, 44))
    for start, strength in ((0.0, 1.0), (INTERVAL, .94)):
        offset = round(start * RATE)
        for i in range(offset, count):
            age = (i - offset) / RATE
            attack = min(1, age / .0015)
            ring = sum(gain * math.sin(TAU * frequency * age) * math.exp(-decay * age)
                       for frequency, gain, decay in modes)
            knock = hammer[i] * .14 * math.exp(-age * 160)
            raw = math.tanh((ring + knock) * .65)
            # Subtle stepped texture matches the other procedural game cues.
            raw = .94 * raw + .06 * round(raw * 255) / 255
            samples[i] += strength * attack * raw
    fade = round(.04 * RATE)
    for i in range(fade):
        samples[-1-i] *= i / fade
    pcm = finish(samples, .16, .70)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != "little":
        pcm.byteswap()
    return pcm.tobytes()


def main():
    pcm = synthesize()
    if "--check" not in sys.argv:
        with wave.open(str(PATH), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(pcm)
    with wave.open(str(PATH), "rb") as wav:
        assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate()) == (1, 2, RATE)
        assert wav.readframes(wav.getnframes()) == pcm, "Installed cue differs from generator"
    samples = array.array("h", pcm)
    if sys.byteorder != "little":
        samples.byteswap()
    def power(start, end):
        window = samples[round(start * RATE):round(end * RATE)]
        return sum(v*v for v in window) / len(window)
    assert power(.005, .045) > power(.18, .22) * 8
    assert power(.235, .275) > power(.18, .22) * 8, "Second strike must be distinct"
    stats = metrics(PATH)
    assert stats["durationSeconds"] == LENGTH and stats["peak"] <= .701
    assert stats["onsetMs"] < 3 and samples[0] == samples[-1] == 0
    assert abs(sum(samples) / len(samples) / 32768) < .002
    result = dict(path=PATH.relative_to(ROOT).as_posix(), sourceId="S19", guid=GUID,
                  sha256=hashlib.sha256(PATH.read_bytes()).hexdigest().upper(),
                  bytes=PATH.stat().st_size, durationSeconds=LENGTH,
                  generator="Tools/GenerateArenaBellAudio.py", metrics=stats)
    print(json.dumps(result, indent=2))
    print("Reference levels:")
    for name in ("CharacterDamage", "CharacterDeath", "BombExplosion"):
        print(name, metrics(PATH.parent / (name + ".wav")))


if __name__ == "__main__":
    main()
