using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalSurveyPlus
{
    class OSPGlobal
    {
        //------------ VERSION STUFF -----------------------------------------------------------------------------
        public const string OSP_TITLE = "Orbital Survey Plus";
        public const string OSP_AUTHOR = "Chase Barnes (Wheffle)";
        public const string OSP_EMAIL = "altoid287@gmail.com";
        public const int VERSION_MAJOR = 2;
        public const int VERSION_MINOR = 3;
        public const int VERSION_PATCH = 2;
        public const int VERSION_DEV = 3;
        public const string VERSION_KSP = "1.2";
        public static readonly DateTime VERSION_DATE = new DateTime(2017, 3, 5);

        public static readonly string VERSION_DEV_STRING =
            VERSION_DEV > 0 ? " (dev " + VERSION_DEV + ")" : "";

        public static readonly string VERSION_STRING = 
            VERSION_PATCH > 0 ? 
            VERSION_MAJOR + "." + VERSION_MINOR + "." + VERSION_PATCH + VERSION_DEV_STRING : 
            VERSION_MAJOR + "." + VERSION_DEV_STRING;

        public static readonly string VERSION_STRING_VERBOSE = VERSION_MAJOR + "." + VERSION_MINOR + "." + VERSION_PATCH + VERSION_DEV_STRING;
        //--------------------------------------------------------------------------------------------------------

        //NODE STUFF
        public const string OSPVersionName = "version";
        public const string OSPScanDataNodeName = "OSPScanData";
        public const string OSPDataMapWidthName = "dataWidth";
        public const string OSPDataMapHeightName = "dataHeight";
        public const string OSPScannedValueName = "data";
        public const string OSPRevealedValueName = "revealed";
        public const string OSPMitsGleanedValueName = "mitsTransmitted";

        //SETTINGS STUFF
        public const string SettingsPath = "GameData/OrbitalSurveyPlus/settings.cfg";
        public const string InfoPath = "GameData/OrbitalSurveyPlus/info.txt";

        public static bool ExtendedSurvey
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsBasic>().ExtendedSurvey; }
        }

        public static bool BiomeMapRequiresScan
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsBasic>().BiomeMapRequiresScan; }
        }

        public static bool OverlayRequiresTransmit
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsBasic>().OverlayRequiresTransmit; }
        }

        public static float ScanAutoCompleteThreshold
        {
            get { return (float) HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsBasic>().ScanAutocompleteThreshold; }
        }

        public static bool AutoRefresh
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsAdvanced>().AutoRefresh; }
        }

        public static bool BackgroundScan
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsAdvanced>().BackgroundScan; }
        }

        public static double TimeBetweenScans
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsAdvanced>().TimeBetweenScans; }
        }

        public static bool RetroactiveScanning
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsAdvanced>().RetroactiveScanning; }
        }

        public static int ProcessingLoad
        {
            get { return HighLogic.CurrentGame.Parameters.CustomParams<OSPParamsAdvanced>().ProcessingLoad; }
        }

        public static double MinAltitudeFactor
        {
            get { return 0.1d; }
        }

        public static double MinAltitudeAbsolute
        {
            get
            {
                if (CachedMinAltitudeAbsolute == 0)
                {
                    CelestialBody smallestWorld = GetSmallestWorld();
                    double smallScaleRatio = smallestWorld.Radius / GILLY_RADIUS;
                    CachedMinAltitudeAbsolute = MIN_ALT_ABSOLUTE * smallScaleRatio;
                }
                return CachedMinAltitudeAbsolute; 
            }
        }

        public static double MaxAltitudeFactor
        {
            get { return 5; }
        }

        public static double MaxAltitudeAbsolute
        {
            get
            {
                if (CachedMaxAltitudeAbsolute == 0)
                {
                    CelestialBody largestWorld = GetLargestWorld();
                    CachedMaxAltitudeAbsolute = MAX_ALT_ABSOLUTE * GetScaleRatio();                   
                }
                return CachedMaxAltitudeAbsolute;
            }
        }

        //Min/Max Altitude Private Values
        private const double MIN_ALT_ABSOLUTE = 25000d;        
        private const double MAX_ALT_ABSOLUTE = 1500000d;
        private static double CachedMinAltitudeAbsolute = 0d;
        private static double CachedMaxAltitudeAbsolute = 0d;

        //DATA ARRAY CONSTANTS
        public const double KERBIN_RADIUS = 600000d;
        public const double EVE_RADIUS = 700000d;
        public const double GILLY_RADIUS = 13000d;
        public const double SCAN_DATA_WIDTH_DIVISOR = 25d; //this makes kerbin's data array 300 width, 150 height; Eve's data array 350 width, 175 height (Eve being the largest scannable world)
        public const double SCAN_DATA_MITS_DIVISOR = 50000d; //this makes kerbin's total scanned data 90 mits

        //PLANET INFORMATION CACHES
        private static CelestialBody CachedLargestWorld = null;
        private static CelestialBody CachedSmallestWorld = null;
        private static double CachedScaleRatio = 0;
        private static Dictionary<int, CelestialBodyInfo> CachedBodyInfo = new Dictionary<int, CelestialBodyInfo>();

        //STATIC METHODS for accessing cached calculations and such
        //some hoops have been jumped through to limit tedius calculations from being performed often (calculations are only ran once and then saved off)
        //I've tried to get this as ambiguous as possible (using largest world and smallest world to scale stuff) so that OSP can work with mods like RSS as well as with stock
        //according to these calculations, the largest scannable body (scannable = can land on it) will have a data array of 350x175, and all other bodies will use that as reference
        public static CelestialBody GetLargestWorld()
        {
            if (CachedLargestWorld == null)
            {
                double largestRadius = 0d;
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (body.Radius > largestRadius && BodyCanBeScanned(body))
                    {
                        largestRadius = body.Radius;
                        CachedLargestWorld = body;
                    }
                }
            }
            return CachedLargestWorld;
        }

        public static CelestialBody GetSmallestWorld()
        {
            if (CachedSmallestWorld == null)
            {
                double smallestRadius = GetLargestWorld().Radius;
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (body.Radius < smallestRadius && BodyCanBeScanned(body))
                    {
                        smallestRadius = body.Radius;
                        CachedSmallestWorld = body;
                    }
                }
            }
            return CachedSmallestWorld;
        }

        public static double GetScaleRatio()
        {
            //scale ratio is currently based off of largest scannable world
            if (CachedScaleRatio == 0)
            {
                CelestialBody largestWorld = GetLargestWorld();
                CachedScaleRatio = largestWorld.Radius / EVE_RADIUS;
            }
            return CachedScaleRatio;
        }

        public static void GetScanDataArraySize(CelestialBody body, out int width, out int height)
        {
            CelestialBodyInfo info = GetCachedBodyInfo(body);
            width = info.DataWidth;
            height = info.DataHeight;
        }

        public static float GetScanDataTotalMits(CelestialBody body)
        {
            CelestialBodyInfo info = GetCachedBodyInfo(body);
            return info.TotalMits;
        }

        public static double ScanMinimumAltitude(CelestialBody body)
        {
            CelestialBodyInfo info = GetCachedBodyInfo(body);
            return info.MinScanAltitude;
        }

        public static double ScanMaximumAltitude(CelestialBody body)
        {
            CelestialBodyInfo info = GetCachedBodyInfo(body);
            return info.MaxScanAltitude;
        }

        //Calculate methods for re-running the math to find out celestial body information and getting cached body info

        private static CelestialBodyInfo GetCachedBodyInfo(CelestialBody body)
        {
            //gets cached objects holding calculations on data array size and total mits for bodies
            //if info doesn't exist yet, calcualations are ran and an info object is created (and cached)

            CelestialBodyInfo info;
            bool success = CachedBodyInfo.TryGetValue(body.flightGlobalsIndex, out info);

            if (!success)
            {
                info = new CelestialBodyInfo(body);
                CachedBodyInfo.Add(body.flightGlobalsIndex, info);
            }

            return info;
        }

        public static void CalculateScanDataArraySize(CelestialBody body, out int width, out int height)
        {
            //width based on body's circumference (2*pi*r), divided by a constant ratio
            double radius = body.Radius / GetScaleRatio();
            double radiusKm = radius / 1000;
            height = (int)((2d * Math.PI * radiusKm) / SCAN_DATA_WIDTH_DIVISOR);
            if (height < 10) height = 10; //for tiny planets (ahem, gilly...) this makes the data array way too small, so make it a minimum here
            width = height * 2;
        }

        public static float CalculateScanDataTotalMits(CelestialBody body)
        {
            //based on surface area
            double radius = body.Radius / GetScaleRatio();
            double radiusKm = radius / 1000;
            int mits = (int)((4d * Math.PI * radiusKm * radiusKm) / SCAN_DATA_MITS_DIVISOR);

            //make it a minimum of 5 mits (minmus and gilly would have 0 otherwise)
            if (mits < 5) mits = 5;

            //add one to be reserved for 100% scanning (to avoid any float approximation errors from causing
            //people not to be able to scan to reveal the last tiny bit of surface)
            mits = mits + 1;

            return mits;
        }

        public static double CalculateScanMinimumAltitude(CelestialBody body)
        {
            double minAltitude = Math.Max(body.Radius * MinAltitudeFactor, MinAltitudeAbsolute);
            return minAltitude;
        }

        public static double CalculateScanMaximumAltitude(CelestialBody body)
        {
            double maxAltitude = Math.Min(body.Radius * MaxAltitudeFactor, MaxAltitudeAbsolute);
            return maxAltitude;
        }

        //STATIC METHODS to do other stuff
        public static int longitudeToX(double lon, int width)
        {
            //lon starts out between -180 and 180
            //shift to between 0 and 360
            lon += 270;

            //double check for weird numbers from KSP engine
            while (lon < 0) lon += 360;
            while (lon > 360) lon -= 360;

            //find pixel based on scale
            double scale = 1 - (lon / 360);
            return (int)Math.Round(scale * width);
        }

        public static double XToLongitude(int x, int width)
        {
            //shift x because ksp's system is weird
            x = (x + (width / 4)) % width;

            //get a ratio between -1 and 1, 0 being in the middle
            double ratio = 1 - ((double)x / width);
            ratio = (ratio * 2) - 1;

            //longitude is a value between -180 and 180, so just multiply and return
            return ratio * 180;
        }

        public static int latitudeToY(double lat, int height)
        {
            //lat starts out between -90 and 90
            //shift to between 0 and 180
            lat += 90;

            //find pixel based on scale
            double scale = lat / 180;
            return (int)Math.Round(scale * height);
        }

        public static double YToLatitude(int y, int height)
        {
            //get a ratio between -1 and 1, 0 being in the middle
            double ratio = (double)y / height;
            ratio = (ratio * 2) - 1;

            //latitude is a value between -90 and 90, so just multiply and return
            return ratio * 90;
        }

        public static double MercatorScaleFactor(double latitude)
        {
            return 1 / Math.Cos(DegreesToRadians(latitude));
        }

        public static bool withinDistance(double x1, double y1, double x2, double y2, double dist)
        {
            return ((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1)) <= dist * dist;
        }

        public static double ClampLongitude(double lon)
        {
            //clamps longitude to standard, between -180 and 180
            lon += 180;
            lon %= 360;
            lon -= 180;
            return lon;
        }

        public static double ClampLatitude(double lat)
        {
            //clamps latitude to standard, between -90 and 90
            lat += 90;
            lat %= 180;
            lat -= 90;
            return lat;
        }

        public static void Log(string log)
        {
            PDebug.Log("[OrbitalSurveyPlus] " + log);
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        public static string DistanceToString(double dist)
        {
            if (dist < 10000)        return String.Format("{0:0.##}m", dist);
            if (dist < 10000000)     return String.Format("{0:0.##}km", dist/1000);
            if (dist < 10000000000)  return String.Format("{0:0.##}Mm", dist / 1000000);
            return String.Format("{0:0.##}Gm", dist / 1000000000);
        }

        public static bool BodyCanBeScanned(CelestialBody body)
        {
            if (body.BiomeMap == null || !body.hasSolidSurface) return false;
            return true;
        }

        public static Color32[] GetPixels32FromBiomeMap(CelestialBody body, out int width, out int height)
        {
            CBAttributeMapSO bm = body.BiomeMap;
            width = bm.Width;
            height = bm.Height;
            Color32[] pixels = new Color32[width * height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    pixels[(j * width) + i] = bm.GetPixelColor32(i, j);
                }
            }
            return pixels;
        }

        public static double GetVesselResourceProduction(Vessel v, string resName)
        {
            double production = 0;

            //Check Electric Generators
            List<ModuleGenerator> generators = v.FindPartModulesImplementing<ModuleGenerator>();
            foreach (ModuleGenerator m in generators)
            {
                if (m.generatorIsActive)
                {
                    foreach(ModuleResource r in m.resHandler.outputResources)
                    {
                        if (r.name == resName) production += r.rate;
                    }
                }
            }

            //Check Solar Panels
            List<ModuleDeployableSolarPanel> solarPanels = v.FindPartModulesImplementing<ModuleDeployableSolarPanel>();
            foreach (ModuleDeployableSolarPanel m in solarPanels)
            {
                if (m.deployState == ModuleDeployablePart.DeployState.EXTENDED)
                {
                    if (m.resourceName == resName) production += m.chargeRate;
                }
            }

            //Check Converters
            List<ModuleResourceConverter> converters = v.FindPartModulesImplementing<ModuleResourceConverter>();
            foreach (ModuleResourceConverter m in converters)
            {
                if (m.ModuleIsActive())
                {
                    foreach (ResourceRatio r in m.outputList)
                    {
                        if (r.ResourceName == resName) production += r.Ratio;
                    }
                }
            }

            return production;
        }

        public static char BinaryToHex(string bin)
        {
            switch (bin)
            {
                case "0000": return '0';
                case "0001": return '1';
                case "0010": return '2';
                case "0011": return '3';
                case "0100": return '4';
                case "0101": return '5';
                case "0110": return '6';
                case "0111": return '7';
                case "1000": return '8';
                case "1001": return '9';
                case "1010": return 'A';
                case "1011": return 'B';
                case "1100": return 'C';
                case "1101": return 'D';
                case "1110": return 'E';
                case "1111": return 'F';
            }
            OSPGlobal.Log("ERROR: conversion from binary string to hex string got weird input: " + bin);
            return '0';
        }

        public static string HexToBinary(char hex)
        {
            switch (hex)
            {
                case '0': return "0000";
                case '1': return "0001";
                case '2': return "0010";
                case '3': return "0011";
                case '4': return "0100";
                case '5': return "0101";
                case '6': return "0110";
                case '7': return "0111";
                case '8': return "1000";
                case '9': return "1001";
                case 'A': case 'a': return "1010";
                case 'B': case 'b': return "1011";
                case 'C': case 'c': return "1100";
                case 'D': case 'd': return "1101";
                case 'E': case 'e': return "1110";
                case 'F': case 'f': return "1111";
            }
            OSPGlobal.Log("ERROR: conversion from hex string to binary string on scan data serializaion got weird input: " + hex);
            return "0000";
        }

        public static bool[,] SnapshotDataMap(bool[,] array)
        {
            int width = array.GetLength(0);
            int height = array.GetLength(1);
            bool[,] snapshot = new bool[width, height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++)
                {
                    snapshot[i, j] = array[i, j];
                }
            }
            return snapshot;
        }

        private class CelestialBodyInfo
        {
            //this is mainly a data bucket for cached values from tedius calculations we only want to run once

            public CelestialBody Body { get; set; }
            public int DataWidth { get; set; }
            public int DataHeight { get; set; }
            public float TotalMits { get; set; }
            public double MinScanAltitude { get; set; }
            public double MaxScanAltitude { get; set; }

            public CelestialBodyInfo(CelestialBody body)
            {
                Body = body;
                CalculateDataDimensions();
                CalculateTotalMits();
                MinScanAltitude = CalculateScanMinimumAltitude(body);
                MaxScanAltitude = CalculateScanMaximumAltitude(body);
            }

            public void CalculateDataDimensions()
            {
                int width, height;
                CalculateScanDataArraySize(Body, out width, out height);
                DataWidth = width;
                DataHeight = height;
            }

            public void CalculateTotalMits()
            {
                TotalMits = CalculateScanDataTotalMits(Body);
            }
        }
    }
}
