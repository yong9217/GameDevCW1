using UnityEngine;

// Attached only to the crystal mesh; its base and discharge anchor stay fixed.
public sealed class TowerPreviewCrystalSpin : MonoBehaviour
{
    public float degreesPerSecond = 30f;
    Vector3 localCenter;
    void Awake()
    {
        var mesh=GetComponent<MeshFilter>();
        localCenter=mesh&&mesh.sharedMesh ? mesh.sharedMesh.bounds.center : Vector3.zero;
    }
    void Update()
    {
        transform.RotateAround(transform.TransformPoint(localCenter),Vector3.up,degreesPerSecond*Time.deltaTime);
    }
}
