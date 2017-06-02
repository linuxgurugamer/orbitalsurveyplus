using System;
using UnityEngine;
using KSP.UI.Screens;

namespace OrbitalSurveyPlus
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    class OSPUI : MonoBehaviour
    {
        private static bool primaryInitialize = true;
        private static Texture2D iconBiome = null;

        private static ApplicationLauncherButton appButtonBiomeOverlay = null;

        protected virtual void Awake()
        {
            //subscribe to app launcher events
            GameEvents.onGUIApplicationLauncherReady.Add(AppLancherReadyCallback);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(AppLauncherDestroyedCallback);
            GameEvents.onGameSceneLoadRequested.Add(AppLauncherSceneLoadCallback);

            //only do this portion once ever
            if (primaryInitialize)
            {
                primaryInitialize = false;
                Info();
            }
        }

        protected virtual void OnDestroy()
        {
            //unsubscrite to app launcher events
            GameEvents.onGUIApplicationLauncherReady.Remove(AppLancherReadyCallback);
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(AppLauncherDestroyedCallback);
            GameEvents.onGameSceneLoadRequested.Remove(AppLauncherSceneLoadCallback);
            AppLauncherRemoveButtons();
        }      

        public void AppLancherReadyCallback()
        {
            AppLauncherAddButtons();        
        }

        public void AppLauncherDestroyedCallback()
        {
            AppLauncherRemoveButtons();           
        }

        public void AppLauncherSceneLoadCallback(GameScenes scene)
        {
            AppLauncherRemoveButtons();
        }

        public void AppLauncherAddButtons()
        {
            if (ApplicationLauncher.Ready && appButtonBiomeOverlay == null)
            {
                if (iconBiome == null)
                {
                    iconBiome = GameDatabase.Instance.GetTexture("OrbitalSurveyPlus/Textures/OSPIcon-Biome", false);
                }

                appButtonBiomeOverlay = ApplicationLauncher.Instance.AddModApplication(
                    ShowBiomeOverlay,
                    ShowBiomeOverlay,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.TRACKSTATION,
                    iconBiome
                );
            }
        }

        public void AppLauncherRemoveButtons()
        {
            if (appButtonBiomeOverlay != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appButtonBiomeOverlay);
                appButtonBiomeOverlay = null;
            }
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
                    body.GetDisplayName()),
                    5.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            //overlay switch            
            if (body.ResourceMap != null)
            {
                //turn off
                body.SetResourceMap(null);
                body.HideSurfaceResource();
            }
            else if (OSPGlobal.ExtendedSurvey && OSPGlobal.BiomeMapRequiresScan)
            {
                body.HideSurfaceResource();

                //turn on and shroud basnd on survey data
                int biomeWidth;
                int biomeHeight;
                Color32[] oldColors = OSPGlobal.GetPixels32FromBiomeMap(body, out biomeWidth, out biomeHeight);
                OSPScenario.UpdateBodyFullOverlayCache(body, oldColors);
                ScanDataBody data = OSPScenario.GetBodyScanData(body);
                Color32[] newColors = OSPScenario.ShroudColor32Array(oldColors, biomeWidth, biomeHeight, data);
                
                Texture2D newTexture = new Texture2D(biomeWidth, biomeHeight, TextureFormat.ARGB32, true);
                newTexture.SetPixels32(newColors);
                newTexture.Apply();

                body.SetResourceMap(newTexture);
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
            settings.AddValue("Version", OSPGlobal.VERSION_STRING_VERBOSE);
            settings.AddValue("ReleaseDate", OSPGlobal.VERSION_DATE.ToLongDateString());
            settings.AddValue("KSP", OSPGlobal.VERSION_KSP);
            settings.AddValue("Author", OSPGlobal.OSP_AUTHOR);
            settings.AddValue("Email", OSPGlobal.OSP_EMAIL);

            file.Save(OSPGlobal.InfoPath);
        }
    }
}
