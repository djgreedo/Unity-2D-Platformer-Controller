using System;
using PC2D;
using UnityEngine;

public class PlatformerMotor2D : ColliderMotor2d {

    //
    // Jumps
    //

    /// <summary>
    /// If jumpingHeld is set to true then the motor will jump further. Set to false if jumping isn't 'held'.
    /// </summary>
    public bool jumpingHeld
    {
        get
        {
            return _jumping.held;
        }

        set
        {
            // Since we set held to true on pressed, we only set to false here. This prevent held from being set after a release.
            if (!value)
            {
                _jumping.held = false;
            }

        }
    }

    /// <summary>
    /// Returns the amount the motor has jumped. This ceases to keep calculating after the motor starts to come down.
    /// </summary>
    public float amountJumpedFor { get; private set; }

    // Contains the various jump variables, this is for organization.
    private class JumpState
    {
        public bool pressed;
        public bool held;
        public int numAirJumps;

        public int timeToldFrames;
        public int allowExtraFrames;

        public bool force;
        public float height;

        public bool ignoreGravity;

        public float jumpGraceFrames;
        public bool jumpTypeChanged;

        public JumpType lastValidJump
        {
            get { return _lastValidJump; }
            set
            {
                if (value != JumpType.None)
                {
                    jumpTypeChanged = true;
                }
                else
                {
                    jumpGraceFrames = -1;
                }

                _lastValidJump = value;
            }
        }

        public enum JumpType
        {
            None,
            Normal,
            RightWall,
            LeftWall,
            Corner
        }

        private JumpType _lastValidJump;
    }
    private JumpState _jumping = new JumpState();
    private float _currentWallJumpDegree;
    private Vector2 _wallJumpVector;

    /// <summary>
    /// Call this to have the GameObject try to jump, once called it will be handled in the FixedUpdate tick. The y axis is
    /// considered jump.
    /// </summary>
    public void Jump()
    {
        _jumping.pressed = true;
        _jumping.timeToldFrames = GetFrameCount(jumpWindowWhenActivated);
        _jumping.height = jumpHeight;

        // Consider jumping held in case there are multiple fixed ticks before the next update tick.
        // This is useful as jumpingHeld may not be set to true with a GetButton() call.
        _jumping.held = true;
    }

    /// <summary>
    /// Jump that allows a custom height.
    /// </summary>
    /// <param name="customHeight">The height the motor should jump to. The extraJumpHeight is still applicable.</param>
    public void Jump(float customHeight)
    {
        Jump();
        _jumping.height = customHeight;
    }

    /// <summary>
    /// This will force a jump to occur even if the motor doesn't think a jump is valid. This function will not work if the motor
    /// is dashing.
    /// </summary>
    public void ForceJump()
    {
        Jump();
        _jumping.force = true;
    }

    /// <summary>
    /// Force a jump with a custom height.
    /// </summary>
    /// <param name="customHeight">The height the motor should jump to. The extraJumpHeight is still applicable.</param>
    public void ForceJump(float customHeight)
    {
        ForceJump();
        _jumping.height = customHeight;
    }

    /// <summary>
    /// Call to end a jump. Causes the motor to stop calculated held speed for a jump.
    /// </summary>
    public void EndJump()
    {
        if (motorState == MotorState.Jumping)
        {
            _jumping.pressed = false;
            motorState = MotorState.Falling;
            _jumping.timeToldFrames = -1;
            _jumping.numAirJumps = 0;
        }
    }

    /// <summary>
    /// Resets the state for air jumps by setting the counter to 0.
    /// </summary>
    public void ResetAirJump()
    {
        _jumping.numAirJumps = 0;
    }

