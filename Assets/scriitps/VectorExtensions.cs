using UnityEngine;

public static class VectorExtensions
{
    public static Vector3 XZVector(this Vector3 v) => new(v.x, 0f, v.z);
}
