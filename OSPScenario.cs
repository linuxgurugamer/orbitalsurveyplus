using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace OrbitalSurveyPlus
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToExistingGames, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    class OSPScenario : ScenarioModule
    {
        private static ScanDataUniverse scanData = new ScanDataUniverse();
        private static Dictionary<int, int> currentOverlays = new Dictionary<int, int>();
        private static double lastScanTime = 0;

        public override void OnAwake()
        {
            base.OnAwake();
            lastScanTime = 0;
            GameEvents.OnMapEntered.Add(OnMapEnteredCallback);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            scanData.Load(node);
            lastScanTime = 0;
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            scanData.Save(node);
            node.AddValue(OSPGlobal.OSPVersionName, OSPGlobal.VERSION_STRING_VERBOSE);
        }

        public static ScanDataBody GetBodyScanData(int bodyIndex)
        {
            return scanData.GetBodyScanData(bodyIndex);
        }

        public static ScanDataBody GetBodyScanData(CelestialBody body)
        {
            return GetBodyScanData(body.flightGlobalsIndex);
        }

        public static void UpdateScanData(CelestialBody body, bool scanned, double lon, double lat, int radius)
        {
            scanData.UpdateScanData(body, scanned, lon, lat, radius);

            if (scanned && !OSPGlobal.OverlayRequiresTransmit)
            {
                ResourceMap.Instance.UnlockPlanet(body.flightGlobalsIndex);
            }
        }

        public static Color32[] ShroudColor32Array(Color32[] oldColors, int width, int height, ScanDataBody data)
        {
            Color32[] newColors = new Color32[oldColors.Length];

            //if the data is null, shroud everything
            if (data == null)
            {
                for (int j = 0; j < height; j++)
                {
                    for (int i = 0; i < width; i++)
                    {
                        newColors[(j * width) + i] = ProduceShroudedPixel();
                    }
                }
                return newColors;
            }

            int dataWidth = data.Width;
            int dataHeight = data.Height;

            //go pixel-by-pixel and shroud areas that aren't scanned
            double scalex = (double)width / (double)dataWidth;
            double scaley = (double)height / (double)dataHeight;
            
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    //get point corresponding with the current texture pixel
                    int point_x = Math.Min((int)Math.Round(i / scalex), dataWidth - 1);
                    int point_y = Math.Min((int)Math.Round(j / scaley), dataHeight - 1);

                    //decide whether to reveal this pixel
                    Color32 pixel = ProduceShroudedPixel();
                    if (data.IsPointRevealed(point_x, point_y))
                    {
                        pixel = oldColors[(j * width) + i];
                    }

                    newColors[(j * width) + i] = pixel;
                }
            }

            return newColors;
        }

        private static Color32 ProduceShroudedPixel()
        {
            return new Color32(127, 127, 127, 127);
        }

        public static Texture2D ShroudBodyOverlay(CelestialBody body)
        {
            Texture2D resourceMap = body.ResourceMap;

            //if there is no overlay for this body, don't do anything
            if (resourceMap == null) return null;

            //get scan body data (could be null)
            ScanDataBody data = GetBodyScanData(body.flightGlobalsIndex);
            
            //get info from current resource map
            int width = resourceMap.width;
            int height = resourceMap.height;
            Color32[] oldColors = resourceMap.GetPixels32();

            //get color map for new texture
            Color32[] newColors = ShroudColor32Array(oldColors, width, height, data);

            //set pixels to new colors in resource map
            resourceMap.SetPixels32(newColors);
            resourceMap.Apply();

            //return resource map
            return resourceMap;
        }

        //this is for debugging
        public static void ShowScannedRegionsOverlay(CelestialBody body)
        {
            ScanDataBody data = GetBodyScanData(body);
            int width = data.Width;
            int height = data.Height;
            Color32[] pixels = new Color32[width * height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    Color32 pixel = Color.clear;
                    if (data.IsPointScanned(i, j))
                    {
                        pixel = Color.yellow;
                    }

                    pixels[(j * width) + i] = pixel;
                }
            }

            Texture2D overlay = new Texture2D(width, height, TextureFormat.ARGB32, true);
            overlay.SetPixels32(pixels);
            overlay.Apply();

            body.SetResourceMap(overlay);
            setCurrentKnownOverlay(body, overlay.GetInstanceID());
        }

        public static void setCurrentKnownOverlay(CelestialBody body, int textureInstanceID)
        {
            currentOverlays[body.flightGlobalsIndex] = textureInstanceID;
        }

        public void LateUpdate()
        {
            //OVERLAY HIJACKER: snoop for recently placed planet overlays and shroud them appropriately
            if (OSPGlobal.ExtendedSurvey && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneHasPlanetarium))
            {
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    int bodyIndex = body.flightGlobalsIndex;
                    int lastKnownTextureID = -1;
                    if (currentOverlays.ContainsKey(bodyIndex))
                    {
                        lastKnownTextureID = currentOverlays[bodyIndex];
                    }

                    if (body.ResourceMap == null)
                    {
                        currentOverlays[bodyIndex] = -1;
                    }
                    else
                    {
                        if (body.ResourceMap.GetInstanceID() != lastKnownTextureID)
                        {
                            OSPGlobal.Log("Swapping new texture found with shrouded texture");
                            Texture2D newTexture = ShroudBodyOverlay(body);
                            currentOverlays[bodyIndex] = newTexture.GetInstanceID();
                        }
                    }
                }
            }
        }

        public void OnMapEnteredCallback()
        {
            currentOverlays.Clear();
        }

        public void Update()
        {
            //Background Scanning
            if (OSPGlobal.ExtendedSurvey && OSPGlobal.BackgroundScan)
            {
                double ut = Planetarium.GetUniversalTime();
                double elapsedTime = ut - lastScanTime;
                if (elapsedTime >= OSPGlobal.TimeBetweenScans)
                {
                    lastScanTime = ut;
                    BackgroundScan();
                }
            }
        }

        public static void BackgroundScan()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {               
                if (!v.loaded)
                {
                    bool hasScanned = false;
                    ProtoVessel pv = v.protoVessel;

                    foreach (ProtoPartSnapshot pps in pv.protoPartSnapshots)
                    {
                        if (hasScanned) break;
                        foreach (ProtoPartModuleSnapshot ppms in pps.modules)
                        {
                            if (hasScanned) break;
                            if (ppms.moduleName == "ModuleOrbitalSurveyorPlus")
                            {
                                ConfigNode node = ppms.moduleValues;
                                if (node.HasValue("perpetualScan"))
                                {
                                    bool isScanning;
                                    string value = node.GetValue("perpetualScan");
                                    bool a = bool.TryParse(value, out isScanning);

                                    if (a && isScanning)
                                    {
                                        if (v.altitude >= OSPGlobal.ScanMinimumAltitude(v.mainBody) &&
                                            v.altitude <= OSPGlobal.ScanMaximumAltitude(v.mainBody))
                                        {
                                            //get scan radius
                                            if (!node.HasValue("ScanRadius"))
                                            {
                                                OSPGlobal.Log("Field 'ScanRadius' missing on craft " + v.name + ", save might be legacy. Assuming radius 8.");
                                                node.AddValue("ScanRadius", 8);
                                            }
                                            string scanRadiusString = node.GetValue("ScanRadius");
                                            int scanRadius = int.Parse(scanRadiusString);

                                            //do the scan!
                                            UpdateScanData(v.mainBody, true, v.longitude, v.latitude, scanRadius);
                                            hasScanned = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } //end ridiculous nested-loop-hell of BackgroundScan()

    }
}
