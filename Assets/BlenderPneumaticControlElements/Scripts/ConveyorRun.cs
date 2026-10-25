using UnityEngine;

public class ConveyorRun : MonoBehaviour
{
    [Header("Conveyor Settings")]
    [SerializeField] private float speed = 2.0f;
    [SerializeField] private Vector3 localDirection = Vector3.forward;

    [Header("Visuals (Optional)")]
    [SerializeField] private Renderer beltRenderer;
    [SerializeField] private string texturePropertyName = "_MainTex";

    private Material beltMaterial;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (beltRenderer != null)
        {
            beltMaterial = beltRenderer.material;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Scroll the texture for visual motion
        if (beltMaterial != null && beltMaterial.HasProperty(texturePropertyName))
        {
            Vector2 offset = beltMaterial.GetTextureOffset(texturePropertyName);
            offset.y -= speed * Time.deltaTime;
            beltMaterial.SetTextureOffset(texturePropertyName, offset);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Rigidbody rb = collision.rigidbody;

        // Move physics objects riding on the conveyor
        if (rb != null && !rb.isKinematic)
        {
            Vector3 worldDirection = transform.TransformDirection(localDirection.normalized);
            Vector3 targetPosition = rb.position + worldDirection * speed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }
    }
}