    private void SetLastJumpType()
    {
        if (motorState == MotorState.OnGround)
        {
            _jumping.lastValidJump = JumpState.JumpType.Normal;
        }
        else if (enableWallJumps)
        {
            if (PressingIntoLeftWall())
            {
                _jumping.lastValidJump = JumpState.JumpType.LeftWall;
            }
            else if (PressingIntoRightWall())
            {
                _jumping.lastValidJump = JumpState.JumpType.RightWall;
            }
        }
        else if (motorState == MotorState.OnCorner)
        {
            _jumping.lastValidJump = JumpState.JumpType.Corner;
        }

        // We don't track air jumps as they are always valid in the air.
        if (_jumping.jumpTypeChanged && _jumping.lastValidJump != JumpState.JumpType.None)
        {
            _jumping.jumpTypeChanged = false;
            _jumping.jumpGraceFrames = GetFrameCount(jumpWindowWhenFalling);
        }
    }

    private void HandlePreJumping()
    {
        if (_jumping.timeToldFrames >= 0)
        {
            _jumping.pressed = true;
        }

        _jumping.ignoreGravity = false;

        if (_currentWallJumpDegree != wallJumpAngle)
        {
            _wallJumpVector = Quaternion.AngleAxis(wallJumpAngle, Vector3.forward) * Vector3.right;
            _currentWallJumpDegree = wallJumpAngle;
        }

        // If we're currently jumping and the jump button is still held down ignore gravity to allow us to achieve the extra
        // height.
        if (motorState == MotorState.Jumping && _jumping.held && _jumping.allowExtraFrames > 0)
        {
            _jumping.ignoreGravity = true;
        }

        // Jump?
        if (_jumping.pressed)
        {
            bool jumped = true;

            // Jump might mean different things depending on the state.
            if ((_jumping.lastValidJump == JumpState.JumpType.Normal && _jumping.jumpGraceFrames >= 0) ||
                motorState == MotorState.OnGround ||
                motorState == MotorState.Slipping ||
                _jumping.force)
            {
                // Normal jump.
                if (IsSlipping())
                {
                    _velocity = slopeNormal * CalculateSpeedNeeded(_jumping.height);
                }
                else
                {
                    _velocity.y = CalculateSpeedNeeded(_jumping.height);
                }
            }
            else if (motorState == MotorState.OnCorner ||
                     _jumping.lastValidJump == JumpState.JumpType.Corner && _jumping.jumpGraceFrames >= 0)
            {
                // If we are on a corner then jump up.
                _velocity = Vector2.up * CalculateSpeedNeeded(_jumping.height) * cornerJumpMultiplier;
                _ignoreMovementFrames = GetFrameCount(ignoreMovementAfterJump);

                if (onCornerJump != null)
                {
                    onCornerJump();
                }
            }
            else if (enableWallJumps &&
                ((_jumping.lastValidJump == JumpState.JumpType.LeftWall && _jumping.jumpGraceFrames >= 0) ||
                (_isValidWallInteraction && PressingIntoLeftWall())))
            {
                // If jump was pressed as we or before we entered the wall then just jump away.
                _velocity = _wallJumpVector * CalculateSpeedNeeded(_jumping.height) * wallJumpMultiplier;

                // It's likely the player is still pressing into the wall, ignore movement for a little amount of time.
                // TODO: Only ignore left movement?
                _ignoreMovementFrames = GetFrameCount(ignoreMovementAfterJump);

                // If wall jump is allowed but not wall slide then double jump will not be allowed earlier, allow it now.
                _jumping.numAirJumps = 0;

                if (onWallJump != null)
                {
                    onWallJump(Vector2.right);
                }
            }
            else if (enableWallJumps &&
                ((_jumping.lastValidJump == JumpState.JumpType.RightWall && _jumping.jumpGraceFrames >= 0) ||
                (_isValidWallInteraction && PressingIntoRightWall())))
            {

                _velocity = _wallJumpVector * CalculateSpeedNeeded(_jumping.height) * wallJumpMultiplier;
                _velocity.x *= -1;

                _ignoreMovementFrames = GetFrameCount(ignoreMovementAfterJump);
                _jumping.numAirJumps = 0;

                if (onWallJump != null)
                {
                    onWallJump(-Vector2.right);
                }
            }
            else if (_jumping.numAirJumps < numOfAirJumps)
            {
                _velocity.y = CalculateSpeedNeeded(_jumping.height);
                _jumping.numAirJumps++;

                if (onAirJump != null)
                {
                    onAirJump();
                }
            }
            else
            {
                // Guess we aren't jumping!
                jumped = false;
            }

            if (jumped)
            {
                _jumping.pressed = false;
                _jumping.force = false;
                _jumping.allowExtraFrames = GetFrameCount(extraJumpHeight / CalculateSpeedNeeded(_jumping.height));
                amountJumpedFor = 0;
                motorState = MotorState.Jumping;
                _movingPlatformState.platform = null;
                _jumping.lastValidJump = JumpState.JumpType.None;
                _jumping.timeToldFrames = -1;

                if (onJump != null)
                {
                    onJump();
                }
            }
        }

        _jumping.pressed = false;
    }

