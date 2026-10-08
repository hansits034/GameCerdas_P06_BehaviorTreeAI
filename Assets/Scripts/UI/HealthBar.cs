using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private GameObject target;      
    [SerializeField] private Image fill;
    [SerializeField] private bool faceCamera = true; 
    [SerializeField] private Color fullColor = Color.green;
    [SerializeField] private Color emptyColor = Color.red;

    private IHealth health;
    private Camera cam;

    private void Awake()
    {
        health = target != null
            ? target.GetComponent<IHealth>()
            : GetComponentInParent<IHealth>();

        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (health == null || fill == null)
            return;

        float ratio =
            (float)health.CurrentHealth / health.MaxHealth;

        fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        fill.color = Color.Lerp(emptyColor, fullColor, ratio);

        if (faceCamera && cam != null)
            transform.rotation = cam.transform.rotation;
    }
}