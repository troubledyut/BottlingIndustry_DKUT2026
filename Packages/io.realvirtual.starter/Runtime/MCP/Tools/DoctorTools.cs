// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using realvirtual;
using realvirtual.MCP;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP diagnostics tool for realvirtual scenes.
    //!
    //! Scans the scene for the typical mis-configurations that make a material-flow demo
    //! silently not work - MUs on the wrong layer, sensors with an empty or invalid raycast
    //! layer mask, drives that will never move, and legacy "g4a *" layer names left over in
    //! sensor/source configuration. Each finding carries a concrete fix suggestion.
    public static class DoctorTools
    {
        //! Validates a realvirtual scene and reports likely mis-configurations with fixes.
        //! Returns a list of findings: {path, component, severity, problem, fix}.
        [McpTool("Validate a realvirtual scene - finds MUs on wrong layer, sensors with empty/invalid raycast mask, drives that never move, and legacy 'g4a' layer names. Returns findings with fixes.", "rv_doctor")]
        public static string RvDoctor()
        {
            var findings = new JArray();
            int errors = 0, warnings = 0;

            void Add(GameObject go, string component, string severity, string problem, string fix)
            {
                findings.Add(new JObject
                {
                    ["path"] = go != null ? Path(go) : "(scene)",
                    ["component"] = component,
                    ["severity"] = severity,
                    ["problem"] = problem,
                    ["fix"] = fix
                });
                if (severity == "error") errors++; else warnings++;
            }

            // --- Sensors: raycast layer mask must be present and valid -----------------
            foreach (var sensor in Object.FindObjectsByType<Sensor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var layers = sensor.AdditionalRayCastLayers;
                bool hasAnyValid = false;

                if (layers != null)
                {
                    foreach (var layerName in layers)
                    {
                        if (string.IsNullOrEmpty(layerName))
                            continue;
                        if (LayerMask.NameToLayer(layerName) == -1)
                            Add(sensor.gameObject, "Sensor", "error",
                                $"Raycast layer '{layerName}' does not exist (dead/legacy layer). The sensor will never detect MUs on it.",
                                $"Replace '{layerName}' with a valid layer such as 'rvMU' or 'rvMUSensor' in AdditionalRayCastLayers.");
                        else
                            hasAnyValid = true;
                    }
                }

                if (sensor.UseRaycast)
                {
                    if (layers == null || layers.Count == 0 || !hasAnyValid)
                        Add(sensor.gameObject, "Sensor", "error",
                            "UseRaycast is enabled but the sensor has no valid layer in AdditionalRayCastLayers, so it cannot detect anything.",
                            "Add 'rvMU' (and/or 'rvMUSensor') to AdditionalRayCastLayers.");

                    if (sensor.RayCastLength <= 0f)
                        Add(sensor.gameObject, "Sensor", "warning",
                            $"UseRaycast is enabled but RayCastLength is {sensor.RayCastLength} mm - the beam has no length.",
                            "Set RayCastLength to a positive value in millimeters (e.g. 1000).");
                }
            }

            // --- Sources: GenerateOnLayer must be a real layer name (not an index / legacy) --
            foreach (var source in Object.FindObjectsByType<Source>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var layerName = source.GenerateOnLayer;
                if (!string.IsNullOrEmpty(layerName) && LayerMask.NameToLayer(layerName) == -1)
                    Add(source.gameObject, "Source", "error",
                        $"GenerateOnLayer '{layerName}' is not a valid layer name" +
                        (int.TryParse(layerName, out _) ? " (a layer index string is ignored - the field expects a NAME)" : " (dead/legacy layer)") +
                        ". Generated MUs will not be re-layered and stay invisible to sensors/transport.",
                        "Set GenerateOnLayer to a layer NAME such as 'rvMU'.");
            }

            // --- MUs: must sit on an rvMU* layer to be seen by sensors and transport ----
            foreach (var mu in Object.FindObjectsByType<MU>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var layerName = LayerMask.LayerToName(mu.gameObject.layer);
                if (string.IsNullOrEmpty(layerName) || layerName == "Default" || !layerName.StartsWith("rv"))
                    Add(mu.gameObject, "MU", "warning",
                        $"MU is on layer '{(string.IsNullOrEmpty(layerName) ? mu.gameObject.layer.ToString() : layerName)}'. Sensors and transport surfaces only detect MUs on the rvMU* layers.",
                        "Move the MU (or its Source's GenerateOnLayer) to 'rvMU'.");
            }

            // --- Drives: a drive with speed 0 will never move ---------------------------
            foreach (var drive in Object.FindObjectsByType<Drive>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Mathf.Approximately(drive.TargetSpeed, 0f))
                    Add(drive.gameObject, "Drive", "warning",
                        "Drive TargetSpeed is 0 - the drive will not move even when started.",
                        "Set TargetSpeed (mm/s or deg/s) to a non-zero value, e.g. 200.");
            }

            return new JObject
            {
                ["status"] = "ok",
                ["findings"] = findings,
                ["count"] = findings.Count,
                ["errors"] = errors,
                ["warnings"] = warnings,
                ["summary"] = findings.Count == 0
                    ? "No common mis-configurations found."
                    : $"{errors} error(s), {warnings} warning(s). Fix errors first - they usually mean 'nothing happens' at runtime."
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Full hierarchy path of a GameObject.
        private static string Path(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
#endif
