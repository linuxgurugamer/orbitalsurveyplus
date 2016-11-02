using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public const int VERSION_MINOR = 2;
        public const int VERSION_PATCH = 0;
        public const string VERSION_KSP = "1.2.1";
        public static readonly DateTime VERSION_DATE = new DateTime(2016, 11, 1);

        public static readonly string VERSION_STRING = 
            VERSION_PATCH > 0 ? 
            VERSION_MAJOR + "." + VERSION_MINOR + "." + VERSION_PATCH : 
            VERSION_MAJOR + "." + VERSION_MINOR;

        public static readonly string VERSION_STRING_VERBOSE = VERSION_MAJOR + "." + VERSION_MINOR + "." + VERSION_PATCH;
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

        public const string sExtendedSurvey = "ExtendedSurvey";
        public const bool ExtendedSurveyDefault = true;
        public static bool ExtendedSurvey
        {
            get;
            set;
        } = ExtendedSurveyDefault;

        public const string sBiomeMapRequiresScan = "BiomeMapRequiresScan";
        public const bool BiomeMapRequiresScanDefault = true;
        public static bool BiomeMapRequiresScan
        {
            get;
            set;
        } = BiomeMapRequiresScanDefault;

        public const string sOverlayRequiresTransmit = "OverlayRequiresTransmit";
        public const bool OverlayRequiresTransmitDefault = true;
        public static bool OverlayRequiresTransmit
        {
            get;
            set;
        } = OverlayRequiresTransmitDefault;

        public const string sBackgroundScan = "BackgroundScan";
        public const bool BackgroundScanDefault = true;
        public static bool BackgroundScan
        {
            get;
            set;
        } = BackgroundScanDefault;

        public const string sScanAutoCompleteThreshold = "ScanAutoCompleteThreshold";
        public const float ScanAutoCompleteThresholdDefault = 0.95f;
        public static float ScanAutoCompleteThreshold
        {
            get;
            set;
        } = ScanAutoCompleteThresholdDefault;

        public const string sMinAltitudeFactor = "MinAltitudeFactor";
        public const double MinAltitudeFactorDefault = 0.1;
        public static double MinAltitudeFactor
        {
            get;
            set;
        } = MinAltitudeFactorDefault;

        public const string sMinAltitudeAbsolute = "MinAltitudeAbsolute";
        public const double MinAltitudeAbsoluteDefault = 25000;
        public static double MinAltitudeAbsolute
        {
            get;
            set;
        } = MinAltitudeAbsoluteDefault;

        public const string sMaxAltitudeFactor = "MaxAltitudeFactor";
        public const double MaxAltitudeFactorDefault = 5;
        public static double MaxAltitudeFactor
        {
            get;
            set;
        } = MaxAltitudeFactorDefault;

        public const string sMaxAltitudeAbsolute = "MaxAltitudeAbsolute";
        public const double MaxAltitudeAbsoluteDefault = 15000000;
        public static double MaxAltitudeAbsolute
        {
            get;
            set;
        } = MaxAltitudeAbsoluteDefault;

        public const string sTimeBetweenScans = "TimeBetweenScans";
        public const double TimeBetweenScansDefault = 1;
        public static double TimeBetweenScans
        {
            get;
            set;
        } = TimeBetweenScansDefault;

        //DATA ARRAY SIZE CONSTANT DIVISOR
        public const double SCAN_DATA_WIDTH_DIVISOR = 25; //this makes kerbin's data array 300 width, 150 height
        public const double SCAN_DATA_MITS_DIVISOR = 50000; //this makes kerbin's total scanned data 90 mits

        //STATIC METHODS
        public static void ResetSettingsToDefault()
        {
            ExtendedSurvey = ExtendedSurveyDefault;
            BiomeMapRequiresScan = BiomeMapRequiresScanDefault;
            OverlayRequiresTransmit = OverlayRequiresTransmitDefault;
            BackgroundScan = BackgroundScanDefault;
            ScanAutoCompleteThreshold = ScanAutoCompleteThresholdDefault;
            MinAltitudeFactor = MinAltitudeFactorDefault;
            MinAltitudeAbsolute = MinAltitudeAbsoluteDefault;
            MaxAltitudeFactor = MaxAltitudeFactorDefault;
            MaxAltitudeAbsolute = MaxAltitudeAbsoluteDefault;
            TimeBetweenScans = TimeBetweenScansDefault;
        }

        public static void GetScanDataArraySize(CelestialBody body, out int width, out int height)
        {
            //width based on body's circumference (2*pi*r), divided by a constant ratio
            double radiusKm = body.Radius / 1000;
            height = (int)((2d * Math.PI * radiusKm) / SCAN_DATA_WIDTH_DIVISOR);
            if (height < 10) height = 10; //for tiny planets (ahem, gilly...) this makes the data array way too small, so make it a minimum here
            width = height * 2;
        }

        public static float GetScanDataTotalMits(CelestialBody body)
        {
            //based on surface area
            double radiusKm = body.Radius / 1000;
            int mits = (int)((4d * Math.PI * radiusKm * radiusKm)/ SCAN_DATA_MITS_DIVISOR);

            //make it a minimum of 5 mits (minmus and gilly would have 0 otherwise)
            if (mits < 5) mits = 5;

            //add one to be reserved for 100% scanning (to avoid any float approximation errors from causing
            //people not to be able to scan to reveal the last tiny bit of surface)
            mits = mits + 1;

            return mits;
        }

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

        public static int latitudeToY(double lat, int height)
        {
            //lat starts out between -90 and 90
            //shift to between 0 and 180
            lat += 90;

            //find pixel based on scale
            double scale = lat / 180;
            return (int)Math.Round(scale * height);
        }

        public static bool withinDistance(double x1, double y1, double x2, double y2, double dist)
        {
            return ((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1)) <= dist * dist;
        }

        public static void Log(String log)
        {
            PDebug.Log("[OrbitalSurveyPlus] " + log);
        }

        public static double ScanMinimumAltitude(CelestialBody body)
        {
            double minAltitude = Math.Max(body.Radius * MinAltitudeFactor, MinAltitudeAbsolute);
            return minAltitude;
        }

        public static double ScanMaximumAltitude(CelestialBody body)
        {
            double maxAltitude = Math.Min(body.Radius * MaxAltitudeFactor, MaxAltitudeAbsolute);
            return maxAltitude;
        }

        public static string DistanceToString(double dist)
        {
            if (dist < 10000)        return String.Format("{0:0.00}m", dist);
            if (dist < 10000000)     return String.Format("{0:0.00}km", dist/1000);
            if (dist < 10000000000)  return String.Format("{0:0.00}Mm", dist / 1000000);
            return String.Format("{0:0.00}Gm", dist / 1000000000);
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
            OSPGlobal.Log("ERROR: conversion from binary string to hex string got weird input!");
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
            OSPGlobal.Log("ERROR: conversion from hex string to binary string on scan data serializaion got weird input!");
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
    }
}
