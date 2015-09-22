using System;
using PC2D;
using UnityEngine;

public class PlatformerMotor2D : ColliderMotor2d {

    /// <summary>
    /// The velocity of the motor. This should be queried instead of the rigidbody's velocity. Setting this during a dash doesn't
    /// have any meaning.
    /// </summary>
    public Vector2 velocity
    {
        get
        {
            if (motorState == MotorState.Dashing)
            {
                return _dashing.dashDir * GetDashSpeed();
            }

            return _velocity;
        }
        set
        {
            _velocity = value;
        }
    }

    //
    // Jump Properties
    //

    /// <summary>
    /// Delegate to attach to when the motor jumps (ALL JUMPS!).
    /// </summary>
    public Action onJump;

    /// <summary>
    /// Delegate to attach to when the motor air jumps (called before onJump).
    /// </summary>
    public Action onAirJump;

    /// <summary>
    /// Delegate to attach to when the motor walls jumps (called before onJump). The vector passed is the normal of the wall.
    /// </summary>
    public Action<Vector2> onWallJump;

    /// <summary>
    /// Delegate to attach to when the motor corner jumps (called before onJump).
    /// </summary>
    public Action onCornerJump;

    /// <summary>
    /// The height the motor will jump when a jump command is issued.
    /// </summary>
    public float jumpHeight = 1.5f;

    /// <summary>
    /// The extra height the motor will jump if jump is 'held' down.
    /// </summary>
    public float extraJumpHeight = 1.5f;

    /// <summary>
    /// Number of air jumps allowed.
    /// </summary>
    public int numOfAirJumps = 1;

    /// <summary>
    /// The amount of time once the motor has left an environment that a jump will be allowed.
    /// </summary>
    public float jumpWindowWhenFalling = 0.2f;

    /// <summary>
    /// The grace period once the motor is told to jump where it will jump.
    /// </summary>
    public float jumpWindowWhenActivated = 0.2f;

    /// <summary>
    /// If wall jumps are allowed.
    /// </summary>
    public bool enableWallJumps = true;

    /// <summary>
    /// The jump speed multiplier when wall jumping. This is useful to force bigger jumps off of the wall.
    /// </summary>
    public float wallJumpMultiplier = 1f;

    /// <summary>
    /// After a corner or wall jump, this is how longer horizontal input is ignored.
    /// </summary>
    public float ignoreMovementAfterJump = 0.2f;

    /// <summary>
    /// The angle (degrees) in which the motor will jump away from the wall. 0 is horizontal and 90 is straight up.
    /// </summary>
    [Range(0f, 90f)]
    public float wallJumpAngle = 70;

    /// <summary>
    /// The jump speed multiplier when jumping from a corner grab. Useful to forcing bigger jumps.
    /// </summary>
    public float cornerJumpMultiplier = 1f;

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

    //
    // Jump Methods
    //

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
    // Dashing Properties
    //

    /// <summary>
    /// Delegate to attach to when the motor dashes.
    /// </summary>
    public Action onDash;

    /// <summary>
    /// Delegate to attach to when the motor's dash ends.
    /// </summary>
    public Action onDashEnd;

    /// <summary>
    /// Is dashing allowed?
    /// </summary>
    public bool enableDashes = true;

    /// <summary>
    /// How far the motor will dash.
    /// </summary>
    public float dashDistance = 3;

    /// <summary>
    /// How long the dash lasts in seconds.
    /// </summary>
    public float dashDuration = 0.2f;

    /// <summary>
    /// When the motor will be allowed to dash again after dashing. The cooldown begins at the end of a dash.
    /// </summary>
    public float dashCooldown = 0.76f;

    /// <summary>
    /// The easing function used during the dash. Pick 'Linear' for just a set speed.
    /// </summary>
    public EasingFunctions.Functions dashEasingFunction = EasingFunctions.Functions.EaseOutQuad;

    /// <summary>
    /// Delay (in seconds) before gravity is turned back on after a dash.
    /// </summary>
    public float endDashNoGravityDuration = 0.1f;


    // The function is cached to avoid unnecessary memory allocation.
    private EasingFunctions.EasingFunc _dashFunction;
    private EasingFunctions.EasingFunc _dashDerivativeFunction;
    private EasingFunctions.Functions _currentDashEasingFunction;

    // Contains the various dash variables.
    private class DashState
    {
        public bool pressed;
        public float cooldownFrames;
        public int dashingFrames;
        public bool dashWithDirection;
        public Vector2 dashDir = Vector2.zero;
        public float distanceCalculated;
        public float distanceDashed;
        public bool force;
        public float gravityEnabledFrames;
    }
    private DashState _dashing = new DashState();

