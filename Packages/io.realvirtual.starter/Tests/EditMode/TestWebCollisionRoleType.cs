// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace realvirtual
{
    //! Guards the C# side of the collision-role contract with realvirtual WEB (plan-419).
    //! The enum NAMES are exported into the GLB as plain strings and must stay wordwise
    //! identical to COLLISION_ROLES in rv-collision-role.ts; new roles are only appended
    //! (plan-409 rule), which is why the numeric values are pinned as well.
    public class TestWebCollisionRoleType
    {
        private readonly List<GameObject> testObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var testObject in testObjects)
            {
                if (testObject != null)
                    UnityEngine.Object.DestroyImmediate(testObject);
            }
            testObjects.Clear();
        }

        [Test]
        public void Test_WebCollisionRoleType_Names_MatchWebViewerCollisionRoles()
        {
            // Wordwise identical to COLLISION_ROLES in
            // Assets/realvirtual-WebViewer~/src/core/engine/rv-collision-role.ts
            var expected = new[]
            {
                "None", "Tool", "Workpiece", "Machine", "Robot", "Environment", "Cutter"
            };

            CollectionAssert.AreEqual(expected, Enum.GetNames(typeof(WebCollisionRoleType)),
                "WebCollisionRoleType names/order must match COLLISION_ROLES in rv-collision-role.ts");
        }

        [Test]
        public void Test_WebCollisionRoleType_Values_AreAppendOnly()
        {
            // Pinning the numeric values makes an insertion or a reorder fail loudly
            // instead of silently remapping already authored scenes.
            Assert.AreEqual(0, (int)WebCollisionRoleType.None);
            Assert.AreEqual(1, (int)WebCollisionRoleType.Tool);
            Assert.AreEqual(2, (int)WebCollisionRoleType.Workpiece);
            Assert.AreEqual(3, (int)WebCollisionRoleType.Machine);
            Assert.AreEqual(4, (int)WebCollisionRoleType.Robot);
            Assert.AreEqual(5, (int)WebCollisionRoleType.Environment);
            Assert.AreEqual(6, (int)WebCollisionRoleType.Cutter);
            Assert.AreEqual(7, Enum.GetValues(typeof(WebCollisionRoleType)).Length,
                "A new role must be APPENDED - adding one here means the WebViewer enum needs it too");
        }

        [Test]
        public void Test_WebCollisionRoleType_DefaultValue_IsNone()
        {
            Assert.AreEqual(WebCollisionRoleType.None, default(WebCollisionRoleType));
        }

        [Test]
        public void Test_Source_CollisionRoleForMUs_DefaultsToNone()
        {
            var source = CreateSource();

            Assert.AreEqual(WebCollisionRoleType.None, source.CollisionRoleForMUs,
                "Source.CollisionRoleForMUs must default to None so existing scenes keep their behaviour");
        }

        [Test]
        public void Test_Source_CollisionRoleForMUs_IsAssignable()
        {
            var source = CreateSource();

            source.CollisionRoleForMUs = WebCollisionRoleType.Workpiece;

            Assert.AreEqual(WebCollisionRoleType.Workpiece, source.CollisionRoleForMUs);
        }

        [Test]
        public void Test_Source_CollisionRoleForMUs_IsPublicSerializedField()
        {
            // The GLB exporter serializes public instance fields by reflection - a property
            // or a private field would silently drop out of the export.
            var field = typeof(Source).GetField("CollisionRoleForMUs");

            Assert.IsNotNull(field, "Source.CollisionRoleForMUs must be a public field for the GLB export");
            Assert.AreEqual(typeof(WebCollisionRoleType), field.FieldType);
            Assert.IsFalse(field.IsStatic, "CollisionRoleForMUs must be an instance field");
        }

        private Source CreateSource()
        {
            var go = new GameObject("TestSource");
            testObjects.Add(go);
            return go.AddComponent<Source>();
        }
    }
}
