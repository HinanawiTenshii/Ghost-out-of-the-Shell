"""Original dry crystalline fracture, no recordings/samples. Python standard library only."""
import hashlib
import json
import math
from pathlib import Path
import random
import sys
import wave
from GenerateSandwormAudio import RATE, TAU, finish, noise, metrics
from GenerateDestructionAudio import add_grain, texture

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'Assets/Resources/Audio/EnergyCrystalShatter.wav'

def synthesize():
    signal = [0.] * round(.62 * RATE)
    for offset, level, seed in [(0, 1., 8101), (.012, .63, 8107), (.049, .4, 8111)]:
        add_grain(signal, offset, .1, seed, level, True)
    rng = random.Random(8117)
    for j in range(17):
        start = .08 + .027 * j
        add_grain(signal, start, rng.uniform(.025, .063), 8123+j, .32*(1-start/.65), True)
    gritty = noise(len(signal), 8171, 11025, 4500)
    for i in range(len(signal)):
        t = i/RATE
        # Fixed inharmonic modes: brittle mineral, not a musical pickup/laser.
        ring = sum(math.sin(TAU*f*t)*a*math.exp(-t*d) for f,a,d in
                   [(631,.12,17),(1733,.1,20),(2819,.07,24)])
        signal[i] += ring + gritty[i]*.25*math.exp(-t*22)
    pcm = finish(texture(signal), .16, .78)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != 'little': pcm.byteswap()
    return pcm.tobytes()

if __name__ == '__main__':
    expected = synthesize()
    if '--check' in sys.argv:
        with wave.open(str(OUT),'rb') as w:
            assert (w.getnchannels(),w.getsampwidth(),w.getframerate()) == (1,2,RATE)
            assert w.readframes(w.getnframes()) == expected
    else:
        with wave.open(str(OUT),'wb') as w:
            w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(expected)
    result=metrics(OUT)
    assert result['onsetMs'] < 2 and result['peak'] <= .781 and result['rms'] <= .161
    result['sha256']=hashlib.sha256(OUT.read_bytes()).hexdigest()
    print(json.dumps(result))