    /// <summary>
    /// Returns the direction of the current dash. If not dashing then returns Vector2.zero.
    /// </summary>
    public Vector2 dashDirection
    {
        get
        {
            if (motorState == MotorState.Dashing)
            {
                return _dashing.dashDir;
            }

            return Vector2.zero;
        }
    }

    /// <summary>
    /// Returns the amount of distance dashed. If not dashing then returns 0.
    /// </summary>
    public float distanceDashed
    {
        get
        {
            if (motorState == MotorState.Dashing)
            {
                return _dashing.distanceDashed;
            }

            return 0;
        }
    }

    /// <summary>
    /// This is the distance calculated for dashed. Not be confused with distanceDashed. This doesn't care if the motor has
    /// hit a wall.
    /// </summary>
    public float dashDistanceCalculated
    {
        get
        {
            if (motorState == MotorState.Dashing)
            {
                return _dashing.distanceCalculated;
            }

            return 0;
        }
    }

    //
    // Dash methods
    //

    /// <summary>
    /// If the motor is currently able to dash.
    /// </summary>
    public bool canDash
    {
        get { return _dashing.cooldownFrames < 0; }
    }

    /// <summary>
    /// Reset the cooldown for dash.
    /// </summary>
    public void ResetDashCooldown()
    {
        _dashing.cooldownFrames = -1;
    }

    /// <summary>
    /// Call this to have the motor try to dash, once called it will be handled in the FixedUpdate tick.
    /// This causes the object to dash along their facing (if left or right for side scrollers).
    /// </summary>
    public void Dash()
    {
        _dashing.pressed = true;
        _dashing.dashWithDirection = false;
    }

    /// <summary>
    /// Forces the motor to dash regardless if the motor thinks it is valid or not.
    /// </summary>
    public void ForceDash()
    {
        Dash();
        _dashing.force = true;
    }

    /// <summary>
    /// Send a direction vector to dash allow dashing in a specific direction.
    /// </summary>
    /// <param name="dir">The normalized direction of the dash.</param>
    public void Dash(Vector2 dir)
    {
        _dashing.pressed = true;
        _dashing.dashWithDirection = true;
        _dashing.dashDir = dir;
    }

    /// <summary>
    /// Forces a dash along a specified direction.
    /// </summary>
    /// <param name="dir">The normalized direction of the dash.</param>
    public void ForceDash(Vector2 dir)
    {
        Dash(dir);
        _dashing.force = true;
    }

    /// <summary>
    /// Call to end dash immediately.
    /// </summary>
    public void EndDash()
    {
        // If dashing then end now.
        if (motorState == MotorState.Dashing)
        {
            _dashing.cooldownFrames = GetFrameCount(dashCooldown);
            _dashing.pressed = false;
            _dashing.gravityEnabledFrames = GetFrameCount(endDashNoGravityDuration);

            _velocity = _dashing.dashDir * GetDashSpeed();

            if (IsGrounded())
            {
                motorState = MotorState.OnGround;
            }
            else
            {
                motorState = MotorState.Falling;
            }

            if (onDashEnd != null)
            {
                onDashEnd();
            }
        }
    }

    private void SetDashFunctions()
    {
        _dashFunction = EasingFunctions.GetEasingFunction(dashEasingFunction);
        _dashDerivativeFunction = EasingFunctions.GetEasingFunctionDerivative(dashEasingFunction);
        _currentDashEasingFunction = dashEasingFunction;
    }

    private void StartDash()
    {
        // Set facing now and it won't be set again during dash.
        SetFacing();

        if (!_dashing.dashWithDirection)
        {
            // We dash depending on our direction.
            _dashing.dashDir = facingLeft ? -Vector2.right : Vector2.right;
        }

        _dashing.distanceDashed = 0;
        _dashing.distanceCalculated = 0;
        _previousLoc = _collider2D.bounds.center;

        // This will begin the dash this frame.
        _dashing.dashingFrames = GetFrameCount(dashDuration) - 1;
        _dashing.force = false;

        motorState = MotorState.Dashing;

        if (onDash != null)
        {
            onDash();
        }
    }

    private float GetDashSpeed()
    {
        float normalizedTime = (float)(GetFrameCount(dashDuration) - _dashing.dashingFrames) /
            GetFrameCount(dashDuration);

        float speed = _dashDerivativeFunction(0, dashDistance, normalizedTime) / dashDuration;

        // Some of the easing functions may result in infinity, we'll uh, lower our expectations and make it maxfloat.
        // This will almost certainly be clamped.
        if (float.IsNegativeInfinity(speed))
        {
            speed = float.MinValue;
        }
        else if (float.IsPositiveInfinity(speed))
        {
            speed = float.MaxValue;
        }

        return speed;
    }

