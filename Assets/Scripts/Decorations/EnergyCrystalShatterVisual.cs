using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Faceted green fragments burst, fall and dissolve; pedestal is not destroyed.</summary>
public sealed class EnergyCrystalShatterVisual : MonoBehaviour
{
    private const float Duration = .85f;
    private CrystalMesh shards;
    private Material material;
    private float elapsed;
    public static void Spawn(Scene scene, Vector3 position, float scale, AudioClip clip, float volume)
    {
        var root = new GameObject("Energy Crystal Shatter");
        SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = position;
        root.transform.localScale = Vector3.one * Mathf.Max(.01f, scale);
        root.AddComponent<CameraVisionStreamingExempt>();
        var effect = root.AddComponent<EnergyCrystalShatterVisual>();
        effect.material = new Material(Shader.Find("Sprites/Default"));
        effect.shards = new CrystalMesh(root.transform, "Green Crystal Fragments", effect.material, 2);
        effect.WriteFrame(0);
        if (clip == null) clip = Resources.Load<AudioClip>("Audio/EnergyCrystalShatter");
        if (clip != null)
        {
            var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false;
            audio.clip = clip; audio.volume = volume; audio.spatialBlend = 1; audio.dopplerLevel = 0;
            audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 2; audio.maxDistance = 18; audio.Play();
        }
        Destroy(root, Mathf.Max(Duration, clip != null ? clip.length+.05f : 0));
    }
    private void Update() { elapsed += Time.deltaTime; WriteFrame(elapsed); }
    private void WriteFrame(float time)
    {
        if (shards == null) return;
        shards.Clear();
        float progress = Mathf.Clamp01(time/Duration);
        for(int i=0;i<14;i++)
        {
            float angle = i*2.39996f;
            float radius = (.32f + (i%4)*.15f)*(1-Mathf.Pow(1-progress,2));
            Vector2 center = new Vector2(Mathf.Cos(angle)*radius, Mathf.Sin(angle)*radius+.42f*Mathf.Sin(progress*Mathf.PI)-progress*progress*.48f);
            float spin=angle+time*(i%2==0?5:-6);
            Vector2 a=new Vector2(Mathf.Cos(spin),Mathf.Sin(spin))*(.08f+(i%3)*.025f)*(1-progress*.4f);
            Vector2 b=new Vector2(-a.y,a.x)*.55f;
            Color color = Color.Lerp(new Color(.06f,.7f,.28f),new Color(.64f,1,.72f),(i%3)*.5f);
            color.a=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1,progress));
            shards.Triangle(center+a,center-a+b,center-a-b,color);
        }
        if(time<.12f)shards.Quad(Vector2.zero,Vector2.one*(.3f+time*3),new Color(.7f,1,.78f,(1-time/.12f)*.9f));
        shards.Upload();
    }
    private void OnDestroy() { shards?.Dispose(); CrystalMesh.Release(material); }
}
