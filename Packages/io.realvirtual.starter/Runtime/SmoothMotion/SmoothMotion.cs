// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using UnityEngine;

namespace realvirtual
{
    #region doc
    //! Implements advanced S-curve motion profiles for smooth, jerk-limited movement in industrial automation.

    //! This professional motion control system generates optimized acceleration profiles that minimize mechanical
    //! stress and vibration in high-speed automation equipment. Essential for precise positioning in robotics,
    //! CNC machines, and material handling systems where smooth acceleration transitions prevent product damage
    //! and extend equipment lifetime. Supports real-time trajectory modification and speed override capabilities.
    //!
    //! plan-281: on Windows Editor/Standalone with the Professional package installed, playback authority
    //! (position/velocity/acceleration, effective target, deceleration lead) comes from the rv-smooth-motion
    //! Rust core via RvSmoothMotionNative (f64, see realvirtualReleaseDLLs/rv-smooth-motion/ABI.md). On every
    //! other platform, or if the native DLL fails to load, SmoothMotion transparently falls back to the
    //! original managed 7-segment kernel (SCurveGenerator7Segment/SCurve) - the two backends never mix within
    //! one active profile. `profile` is always populated by the managed generator (compatibility/debug
    //! snapshot, ABI v1 does not expose per-segment phase data across the boundary) so existing consumers of
    //! `profile.targetPosition`/`profile.duration`/`profile.EvaluateExact()` keep working unchanged regardless
    //! of which backend drives `_state`.
    #endregion
    [System.Serializable]
    public class SmoothMotion : IDisposable
    {
        [HideInInspector] public float maxVelocity = 5f;
        [HideInInspector] public float maxAcceleration = 2f;
        [HideInInspector] public float jerk = 5f;

        public SCurve profile;

        public MotionState _state = new MotionState();
        public MotionState _targetState = new MotionState();
        [HideInInspector] public float speedOverride = 1;

        private float _time;
        private float durationScale;

#if REALVIRTUAL_PROFESSIONAL
        [NonSerialized] private RvSmoothMotionNative _native;
        [NonSerialized] private bool _hasNativeSnapshot;
        [NonSerialized] private RvSmoothMotionNative.Snapshot _nativeSnapshot;

        //! True while this instance is actively backed by the native Rust context. Debug/Inspector use only.
        public bool IsNativeBackend => _native != null && _native.IsValid;

        //! Lazily creates (or recreates, e.g. after a domain reload wiped the [NonSerialized] handle) the
        //! native context and pushes the current limits into it. No-op, returns false, if the native DLL is
        //! unavailable on this platform - callers fall back to the managed path in that case.
        private bool EnsureNative()
        {
            if (!RvSmoothMotionNative.IsAvailable)
                return false;

            if (_native == null || !_native.IsValid)
            {
                _native = RvSmoothMotionNative.TryCreate();
                if (_native == null)
                    return false;
            }

            _native.Configure(maxVelocity, maxAcceleration, jerk);
            return true;
        }

        private void RefreshNativeSnapshot()
        {
            if (_native == null || !_native.IsValid) return;
            if (_native.GetSnapshot(out var snapshot) == RvSmoothMotionNative.StatusOk)
            {
                _nativeSnapshot = snapshot;
                _hasNativeSnapshot = true;
                ApplySnapshotToState(snapshot);
            }
        }

        private void ApplySnapshotToState(in RvSmoothMotionNative.Snapshot snapshot)
        {
            _state.position = (float)snapshot.Position;
            _state.velocity = (float)snapshot.Velocity;
            _state.acceleration = (float)snapshot.Acceleration;
        }
#endif

        public void SetTarget(float position, float velocity)
        {
            MotionState state = new MotionState();
            state.position = position;
            state.velocity = velocity;
            state.acceleration = 0;
            durationScale = 1;

            // Always (re)generate the managed profile: it is the compatibility/debug snapshot surface
            // (`profile.targetPosition`/`duration`/`EvaluateExact()`) regardless of which backend actually
            // drives playback - ABI v1 does not expose segment/phase data, so the managed 7-segment kernel
            // remains the only source for those fields (plan-281 §2.5 API-Kompatibilitätsmatrix).
            InitTransitionToState(state);

#if REALVIRTUAL_PROFESSIONAL
            if (EnsureNative())
            {
                _native.SetState(_state.position, _state.velocity, _state.acceleration);
                _native.SetTarget(position, velocity);
                RefreshNativeSnapshot();
            }
#endif
        }

        public bool Integrate(float dt)
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_native != null && _native.IsValid)
            {
                int status = _native.Step(dt, speedOverride, out var snapshot);
                if (status == RvSmoothMotionNative.StatusOk)
                {
                    _nativeSnapshot = snapshot;
                    _hasNativeSnapshot = true;
                    ApplySnapshotToState(snapshot);
                    _time += dt * speedOverride;
                    return true;
                }
                // Non-OK (e.g. INVALID_HANDLE after a domain reload the lazy EnsureNative() in SetTarget
                // has not yet re-run for) - fall through to the managed evaluator below so playback never
                // silently freezes; the next SetTarget()/replan re-syncs the native side.
            }
#endif
            if (profile == null)
                return false;
            _time += dt * speedOverride;
            _state = profile.EvaluateCurves(_time/durationScale);

