# Renders the in-game lo-fi music loop (Sfx.Music in unity/Assets/Scripts/Sfx.cs) to a WAV file, for the App Store
# preview and header videos (simulator recordings have no sound). Keep in step with Sfx.Music.
# Usage: python3 tools/music_wav.py <out.wav> [loops]
import math, random, struct, sys, wave

SR = 22050
TAU = math.pi * 2
out = sys.argv[1]
loops = int(sys.argv[2]) if len(sys.argv) > 2 else 3
rnd = random.Random(3)
def N(): return rnd.random() * 2 - 1
def exp(x): return math.exp(x)

bpm = 84.0; beat = 60.0 / bpm
bars = 4; dur = beat * 4 * bars
n = int(SR * dur)
chords = [[174.61, 220, 261.63, 329.63], [164.81, 196, 246.94, 293.66], [146.83, 174.61, 220, 261.63], [130.81, 164.81, 196, 246.94]]
roots = [87.31, 82.41, 73.42, 65.41]
mel = [659.25, 587.33, 523.25, 587.33, 659.25, 783.99, 659.25, 523.25]
hp = 0.0; blp = 0.0
d = []
for i in range(n):
    t = i / SR; bt = t / beat
    bi = int(bt); bar = (bi // 4) % bars
    ib = (bt - bi) * beat
    kick = math.sin(TAU * (50 + 70 * exp(-ib * 30)) * ib) * exp(-ib * 9) * 0.5 if bi % 2 == 0 else 0
    nz = N()
    snare = (nz - hp) * exp(-ib * 22) * 0.12 if bi % 2 == 1 else 0
    e8 = bt * 2; e8i = int(e8); i8 = (e8 - e8i) * beat / 2
    hat = (nz - hp) * exp(-i8 * 70) * 0.05; hp = nz
    bf = roots[bar] * (1.5 if (bi % 4 == 3 and ib > beat * 0.5) else 1)
    blp += (math.sin(TAU * bf * t) + 0.4 * math.sin(TAU * bf * 2 * t) - blp) * 0.2
    bass = blp * 0.28 * (1 - 0.4 * exp(-ib * 6))
    barT = (bt % 4) * beat
    s1 = barT; s2 = barT - beat * 1.5
    ep = 0
    for f in chords[bar]:
        if s1 >= 0: ep += math.sin(TAU * f * s1 + math.sin(TAU * f * 2 * s1) * 0.4 * exp(-s1 * 3)) * exp(-s1 * 1.6)
        if s2 >= 0: ep += math.sin(TAU * f * s2 + math.sin(TAU * f * 2 * s2) * 0.4 * exp(-s2 * 3)) * exp(-s2 * 2.2) * 0.6
    ep *= 0.05
    bell = math.sin(TAU * mel[bi % 8] * ib) * exp(-ib * 5) * 0.045 if bi % 2 == 0 else 0
    crackle = N() * 0.15 if rnd.random() < 0.0006 else 0
    d.append(max(-1, min(1, (kick + snare + hat + bass + ep + bell + crackle) * 0.9)))

peak = max(abs(x) for x in d) or 1
gain = 0.9 / peak   # the game plays it at 0.6 volume under the effects; a video has nothing else, so normalize
with wave.open(out, 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
    frame = b''.join(struct.pack('<h', int(x * gain * 32767)) for x in d)
    w.writeframes(frame * loops)
print('wrote', out, round(dur * loops, 1), 's')
