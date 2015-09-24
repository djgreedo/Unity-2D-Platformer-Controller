using System;
using PC2D;
using UnityEngine;

public class MovableObjectMotor2D : ColliderMotor2d {
    /// <summary>
    /// Layer of movable Objects
    /// </summary>
    public LayerMask playerLayerMask;

    protected override void Start()
    {
        base.Start();
        motorState = MotorState.Falling;
        _validCollisionLayerMask = staticEnvLayerMask | movingPlatformLayerMask | playerLayerMask;
    }

    private bool IsPlayer(GameObject obj)
    {
        return ((0x1 << obj.layer) & playerLayerMask) != 0;
    }

    protected override void UpdateState(bool forceSurroundingsCheck, bool updateSurroundings = true) {
        base.UpdateState(forceSurroundingsCheck, updateSurroundings);

        // PressingIntoLeftWall() &&
        if (_collidersUpAgainst[DIRECTION_LEFT] && IsPlayer(_collidersUpAgainst[DIRECTION_LEFT].gameObject))
        {
            PlatformerMotor2D player = _collidersUpAgainst[DIRECTION_LEFT].gameObject.GetComponent<PlatformerMotor2D>();
            Debug.Log("player at left side");

            // nothing at right side...

            // player is pushing? -> move
        }

        if (_collidersUpAgainst[DIRECTION_RIGHT] && IsPlayer(_collidersUpAgainst[DIRECTION_RIGHT].gameObject))
        {
            Debug.Log("player at right side");
        }


    }
}