    private float CalculateSpeedNeeded(float height)
    {
        return Mathf.Sqrt(-2 * height * gravityMultiplier * Physics2D.gravity.y);
    }

    //
    // Jumps override
    //

    protected override void Start()
    {
        base.Start();
        _currentWallJumpDegree = wallJumpAngle;
        _wallJumpVector = Quaternion.AngleAxis(wallJumpAngle, Vector3.forward) * Vector3.right;
    }

    protected override void UpdateTimers()
    {
        base.UpdateTimers();
        _jumping.jumpGraceFrames--;
        _jumping.timeToldFrames--;
        _jumping.allowExtraFrames--;
    }

    protected override void ReadjustTimers(float multiplier)
    {
        base.ReadjustTimers(multiplier);
        _jumping.jumpGraceFrames = Mathf.RoundToInt(_jumping.jumpGraceFrames * multiplier);
        _jumping.timeToldFrames = Mathf.RoundToInt(_jumping.timeToldFrames * multiplier);
        _jumping.allowExtraFrames = Mathf.RoundToInt(_jumping.allowExtraFrames * multiplier);
    }

    protected override void UpdateState(bool forceSurroundingsCheck)
    {
        base.UpdateState(forceSurroundingsCheck);
        // If our state is not in the air then open up the possibility of air jumps (we need to be able to air jump if
        // we walk off an edge so it can't be based of when a jump occurred).
        if (!IsInAir())
        {
            _jumping.numAirJumps = 0;
        }

        SetLastJumpType();
    }

    protected override void UpdateVelocity()
    {
        Debug.Log("Plataformer!");

        base.UpdateVelocity();
        if (motorState != MotorState.Dashing)
        {
            // Handle jumping.
            HandlePreJumping();
        }
    }

    protected override void HandleFalling()
    {
        if (motorState == MotorState.FreedomState)
        {
            return;
        }
        else if (!_jumping.ignoreGravity) {
            base.HandleFalling();
        }
    }

    protected override void UpdateInformationFromMovement()
    {
        float diffInPositions = Mathf.Abs(_collider2D.bounds.center.y - _previousLoc.y);

        base.UpdateInformationFromMovement();
        // Jumps
        if (motorState == MotorState.Jumping && _velocity.y <= 0)
        {
            motorState = MotorState.Falling;
        }

        if (motorState == MotorState.Jumping)
        {
            amountJumpedFor += diffInPositions;
        }
    }

    protected override bool IsInAir()
    {
        return motorState == MotorState.Jumping || base.IsInAir();
    }

    protected override void HandlePreWallInteraction()
    {
        if (!(motorState == MotorState.Jumping || _wallInfo.wallInteractionCooldownFrames >= 0 || HasFlag(CollidedSurface.Ground)))
        {
            base.HandlePreWallInteraction();
        }
    }
    protected override bool IsGrounded()
    {
        return motorState != MotorState.Jumping && base.IsGrounded();
    }

}
