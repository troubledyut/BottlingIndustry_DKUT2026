// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_PROFESSIONAL
using System;
using System.Runtime.InteropServices;

namespace realvirtual
{
    #region doc
    //! P/Invoke binding to the rv-smooth-motion native core (plan-281) - the versioned raw C-ABI shared by
    //! the Unity native DLL and the realvirtual WEB WASM provider (see
    //! realvirtualReleaseDLLs/rv-smooth-motion/ABI.md for the authoritative wire-format contract).

    //! RvSmoothMotionNative wraps rv_smooth_motion.dll exactly at the ABI boundary: every call converts
    //! Unity's float fields to the f64 the core requires (E2/G1 - bit-identity to the legacy float kernel
    //! is explicitly not a goal). One instance owns exactly one generational u32 context handle; callers
    //! (SmoothMotion) are responsible for Dispose()/lazy-recreate around Unity's own lifecycle (domain
    //! reload, scene unload, component destroy) - see SmoothMotion.cs and RvSmoothMotionPlugin.cs
    //! (AssemblyReloadEvents.beforeAssemblyReload -&gt; ResetAll()).
    //!
    //! Kill-switch behaviour: IsAvailable is a cached, DllNotFoundException-safe probe
    //! (rv_smooth_abi_version() == 1). TryCreate() returns null - never throws - if the DLL is missing,
    //! mismatched, or context creation failed. There is no managed fallback INSIDE this class; the caller
    //! (SmoothMotion) is what actually falls back to the frozen managed 7-segment kernel when IsAvailable
    //! is false, matching E7 ("Managed Fallback bis echte Player-Matrix grün").
    //!
    //! Only the scalar + native-out-pointer entries of ABI v1 are bound here (14 exported functions per
    //! ABI.md's native shape: abi_version, create, destroy, reset_all, configure, set_state, set_target,
    //! adjust_duration, rebase, shift_position, step, get_snapshot, plan_duration, step_many). The eight
    //! WASM scratch-pointer exports (rv_smooth_snapshot_ptr etc.) exist only for the raw-WASM boundary,
    //! which cannot form an out-pointer; native P/Invoke can pass real C# struct pointers directly, so
    //! those exports are irrelevant here.
    #endregion
    internal sealed class RvSmoothMotionNative : IDisposable
    {
        private const string DllName = "rv_smooth_motion";

        // ABI version this binding was written against - kept in sync with rv-smooth-motion's
        // abi::ABI_VERSION / ABI.md. IsAvailable rejects a DLL that reports a different version.
        private const uint ExpectedAbiVersion = 1;

        #region ABI structs (see ABI.md "Struct layout")

        //! Mirrors RvMotionSnapshot (#[repr(C)], sizeof 80, align 8) field-for-field.
        [StructLayout(LayoutKind.Sequential)]
        public struct Snapshot
        {
            public double Position;
            public double Velocity;
            public double Acceleration;
            public double RequestedTarget;
            public double EffectiveTarget;
            public double Duration;
            public double Elapsed;
            public double DecelerationStart;
            public uint PhaseCount;
            public int Outcome; // PlanOutcome: 0 Reached, 1 StoppedAtEarliestReachable, 2 AlreadyAtTarget
            public int Finished; // 0/1
            public int Status;
        }

        //! Mirrors RvMotionPlanInput (#[repr(C)], sizeof 64, align 8).
        [StructLayout(LayoutKind.Sequential)]
        public struct PlanInput
        {
            public double Position;
            public double Velocity;
            public double Acceleration;
            public double MaxVelocity;
            public double MaxAcceleration;
            public double MaxJerk;
            public double TargetPosition;
            public double TargetVelocity;
        }

        //! Mirrors RvMotionPlanResult (#[repr(C)], sizeof 40, align 8; explicit tail-pad Reserved).
        [StructLayout(LayoutKind.Sequential)]
        public struct PlanResult
        {
            public double Duration;
            public double EffectiveTarget;
            public double DecelerationStart;
            public uint PhaseCount;
            public int Outcome;
            public int Status;
            public uint Reserved;
        }

        #endregion

        #region Status codes (ABI.md "Status codes")

        public const int StatusOk = 0;
        public const int StatusInvalidHandle = -1;
        public const int StatusInvalidArgument = -2;
        public const int StatusInvalidLimits = -3;
        public const int StatusInvalidState = -4;
        public const int StatusAbiMismatch = -5;
        public const int StatusPanicCaught = -100;

        #endregion

        #region P/Invoke declarations (14 native exports, ABI.md "native shape")

