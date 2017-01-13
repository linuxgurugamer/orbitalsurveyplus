using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OrbitalSurveyPlus
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames | ScenarioCreationOptions.AddToExistingGames, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    class OSPScenario : ScenarioModule
    {
        private static ScanDataUniverse scanData = new ScanDataUniverse();
        private static double lastScanTime = 0;
        private static Queue<ScanRequest> scanQueue = new Queue<ScanRequest>();
        private static Dictionary<Guid, ScanRequest> lastScanRequests = new Dictionary<Guid, ScanRequest>();
        private static Dictionary<int, Color32[]> bodyFullOverlays = new Dictionary<int, Color32[]>();
        private static DateTime lastRefresh = DateTime.UtcNow;

        private static readonly double AUTO_REFRESH_INTERVAL = 500; //milliseconds between each auto-refresh

        public override void OnAwake()
        {
            base.OnAwake();
            bodyFullOverlays.Clear();
            lastScanTime = 0;
            lastScanRequests.Clear();
            //not clearing this anymore, we want queued scan processing to continue across scenes
            /*
            scanQueue.Clear();   
            */      
        }

        public void OnStart()
        {
            GameEvents.onGameSceneLoadRequested.Add(SceneChangeHook);
        }

        public void OnDestroy()
        {
            GameEvents.onGameSceneLoadRequested.Remove(SceneChangeHook);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            scanData.Load(node);
            lastScanTime = 0;
            scanQueue.Clear();
            lastScanRequests.Clear();
            bodyFullOverlays.Clear();
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
                RefreshBodyOverlay(body);
            }
        }

        public void SceneChangeHook(GameScenes scene)
        {
            //this disallows retroactive scanning for the first game update of a new scene, fixing some funky behavior
            lastScanTime = 0;
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

        public static void ShroudBodyOverlay(CelestialBody body)
        {
            Texture2D resTexture = body.ResourceMap;

            //if there is no overlay for this body, don't do anything
            if (resTexture == null)
            {
                return;
            }
            
            //get scan body data (could be null)
            ScanDataBody data = GetBodyScanData(body.flightGlobalsIndex);

            //get info from current resource map
            int width = resTexture.width;
            int height = resTexture.height;
            Color32[] oldColors = resTexture.GetPixels32();
            UpdateBodyFullOverlayCache(body, oldColors);

            //get color map for new texture
            Color32[] newColors = ShroudColor32Array(oldColors, width, height, data);
            
            //set pixels to new colors in resource map
            resTexture.SetPixels32(newColors);
            resTexture.Apply();
        }

        public static void UpdateBodyFullOverlayCache(CelestialBody body, Color32[] pixels)
        {
            bodyFullOverlays[body.flightGlobalsIndex] = pixels;
        }

        public static void RefreshBodyOverlay(CelestialBody body, bool forceRefresh = false)
        {
            if (!OSPGlobal.AutoRefresh) return;

            //if body currently has no resource texture, abort
            Texture2D resTexture = body.ResourceMap;
            if (resTexture == null) return;

            //if no full overlay data exists, abort
            int bodyIndex = body.flightGlobalsIndex;
            if (!bodyFullOverlays.ContainsKey(bodyIndex)) return;

            //don't try to refresh large overlays (biome overlays), it takes too long
            if (!forceRefresh && HasLargeOverlayTexture(body)) return;

            //if it hasn't been long enough since the last refresh, skip the refresh
            double msSinceLastRefresh = (DateTime.UtcNow - lastRefresh).TotalMilliseconds;
            if (!forceRefresh && msSinceLastRefresh < AUTO_REFRESH_INTERVAL) return;           
            
            //reset texture with full overlay and re-execute the shroud function to update the map
            Color32[] fullPixels = bodyFullOverlays[bodyIndex];
            resTexture.SetPixels32(fullPixels);
            resTexture.Apply();
            ShroudBodyOverlay(body);
            lastRefresh = DateTime.UtcNow;
        }

        private static bool HasLargeOverlayTexture(CelestialBody body)
        {
            Texture2D texture = body.ResourceMap;
            if (texture == null) return false;
            CBAttributeMapSO bm = body.BiomeMap;
            int w = bm.Width;
            int h = bm.Height;
            if (texture.width >= w && texture.height >= h) return true;
            return false;
        }

        public static bool CurrentOverlayIsShrouded(CelestialBody body)
        {
            Texture2D overlay = body.ResourceMap;
            if (overlay == null) return true;
            Color32[] colors = overlay.GetPixels32();
            Color32 shrouded = ProduceShroudedPixel();
            foreach (Color32 c in colors)
            {
                if (c.Equals(shrouded)) return true;
            }

            return false;
        }

        public void LateUpdate()
        {
            //OVERLAY HIJACKER: snoop for recently placed planet overlays and shroud them appropriately
            if (OSPGlobal.ExtendedSurvey && (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneHasPlanetarium))
            {
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (body.ResourceMap == null) continue;
                    if (scanData.BodyIsFullyScanned(body.flightGlobalsIndex)) continue;
                    if (CurrentOverlayIsShrouded(body)) continue;
                    ShroudBodyOverlay(body);                  
                }
            }
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
                    BackgroundScan();
                    lastScanTime = ut;
                }
            }

            //Execute scan requests, the number based on processing load parameter
            for (int i = 0; i < OSPGlobal.ProcessingLoad; i++)
            {
                if (scanQueue.Count == 0) break;
                ScanRequest req = scanQueue.Dequeue();
                req.ExecuteRequest();
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

                    // if scan data is 100% or target is un-scannable, skip
                    if (!OSPGlobal.BodyCanBeScanned(v.mainBody)) continue;
                    if (scanData.BodyIsFullyScanned(v.mainBody.flightGlobalsIndex)) continue;
                    
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
                                            if (lastScanTime == 0) QueueScanRequest(v, v.mainBody, v.longitude, v.latitude, true, scanRadius);
                                            else QueueScanRequestReroactive(v, true, scanRadius, lastScanTime); 
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

        //if more than "TimeBetweenScans" time has elapsed on high warp, this will retroactively "catch up" on scanning to prevent missing spots
        public static void QueueScanRequestReroactive(Vessel v, bool scanned, int scanRadius, double lastScanTime)
        {
            if (!OSPGlobal.RetroactiveScanning)
            {
                QueueScanRequest(v, v.mainBody, v.longitude, v.latitude, scanned, scanRadius);
                return;
            }

            double ut = Planetarium.GetUniversalTime();
            lastScanTime += OSPGlobal.TimeBetweenScans;
            while (lastScanTime <= ut)
            {
                //get orbital parameters at scan time
                Orbit orbit = v.orbit;
                CelestialBody body = orbit.referenceBody;
                Vector3d position = orbit.getPositionAtUT(lastScanTime);
                double lon = OSPGlobal.ClampLongitude(body.GetLongitude(position));                
                double lat = OSPGlobal.ClampLatitude(body.GetLatitude(position));

                //execute scan
                QueueScanRequest(v, body, lon, lat, scanned, scanRadius);

                //run the clock foward
                lastScanTime += OSPGlobal.TimeBetweenScans;
            }
        }

        public static void QueueScanRequest(Vessel v, CelestialBody body, double lon, double lat, bool doScan, int scanRadius)
        {
            if (lastScanRequests.ContainsKey(v.id))
            {
                //if the last scan was extremely close to this one, skip it
                ScanRequest lastReq = lastScanRequests[v.id];
                if (lastReq.body.flightGlobalsIndex == body.flightGlobalsIndex)
                {
                    //map longitudes and latitudes to points on the scan data grid
                    int width;
                    int height;
                    OSPGlobal.GetScanDataArraySize(body, out width, out height);

                    double xscale = OSPGlobal.MercatorScaleFactor(lat);

                    int x1 = (int) Math.Round(OSPGlobal.longitudeToX(lon, width) / xscale);
                    int y1 = OSPGlobal.latitudeToY(lat, height);

                    int x2 = (int) Math.Round(OSPGlobal.longitudeToX(lastReq.longitude, width) / xscale);
                    int y2 = OSPGlobal.latitudeToY(lastReq.latitude, height);

                    //if distance is less than scan radius / 4, throw it out
                    int distCheck = Math.Max(scanRadius / 4, 1);
                    if (OSPGlobal.withinDistance(x1, y1, x2, y2, distCheck)) return;
                }
            }

            //create new scan request and queue it
            ScanRequest req = new ScanRequest(body, lon, lat, doScan, scanRadius);
            scanQueue.Enqueue(req);
            lastScanRequests[v.id] = req;
        }

        private class ScanRequest
        {
            public CelestialBody body { get; set; }
            public double longitude { get; set; }
            public double latitude { get; set; }
            public bool reveal { get; set; }
            public int scanRadius { get; set; }

            public ScanRequest(CelestialBody b, double lon, double lat, bool doScan, int radius)
            {
                body = b;
                longitude = lon;
                latitude = lat;
                reveal = doScan;
                scanRadius = radius;               
            }

            public void ExecuteRequest()
            {
                UpdateScanData(body, reveal, longitude, latitude, scanRadius);
            }
        }

    }
}
