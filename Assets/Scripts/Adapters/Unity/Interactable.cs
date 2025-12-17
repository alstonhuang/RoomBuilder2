using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    public UnityEvent onInteract;
    public Outline outlineScript;
    public bool debugInteract = false;
    public bool debugFocus = false;
    public bool useFallbackHighlight = true;
    public Color fallbackColor = new Color(1f, 0.92f, 0.16f, 1f);

    private Renderer[] _highlightRenderers;
    private MaterialPropertyBlock _mpb;
    private readonly System.Collections.Generic.Dictionary<Renderer, Color> _originalMaterialColors =
        new System.Collections.Generic.Dictionary<Renderer, Color>();

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        EnsureOutlineCached();
    }

    public void OnInteract()
    {
        if (debugInteract) Debug.Log($"[Interactable] OnInteract on {name}");

        if (onInteract != null && onInteract.GetPersistentEventCount() > 0)
        {
            onInteract.Invoke();
            return;
        }

        var key = GetComponent<KeyController>();
        if (key != null)
        {
            key.PickUp();
            return;
        }

        var door = GetComponent<DoorController>();
        if (door != null)
        {
            door.TryOpen();
            return;
        }

        onInteract?.Invoke();
    }

    public void OnFocus()
    {
        EnsureOutlineCached();
        if (outlineScript != null) outlineScript.enabled = true;
        if (useFallbackHighlight) SetFallbackHighlight(true);
        if (debugFocus) Debug.Log($"[Interactable] Focus {name} outline={(outlineScript != null ? "on" : "missing")}");
    }

    public void OnLoseFocus()
    {
        if (outlineScript != null) outlineScript.enabled = false;
        if (useFallbackHighlight) SetFallbackHighlight(false);
    }

    private void EnsureOutlineCached()
    {
        if (outlineScript == null) outlineScript = GetComponentInChildren<Outline>(true);

        if (outlineScript == null)
        {
            var renderer = GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                outlineScript = renderer.GetComponent<Outline>();
                if (outlineScript == null) outlineScript = renderer.gameObject.AddComponent<Outline>();
            }
        }

        if (outlineScript != null)
        {
            outlineScript.OutlineMode = Outline.Mode.OutlineAll;
            outlineScript.OutlineColor = Color.yellow;
            outlineScript.OutlineWidth = 5f;
            outlineScript.enabled = false;
        }
    }

    private void EnsureHighlightRenderers()
    {
        if (_highlightRenderers != null && _highlightRenderers.Length > 0) return;
        _highlightRenderers = GetComponentsInChildren<Renderer>(true);
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    private void SetFallbackHighlight(bool on)
    {
        EnsureHighlightRenderers();
        if (_highlightRenderers == null) return;

        for (int i = 0; i < _highlightRenderers.Length; i++)
        {
            var r = _highlightRenderers[i];
            if (r == null) continue;
            var mat = r.sharedMaterial;
            bool canUseMpb = mat != null && (mat.HasProperty(ColorId) || mat.HasProperty(BaseColorId));
            if (on)
            {
                if (canUseMpb)
                {
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(ColorId, fallbackColor);
                    _mpb.SetColor(BaseColorId, fallbackColor);
                    r.SetPropertyBlock(_mpb);
                }
                else
                {
                    if (!_originalMaterialColors.ContainsKey(r) && r.material != null)
                    {
                        var m = r.material;
                        if (m.HasProperty(ColorId)) _originalMaterialColors[r] = m.GetColor(ColorId);
                        else if (m.HasProperty(BaseColorId)) _originalMaterialColors[r] = m.GetColor(BaseColorId);
                        else _originalMaterialColors[r] = Color.white;

                        if (m.HasProperty(ColorId)) m.SetColor(ColorId, fallbackColor);
                        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, fallbackColor);
                    }
                }
            }
            else
            {
                r.SetPropertyBlock(null);
                if (_originalMaterialColors.TryGetValue(r, out var original) && r.material != null)
                {
                    var m = r.material;
                    if (m.HasProperty(ColorId)) m.SetColor(ColorId, original);
                    if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, original);
                }
            }
        }

        if (!on) _originalMaterialColors.Clear();
    }
}
