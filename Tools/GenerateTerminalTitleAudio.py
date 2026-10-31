"""Quiet original terminal boot/navigation/confirmation cues (no recordings)."""
import argparse
import math
import wave
from array import array
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[1] / "Assets/Resources/Audio"


def make(name, length, notes, peak, check):
    data = [0.0] * round(length * RATE)
    for start, duration, frequency in notes:
        for i in range(round(duration * RATE)):
            index = round(start * RATE) + i
            if index >= len(data):
                break
            t = i / RATE
            phase = math.tau * frequency * t
            # Restrained odd harmonics suggest a tiny terminal speaker, not an alarm.
            tone = math.sin(phase) + .13 * math.sin(phase * 3) + .035 * math.sin(phase * 5)
            env = min(1., t / .003) * min(1., (duration - t) / .012) * math.exp(-t * 13)
            data[index] += tone * env
    scale = peak / max(abs(x) for x in data)
    pcm = array("h", [round(x * scale * 32767) for x in data])
    pcm[0] = pcm[-1] = 0
    path = OUT / (name + ".wav")
    if not check:
        with wave.open(str(path), "wb") as output:
            output.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            output.writeframes(pcm.tobytes())
    with wave.open(str(path), "rb") as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate()) == (1, 2, RATE)
        assert source.readframes(source.getnframes()) == pcm.tobytes()
    print(f"{name}: {length:.3f}s, peak {max(abs(x) for x in pcm)/32768:.3f}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    check = parser.parse_args().check
    # Starts at 0.30 s of the 2.30 s boot: window, title bands, text, ready.
    # Deliberate silent gaps, not a continuous tone or one beep per character.
    make("TitleTerminalBoot", 2.0, [(0, .055, 392), (.22, .045, 523.25),
         (.46, .045, 587.33), (.66, .06, 659.25), (.90, .035, 784),
         (1.16, .035, 784), (1.40, .035, 784), (1.80, .16, 1046.5)], .18, check)
    make("TitleTerminalMove", .045, [(0, .045, 587.33)], .12, check)
    make("TitleTerminalConfirm", .16, [(0, .045, 659.25), (.055, .105, 987.77)], .18, check)
    print("TERMINAL_AUDIO_PASS")
