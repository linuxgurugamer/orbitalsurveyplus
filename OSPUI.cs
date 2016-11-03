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

        public void Awake()
        {
            //only do this portion once ever
            if (primaryInitialize)
            {
                primaryInitialize = false;
                OSPGlobal.Log("Initializing");

                //application launcher stuff
                AddAppButtons();      

                //Produce info.txt file
                Info();
            }
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
            if (OSPGlobal.BiomeMapRequiresScan && !ResourceMap.Instance.IsPlanetScanned(body.flightGlobalsIndex))
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
