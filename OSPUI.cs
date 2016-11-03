using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KSP.UI.Screens;

namespace OrbitalSurveyPlus
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    class OSPUI : MonoBehaviour
    {
        private static bool primaryInitialize = true;

        private static ApplicationLauncherButton appButtonBiomeOverlay = null;
        private static ApplicationLauncherButton appButtonConfigWindow = null;
        private static bool showConfigWindow = false;

        private static Rect configWindowRect;

        private static string uiElectricDrain;

        public void Awake()
        {
            //only do this portion once ever
            if (primaryInitialize)
            {
                primaryInitialize = false;
                OSPGlobal.Log("Initializing");

                AddAppButtons();      

                //load settings if config file exists
                if (System.IO.File.Exists(OSPGlobal.SettingsPath))
                {
                    ConfigNode settingsRoot = ConfigNode.Load(OSPGlobal.SettingsPath);
                    ConfigNode settings = settingsRoot.GetNode("SETTINGS");

                    //EXTENDED SURVEY
                    if (settings.HasValue(OSPGlobal.sExtendedSurvey))
                    {
                        bool result;
                        bool a = bool.TryParse(settings.GetValue(OSPGlobal.sExtendedSurvey), out result);
                        if (a) OSPGlobal.ExtendedSurvey = result;
                    }

                    //BIOME MAP REQUIRES SCAN
                    if (settings.HasValue(OSPGlobal.sBiomeMapRequiresScan))
                    {
                        bool result;
                        bool a = bool.TryParse(settings.GetValue(OSPGlobal.sBiomeMapRequiresScan), out result);
                        if (a) OSPGlobal.BiomeMapRequiresScan = result;
                    }

                    //OVERLAY REQUIRES TRANSMIT
                    if (settings.HasValue(OSPGlobal.sOverlayRequiresTransmit))
                    {
                        bool result;
                        bool a = bool.TryParse(settings.GetValue(OSPGlobal.sOverlayRequiresTransmit), out result);
                        if (a) OSPGlobal.OverlayRequiresTransmit = result;
                    }

                    //BACKGROUND SCAN
                    if (settings.HasValue(OSPGlobal.sBackgroundScan))
                    {
                        bool result;
                        bool a = bool.TryParse(settings.GetValue(OSPGlobal.sBackgroundScan), out result);
                        if (a) OSPGlobal.BackgroundScan = result;
                    }

                    //SCAN AUTO COMPLETE THRESHOLD
                    if (settings.HasValue(OSPGlobal.sScanAutoCompleteThreshold))
                    {
                        float result;
                        bool a = float.TryParse(settings.GetValue(OSPGlobal.sScanAutoCompleteThreshold), out result);
                        if (a) OSPGlobal.ScanAutoCompleteThreshold = result;
                    }

                    //MIN ALTITUDE STUFF
                    if (settings.HasValue(OSPGlobal.sMinAltitudeFactor))
                    {
                        double result;
                        bool a = double.TryParse(settings.GetValue(OSPGlobal.sMinAltitudeFactor), out result);
                        if (a) OSPGlobal.MinAltitudeFactor = result;
                    }

                    if (settings.HasValue(OSPGlobal.sMinAltitudeAbsolute))
                    {
                        double result;
                        bool a = double.TryParse(settings.GetValue(OSPGlobal.sMinAltitudeAbsolute), out result);
                        if (a) OSPGlobal.MinAltitudeAbsolute = result;
                    }

                    //MAX ALTITUDE STUFF
                    if (settings.HasValue(OSPGlobal.sMaxAltitudeFactor))
                    {
                        double result;
                        bool a = double.TryParse(settings.GetValue(OSPGlobal.sMaxAltitudeFactor), out result);
                        if (a) OSPGlobal.MaxAltitudeFactor = result;
                    }

                    if (settings.HasValue(OSPGlobal.sMaxAltitudeAbsolute))
                    {
                        double result;
                        bool a = double.TryParse(settings.GetValue(OSPGlobal.sMaxAltitudeAbsolute), out result);
                        if (a) OSPGlobal.MaxAltitudeAbsolute = result;
                    }

                    //BACKGROUND SCANNING
                    if (settings.HasValue(OSPGlobal.sTimeBetweenScans))
                    {
                        double result;
                        bool a = double.TryParse(settings.GetValue(OSPGlobal.sTimeBetweenScans), out result);
                        if (a) OSPGlobal.TimeBetweenScans = result;
                    }
                }
                else
                {
                    OSPGlobal.Log("setings.cfg not found - new one created with default values");
                    Save();
                }

                //Produce info.txt file
                Info();

                //config window position
                configWindowRect = new Rect(50f, 100f, 310f, 150f);
            }

            //set in-game menu strings
            SetUIStrings();

        }

        public static void AddAppButtons()
        {                          
            appButtonBiomeOverlay = ApplicationLauncher.Instance.AddModApplication(
            ShowBiomeOverlay,
            ShowBiomeOverlay,
            null,
            null,
            null,
            null,
            ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.TRACKSTATION,
            GameDatabase.Instance.GetTexture("OrbitalSurveyPlus/Textures/OSPIcon-Biome", false)
            );
                

                
            appButtonConfigWindow = ApplicationLauncher.Instance.AddModApplication(
            ShowConfigWindow,
            HideConfigWindow,
            null,
            null,
            HideConfigWindow,
            null,
            ApplicationLauncher.AppScenes.SPACECENTER,
            GameDatabase.Instance.GetTexture("OrbitalSurveyPlus/Textures/OSPIcon-Config", false)
            );   
        }

        public static void ShowBiomeOverlay()
        {
            //since button is a toggle, keep it at "disabled" graphic
            appButtonBiomeOverlay.SetFalse(false);

            Vessel vessel = null;
            CelestialBody body = null;

            if (MapView.MapIsEnabled)
            {
                //find the object the mapview camera is focused on
                MapObject focusedObj = MapView.MapCamera.target;

                //it could either be a vessel or a body
                vessel = focusedObj.vessel;
                body = focusedObj.celestialBody;

                //depending on whether camera focus is the vessel or a body, fill in the other
                if (vessel != null)
                {
                    //focused object is a vessel

                    //set body to the current main body
                    body = vessel.mainBody;
                }
                else if (body != null)
                {
                    //focused object is a body

                    //set vessel to active vessel, if possible
                    vessel = FlightGlobals.ActiveVessel;
                }
            }
            else
            {
                body = FlightGlobals.getMainBody();
                vessel = FlightGlobals.ActiveVessel;
            }

            //sanity check
            if (body == null)
            {
                OSPGlobal.Log("Error: unable to identify focused body!");
                return;
            }

            //scan check
            if (OSPScenario.GetBodyScanData(body) == null || (OSPGlobal.BiomeMapRequiresScan && !ResourceMap.Instance.IsPlanetScanned(body.flightGlobalsIndex)))
            {
                ScreenMessages.PostScreenMessage(String.Format("Biome Map Unavailable: No survey data available for {0}",
                    body.RevealName()),
                    5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            //overlay switch
            if (body.ResourceMap != null)
            {
                //turn off
                body.SetResourceMap(null);
            }
            else if (OSPGlobal.ExtendedSurvey && OSPGlobal.BiomeMapRequiresScan)
            {
                //turn on and shroud basnd on survey data
                int biomeWidth;
                int biomeHeight;
                Color32[] oldColors = OSPGlobal.GetPixels32FromBiomeMap(body, out biomeWidth, out biomeHeight);
                ScanDataBody data = OSPScenario.GetBodyScanData(body);
                Color32[] newColors = OSPScenario.ShroudColor32Array(oldColors, biomeWidth, biomeHeight, data);
                
                Texture2D newTexture = new Texture2D(biomeWidth, biomeHeight, TextureFormat.ARGB32, true);
                newTexture.SetPixels32(newColors);
                newTexture.Apply();

                OSPScenario.setCurrentKnownOverlay(body, newTexture.GetInstanceID());
                body.SetResourceMap(newTexture);
                
                //OSPScenario.ShowScannedRegionsOverlay(body); //(DEBUGGING)
            }
            else
            {
                //turn on in full
                Texture2D biomeTexture = body.BiomeMap.CompileRGB();
                body.SetResourceMap(biomeTexture);
            }
        }

        public static void ShowConfigWindow()
        {
            showConfigWindow = true;
        }

        public static void HideConfigWindow()
        {
            showConfigWindow = false;
        }

        public void OnGUI()
        {
            if (showConfigWindow)
            {
                Rect rect = GUILayout.Window(
                    GetInstanceID(),
                    configWindowRect,
                    DrawConfigWindow,
                    OSPGlobal.OSP_TITLE + " " + OSPGlobal.VERSION_STRING,
                    GUILayout.ExpandHeight(true),
                    GUILayout.ExpandWidth(true)
                );

                configWindowRect = rect;
            }
        }

        public void DrawConfigWindow(int windowId)
        {
            bool change = false;

            GUILayout.BeginVertical();

            //-----------------------------------------------------

            GUILayout.BeginHorizontal();
            bool extendedSurvey = GUILayout.Toggle(OSPGlobal.ExtendedSurvey, "  Enable Orbital Surveyor Plus");
            if (extendedSurvey != OSPGlobal.ExtendedSurvey)
            {
                OSPGlobal.ExtendedSurvey = extendedSurvey;
                change = true;
            }
            GUILayout.EndHorizontal();

            //------------------------------------------------------           

            GUILayout.BeginHorizontal();
            bool biomeMapRequiresScan = GUILayout.Toggle(OSPGlobal.BiomeMapRequiresScan, "  Biome Map Requires Scan");
            if (biomeMapRequiresScan != OSPGlobal.BiomeMapRequiresScan)
            {
                OSPGlobal.BiomeMapRequiresScan = biomeMapRequiresScan;
                change = true;
            }
            GUILayout.EndHorizontal();

            //------------------------------------------------------           

            GUILayout.BeginHorizontal();
            bool overlayRequiresTransmit = GUILayout.Toggle(OSPGlobal.OverlayRequiresTransmit, 
                OSPGlobal.ExtendedSurvey ? "  Overlays Require Data Transmit" : "<color=#6d6d6d>  Overlays Require Data Transmit</color>");
            if (overlayRequiresTransmit != OSPGlobal.OverlayRequiresTransmit)
            {
                OSPGlobal.OverlayRequiresTransmit = overlayRequiresTransmit;
                change = true;
            }
            GUILayout.EndHorizontal();

            //------------------------------------------------------

            GUILayout.BeginHorizontal();
            bool backgroundScan = GUILayout.Toggle(OSPGlobal.BackgroundScan,
                OSPGlobal.ExtendedSurvey ? "  Enable Background Scan" : "<color=#6d6d6d>  Enable Background Scan</color>");
            if (backgroundScan != OSPGlobal.BackgroundScan)
            {
                OSPGlobal.BackgroundScan = backgroundScan;
                change = true;
            }
            GUILayout.EndHorizontal();

            //------------------------------------------------------
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.Label(
                OSPGlobal.ExtendedSurvey && OSPGlobal.BackgroundScan ? "Time Between Scans (sec)" : "<color=#6d6d6d>Time Between Scans (sec)</color>");
            GUILayout.EndVertical();


            //-------------------------------------------------------

            GUILayout.BeginVertical();

            //-------------------------------------------------------

            GUILayout.BeginHorizontal();
            uiElectricDrain = GUILayout.TextField(uiElectricDrain);

            float time;
            if (float.TryParse(uiElectricDrain, out time) &&
                time != OSPGlobal.TimeBetweenScans)
            {
                OSPGlobal.TimeBetweenScans = time;
                change = true;
            }
            GUILayout.EndHorizontal();


            //-------------------------------------------------------
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            //------------------------------

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to Defaults"))
            {
                OSPGlobal.ResetSettingsToDefault();
                SetUIStrings();
                change = true;
            }
            GUILayout.EndHorizontal();

            //------------------------------

            GUILayout.EndVertical();

            GUI.DragWindow();

            if (change)
            {
                Save();
            }
        }

        public void SetUIStrings()
        {
            uiElectricDrain = OSPGlobal.TimeBetweenScans.ToString();

            //make sure floats are shown to be decimals to user
            if (!uiElectricDrain.Contains("."))
            {
                uiElectricDrain += ".0";
            }
        }

        public void Save() //Settings file save
        {
            //Save Settings File
            ConfigNode file = new ConfigNode();

            ConfigNode settings = file.AddNode("SETTINGS");

            settings.AddValue(OSPGlobal.sExtendedSurvey, OSPGlobal.ExtendedSurvey, 
                "disabling this will revert the scan process to stock (biome map will still be available)");
            settings.AddValue(OSPGlobal.sBiomeMapRequiresScan, OSPGlobal.BiomeMapRequiresScan,
                "whether the biome map requires a resource scan (either OSP scan or stock scan)");
            settings.AddValue(OSPGlobal.sOverlayRequiresTransmit, OSPGlobal.OverlayRequiresTransmit,
                "with this enabled, resource and biome overlays will be shrouded until scan data is transmitted back to kerbin");
            settings.AddValue(OSPGlobal.sBackgroundScan, OSPGlobal.BackgroundScan,
                "disabling this will completely shut off any background scanning (only active vessels will scan)");
            settings.AddValue(OSPGlobal.sTimeBetweenScans, OSPGlobal.TimeBetweenScans,
                "time (seconds) between scan updates on scanning vessels (both active and background)");
            settings.AddValue(OSPGlobal.sScanAutoCompleteThreshold, OSPGlobal.ScanAutoCompleteThreshold,
                "a value between 0 and 1 (0=0%, 1=100%), representing the point at which the survey of a planet will auto-complete to 100%");

            settings.AddValue(OSPGlobal.sMinAltitudeFactor, OSPGlobal.MinAltitudeFactor,
                "multiplied with a planet's radius (meters) to get the minimum altitude for scanning");
            settings.AddValue(OSPGlobal.sMinAltitudeAbsolute, OSPGlobal.MinAltitudeAbsolute,
                "a hard floor on minimum scanning altitude, in meters");
            settings.AddValue(OSPGlobal.sMaxAltitudeFactor, OSPGlobal.MaxAltitudeFactor,
                "multiplied with a planet's radius (meters) to get the maximum altitude for scanning");
            settings.AddValue(OSPGlobal.sMaxAltitudeAbsolute, OSPGlobal.MaxAltitudeAbsolute,
                "a hard ceiling on maximum scanning altitude, in meters");

            file.Save(OSPGlobal.SettingsPath);

        }

        public void Info()
        {
            ConfigNode file = new ConfigNode();

            ConfigNode settings = file.AddNode("INFO");

            settings.AddValue("Name", OSPGlobal.OSP_TITLE);
            settings.AddValue("Version", OSPGlobal.VERSION_STRING);
            settings.AddValue("ReleaseDate", OSPGlobal.VERSION_DATE.ToLongDateString());
            settings.AddValue("KSP", OSPGlobal.VERSION_KSP);
            settings.AddValue("Author", OSPGlobal.OSP_AUTHOR);
            settings.AddValue("Email", OSPGlobal.OSP_EMAIL);

            file.Save(OSPGlobal.InfoPath);
        }
    }
}
