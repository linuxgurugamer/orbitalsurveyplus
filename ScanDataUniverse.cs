using System;
using System.Collections.Generic;

namespace OrbitalSurveyPlus
{
    class ScanDataUniverse
    {
        private Dictionary<int, ScanDataBody> ScanDatabase;

        public ScanDataUniverse()
        {
            ScanDatabase = new Dictionary<int, ScanDataBody>();
        }

        public void Clear()
        {
            ScanDatabase.Clear();
        }

        public void Load(ConfigNode node)
        {
            string nodeName = OSPGlobal.OSPScanDataNodeName;
            string scannedDataName = OSPGlobal.OSPScannedValueName;
            string revealedDataName = OSPGlobal.OSPRevealedValueName;
            string mitsGleanedName = OSPGlobal.OSPMitsGleanedValueName;
            string dataWidthName = OSPGlobal.OSPDataMapWidthName;
            string dataHeightName = OSPGlobal.OSPDataMapHeightName;

            //clear current data
            Clear();

            //check to see if node exists
            ConfigNode OSPScanData = node.GetNode(nodeName);
            if (OSPScanData == null) return;

            //node exists, so load stored data for bodies that have been scanned and saved
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                //get body attributes
                String bodyName = body.GetName();
                int bodyId = body.flightGlobalsIndex;

                //retrieve scan data from node, if it exists
                ConfigNode bodyNode = OSPScanData.GetNode(bodyName);

                if (bodyNode != null)
                {
                    ScanDataBody bodyScanData = null;
                    int width = 0;
                    int height = 0;

                    //get width and height information for the scanned data, create scan data object
                    if (bodyNode.HasValue(dataWidthName) && bodyNode.HasValue(dataHeightName))
                    {
                        string widthValue = bodyNode.GetValue(dataWidthName);
                        width = int.Parse(widthValue);

                        string heightValue = bodyNode.GetValue(dataHeightName);
                        height = int.Parse(heightValue);

                        bodyScanData = new ScanDataBody(body, width, height);

                        //make warning message if saved array size is legacy
                        int checkWidth = 0;
                        int checkHeight = 0;
                        OSPGlobal.GetScanDataArraySize(body, out checkWidth, out checkHeight);
                        if (width != checkWidth || height != checkHeight)
                        {
                            OSPGlobal.Log("WARNING: Saved data size for " + body.name + " is incorrect, this could be from an older verion of OSP - saved size will be used regardless");
                        }
                    }
                    else
                    {
                        bodyScanData = new ScanDataBody(body);
                        width = bodyScanData.Width;
                        height = bodyScanData.Height;
                    }

                    //Load in scanned data info
                    if (bodyNode.HasValue(scannedDataName))
                    {
                        string serialized = bodyNode.GetValue(scannedDataName);
                        bodyScanData.LoadScannedMap(serialized);
                    }
                    else OSPGlobal.Log("Scan data for " + bodyName + " blank: data reset");

                    //Load in revealed data info
                    if (bodyNode.HasValue(revealedDataName))
                    {
                        string serialized = bodyNode.GetValue(revealedDataName);
                        bodyScanData.LoadRevealedMap(serialized);
                    }
                    else OSPGlobal.Log("Reveal data for " + bodyName + " blank: data reset");

                    //Load mits info
                    if (bodyNode.HasValue(mitsGleanedName))
                    {
                        string mitsGleanedValue = bodyNode.GetValue(mitsGleanedName);
                        bodyScanData.MitsGleaned = float.Parse(mitsGleanedValue);
                    }
                    else OSPGlobal.Log("Mits gleaned for " + bodyName + " blank: data reset");

                    //add body data to database
                    ScanDatabase.Add(bodyId, bodyScanData);
                }
            }
        }

        public void Save(ConfigNode node)
        {
            string nodeName = OSPGlobal.OSPScanDataNodeName;
            string scannedDataName = OSPGlobal.OSPScannedValueName;
            string revealedDataName = OSPGlobal.OSPRevealedValueName;
            string mitsGleanedName = OSPGlobal.OSPMitsGleanedValueName;
            string dataWidthName = OSPGlobal.OSPDataMapWidthName;
            string dataHeightName = OSPGlobal.OSPDataMapHeightName;

            //create OSPScenario node
            ConfigNode OSPScanData = new ConfigNode(nodeName);

            //go through each body data object in database
            foreach(int bodyIndex in ScanDatabase.Keys)
            {
                ScanDataBody bodyData;
                bool dataExists = ScanDatabase.TryGetValue(bodyIndex, out bodyData);

                if (dataExists)
                {
                    //create new config node for his planet
                    string bodyName = bodyData.Body.name;
                    ConfigNode bodyNode = new ConfigNode(bodyName);

                    //store data map width and height
                    //this allows saves to remain unbroken if future updates change data map size
                    bodyNode.AddValue(dataWidthName, bodyData.Width);
                    bodyNode.AddValue(dataHeightName, bodyData.Height);

                    //serialize the scanned map and store it
                    string serializedScannedMap = bodyData.GetSerializedScannedMap();
                    bodyNode.AddValue(scannedDataName, serializedScannedMap);

                    //serialize the revealed map and store it
                    string serializedRevealedMap = bodyData.GetSerializedRevealedMap();
                    bodyNode.AddValue(revealedDataName, serializedRevealedMap);

                    //store mits gleaned
                    bodyNode.AddValue(mitsGleanedName, bodyData.MitsGleaned);

                    //attach to OSPScenario node
                    OSPScanData.AddNode(bodyNode);
                }
            }

            //add OSPScenario node to parent node
            node.AddNode(OSPScanData);
        }

        public ScanDataBody GetBodyScanData(int bodyIndex)
        {
            if (ScanDatabase.ContainsKey(bodyIndex))
            {
                return ScanDatabase[bodyIndex];
            }
            return null;
        }

        public double GetBodyScanPercent(int bodyIndex)
        {
            ScanDataBody data = GetBodyScanData(bodyIndex);
            if (data == null) return 0;
            return data.ScanPercent;
        }

        public bool BodyIsFullyScanned(int bodyIndex)
        {
            if (GetBodyScanPercent(bodyIndex) < 1) return false;
            return true;
        }

        public void UpdateScanData(CelestialBody body, bool scanned, double lon, double lat, int radius)
        {
            //get existing data, or create new one if none exist
            ScanDataBody scanData = GetBodyScanData(body.flightGlobalsIndex);
            if (scanData == null)
            {
                scanData = new ScanDataBody(body);
                ScanDatabase.Add(body.flightGlobalsIndex, scanData);
            }

            scanData.UpdateScanData(scanned, lon, lat, radius);
        }
    }
}
