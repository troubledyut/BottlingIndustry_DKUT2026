// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    //! LOP #155 - pins down which ways of adding a Drive in the editor run Drive.Reset() and with it
    //! the automatic kinematic Rigidbody, and that the Drive's default does not move that Rigidbody.
    public class TestDriveRigidbodyReset
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

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            testObjects.Add(go);
            return go;
        }

        private static string Describe(Rigidbody rb)
        {
            return rb == null ? "no Rigidbody" : $"Rigidbody isKinematic={rb.isKinematic} useGravity={rb.useGravity}";
        }

        [Test]
        public void Test_Drive_ObjectFactoryAddComponent_AddsKinematicRigidbody()
        {
            // Arrange
            var go = NewObject("DriveObjectFactory");

            // Act - the path of the Inspector "Add Component" button
            var drive = ObjectFactory.AddComponent<Drive>(go);

            // Assert
            var rb = go.GetComponent<Rigidbody>();
            Debug.Log($"[LOP155] ObjectFactory.AddComponent<Drive>: {Describe(rb)}");
            Assert.IsNotNull(drive);
            Assert.IsNotNull(rb, "Drive.Reset() did not add a Rigidbody when added like from the Inspector");
            Assert.IsTrue(rb.isKinematic, "Rigidbody added by Drive.Reset() is not kinematic");
            Assert.IsFalse(rb.useGravity, "Rigidbody added by Drive.Reset() uses gravity");
        }

        [Test]
        public void Test_Drive_ScriptAddComponentInEditMode_ReportsRigidbody()
        {
            // Arrange
            var go = NewObject("DriveScriptAdd");

            // Act - plain AddComponent from an editor script (CAD import tools, MCP component_add)
            go.AddComponent<Drive>();

            // Assert - documents the behavior, both outcomes are legal Unity behavior
            var rb = go.GetComponent<Rigidbody>();
            Debug.Log($"[LOP155] GameObject.AddComponent<Drive> in Edit Mode: {Describe(rb)}");
            if (rb != null)
                Assert.IsTrue(rb.isKinematic, "Rigidbody added by Drive.Reset() is not kinematic");
            Assert.Pass(rb == null
                ? "Script AddComponent in Edit Mode does NOT run Reset - no Rigidbody"
                : "Script AddComponent in Edit Mode runs Reset - kinematic Rigidbody added");
        }

        [Test]
        public void Test_Drive_Default_MoveThisRigidBodyIsOff()
        {
            // Arrange
            var go = NewObject("DriveDefault");

            // Act
            var drive = ObjectFactory.AddComponent<Drive>(go);

            // Assert - with this default the Drive moves its transform, not the Rigidbody
            Assert.IsFalse(drive.MoveThisRigidBody,
                "MoveThisRigidBody is on by default - then the Rigidbody would be required");
        }
    }
}
