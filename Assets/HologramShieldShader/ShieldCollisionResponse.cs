using System.Collections.Generic;
using UnityEngine;

namespace LuminaryLabs.HologramShieldShader
{
    public class ShieldCollisionResponse : MonoBehaviour
    {
        // Reference to the shield's renderer
        private Renderer shieldRenderer;
        // Instance of the material (unique per shield)
        private Material shieldMaterial;

        // Duration for which each collision effect lasts (in seconds)
        public float effectDuration = 1f;
        // Collision effect parameters
        public float collisionRadius = 0.5f;
        public float collisionIntensity = 1f;

        // Maximum collisions the shader supports (must match shader's MAX_COLLISIONS)
        private const int MAX_COLLISIONS = 4;

        // Data structure for individual collision events
        private class CollisionData
        {
            public Vector3 point;
            public float radius;
            public float intensity;
            public float startTime;
        }

        // List to track active collisions
        private List<CollisionData> activeCollisions = new List<CollisionData>();

        void Awake()
        {
            shieldRenderer = GetComponent<Renderer>();
            // Clone the material so that changes affect only this shield instance
            shieldMaterial = shieldRenderer.material;
        }

        void Update()
        {
            float currentTime = Time.time;

            // Remove collisions whose effect duration has elapsed
            activeCollisions.RemoveAll(c => (currentTime - c.startTime) > effectDuration);

            // Prepare arrays for shader update
            Vector4[] collisionPoints = new Vector4[MAX_COLLISIONS];
            float[] collisionRadii = new float[MAX_COLLISIONS];
            float[] collisionIntensities = new float[MAX_COLLISIONS];
            float[] collisionStartTimes = new float[MAX_COLLISIONS];

            // Fill arrays with active collision data, applying a fade factor
            int count = Mathf.Min(activeCollisions.Count, MAX_COLLISIONS);
            for (int i = 0; i < count; i++)
            {
                CollisionData col = activeCollisions[i];
                // Calculate fade factor (1 at start, 0 at effectDuration)
                float fade = Mathf.Clamp01(1f - ((currentTime - col.startTime) / effectDuration));
                collisionPoints[i] = new Vector4(col.point.x, col.point.y, col.point.z, 0f);
                collisionRadii[i] = col.radius;
                collisionIntensities[i] = col.intensity * fade;
                collisionStartTimes[i] = col.startTime;
            }
            // Fill any remaining array slots with zeros.
            for (int i = count; i < MAX_COLLISIONS; i++)
            {
                collisionPoints[i] = Vector4.zero;
                collisionRadii[i] = 0f;
                collisionIntensities[i] = 0f;
                collisionStartTimes[i] = 0f;
            }

            // Update shader arrays and parameters
            shieldMaterial.SetVectorArray("_CollisionPoints", collisionPoints);
            shieldMaterial.SetFloatArray("_CollisionRadii", collisionRadii);
            shieldMaterial.SetFloatArray("_CollisionIntensities", collisionIntensities);
            shieldMaterial.SetFloatArray("_CollisionStartTimes", collisionStartTimes);
            shieldMaterial.SetInt("_NumCollisions", count);
            shieldMaterial.SetFloat("_EffectDuration", effectDuration);
        }

        // When a collision occurs, add a new collision event.
        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount > 0)
            {
                // Get the first contact point.
                ContactPoint cp = collision.contacts[0];
                Vector3 collisionPoint = cp.point;

                CollisionData newCollision = new CollisionData
                {
                    point = collisionPoint,
                    radius = collisionRadius,
                    intensity = collisionIntensity,
                    startTime = Time.time
                };

                // If there's room, add the new collision.
                if (activeCollisions.Count < MAX_COLLISIONS)
                {
                    activeCollisions.Add(newCollision);
                }
                else
                {
                    // Otherwise, replace the collision with the least remaining time.
                    float minRemaining = float.MaxValue;
                    int replaceIndex = 0;
                    float currentTime = Time.time;
                    for (int i = 0; i < activeCollisions.Count; i++)
                    {
                        float remaining = effectDuration - (currentTime - activeCollisions[i].startTime);
                        if (remaining < minRemaining)
                        {
                            minRemaining = remaining;
                            replaceIndex = i;
                        }
                    }
                    activeCollisions[replaceIndex] = newCollision;
                }
            }
        }
    }
}