        [DllImport(DllName, EntryPoint = "rv_smooth_abi_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint rv_smooth_abi_version();

        [DllImport(DllName, EntryPoint = "rv_smooth_create", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint rv_smooth_create();

        [DllImport(DllName, EntryPoint = "rv_smooth_destroy", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_destroy(uint handle);

        [DllImport(DllName, EntryPoint = "rv_smooth_reset_all", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_reset_all();

        [DllImport(DllName, EntryPoint = "rv_smooth_configure", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_configure(uint handle, double vmax, double amax, double jmax);

        [DllImport(DllName, EntryPoint = "rv_smooth_set_state", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_set_state(uint handle, double position, double velocity, double acceleration);

        [DllImport(DllName, EntryPoint = "rv_smooth_set_target", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_set_target(uint handle, double requestedPosition, double targetVelocity);

        [DllImport(DllName, EntryPoint = "rv_smooth_adjust_duration", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_adjust_duration(uint handle, double requestedDuration);

        [DllImport(DllName, EntryPoint = "rv_smooth_rebase", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_rebase(uint handle, double position, double velocity, double acceleration);

        [DllImport(DllName, EntryPoint = "rv_smooth_shift_position", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_shift_position(uint handle, double delta);

        [DllImport(DllName, EntryPoint = "rv_smooth_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_step(uint handle, double dt, double speedOverride, out Snapshot outSnapshot);

        [DllImport(DllName, EntryPoint = "rv_smooth_get_snapshot", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_get_snapshot(uint handle, out Snapshot outSnapshot);

        [DllImport(DllName, EntryPoint = "rv_smooth_plan_duration", CallingConvention = CallingConvention.Cdecl)]
        private static extern int rv_smooth_plan_duration(ref PlanInput planInput, out PlanResult outResult);

        [DllImport(DllName, EntryPoint = "rv_smooth_step_many", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe int rv_smooth_step_many(uint* handles, uint count, double dt, double* overrides, Snapshot* outSnapshots);

        #endregion

        private static bool? isAvailableCached;

        //! True if rv_smooth_motion.dll is present, loadable, and reports the expected ABI version -
        //! evaluated once (DllNotFoundException/EntryPointNotFoundException/BadImageFormatException are all
        //! treated as "unavailable", never thrown onward) and cached for the process lifetime. This is the
        //! kill-switch: on any platform without the DLL, SmoothMotion falls back to the managed kernel.
        public static bool IsAvailable
        {
            get
            {
                if (isAvailableCached.HasValue) return isAvailableCached.Value;
                try
                {
                    isAvailableCached = rv_smooth_abi_version() == ExpectedAbiVersion;
                }
                catch (DllNotFoundException)
                {
                    isAvailableCached = false;
                }
                catch (EntryPointNotFoundException)
                {
                    isAvailableCached = false;
                }
                catch (BadImageFormatException)
                {
                    isAvailableCached = false;
                }
                return isAvailableCached.Value;
            }
        }

        //! Re-runs the availability probe on next access - only used by the Editor-only assembly-reload
        //! guard / tests, never on the hot path.
        internal static void ResetAvailabilityProbe()
        {
            isAvailableCached = null;
        }

        private uint handle;

        //! False once Dispose()d or if creation never succeeded. A stale (post reset_all/destroy) handle
        //! still reports true here - the native side resolves it to INVALID_HANDLE safely (generational
        //! IDs, ABI.md "Handles") instead of crashing; callers should treat any non-OK status as a signal
        //! to recreate lazily.
        public bool IsValid => handle != 0;

        private RvSmoothMotionNative(uint handle)
        {
            this.handle = handle;
        }

        //! Creates a new native context. Returns null (never throws) if the DLL is unavailable or context
        //! creation failed - callers (SmoothMotion) fall back to the managed kernel in that case.
        public static RvSmoothMotionNative TryCreate()
        {
            if (!IsAvailable) return null;
            uint h;
            try
            {
                h = rv_smooth_create();
            }
            catch (Exception)
            {
                return null;
            }
            return h == 0 ? null : new RvSmoothMotionNative(h);
        }

        public int Configure(double maxVelocity, double maxAcceleration, double maxJerk)
        {
            return IsValid ? rv_smooth_configure(handle, maxVelocity, maxAcceleration, maxJerk) : StatusInvalidHandle;
        }

        public int SetState(double position, double velocity, double acceleration)
        {
            return IsValid ? rv_smooth_set_state(handle, position, velocity, acceleration) : StatusInvalidHandle;
        }

        public int SetTarget(double requestedPosition, double targetVelocity)
        {
            return IsValid ? rv_smooth_set_target(handle, requestedPosition, targetVelocity) : StatusInvalidHandle;
        }

        //! Stretches playback to a requested wall-clock duration (F15 clamp contract - never violates
        //! v/a/jerk limits; too-short a request is clamped to the physically minimal profile time).
        public int AdjustDuration(double requestedDuration)
        {
            return IsValid ? rv_smooth_adjust_duration(handle, requestedDuration) : StatusInvalidHandle;
        }

        //! Re-anchors the context to a new authoritative state and plans a continuous stop from it
        //! (target velocity 0) - see ABI.md "rv_smooth_rebase". Used for hard limit clamps, raycast-limit
        //! correction and Reset (plan-281 §2.7).
        public int Rebase(double position, double velocity, double acceleration)
        {
            return IsValid ? rv_smooth_rebase(handle, position, velocity, acceleration) : StatusInvalidHandle;
        }

        //! Atomically translates state, profile and effective target by delta - used for limit-wrap
        //! (plan-281 §2.7 "Wrap Upper->Lower / Lower->Upper").
        public int ShiftPosition(double delta)
        {
            return IsValid ? rv_smooth_shift_position(handle, delta) : StatusInvalidHandle;
        }

        public int Step(double dt, double speedOverride, out Snapshot snapshot)
        {
            if (!IsValid)
            {
                snapshot = default;
                return StatusInvalidHandle;
            }
            return rv_smooth_step(handle, dt, speedOverride, out snapshot);
        }

        public int GetSnapshot(out Snapshot snapshot)
        {
            if (!IsValid)
            {
                snapshot = default;
                return StatusInvalidHandle;
            }
            return rv_smooth_get_snapshot(handle, out snapshot);
        }

        //! Pure duration query (rv_smooth_plan_duration) - touches no context and mutates no active
        //! profile (plan-281 verified bug fix: Drive.GetTimeTo() used to replan the live profile).
        public static int PlanDuration(double position, double velocity, double acceleration,
            double maxVelocity, double maxAcceleration, double maxJerk,
            double targetPosition, double targetVelocity, out PlanResult result)
        {
            if (!IsAvailable)
            {
                result = default;
                return StatusInvalidHandle;
            }
            var input = new PlanInput
            {
                Position = position,
                Velocity = velocity,
                Acceleration = acceleration,
                MaxVelocity = maxVelocity,
                MaxAcceleration = maxAcceleration,
                MaxJerk = maxJerk,
                TargetPosition = targetPosition,
                TargetVelocity = targetVelocity
            };
            return rv_smooth_plan_duration(ref input, out result);
        }

        //! Batch step (rv_smooth_step_many, ABI v1 - §2.4/NFR "Perf-Gate"). Reserved for the Phase 5
        //! end-to-end 1000-drive boundary benchmark; not yet called from Drive.cs in Phase 3, which steps
        //! one context per drive via Step() above.
        public static unsafe int StepMany(uint[] handles, double dt, double[] speedOverrides, Snapshot[] outSnapshots)
        {
            if (handles == null || speedOverrides == null || outSnapshots == null) return StatusInvalidArgument;
            int count = handles.Length;
            if (speedOverrides.Length != count || outSnapshots.Length != count) return StatusInvalidArgument;

            fixed (uint* handlesPtr = handles)
            fixed (double* overridesPtr = speedOverrides)
            fixed (Snapshot* outPtr = outSnapshots)
            {
                return rv_smooth_step_many(handlesPtr, (uint)count, dt, overridesPtr, outPtr);
            }
        }

        //! Discards ALL native contexts process-wide (Domain-Reload / epoch reset, plan-281 Finding #4).
        //! Every RvSmoothMotionNative instance still referenced by a live (non-reloaded) object becomes
        //! silently invalid afterwards - safe, because handles are looked up through the generational
        //! registry rather than dereferenced raw; the next call simply returns INVALID_HANDLE and the
        //! caller recreates lazily. Called from RvSmoothMotionPlugin's AssemblyReloadEvents hook.
        public static void ResetAll()
        {
            if (!IsAvailable) return;
            try
            {
                rv_smooth_reset_all();
            }
            catch (Exception)
            {
                // best-effort during teardown - never throw out of an assembly-reload callback
            }
        }

        //! Frees the native handle. Safe to call more than once (a no-op after the first call).
        public void Dispose()
        {
            if (handle == 0) return;
            try
            {
                rv_smooth_destroy(handle);
            }
            catch (DllNotFoundException)
            {
                // DLL was unloaded/removed between creation and dispose (should not normally happen) -
                // there is nothing left to free on the C# side either way.
            }
            handle = 0;
            GC.SuppressFinalize(this);
        }

        ~RvSmoothMotionNative()
        {
            // Backstop only - callers should always Dispose() explicitly (SmoothMotion.Dispose(), called
            // from Drive.OnDestroy()). rv_smooth_destroy() is a plain handle free with no managed-object
            // interaction, so it is safe to run from the finalizer thread during a normal process exit;
            // during an Editor domain reload the whole native module is unloaded anyway (same tradeoff
            // already accepted for the IK/Kinematics native DLLs).
            if (handle != 0)
            {
                try
                {
                    rv_smooth_destroy(handle);
                }
                catch
                {
                    // never throw from a finalizer
                }
            }
        }
    }
}
#endif
