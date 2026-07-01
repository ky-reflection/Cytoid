using UnityEngine;

/// <summary>
/// Hold progress ring shader driver. Uses per-renderer property blocks so concurrent holds
/// do not stomp each other's fill color/cutoff (sharedMaterial crosstalk — see hold-seek RC-1).
/// </summary>
[ExecuteInEditMode]
public class ProgressRing : MonoBehaviour
{
    [Range(0, 1)] public float maxCutoff;
    [Range(0, 1)] public float fillCutoff;
    public Color fillColor;

    public SpriteRenderer spriteRenderer;
    private int fillCutoffId, fillColorId, maxCutoffId;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        maxCutoffId = Shader.PropertyToID("_MaxCutoff");
        fillColorId = Shader.PropertyToID("_FillColor");
        fillCutoffId = Shader.PropertyToID("_FillCutoff");
    }

    public void OnUpdate()
    {
        spriteRenderer.enabled = true;
        fillCutoff = Mathf.Min(fillCutoff, maxCutoff);
        // MaterialPropertyBlock: isolate shader uniforms per ring instance (Lab + production).
        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.SetFloat(fillCutoffId, fillCutoff);
        propertyBlock.SetFloat(maxCutoffId, maxCutoff);
        propertyBlock.SetColor(fillColorId, fillColor);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    public void Reset()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.SetPropertyBlock(null);
            spriteRenderer.enabled = false;
        }

        maxCutoff = 0;
        fillCutoff = 0;
    }
}
