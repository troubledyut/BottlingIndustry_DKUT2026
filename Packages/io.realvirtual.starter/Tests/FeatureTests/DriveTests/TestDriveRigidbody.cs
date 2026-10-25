// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    // LOP #155 - "Rigidbodies an Drives, sind diese immer noetig?"
    //
    // Drive.Reset() adds a kinematic Rigidbody to every Drive created in the editor, but with the
    // default MoveThisRigidBody = false the Drive moves its transform and never touches that body.
    // These tests run the physics situations a Drive is used in twice - once with the Rigidbody
    // Reset() would add, once without - so a dependency on the Rigidbody shows up as a pair of
    // results that differ. Each scenario uses a SLEEPING MU, the strict case: a resting part on a
    // stopped conveyor is asleep, and a static collider moved by its transform may not wake it.
    //
    // Result on Unity 6000.3 (2026-10-05): the Rigidbody IS needed. Without it a pusher passes
    // through a sleeping MU and a sensor never sees a part without Rigidbody (DES MU, CAD part).
    // Those two "Without" tests therefore assert the failure; if they turn red, Unity physics
    // changed and the automatic Drive Rigidbody can be reconsidered.

    //! Shared setup for the Drive Rigidbody tests: one Drive moving along X with a destination motor.
    public abstract class DriveRigidbodyTestBase : FeatureTestBase
    {
        protected abstract bool DriveHasRigidbody { get; }

        protected string Variant => DriveHasRigidbody ? "with Drive Rigidbody" : "without Drive Rigidbody";

        //! Creates a linear X Drive at the given position that starts immediately towards the destination.
        protected Drive CreateDrive(string name, Vector3 position, float destination, float speed)
        {
            var driveGO = CreateGameObject(name);
            driveGO.transform.position = position;

            if (DriveHasRigidbody)
            {
                // Exactly what Drive.Reset() adds in the editor
                var rb = driveGO.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            var drive = driveGO.AddComponent<Drive>();
            drive.Direction = DIRECTION.LinearX;
            drive.UseAcceleration = false;
            drive.MoveThisRigidBody = false; // the default this LOP is about

            var motor = driveGO.AddComponent<Drive_DestinationMotor>();
            var speedSignal = CreateTestObject<PLCOutputFloat>(name + "Speed");
            speedSignal.Value = speed;
            motor.TargetSpeed = speedSignal;
            var destinationSignal = CreateTestObject<PLCOutputFloat>(name + "Destination");
            destinationSignal.Value = destination;
            motor.Destination = destinationSignal;
            var startSignal = CreateTestObject<PLCOutputBool>(name + "Start");
            startSignal.Value = true;
            motor.StartDrive = startSignal;

            return drive;
        }

        //! Creates a floor slab whose top surface is at y = 0.05 m relative to the test origin.
        protected void CreateFloor(Vector3 center)
        {
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor");
            floor.transform.position = center;
            floor.transform.localScale = new Vector3(2f, 0.1f, 0.6f);
            floor.layer = LayerMask.NameToLayer("rvTransport");
        }

        //! Creates a 100 mm cube MU with a dynamic Rigidbody, resting on the floor and put to sleep.
        protected MU CreateSleepingMU(Vector3 position, out Rigidbody rb)
        {
            var muGO = CreatePrimitive(PrimitiveType.Cube, "MU");
            muGO.transform.position = position;
            muGO.transform.localScale = Vector3.one * 0.1f;
            muGO.layer = LayerMask.NameToLayer("rvMU");
            rb = muGO.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            var mu = muGO.AddComponent<MU>();
            mu.Rigidbody = rb;
            rb.Sleep();
            return mu;
        }

        //! Creates a trigger box with a Sensor component (collider mode, OnTriggerEnter - the default).
        protected Sensor CreateSensor(string name, Vector3 position, Transform parent, Vector3 size)
        {
            var sensorGO = CreateGameObject(name, parent);
            sensorGO.transform.position = position;
            var box = sensorGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            sensorGO.layer = LayerMask.NameToLayer("rvSensor");
            var sensor = sensorGO.AddComponent<Sensor>();
            sensor.LimitSensorToTag = "";
            sensor.UseOverlapDetection = false;
            return sensor;
        }
    }

    //! Records whether a sensor was occupied at any time - a pass-through must not be missed.
    public class DriveRigidbodySensorLatch : MonoBehaviour
    {
        public Sensor Sensor;
        public bool WasOccupied;

        private void FixedUpdate()
        {
            if (Sensor != null && Sensor.Occupied)
                WasOccupied = true;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 1: a Sensor sits on a moving Drive and drives onto a resting (sleeping) MU.
    // Typical: sensor on a lift, a shuttle or a gripper carriage.
    // -----------------------------------------------------------------------------------------

    //! Sensor carried by a Drive must detect a sleeping MU it moves onto.
    public abstract class DriveRigidbodySensorOnDriveBase : DriveRigidbodyTestBase
    {
        protected override string TestName => $"Sensor on a moving Drive detects a sleeping MU ({Variant})";

        private Sensor sensor;
        private DriveRigidbodySensorLatch latch;
        private Drive drive;
        private Rigidbody muRb;

        protected override void SetupTest()
        {
            MinTestTime = 3f; // 400 mm at 300 mm/s = 1.33 s

            CreateFloor(TestPosition(0.5f, 0f, 0f));
            CreateSleepingMU(TestPosition(0.4f, 0.1f, 0f), out muRb);

            drive = CreateDrive("SensorDrive", TestPosition(0f, 0.1f, 0f), 400f, 300f);
            // 80 mm high box centered at 100 mm - clear of the floor top at 50 mm
            sensor = CreateSensor("SensorOnDrive", TestPosition(0f, 0.1f, 0f), drive.transform,
                new Vector3(0.05f, 0.08f, 0.2f));

            latch = sensor.gameObject.AddComponent<DriveRigidbodySensorLatch>();
            latch.Sensor = sensor;

            LogTest($"Setup ({Variant}): MU sleeping={muRb.IsSleeping()}, sensor drives 0 -> 400 mm onto MU at 400 mm");
        }

        protected override string ValidateResults()
        {
            if (drive == null || sensor == null || latch == null)
                return "Test objects were destroyed";

            LogTest($"Result ({Variant}): Drive={drive.CurrentPosition:F1}mm, Occupied={sensor.Occupied}, " +
                    $"WasOccupied={latch.WasOccupied}, MU sleeping={muRb.IsSleeping()}");

            if (drive.CurrentPosition < 390f)
                return $"Drive did not reach the MU: {drive.CurrentPosition:F1}mm";

            if (!latch.WasOccupied)
                return $"Sensor on the Drive never detected the sleeping MU ({Variant})";

            return "";
        }
    }

    public class TestDriveRigidbodySensorOnDriveWith : DriveRigidbodySensorOnDriveBase
    {
        protected override bool DriveHasRigidbody => true;
    }

    public class TestDriveRigidbodySensorOnDriveWithout : DriveRigidbodySensorOnDriveBase
    {
        protected override bool DriveHasRigidbody => false;
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 2: a solid part on a Drive pushes a resting (sleeping) MU.
    // Typical: pusher, stopper, slide that shifts parts.
    // -----------------------------------------------------------------------------------------

    //! A collider moved by a Drive must push a sleeping MU instead of passing through it.
    public abstract class DriveRigidbodyPusherBase : DriveRigidbodyTestBase
    {
        protected override string TestName => $"Pusher on a Drive moves a sleeping MU ({Variant})";

        private Drive drive;
        private Rigidbody muRb;
        private float muStartX;

        protected override void SetupTest()
        {
            MinTestTime = 4f; // 700 mm at 300 mm/s = 2.33 s

            CreateFloor(TestPosition(0.5f, 0f, 0f));
            CreateSleepingMU(TestPosition(0.35f, 0.1f, 0f), out muRb);
            muStartX = muRb.position.x;

            drive = CreateDrive("PusherDrive", TestPosition(0f, 0.1f, 0f), 700f, 300f);
            var pusher = CreatePrimitive(PrimitiveType.Cube, "Pusher", drive.transform);
            pusher.transform.localPosition = Vector3.zero;
            pusher.transform.localScale = new Vector3(0.05f, 0.08f, 0.2f);
            pusher.layer = LayerMask.NameToLayer("rvTransport");

            LogTest($"Setup ({Variant}): MU sleeping={muRb.IsSleeping()}, pusher drives 0 -> 700 mm through MU at 350 mm");
        }

        protected override string ValidateResults()
        {
            if (drive == null || muRb == null)
                return "Test objects were destroyed";

            float moved = (muRb.position.x - muStartX) * 1000f;
            LogTest($"Result ({Variant}): Drive={drive.CurrentPosition:F1}mm, MU moved {moved:F1}mm in X, " +
                    $"MU sleeping={muRb.IsSleeping()}");

            if (drive.CurrentPosition < 690f)
                return $"Drive did not reach its destination: {drive.CurrentPosition:F1}mm";

            // Pusher front ends at 700 + 25 mm, MU center starts at 350 mm -> about 325 mm of push
            bool pushed = moved >= 200f;
            if (DriveHasRigidbody && !pushed)
                return $"Pusher did not push the sleeping MU, it moved only {moved:F1}mm ({Variant})";

            // Measured on Unity 6000.3: a collider moved by its transform without a Rigidbody does not
            // wake a sleeping MU and passes through it - the reason Drive.Reset() adds the Rigidbody.
            if (!DriveHasRigidbody && pushed)
                return $"Physics changed: the pusher without Drive Rigidbody now pushes the MU ({moved:F1}mm). " +
                       "The automatic Drive Rigidbody may no longer be needed for pushing - revisit LOP #155";

            return "";
        }
    }

    public class TestDriveRigidbodyPusherWith : DriveRigidbodyPusherBase
    {
        protected override bool DriveHasRigidbody => true;
    }

    public class TestDriveRigidbodyPusherWithout : DriveRigidbodyPusherBase
    {
        protected override bool DriveHasRigidbody => false;
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 3: a fixed MU (kinematic, parented) is carried by a Drive into a static Sensor.
    // Typical: part held by a gripper or fixer on a moving axis passes a light barrier.
    // -----------------------------------------------------------------------------------------

    //! A static Sensor must detect a fixed MU carried by a Drive.
    public abstract class DriveRigidbodyCarriedMUBase : DriveRigidbodyTestBase
    {
        protected override string TestName => $"Static Sensor detects an MU carried by a Drive ({Variant})";

        private Drive drive;
        private Sensor sensor;
        private DriveRigidbodySensorLatch latch;

        protected override void SetupTest()
        {
            MinTestTime = 3f; // 300 mm at 300 mm/s = 1 s

            drive = CreateDrive("CarrierDrive", TestPosition(0f, 0.5f, 0f), 300f, 300f);

            var muGO = CreatePrimitive(PrimitiveType.Cube, "CarriedMU", drive.transform);
            muGO.transform.localPosition = Vector3.zero;
            muGO.transform.localScale = Vector3.one * 0.1f;
            muGO.layer = LayerMask.NameToLayer("rvMU");
            var muRb = muGO.AddComponent<Rigidbody>();
            muRb.isKinematic = true; // fixed MU - as Grip / Fixer leave it
            muRb.useGravity = false;
            var mu = muGO.AddComponent<MU>();
            mu.Rigidbody = muRb;

            sensor = CreateSensor("StaticSensor", TestPosition(0.3f, 0.5f, 0f), null,
                new Vector3(0.05f, 0.2f, 0.2f));
            latch = sensor.gameObject.AddComponent<DriveRigidbodySensorLatch>();
            latch.Sensor = sensor;

            LogTest($"Setup ({Variant}): fixed MU carried 0 -> 300 mm into a static sensor at 300 mm");
        }

        protected override string ValidateResults()
        {
            if (drive == null || sensor == null || latch == null)
                return "Test objects were destroyed";

            LogTest($"Result ({Variant}): Drive={drive.CurrentPosition:F1}mm, Occupied={sensor.Occupied}, " +
                    $"WasOccupied={latch.WasOccupied}");

            if (drive.CurrentPosition < 290f)
                return $"Drive did not reach the sensor: {drive.CurrentPosition:F1}mm";

            if (!latch.WasOccupied)
                return $"Static sensor never detected the carried MU ({Variant})";

            return "";
        }
    }

    public class TestDriveRigidbodyCarriedMUWith : DriveRigidbodyCarriedMUBase
    {
        protected override bool DriveHasRigidbody => true;
    }

    public class TestDriveRigidbodyCarriedMUWithout : DriveRigidbodyCarriedMUBase
    {
        protected override bool DriveHasRigidbody => false;
    }

    // -----------------------------------------------------------------------------------------
    // Scenario 4: a Sensor on a Drive meets a part WITHOUT Rigidbody.
    // Typical: DES MUs (DESSource destroys the MU Rigidbody) or plain static CAD parts.
    // Here the Drive Rigidbody may be the only body of the trigger pair - expected to differ.
    // -----------------------------------------------------------------------------------------

    //! Sensor carried by a Drive meeting a bodyless MU - shows whether the Drive Rigidbody is the only body of the pair.
    public abstract class DriveRigidbodyBodylessMUBase : DriveRigidbodyTestBase
    {
        protected override string TestName => $"Sensor on a moving Drive detects a part without Rigidbody ({Variant})";

        private Drive drive;
        private Sensor sensor;
        private DriveRigidbodySensorLatch latch;

        protected override void SetupTest()
        {
            MinTestTime = 3f;

            // A plain part without Rigidbody - the same physics pair a DES MU forms (DESSource destroys
            // the MU Rigidbody). No MU component here: MU.Start() would add a Rigidbody to a non-DES MU.
            var part = CreatePrimitive(PrimitiveType.Cube, "BodylessPart");
            part.transform.position = TestPosition(0.4f, 0.1f, 0f);
            part.transform.localScale = Vector3.one * 0.1f;
            part.layer = LayerMask.NameToLayer("rvMU");

            drive = CreateDrive("SensorDrive", TestPosition(0f, 0.1f, 0f), 400f, 300f);
            sensor = CreateSensor("SensorOnDrive", TestPosition(0f, 0.1f, 0f), drive.transform,
                new Vector3(0.05f, 0.08f, 0.2f));
            latch = sensor.gameObject.AddComponent<DriveRigidbodySensorLatch>();
            latch.Sensor = sensor;

            LogTest($"Setup ({Variant}): sensor drives 0 -> 400 mm onto an MU without Rigidbody");
        }

        protected override string ValidateResults()
        {
            if (drive == null || sensor == null || latch == null)
                return "Test objects were destroyed";

            LogTest($"Result ({Variant}): Drive={drive.CurrentPosition:F1}mm, Occupied={sensor.Occupied}, " +
                    $"WasOccupied={latch.WasOccupied}");

            if (drive.CurrentPosition < 390f)
                return $"Drive did not reach the MU: {drive.CurrentPosition:F1}mm";

            if (DriveHasRigidbody && !latch.WasOccupied)
                return $"Sensor on the Drive never detected the part without Rigidbody ({Variant})";

            // Static vs. static: Unity raises no trigger when neither side has a Rigidbody - the Drive
            // Rigidbody is then the only body of the pair (DES MUs, plain CAD parts).
            if (!DriveHasRigidbody && latch.WasOccupied)
                return "Physics changed: a sensor without Drive Rigidbody now detects a part without Rigidbody. " +
                       "The automatic Drive Rigidbody may no longer be needed for sensing - revisit LOP #155";

            return "";
        }
    }

    public class TestDriveRigidbodyBodylessMUWith : DriveRigidbodyBodylessMUBase
    {
        protected override bool DriveHasRigidbody => true;
    }

    public class TestDriveRigidbodyBodylessMUWithout : DriveRigidbodyBodylessMUBase
    {
        protected override bool DriveHasRigidbody => false;
    }
}
