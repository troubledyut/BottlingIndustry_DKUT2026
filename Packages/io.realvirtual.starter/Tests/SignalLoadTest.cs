// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace realvirtual
{
    //! Reproduces and measures the play mode startup cost of scenes with a very large number of signals.

    //! Interfaces like PLCSIM Advanced create one GameObject per PLC signal, so a big PLC program can
    //! leave several thousand signal objects in the scene and entering play mode becomes slow. This tool
    //! creates that load synthetically - no PLC, no interface and no license needed - and measures the
    //! scene load phase (everything from the subsystem registration up to and including all Awake calls),
    //! which is where the per component costs add up.
    //!
    //! Usage: realvirtual DEV > Testing > Signal Load Test, create the signals, enter play mode and read
    //! the measured time in the console and in the window.
    public class SignalLoadTest : EditorWindow
    {
        private const string ParentName = "SignalLoadTest";
        private const string PrefMeasure = "realvirtual.SignalLoadTest.Measure";
        private const string PrefLastResult = "realvirtual.SignalLoadTest.LastResultMs";

        private int signalcount = 12000;

        [MenuItem("realvirtual DEV/Testing/Signal Load Test/Window", false, 103)]
        public static void ShowWindow()
        {
            GetWindow<SignalLoadTest>("Signal Load Test");
        }

        //! Creates the default load of 12000 signals, the size which made a customer scene slow
        [MenuItem("realvirtual DEV/Testing/Signal Load Test/Create 12000 Signals", false, 104)]
        public static void Create12000Signals()
        {
            CreateSignals(12000);
        }

        //! Removes the signals created by this tool
        [MenuItem("realvirtual DEV/Testing/Signal Load Test/Remove Signals", false, 105)]
        public static void RemoveSignalsMenu()
        {
            RemoveSignals();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Creates a synthetic signal load in the open scene to measure how long entering play mode takes. " +
                "The scene needs a realvirtualController, otherwise the signals report a missing controller.",
                MessageType.Info);

            EditorGUILayout.Space();
            signalcount = EditorGUILayout.IntField("Number of Signals", signalcount);

            var parent = GameObject.Find(ParentName);
            EditorGUILayout.LabelField("Currently in scene",
                parent == null ? "none" : parent.transform.childCount + " signals");

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Create Signals"))
                    CreateSignals(signalcount);

                if (GUILayout.Button("Remove Signals"))
                    RemoveSignals();
            }

            EditorGUILayout.Space();
            var measure = EditorGUILayout.Toggle("Measure Play Mode Start", EditorPrefs.GetBool(PrefMeasure, false));
            EditorPrefs.SetBool(PrefMeasure, measure);

            var last = EditorPrefs.GetFloat(PrefLastResult, -1f);
            EditorGUILayout.LabelField("Last scene load",
                last < 0 ? "not measured yet" : last.ToString("F0") + " ms");
        }

        //! Creates the given number of signal GameObjects below a single parent in the open scene
        public static void CreateSignals(int count)
        {
            RemoveSignals();

            var parent = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Signal Load Test");

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                if (i % 500 == 0)
                    EditorUtility.DisplayProgressBar("Signal Load Test",
                        "Creating signal " + i + " of " + count, (float)i / count);

                var go = new GameObject("PLC_Signal_" + i.ToString("D5"));
                go.transform.parent = parent.transform;

                // Alternate the types so the mix resembles a real PLC import.
                switch (i % 4)
                {
                    case 0: go.AddComponent<PLCOutputBool>(); break;
                    case 1: go.AddComponent<PLCInputBool>(); break;
                    case 2: go.AddComponent<PLCOutputInt>(); break;
                    default: go.AddComponent<PLCInputFloat>(); break;
                }
            }

            EditorUtility.ClearProgressBar();
            watch.Stop();

            EditorPrefs.SetBool(PrefMeasure, true);
            Logger.Message("Signal Load Test: created " + count + " signals in " + watch.ElapsedMilliseconds +
                           " ms. Enter play mode to measure the scene load time.");
        }

        //! Removes the signal GameObjects created by this tool and stops measuring play mode starts
        public static void RemoveSignals()
        {
            var parent = GameObject.Find(ParentName);
            if (parent != null)
                DestroyImmediate(parent);

            // Without the test load there is nothing to measure, so do not keep logging on every play.
            EditorPrefs.SetBool(PrefMeasure, false);
        }

        #region Measurement

        private static Stopwatch _sceneloadwatch;

        //! Starts the measurement before the scene is loaded
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StartMeasurement()
        {
            _sceneloadwatch = EditorPrefs.GetBool(PrefMeasure, false) ? Stopwatch.StartNew() : null;
        }

        //! Stops the measurement after the scene including all Awake calls is loaded
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StopMeasurement()
        {
            if (_sceneloadwatch == null)
                return;

            _sceneloadwatch.Stop();
            var ms = _sceneloadwatch.ElapsedMilliseconds;
            _sceneloadwatch = null;

            EditorPrefs.SetFloat(PrefLastResult, ms);

            var parent = GameObject.Find(ParentName);
            var count = parent == null ? 0 : parent.transform.childCount;
            Logger.Message("Signal Load Test: scene load including all Awake calls took " + ms +
                           " ms with " + count + " test signals in the scene.");
        }

        #endregion
    }
}
#endif
