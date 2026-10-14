"""Original glass snap + wet splash; deterministic stdlib synthesis, no samples."""
import hashlib
import json
import math
from pathlib import Path
import random
import sys
import wave
from GenerateSandwormAudio import RATE, TAU, finish, noise, metrics
from GenerateDestructionAudio import add_grain, texture

OUT = Path(__file__).resolve().parent.parent / 'Assets/Resources/Audio/PotionShatter.wav'

def synthesize():
    signal = [0.] * round(.58 * RATE)
    for offset, level, seed in [(0, 1., 9011), (.014, .65, 9013), (.037, .34, 9017)]:
        add_grain(signal, offset, .075, seed, level, True)
    water = noise(len(signal), 9023, 11025, 2100)
    for i in range(len(signal)):
        t = i / RATE
        # Noise-heavy splash with short damped body, not an explosion's sub-bass.
        wet = water[i] * .9 * (1 - math.exp(-t * 120)) * math.exp(-t * 11)
        signal[i] += wet + .10 * math.sin(TAU * 330 * t) * math.exp(-t * 65)
    rng = random.Random(9029)
    for j in range(15):
        start = .075 + j * .027
        length = rng.uniform(.028, .055)
        offset = round(start * RATE)
        frequency = rng.uniform(680, 1700)
        for k in range(min(round(length * RATE), len(signal) - offset)):
            t = k / RATE
            envelope = min(1, k / 20) * math.exp(-t * 7 / length)
            # Tiny inharmonic plips falling into the liquid sheet.
            signal[offset+k] += .18 * (1-start/.6) * envelope * (
                math.sin(TAU*frequency*t) + .3*math.sin(TAU*frequency*1.71*t))
        if j % 3 == 0:
            add_grain(signal, start, .035, 9040+j, .10*(1-start/.6), True)
    pcm = finish(texture(signal), .16, .77)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != 'little': pcm.byteswap()
    return pcm.tobytes()

if __name__ == '__main__':
    expected = synthesize()
    if '--check' in sys.argv:
        with wave.open(str(OUT), 'rb') as w:
            assert (w.getnchannels(), w.getsampwidth(), w.getframerate()) == (1, 2, RATE)
            assert w.readframes(w.getnframes()) == expected
    else:
        with wave.open(str(OUT), 'wb') as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE); w.writeframes(expected)
    result = metrics(OUT)
    assert result['onsetMs'] < 2 and result['peak'] <= .771 and result['rms'] <= .161
    result['sha256'] = hashlib.sha256(OUT.read_bytes()).hexdigest()
    print(json.dumps(result))