    //
    // override
    //

    protected override void Awake()
    {
        SetDashFunctions();
        base.Awake();
    }

    protected override void Start()
    {
        base.Start();
        _currentWallJumpDegree = wallJumpAngle;
        _wallJumpVector = Quaternion.AngleAxis(wallJumpAngle, Vector3.forward) * Vector3.right;
    }

    protected override void UpdateTimers()
    {
        base.UpdateTimers();

        // jump
        _jumping.jumpGraceFrames--;
        _jumping.timeToldFrames--;
        _jumping.allowExtraFrames--;

        // dashing
        _dashing.cooldownFrames--;
        _dashing.gravityEnabledFrames--;
        _dashing.dashingFrames--;
    }

    protected override void ReadjustTimers(float multiplier)
    {
        base.ReadjustTimers(multiplier);

        // jump
        _jumping.jumpGraceFrames = Mathf.RoundToInt(_jumping.jumpGraceFrames * multiplier);
        _jumping.timeToldFrames = Mathf.RoundToInt(_jumping.timeToldFrames * multiplier);
        _jumping.allowExtraFrames = Mathf.RoundToInt(_jumping.allowExtraFrames * multiplier);

        // dashing
        _dashing.cooldownFrames = Mathf.RoundToInt(_dashing.cooldownFrames * multiplier);
        _dashing.gravityEnabledFrames = Mathf.RoundToInt(_dashing.gravityEnabledFrames * multiplier);
        _dashing.dashingFrames = Mathf.RoundToInt(_dashing.dashingFrames * multiplier);
    }

    protected override void UpdateState(bool forceSurroundingsCheck, bool updateSurroundings = true)
    {
        // dashing

        // Since this is in UpdateState, we can end dashing if the timer is at 0.
        if (motorState == MotorState.Dashing && _dashing.dashingFrames <= 0)
        {
            EndDash();
        }

        UpdateSurroundings(forceSurroundingsCheck);

        if (motorState == MotorState.Dashing)
        {
            // Still dashing, nothing else matters.
            _dashing.distanceDashed += (_collider2D.bounds.center - _previousLoc).magnitude;
            return;
        }

        base.UpdateState(forceSurroundingsCheck, false);

        // jump
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

        // First, are we trying to dash?
        if (enableDashes &&
            (_dashing.pressed &&
            _dashing.cooldownFrames < 0 &&
            motorState != MotorState.Dashing ||
            _dashing.force))
        {
            StartDash();
        }

        _dashing.pressed = false;

        base.UpdateVelocity();

        if (motorState != MotorState.Dashing)
        {
            // Handle jumping.
            HandlePreJumping();
        }
    }

    protected override float MoveMotor()
    {
        if (motorState == MotorState.Dashing)
        {
            float normalizedTime = (float)(GetFrameCount(dashDuration) - _dashing.dashingFrames) /
                GetFrameCount(dashDuration);

            if (_currentDashEasingFunction != dashEasingFunction)
            {
                // This allows the easing function to change during runtime and cut down on unnecessary allocations.
                SetDashFunctions();
            }

            float distance = _dashFunction(0, dashDistance, normalizedTime);

            _velocity = _dashing.dashDir * GetDashSpeed();
            MovePosition(_collider2D.bounds.center + (Vector3)_dashing.dashDir * (distance - _dashing.distanceCalculated));
            _dashing.distanceCalculated = distance;
            // Right now dash only moves along a line, doesn't ever need to adjust. We don't need multiple iterations for that.
            return 0;
        }

        return base.MoveMotor();
    }

    // TODO @llafuente remove _jumping.ignoreGravity -> global ignoreGravity
    protected override void HandleFalling()
    {
        if (motorState == MotorState.FreedomState)
        {
            return;
        }
        else if (!_jumping.ignoreGravity) {
            if (IsInAir() && !fallFast && _dashing.gravityEnabledFrames < 0) {
                if (_velocity.y == -fallSpeed)
                {
                    return;
                }

                if (_velocity.y > -fallSpeed)
                {
                    _velocity.y = Accelerate(
                        _velocity.y,
                        gravityMultiplier * Physics2D.gravity.y,
                        -fallSpeed);
                }
                else
                {
                    _velocity.y = Decelerate(
                        _velocity.y,
                        Mathf.Abs(gravityMultiplier * Physics.gravity.y),
                        -fallSpeed);
                }
            }

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
