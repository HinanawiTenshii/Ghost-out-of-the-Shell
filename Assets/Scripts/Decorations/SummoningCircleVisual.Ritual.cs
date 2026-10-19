using UnityEngine;

/// <summary>Deterministic, save-clock-driven square particles in two reused meshes.</summary>
public sealed partial class SummoningCircleVisual
{
    private CrystalMesh ritualParticles, ritualGlow;
    private static readonly Color RitualPurple = new Color(.54f, .16f, .94f);
    private static readonly Color RitualWhite = new Color(.95f, .81f, 1);

    private void DrawRitual()
    {
        ritualParticles.Clear(); ritualGlow.Clear();
        if (circle != null && circle.IsRitualActive)
        {
            float t = circle.StageElapsed, p = circle.StageProgress;
            if (circle.Stage == SummoningCircle.RitualStage.Gathering) DrawGathering(t, p);
            else if (circle.Stage == SummoningCircle.RitualStage.Erupting) DrawEruption(t, p);
            else DrawDispersal(t, p);
        }
        ritualParticles.Upload(); ritualGlow.Upload();
    }
    private void Spark(Vector2 point, float size, float brightness, float alpha, bool trail = false)
    {
        if (alpha <= .005f || size <= .002f) return;
        Color color = Color.Lerp(RitualPurple, RitualWhite, brightness); color.a = alpha;
        // Upright squares retain the established pixel-particle vocabulary.
        ritualParticles.Quad(point, Vector2.one * size, color);
        if (trail)
        {
            color.a *= .4f;
            ritualParticles.Quad(point - Vector2.up * size * 1.1f, Vector2.one * size * .7f, color);
        }
        color.a = alpha * .32f;
        ritualGlow.Quad(point, Vector2.one * size * 3.8f, color);
    }
    private void DrawGathering(float time, float progress)
    {
        const int perStream = 40;
        for (int crystal = 0; crystal < 6; crystal++)
        {
            Vector2 start = Tip(crystal), side = new Vector2(-start.y, start.x).normalized;
            for (int i = 0; i < perStream; i++)
            {
                float a = Random01(803 + crystal, i, 11), b = Random01(803 + crystal, i, 103);
                float age = time * (1.1f + b * .25f) - i / (float)perStream;
                if (age < 0) continue;
                float u = Mathf.Repeat(age, 1);
                Vector2 point = start * (1 - u) + side * (Mathf.Sin(u * Mathf.PI) * (.14f + .22f * a) * Mathf.Sin(u * 5 + a * 6));
                Spark(point, (.045f + a * .065f) * (.75f + progress * .3f), .15f + u * .65f,
                    Mathf.Clamp01(u * 10) * (1 - u * .25f));
            }
        }
        DrawCore(time, .15f + progress * .45f, .3f + progress * .7f);
    }
    private void DrawCore(float time, float size, float opacity)
    {
        ritualGlow.Quad(Vector2.zero, Vector2.one * size * 5, new Color(.68f, .25f, 1, opacity * .32f));
        for (int i = 0; i < 52; i++)
        {
            float a = Random01(821, i, 13), b = Random01(821, i, 59);
            float angle = a * Mathf.PI * 2 + time * (1.2f + b);
            Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (Mathf.Sqrt(b) * size);
            Spark(position, .045f + .085f * (1 - b), 1 - b * .7f, opacity * (.65f + .35f * Mathf.Sin(time * 5 + i) * Mathf.Sin(time * 5 + i)));
        }
    }
    private void DrawEruption(float time, float progress)
    {
        float onset = Mathf.Clamp01(time / .3f);
        float height = radius * (1.8f + .3f * Mathf.Sin(progress * Mathf.PI));
        // Projected height points up the screen, even if the seal is rotated in the editor.
        Vector2 up = transform.InverseTransformVector(Vector3.up * Mathf.Abs(transform.lossyScale.y));
        Vector2 right = new Vector2(up.y, -up.x);
        for (int i = 0; i < 240; i++)
        {
            float a = Random01(827, i, 71), b = Random01(827, i, 119), c = Random01(827, i, 281);
            float age = time * (1 + c * .55f) - b;
            if (age < 0) continue;
            float u = Mathf.Repeat(age, 1);
            float spread = (.1f + .5f * u * u) * radius;
            Vector2 point = up * (u * height) + right * ((a - .5f) * spread * 2 + Mathf.Sin(u * 8 + c * 6) * .08f);
            float fade = Mathf.Clamp01((1 - u) * 4) * Mathf.Clamp01(u * 14) * onset;
            Spark(point, (.055f + .09f * c) * (1 - u * .4f), .25f + (1 - u) * .7f, fade, true);
        }
        DrawCore(time, .5f + .06f * Mathf.Sin(time * 19), onset);
    }
    private void DrawDispersal(float time, float progress)
    {
        float distance = radius * (1 - Mathf.Exp(-progress * 3.5f)) * 2;
        float fade = 1 - Mathf.SmoothStep(.05f, 1, progress);
        for (int i = 0; i < 250; i++)
        {
            float a = Random01(839, i, 97), b = Random01(839, i, 271), c = Random01(839, i, 659);
            float angle = a * Mathf.PI * 2;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 point = direction * (distance * (.25f + .75f * b));
            // Most particles fan out from the ground; the remaining jet unravels from above.
            if (i % 4 == 0) point += (Vector2)transform.InverseTransformVector(Vector3.up * radius * c * 1.7f) * (1 - progress * .5f);
            Spark(point, (.055f + c * .1f) * Mathf.Sqrt(Mathf.Max(0, 1 - progress)), .3f + c * .65f, fade);
        }
        ritualGlow.Quad(Vector2.zero, Vector2.one * radius * (1 + progress * 2.8f), new Color(.78f, .43f, 1, fade * .48f));
    }
}
