// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace realvirtual
{
    public class TestSmoothMotionReachability
    {
        private readonly List<GameObject> testObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var testObject in testObjects)
            {
                if (testObject != null)
                    Object.DestroyImmediate(testObject);
            }
            testObjects.Clear();
        }

        [TestCase(500f)]
        [TestCase(-500f)]
        public void Test_SetTarget_CurrentPositionWhileMoving_BrakesAtReachablePosition(float initialVelocity)
        {
            SmoothMotion motion = CreateMotion(0f, initialVelocity);

            motion.SetTarget(0f, 0f);

            Assert.That(motion.profile.targetPosition * Mathf.Sign(initialVelocity), Is.GreaterThan(0f));
            Assert.That(motion.GetDuration(), Is.GreaterThan(0f));
            AssertSmoothStop(motion, initialVelocity);
        }

        [Test]
        public void Test_CloseRedirect_StoppingDistanceExceedsRemainingDistance_NoJumpOrVelocitySnap()
        {
            SmoothMotion motion = CreateMotion(0f, 100f);

            motion.SetTarget(10f, 0f);

            Assert.That(motion.profile.targetPosition, Is.GreaterThan(10f));
            MotionState exactEndpoint = motion.profile.EvaluateExact(motion.profile.duration);
            Assert.That(exactEndpoint.position, Is.EqualTo(motion.profile.targetPosition).Within(1e-4f));
            Assert.That(exactEndpoint.velocity, Is.EqualTo(0f).Within(1e-4f));
            AssertSmoothStop(motion, 100f);
        }

        [Test]
        public void Test_OneFrameJogDropout_ResumePreservesMotion()
        {
            SmoothMotion motion = CreateMotion(0f, 0f);
            const float deltaTime = 0.02f;
            motion.SetTarget(1000000f, 0f);

            int accelerationSteps = 0;
            while (motion.GetVelocity() < 100f && accelerationSteps++ < 10000)
                motion.Integrate(deltaTime);
            Assert.That(accelerationSteps, Is.LessThan(10000));

            float speedBeforeDropout = motion.GetVelocity();
            float positionBeforeDropout = motion.GetPosition();
            motion.SetTarget(positionBeforeDropout, 0f);
            motion.Integrate(deltaTime);

            float speedAfterDropout = motion.GetVelocity();
            float positionAfterDropout = motion.GetPosition();
            Assert.That(speedAfterDropout, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(speedAfterDropout - speedBeforeDropout), Is.LessThan(5f));
            Assert.That(positionAfterDropout, Is.GreaterThanOrEqualTo(positionBeforeDropout));

            motion.SetTarget(positionAfterDropout + 1000000f, 0f);
            motion.Integrate(deltaTime);

            Assert.That(motion.GetVelocity(), Is.GreaterThan(0f));
            Assert.That(motion.GetPosition(), Is.GreaterThanOrEqualTo(positionAfterDropout));
        }

        [Test]
        public void Test_NominalPointToPoint_ReachesExactTargetWithinVelocityLimit()
        {
            SmoothMotion motion = CreateMotion(0f, 0f);
            motion.SetTarget(2000f, 0f);
            float maximumVelocity = 0f;
            int integrationSteps = 0;

            while (!motion.IsAtTarget() && integrationSteps++ < 100000)
            {
                motion.Integrate(0.01f);
                maximumVelocity = Mathf.Max(maximumVelocity, Mathf.Abs(motion.GetVelocity()));
            }

            Assert.That(integrationSteps, Is.LessThan(100000));
            Assert.That(maximumVelocity, Is.LessThanOrEqualTo(500.01f));
            Assert.That(motion.GetPosition(), Is.EqualTo(2000f).Within(0.001f));
            Assert.That(motion.GetVelocity(), Is.EqualTo(0f).Within(0.001f));
            Assert.That(motion.IsAtTarget(), Is.True);
        }

        [Test]
        public void Test_SpeedOverridePause_DoesNotRescaleElapsedProfileTime()
        {
            SmoothMotion reference = CreateMotion(0f, 0f);
            reference.SetTarget(1000f, 0f);
            reference.Integrate(0.1f);

            SmoothMotion paused = CreateMotion(0f, 0f);
            paused.SetTarget(1000f, 0f);
            paused.speedOverride = 0f;
            paused.Integrate(1f);
            Assert.That(paused.GetPosition(), Is.EqualTo(0f).Within(1e-6f));

            paused.speedOverride = 1f;
            paused.Integrate(0.1f);
            Assert.That(paused.GetPosition(), Is.EqualTo(reference.GetPosition()).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator Test_DriveTo_ZeroTargetSpeed_LogsErrorInsteadOfThrowing()
        {
            var controller = Object.FindFirstObjectByType<realvirtualController>();
            if (controller == null)
            {
                var controllerObject = new GameObject("SmoothMotionTestController");
                testObjects.Add(controllerObject);
                controller = controllerObject.AddComponent<realvirtualController>();
            }

            var driveObject = new GameObject("InvalidSmoothDrive");
            testObjects.Add(driveObject);
            var drive = driveObject.AddComponent<Drive>();
            drive.Direction = DIRECTION.LinearX;
            drive.UseAcceleration = true;
            drive.SmoothAcceleration = true;
            drive.TargetSpeed = 0f;
            drive.Acceleration = 200f;
            drive.Jerk = 1000f;

            yield return null; // Allow Start() to initialize Drive internals.

            LogAssert.Expect(LogType.Error,
                new Regex("Smooth motion on Drive 'InvalidSmoothDrive' requires TargetSpeed, Acceleration and Jerk"));
            drive.DriveTo(100f);

            Assert.DoesNotThrow(() => drive.CalcFixedUpdate());
            Assert.That(drive.IsStopped, Is.True);
            Assert.That(drive.CurrentSpeed, Is.EqualTo(0f));
        }

        private static SmoothMotion CreateMotion(float position, float velocity)
        {
            var motion = new SmoothMotion
            {
                maxVelocity = 500f,
                maxAcceleration = 200f,
                jerk = 1000f
            };
            motion.SetInitialPosition(position);
            motion.SetInitialVelocity(velocity);
            motion.SetInitialAcceleration(0f);
            return motion;
        }

        private static void AssertSmoothStop(SmoothMotion motion, float initialVelocity)
        {
            float previousPosition = motion.GetPosition();
            float previousVelocity = initialVelocity;
            float direction = Mathf.Sign(initialVelocity);
            float endTime = motion.GetDuration() + 0.05f;
            const float deltaTime = 0.01f;

            while (motion.GetTime() < endTime)
            {
                motion.Integrate(deltaTime);
                float stepDistance = (motion.GetPosition() - previousPosition) * direction;
                Assert.That(stepDistance, Is.GreaterThanOrEqualTo(-1e-4f));
                Assert.That(stepDistance,
                    Is.LessThanOrEqualTo((Mathf.Abs(previousVelocity) + 1f) * deltaTime + 0.01f));
                Assert.That(Mathf.Abs(motion.GetVelocity()), Is.LessThanOrEqualTo(Mathf.Abs(previousVelocity) + 1e-3f));
                Assert.That(motion.GetVelocity() * direction, Is.GreaterThanOrEqualTo(-1e-3f));
                previousPosition = motion.GetPosition();
                previousVelocity = motion.GetVelocity();
            }

            Assert.That(motion.GetPosition(), Is.EqualTo(motion.profile.targetPosition).Within(1e-4f));
            Assert.That(motion.GetVelocity(), Is.EqualTo(0f).Within(1e-4f));
        }
    }
}
