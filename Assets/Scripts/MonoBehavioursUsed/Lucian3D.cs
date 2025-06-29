using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Lucian3D : MonoBehaviour
{
    [Header("Data")]
    public CharacterData lucianData;
    public Material originalLucianMaterial;
    public Material electricityPowerMaterial;

    [Header("Animation")]
    private int currentPriorityIndex = 0;

    [Header("Movement")]
    public float acceleration = 100f;
    public float deceleration = 80f;
    public float maxSpeed = 30f;
    public float airControl = 0.4f;
    public float groundFriction = 8f;
    public Vector2 orientation;
    private Vector2 velocity;
    private Vector2 lastOrientation;
    public bool isMoving;
    public bool wasMoving;
    public bool isFacingRight = true;
    private float currentGravityScale;
    public Vector2 moveInput;

    [Header("Jump")]
    public bool isJumping = false;
    public bool canDoubleJump = true;
    public bool isWallJumping = false;
    public float jumpBufferTime = 0.2f;
    public Vector3 wallJumpForce = new Vector3(30f,30f,0f);

    [Header("Wall Slide")]
    public LayerMask whatIsWall;
    public float wallSlideSpeed;
    public bool isWallSliding;

    public void SetLucianOnRageMaterial()
    {
        if (transform.childCount >= 2)
        {
            Transform child2 = transform.GetChild(1);
            SpriteRenderer[] spriteRenderers = child2.GetComponentsInChildren<SpriteRenderer>();
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                spriteRenderer.material = electricityPowerMaterial;
            }
        }
    }
}