using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// Simple player controller with Photon networking.
// - W: move forward
// - A/D: rotate left/right (yaw)
// - Local player controls movement; remote players are smoothly interpolated using networked position/rotation from OnPhotonSerializeView.
[RequireComponent(typeof(PhotonView))]
public class PlayerController : MonoBehaviourPun, IPunObservable
{
    [Header("Movement")]
    public float moveSpeed = 3f; // units per second
    public float rotationSpeed = 120f; // degrees per second

    [Header("Smoothing (remote)")]
    public float lerpRate = 10f;

    [Header("Runtime refs")]
    public Camera playerCamera; // assignable in prefab; will be enabled only for local player

    Vector3 networkPosition;
    Quaternion networkRotation;
    public FixedJoystick joystick;

    HandStateReceiver receiver;

    void Start()
    {
        // Find HandStateReceiver in the scene
        receiver = FindObjectOfType<HandStateReceiver>();

        // Ensure camera is only active for the local player
        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(photonView.IsMine);
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            HandleInput();
        }
        else
        {
            // smooth remote transforms
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * lerpRate);
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * lerpRate);
        }
    }

    void HandleInput()
    {
        float forward = 0f;
        if (Input.GetKey(KeyCode.W)) forward = 1f;

        if (receiver != null)
        {
            string state = receiver.currentState;  // "KICK", "RUN", "NONE" など
            float confidence = receiver.currentConfidence;

            if (state == "RUN" && confidence > 0.7f)
            {
                forward = 1f;
            }
        }

        float turn = 0f;
        if (Input.GetKey(KeyCode.A)) turn = -1f;
        else if (Input.GetKey(KeyCode.D)) turn = 1f;
        else if (joystick != null) turn = 2 * joystick.Horizontal;

        if (forward != 0f)
        {
            transform.Translate(Vector3.forward * forward * moveSpeed * Time.deltaTime);
        }
        if (turn != 0f)
        {
            transform.Rotate(Vector3.up, turn * rotationSpeed * Time.deltaTime);
        }
    }

    // Photon serialization - send/receive transform
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
