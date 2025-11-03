using UnityEngine;
using System.Collections;

namespace LuminaryLabs.HologramShieldShader.DemoPrefabs.Helpers
{
    /// <summary>
    /// Automatically fires bullets from a specified point at regular intervals.
    /// </summary>
    public class AutoFireGun : MonoBehaviour
    {
        // Transform representing the muzzle position of the gun
        public Transform firePoint;
        
        // Bullet prefab (should be a sphere with a Rigidbody and collider on a child)
        public GameObject bulletPrefab;
        
        // Speed at which the bullet is fired
        public float bulletSpeed = 20f;
        
        // Time interval between each shot (in seconds)
        public float fireInterval = 0.5f;

        void Start()
        {
            // Start the automatic firing coroutine
            StartCoroutine(FireRoutine());
        }

        IEnumerator FireRoutine()
        {
            // Loop infinitely
            while (true)
            {
                Fire();
                // Wait for the specified interval before firing the next bullet
                yield return new WaitForSeconds(fireInterval);
            }
        }

        void Fire()
        {
            // Instantiate bullet prefab at firePoint's position and rotation
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
            Destroy(bullet, 3f); // Destroy the bullet after 2 seconds to prevent clutter
            
            // Get the Rigidbody component to set the initial velocity
            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = firePoint.forward * bulletSpeed;
            }
            else
            {
                Debug.LogWarning("Bullet prefab is missing a Rigidbody component.");
            }
        }
    }
}