            return true;
        }

        public void SetInitialPosition(float position)
        {
            _state.position = position;
        }

        public void SetInitialVelocity(float velocity)
        {
            _state.velocity = velocity;
        }

        public void SetInitialAcceleration(float acceleration)
        {
            _state.acceleration = acceleration;
        }

        public void AdjustDuration(float duration)
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_native != null && _native.IsValid)
            {
                // F15 clamp contract: the native side clamps to the physically minimal profile time
                // instead of ever exceeding v/a/jerk limits (unlike the managed ratio below, which is
                // the frozen legacy behaviour and intentionally left unchanged for the fallback path).
                _native.AdjustDuration(duration);
                RefreshNativeSnapshot();
            }
#endif
            durationScale = profile != null && profile.duration > 1e-6f ? duration / profile.duration : 1f;
        }

        //! Re-anchors the active profile to a new authoritative state and plans a continuous stop from it
        //! (target velocity 0). Used by Drive for hard limit clamps, raycast-limit correction and Reset
        //! (plan-281 §2.7) - closes the "smoothMotion not synchronized on external position change" gap.
        public void Rebase(float position, float velocity, float acceleration)
        {
#if REALVIRTUAL_PROFESSIONAL
            if (EnsureNative())
            {
                _native.Rebase(position, velocity, acceleration);
                RefreshNativeSnapshot();
                return;
            }
#endif
            SetInitialPosition(position);
            SetInitialVelocity(velocity);
            SetInitialAcceleration(acceleration);
            SetTarget(position, 0f);
        }

        //! Atomically translates state, profile and effective target by delta - used by Drive on limit-wrap
        //! (plan-281 §2.7 "Wrap Upper->Lower / Lower->Upper"); time/velocity are preserved.
        public void ShiftPosition(float delta)
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_native != null && _native.IsValid)
            {
                _native.ShiftPosition(delta);
                RefreshNativeSnapshot();
            }
#endif
            _state.position += delta;
            _targetState.position += delta;
            if (profile != null)
            {
                profile.OffsetPosition(delta);
                profile.targetPosition += delta;
            }
        }

        //! Pure duration query for (fromState -> targetPosition/targetVelocity) with the given limits -
        //! does NOT mutate this instance's active profile/state (plan-281 verified bug fix: Drive.GetTimeTo()
        //! used to call SetTarget() internally, replanning the live profile as a side effect of a query).
        public static float QueryDuration(MotionState fromState, float maxVelocityLimit, float maxAccelerationLimit,
            float jerkLimit, float targetPosition, float targetVelocity)
        {
            if (maxVelocityLimit <= 0f || maxAccelerationLimit <= 0f || jerkLimit <= 0f)
                return 0f;

#if REALVIRTUAL_PROFESSIONAL
            if (RvSmoothMotionNative.IsAvailable)
            {
                int status = RvSmoothMotionNative.PlanDuration(
                    fromState.position, fromState.velocity, fromState.acceleration,
                    maxVelocityLimit, maxAccelerationLimit, jerkLimit,
                    targetPosition, targetVelocity, out var result);
                if (status == RvSmoothMotionNative.StatusOk)
                    return (float)result.Duration;
            }
#endif
            SCurve queried = SCurve.Generate(fromState,
                new MotionState { position = targetPosition, velocity = targetVelocity, acceleration = 0f },
                maxVelocityLimit, maxAccelerationLimit, jerkLimit);
            return queried.duration;
        }

        public float GetPosition()
        {
            return _state.position;
        }

        public float GetVelocity()
        {
            return _state.velocity;
        }

        public float GetAcceleration()
        {
            return _state.acceleration;
        }

        public float GetDuration()
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_hasNativeSnapshot && _native != null && _native.IsValid)
                return (float)_nativeSnapshot.Duration;
#endif
            return profile.duration * durationScale;
        }

        public float GetTime()
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_hasNativeSnapshot && _native != null && _native.IsValid)
                return (float)_nativeSnapshot.Elapsed;
#endif
            return _time;
        }

        internal float GetTargetPosition()
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_hasNativeSnapshot && _native != null && _native.IsValid)
                return (float)_nativeSnapshot.EffectiveTarget;
#endif
            return profile != null ? profile.targetPosition : _state.position;
        }

        internal bool IsApproachingDeceleration(float leadTime)
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_hasNativeSnapshot && _native != null && _native.IsValid)
                return _nativeSnapshot.Elapsed + Mathf.Max(0f, leadTime) >= _nativeSnapshot.DecelerationStart;
#endif
            if (profile == null || profile.phases == null || profile.phases.Count == 0 || durationScale <= 0f)
                return false;

            float profileTime = _time / durationScale;
            return profileTime + Mathf.Max(0f, leadTime) >= profile.GetDecelerationStartTime();
        }

        public bool IsAtTarget()
        {
#if REALVIRTUAL_PROFESSIONAL
            if (_hasNativeSnapshot && _native != null && _native.IsValid)
                return _nativeSnapshot.Finished != 0;
#endif
            return _state.position == _targetState.position;
        }

        //! Frees the native Rust context, if one was created. Safe to call more than once. Called from
        //! Drive.OnDestroy()/SmoothMotionController.OnDestroy() (plan-281 §2.5 "deterministic dispose").
        public void Dispose()
        {
#if REALVIRTUAL_PROFESSIONAL
            _native?.Dispose();
            _native = null;
            _hasNativeSnapshot = false;
#endif
        }

        void InitTransitionToState(MotionState state)
        {
            _targetState = state;
            profile = SCurve.Generate(_state, _targetState, maxVelocity, maxAcceleration, jerk);
            _targetState.position = profile.targetPosition;
            _targetState.velocity = profile.targetVelocity;
            _targetState.acceleration = profile.targetAcceleration;
            _time = 0;
        }


    }
}
