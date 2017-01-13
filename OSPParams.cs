
namespace OrbitalSurveyPlus
{
    class OSPParamsBasic : GameParameters.CustomParameterNode
    {

        public override GameParameters.GameMode GameMode
        {
            get{return GameParameters.GameMode.ANY;}
        }

        public override bool HasPresets
        {
            get{return false;}
        }

        public override string Section
        {
            get{return OSPGlobal.OSP_TITLE;}
        }

        public override int SectionOrder
        {
            get{return 1;}
        }

        public override string Title
        {
            get{return  "Gameplay Settings";}
        }

        [GameParameters.CustomParameterUI("Enable Orbital Surveyor Plus", 
            toolTip = "Disabling this will revert the scan process to stock (biome map will still be available).")]
        public bool ExtendedSurvey = true;

        [GameParameters.CustomParameterUI("Biome Map Requires Scan",
            toolTip = "Whether the biome map requires a resource scan (either OSP scan or stock scan).")]
        public bool BiomeMapRequiresScan = true;

        [GameParameters.CustomParameterUI("Require Data Transmission",
            toolTip = "With this enabled, resource and biome overlays will be shrouded until scan data is transmitted back to kerbin.")]
        public bool OverlayRequiresTransmit = true;

        [GameParameters.CustomFloatParameterUI("Scan Autocomplete Threshold", asPercentage = true,
            toolTip = "The point at which a planet's scan data will auto-complete to 100%.")]
        public double ScanAutocompleteThreshold = 0.95f;
    }

    class OSPParamsAdvanced : GameParameters.CustomParameterNode
    {

        public override GameParameters.GameMode GameMode
        {
            get{return GameParameters.GameMode.ANY;}
        }

        public override bool HasPresets
        {
            get{return false;}
        }

        public override string Section
        {
            get{return OSPGlobal.OSP_TITLE;}
        }

        public override int SectionOrder
        {
            get{return 2;}
        }

        public override string Title
        {
            get{return "Advanced Settings";}
        }

        [GameParameters.CustomParameterUI("Auto-Refresh Overlays",
            toolTip = "Automatically refresh overlay map with new scan data when reasonable.")]
        public bool AutoRefresh = true;

        [GameParameters.CustomParameterUI("Enable Background Scan",
            toolTip = "Disabling this will completely shut off any background scanning (only active vessels will scan).")]
        public bool BackgroundScan = true;

        [GameParameters.CustomIntParameterUI("Time Between Scans (sec)", minValue = 1, maxValue = 10,
            toolTip = "Time (seconds) between scan updates on scanning vessels (both active and background).")]
        public int TimeBetweenScans = 3;

        [GameParameters.CustomParameterUI("Retroactive Scanning",
            toolTip = "Attempts to fill in gaps in scanning that occur at high warp speeds by checking past orbital positions.")]
        public bool RetroactiveScanning = true;

        [GameParameters.CustomIntParameterUI("Processing Load", minValue = 1, maxValue = 100,
            toolTip = "The number of backlogged scans (usually queued by retroactive scanning) processed per game update.")]
        public int ProcessingLoad = 2;

        /* 
            //These numbers are too huge and wonky to comfortably put in the settings menu, for now they will be stuck at default

        [GameParameters.CustomFloatParameterUI("Minimum Altitude Factor", minValue = 0.1f, maxValue = 10.0f,
            toolTip = "Multiplied with a planet's radius (meters) to get the minimum altitude for scanning.")]
        public double MinAltitudeFactor = 0.1f;

        [GameParameters.CustomIntParameterUI("Minimum Altitude Absolute", minValue = 0, maxValue = 1000000, stepSize = 1000,
            toolTip = "A hard floor on minimum scanning altitude, in meters.")]
        public int MinAltitudeAbsolute = 25000;

        [GameParameters.CustomFloatParameterUI("Maximum Altitude Factor", minValue = 0.1f, maxValue = 10.0f, stepCount = 1,
            toolTip = "Multiplied with a planet's radius (meters) to get the maximum altitude for scanning.")]
        public double MaxAltitudeFactor = 5.0f;

        [GameParameters.CustomIntParameterUI("Maximum Altitude Absolute", minValue = 0, maxValue = 100000000,
            toolTip = "A hard ceiling on maximum scanning altitude, in meters.")]
        public int MaxAltitudeAbsolute = 15000000;
        */
    }

    class OSPParamsInfo : GameParameters.CustomParameterNode
    {
        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }

        public override string Section
        {
            get { return OSPGlobal.OSP_TITLE; }
        }

        public override int SectionOrder
        {
            get { return 3; }
        }

        public override string Title
        {
            get { return "OSP Info"; }
        }

        [GameParameters.CustomStringParameterUI("Mod Name", autoPersistance = false, lines = 1)]
        public string InfoName = OSPGlobal.OSP_TITLE;

        [GameParameters.CustomStringParameterUI("Mod Version", autoPersistance = false, lines = 1)]
        public string InfoVersion = OSPGlobal.VERSION_STRING_VERBOSE;
    }
}
