"""Original heavy mechanical cannon-shot synthesis, no sampled recordings (S15)."""
import hashlib, json, math, sys, wave
from pathlib import Path
from GenerateSandwormAudio import RATE, TAU, noise, finish, metrics
from GenerateDestructionAudio import texture

OUT = Path(__file__).resolve().parent.parent / 'Assets/Resources/Audio/TurretFire.wav'

def synthesize():
    length = round(.36 * RATE)
    crack = noise(length, 15001, 11025, 4800)
    body = noise(length, 15003, 5000, 550)
    signal = []
    phase = 0
    for i in range(length):
        t = i / RATE
        phase += TAU * (45 + 74 * math.exp(-t * 26)) / RATE
        boom = math.sin(phase) * math.exp(-t * 15)
        metal = (math.sin(TAU * 390 * t) + .35 * math.sin(TAU * 703 * t)) * math.exp(-t * 65)
        signal.append((.78 * boom + .95 * crack[i] * math.exp(-t * 52) +
            .7 * body[i] * math.exp(-t * 19) + .15 * metal) * min(1, i / 8))
    pcm = finish(texture(signal), .16, .77)
    pcm[0] = pcm[-1] = 0
    if sys.byteorder != 'little': pcm.byteswap()
    return pcm.tobytes()

if __name__ == '__main__':
    pcm = synthesize()
    if '--check' in sys.argv:
        with wave.open(str(OUT), 'rb') as f:
            assert (f.getnchannels(), f.getsampwidth(), f.getframerate()) == (1, 2, RATE)
            assert f.readframes(f.getnframes()) == pcm
    else:
        with wave.open(str(OUT), 'wb') as f:
            f.setnchannels(1); f.setsampwidth(2); f.setframerate(RATE); f.writeframes(pcm)
    result = metrics(OUT)
    assert result['onsetMs'] < 2 and result['peak'] <= .771
    result['sha256'] = hashlib.sha256(OUT.read_bytes()).hexdigest()
    print(json.dumps(result))
