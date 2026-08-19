using UnityEngine;

public class DoorFragmentVisual : MonoBehaviour
{
    private static Sprite fragmentSprite;

    private SpriteRenderer spriteRenderer;
    private Color initialColor;
    private Vector2 velocity;
    private float lifetime = 0.45f;
    private float rotationSpeed = 360f;
    private float age;

    public static Sprite FragmentSprite
    {
        get
        {
            if (fragmentSprite == null)
            {
                fragmentSprite = CreateFragmentSprite();
            }

            return fragmentSprite;
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            initialColor = spriteRenderer.color;
        }
    }

    public void Configure(Vector2 initialVelocity, float duration)
    {
        Configure(initialVelocity, duration, 360f);
    }

    public void Configure(Vector2 initialVelocity, float duration, float initialRotationSpeed)
    {
        velocity = initialVelocity;
        lifetime = Mathf.Max(0.01f, duration);
        rotationSpeed = initialRotationSpeed;
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        if (spriteRenderer != null)
        {
            float fadeProgress = Mathf.Clamp01(age / lifetime);
            Color color = initialColor;
            color.a = Mathf.Lerp(initialColor.a, 0f, fadeProgress);
            spriteRenderer.color = color;
        }

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private static Sprite CreateFragmentSprite()
    {
        Texture2D texture = new Texture2D(4, 4);
        texture.filterMode = FilterMode.Point;

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
