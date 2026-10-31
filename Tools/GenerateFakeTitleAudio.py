"""Original, quiet chiptune UI cues for AZURE QUEST; standard-library synthesis."""
import argparse
from array import array
import hashlib
import math
from pathlib import Path
import sys
import wave

RATE = 44100
OUT = Path(__file__).resolve().parent.parent / "Assets/SoundEffect/FakeTitle"


def synthesize(notes, peak):
    samples = []
    for frequency, duration in notes:
        count = round(duration * RATE)
        for i in range(count):
            t = i / RATE
            phase = math.tau * frequency * t
            # Soft band-limited square/triangle timbre; no harsh full-band edges,
            # recordings, copied melodies, or random pitch variation.
            tone = (math.sin(phase) + .22 * math.sin(phase * 3)
                    + .065 * math.sin(phase * 5))
            attack = min(1., t / .0035)
            release = min(1., (count - 1 - i) / (RATE * .018))
            envelope = attack * release * (.5 + .5 * math.exp(-t * 14))
            samples.append(tone * envelope)
    scale = peak / max(abs(value) for value in samples)
    pcm = array("h", (round(value * scale * 32767) for value in samples))
    pcm[0] = pcm[-1] = 0
    return pcm


def write_or_verify(name, notes, peak, check):
    pcm = synthesize(notes, peak)
    rms = math.sqrt(sum((v / 32768.) ** 2 for v in pcm) / len(pcm))
    assert .08 < rms < .17
    assert max(abs(v) for v in pcm) / 32768. <= .241
    assert pcm[0] == pcm[-1] == 0
    if sys.byteorder != "little":
        pcm.byteswap()
    raw = pcm.tobytes()
    path = OUT / (name + ".wav")
    if not check:
        OUT.mkdir(parents=True, exist_ok=True)
        with wave.open(str(path), "wb") as output:
            output.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            output.writeframes(raw)
    with wave.open(str(path), "rb") as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate()) == (1, 2, RATE)
        assert source.readframes(source.getnframes()) == raw
    print(f"{name}: {len(pcm)/RATE:.3f}s, RMS={rms:.4f}, peak={peak:.2f}, "
          f"SHA256={hashlib.sha256(path.read_bytes()).hexdigest()}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    write_or_verify("TitleEnter", [(523.251, .065), (659.255, .065), (783.991, .15)], .24, args.check)
    write_or_verify("TitleConfirm", [(783.991, .045), (1046.502, .095)], .22, args.check)
    print("FAKE_TITLE_AUDIO_PASS")
