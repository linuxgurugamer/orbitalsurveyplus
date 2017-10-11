using System;
using System.Collections.Generic;

namespace OrbitalSurveyPlus
{
    public class ModuleOrbitalSurveyorPlus : PartModule, IAnimatedModule, IResourceConsumer
    {
        static readonly string STATUS_OFF = "standby";
        static readonly string STATUS_ON = "scanning";
        static readonly string STATUS_DONE = "scan complete";
        static readonly string STATUS_TOO_LOW = "too low";
        static readonly string STATUS_TOO_HIGH = "too high";
        static readonly string STATUS_NO_POWER = "not enough ElectricCharge";

        static readonly string transmitScienceName = "Transmit Data";

        [KSPField(guiActive = false, guiName = "Status")]
        string scanStatus = STATUS_OFF;

        [KSPField(guiActive = false, guiName = "Min Altitude")]
        string minAltitude = "";

        [KSPField(guiActive = false, guiName = "Max Altitude")]
        string maxAltitude = "";

        [KSPField(guiActive = false, guiName = "Surface Scanned")]
        string percentScanned = "0%";

        [KSPField(isPersistant = true)]
        public bool scannerOn = false;

        [KSPField(isPersistant = true)]
        public bool perpetualScan = false;

        //Module Tweakable Fields
        [KSPField]
        public float ElectricDrain = 1.00f;
        [KSPField(isPersistant = true)]
        public int ScanRadius = 8;
        [KSPField]
        public int SciBonus = 20;

        private bool activated;
        private List<ModuleOrbitalSurveyor> moduleStockSurveyor;
        private PartResourceDefinition resEC = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
        private double lastUpdate;
        private double lastScan;
        private bool freshStart;

        private bool transmitting;
        private ScanDataBody lastTransmitBodyData;
        private bool[,] lastTransmitScanMap;
        private double lastScanPct = 0;

        private CelestialBody currentBody;

        //IAnimateModule Implement
        public void DisableModule()
        {
            activated = false;
            scannerOn = false;

            SetUIVisibility();
        }

        public void EnableModule()
        {
            if (OSPGlobal.ExtendedSurvey)
            {
                activated = true;
                DisableStockSurveyor();
            }
            lastUpdate = Planetarium.GetUniversalTime();
            lastScan = 0;
            SetAltitudeHelp();
            SetUIVisibility();
        }

        public bool IsSituationValid()
        {
            return true;
        }

        public bool ModuleIsActive()
        {
            return activated;
        }

        //IResourceConsumer Implement
        public List<PartResourceDefinition> GetConsumedResources()
        {
            PartResourceDefinition consumedRes = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
            List<PartResourceDefinition> list = new List<PartResourceDefinition>();
            list.Add(consumedRes);
            return list;
        }

        //Unity & KSP Functions
        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            GameEvents.OnTriggeredDataTransmission.Add(TransmitScienceCallback);

            lastUpdate = Planetarium.GetUniversalTime();
            freshStart = true;

            transmitting = false;
            lastTransmitBodyData = null;

            moduleStockSurveyor = part.FindModulesImplementing<ModuleOrbitalSurveyor>();
        }

        public void OnDestroy()
        {
            GameEvents.OnTriggeredDataTransmission.Remove(TransmitScienceCallback);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (freshStart)
                {
                    freshStart = false;
                    currentBody = vessel.mainBody;
                    SetAltitudeHelp();
                    UpdateScanInfo(true);
                    SetUIVisibility();
                    if (OSPGlobal.ExtendedSurvey) DisableStockSurveyor();                    
                }

                if (vessel.mainBody != currentBody)
                {
                    currentBody = vessel.mainBody;
                    SetAltitudeHelp();
                    UpdateScanInfo(true);
                }
            }
        }

        public void FixedUpdate()
        {            
            if (!HighLogic.LoadedSceneIsFlight || !OSPGlobal.ExtendedSurvey) return;
            
            perpetualScan = false;
            if (activated)
            {
                scanStatus = STATUS_OFF;
                UpdateScanInfo(false);

                if (scannerOn)
                {
                    //get elapsed time since last update
                    double timeElapsed = 0;
                    double ut = Planetarium.GetUniversalTime();
                    timeElapsed = ut - lastUpdate;
                    if (timeElapsed > 0) lastUpdate = ut;

                    //if conditions met and there is scanning left to be done, proceed with scan
                    if (GetScanPercentCurrentBody() == 1)
                    {
                        scanStatus = STATUS_DONE;
                    }
                    else if (CheckConditions() && timeElapsed > 0) //CheckConditions() will also chang scanStatus to reflect problems
                    {
                        //check electric charge
                        double drain = ElectricDrain * timeElapsed;
                        double ecAvailable = 0;
                        double ecMax = 0;
                        vessel.GetConnectedResourceTotals(resEC.id, out ecAvailable, out ecMax);

                        if (ecAvailable >= ElectricDrain)
                        {
                            //we are scanning!
                            scanStatus = STATUS_ON;

                            if (lastScan + OSPGlobal.TimeBetweenScans <= ut)
                            {

                                //update scan
                                if (lastScan == 0) OSPScenario.QueueScanRequest(vessel, vessel.mainBody, vessel.longitude, vessel.latitude, true, ScanRadius);
                                else OSPScenario.QueueScanRequestReroactive(vessel, true, ScanRadius, lastScan);
                                lastScan = ut;
                            }

                            //for now, assume perpetual scanning
                            perpetualScan = true;
                        }
                        else
                        {
                            //not enough EC
                            scanStatus = STATUS_NO_POWER;
                        }

                        part.RequestResource(resEC.id, drain);
                    }
                }
            }
        }

        //Scan Buttons and Context Menu
        [KSPEvent(guiName = "Start Survey", guiActive = true)]
        public void StartSurvey()
        {
            scannerOn = true;
            lastUpdate = Planetarium.GetUniversalTime();
            SetUIVisibility();
        }

        [KSPEvent(guiName = "Stop Survey", guiActive = true)]
        public void StopSurvey()
        {
            scannerOn = false;
            SetUIVisibility();
        }

        [KSPEvent(guiName = "Transmit Science", guiActive = true)]
        public void TransmitScience()
        {
            if (transmitting) return; //can't transmit again if currently transmitting

            //check available data
            float availableMits = GetAvailableMitsCurrentBody();
            if (availableMits <= 0)
            {
                ScreenMessages.PostScreenMessage("No scan data available for transmitting", 4.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            //get best transmitter for the job
            IScienceDataTransmitter transmitter = FindBestTransmitter();
            if (transmitter == null)
            {
                ScreenMessages.PostScreenMessage("No transmitters available on this vessel", 4.0f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            //get the scan data for the current body, grab a snapshot of the scan map from it
            lastTransmitBodyData = GetScanDataCurrentBody();
            lastTransmitScanMap = OSPGlobal.SnapshotDataMap(lastTransmitBodyData.ScannedMap);

            //create science data instance
            List<ScienceData> sdList = new List<ScienceData>();
            sdList.Add(GetScienceData(vessel.mainBody, availableMits));

            //start transmission
            transmitter.TransmitData(sdList);
            SetAllOSPModulesTransmitting(true);
        }

        private void SetUIVisibility()
        {
            Fields["scanStatus"].guiActive = false;
            Fields["minAltitude"].guiActive = false;
            Fields["maxAltitude"].guiActive = false;
            Fields["percentScanned"].guiActive = false;

            Events["TransmitScience"].active = false;
            Events["StartSurvey"].active = false;
            Events["StopSurvey"].active = false;           

            if (OSPGlobal.ExtendedSurvey && activated)
            {
                Fields["scanStatus"].guiActive = true;
                Fields["minAltitude"].guiActive = true;
                Fields["maxAltitude"].guiActive = true;
                Fields["percentScanned"].guiActive = true;

                if (GetAvailableMitsCurrentBody() > 0 && !transmitting)
                {
                    Events["TransmitScience"].active = true;
                }

                if (scannerOn)
                {
                    Events["StopSurvey"].active = true;
                }
                else
                {
                    Events["StartSurvey"].active = true;
                }
            }
        }

        private void SetAltitudeHelp()
        {
            minAltitude = OSPGlobal.DistanceToString(OSPGlobal.ScanMinimumAltitude(vessel.mainBody));
            maxAltitude = OSPGlobal.DistanceToString(OSPGlobal.ScanMaximumAltitude(vessel.mainBody));
            Fields["minAltitude"].guiName = "Min Altitude (" + vessel.mainBody.name + ")";
            Fields["maxAltitude"].guiName = "Max Altitude (" + vessel.mainBody.name + ")";
        }

        private void UpdateScanInfo(bool forceUpdate)
        {
            if (!forceUpdate && lastScanPct == GetScanPercentCurrentBody()) return;

            lastScanPct = GetScanPercentCurrentBody();
            float mitsAvailable = GetAvailableMitsCurrentBody();

            //update scan percent field
            int pct = (int)Math.Floor(lastScanPct * 100);
            percentScanned = pct.ToString() + "%";

            //update available science field
            string s = transmitScienceName + ": ";
            if (mitsAvailable <= 0) s += "0 Mits";
            else if (mitsAvailable < 1) s += "<1 Mits";
            else s += (int)mitsAvailable + " Mits";

            Events["TransmitScience"].guiName = s;
            SetUIVisibility();
        }

        //Other Stuff
        private bool CheckConditions()
        {
            CelestialBody body = vessel.mainBody;
            if (body == null)
            {
                return false;
            }

            if (!OSPGlobal.BodyCanBeScanned(body))
            {
                if (body.flightGlobalsIndex == 0)
                    scanStatus = "blinded by the light";
                else
                    scanStatus = "unable to scan target";

                return false;
            }

            double minAltitude = OSPGlobal.ScanMinimumAltitude(body);
            double maxAltitude = OSPGlobal.ScanMaximumAltitude(body);

            if (vessel.altitude < minAltitude)
            {
                scanStatus = STATUS_TOO_LOW;
                return false;
            }

            if (vessel.altitude > maxAltitude)
            {
                scanStatus = STATUS_TOO_HIGH;
                return false;
            }

            return true;
        }

        private ScanDataBody GetScanDataCurrentBody()
        {
            return OSPScenario.GetBodyScanData(vessel.mainBody);
        }

        private void DisableStockSurveyor()
        {
            if (moduleStockSurveyor != null)
            {
                foreach (ModuleOrbitalSurveyor m in moduleStockSurveyor)
                {
                    m.DisableModule();
                }
            }
        }

        public override string GetInfo()
        {
            String s = base.GetInfo();

            //scan radius
            s += "Scanning radius: " + ScanRadius + " units";

            //science bonus
            s += "\n<color=#00ffff>Complete scan science bonus: " + SciBonus + "</color>";

            //altitude information
            /*  Apparently KSP front-loads all of the GetInfo stuff during startup, so displaying scaled factors here is impossible. Unfortunate!
            double minFactorDivisor = 1 / OSPGlobal.MinAltitudeFactor;
            s += "\nMin. Altitude: " + OSPGlobal.MinAltitudeAbsolute;
            s += "\n     <color=#8a939c>[unless: body.radius / " + minFactorDivisor + " is bigger]</color>";
            s += "\nMax. Altitude: " + OSPGlobal.MaxAltitudeAbsolute;
            s += "\n     <color=#8a939c>[unless: body.radius * " + OSPGlobal.MaxAltitudeFactor + " is smaller]</color>";
            */

            //electric charge
            s += "\n\n<b><color=#f97306>Requires:</color></b>";
            s += "\n- Electric Charge: " + ElectricDrain + "/sec.";
            return s;
        }

        //Science Stuff
        private float GetAvailableMitsCurrentBody()
        {
            ScanDataBody data = GetScanDataCurrentBody();
            if (data == null) return 0;
            return data.GetAvailableMits();
        }

        private double GetScanPercentCurrentBody()
        {
            ScanDataBody data = GetScanDataCurrentBody();
            if (data == null) return 0;
            return data.ScanPercent;
        }

        private float GetScanScienceMultiplier(CelestialBody body)
        {
            CelestialBodyScienceParams sciParams = vessel.mainBody.scienceValues;
            return sciParams.InSpaceLowDataValue;
        }

        private float GetScienceFromMits(float mits, CelestialBody body)
        {
            float mitsTotal = OSPScenario.GetBodyScanData(body).TotalMits;
            return (mits / mitsTotal) * SciBonus * GetScanScienceMultiplier(body);
        }

        private ScienceData GetScienceData(CelestialBody body, float mits)
        {
            //subject ID will hold the instance ID of the module that started the transmission
            string subject = GetOSPScienceSubject();
            ScienceData sd = new ScienceData(mits, 1, 0, subject, "Orbital survey data from " + body.GetName(), true);
            return sd;
        }

        private string GetOSPScienceSubject()
        {
            return "OrbitalSurveyPlus@"+GetInstanceID().ToString();
        }

        public void TransmitScienceCallback(ScienceData sd, Vessel v, bool transmitFailed)
        {
            try
            {
                //extract the instance id from the subjectID of the ScienceData
                int id = 0;
                bool a = false;
                string[] splitSubject = sd.subjectID.Split('@');
                if (splitSubject.Length > 1) { a = int.TryParse(splitSubject[1], out id); }

                if (!a)
                {
                    OSPGlobal.Log("ERROR: TransmitScienceCallback recieved a bad value in science data subject");
                }
                else if (!transmitFailed && id == GetInstanceID()) //if this is the modules that sent this out, continue
                {
                    
                    //update body data object to reflect mits have been gleaned
                    lastTransmitBodyData.MitsGleaned += sd.dataAmount;
                    
                    //update body data "revealed map"
                    lastTransmitBodyData.SetRevealedMap(lastTransmitScanMap);
                    
                    //get science from transmitted mits and reward them
                    float science = GetScienceFromMits(sd.dataAmount, lastTransmitBodyData.Body);

                    if (ResearchAndDevelopment.Instance != null) //R&D instance seems to not exist sometimes in sandbox, using this to avoid nullpointerexception
                    {
                        ResearchAndDevelopment.Instance.AddScience(science, TransactionReasons.ScienceTransmission);
                    }                 
                    string message = sd.title + " received. " + ResearchAndDevelopment.ScienceTransmissionRewardString(science);                   
                    ScreenMessages.PostScreenMessage(message, 4.0f, ScreenMessageStyle.UPPER_LEFT);
                    
                    //unlock this body's orbital maps if not already done so
                    ResourceMap.Instance.UnlockPlanet(lastTransmitBodyData.Body.flightGlobalsIndex);
                    OSPScenario.RefreshBodyOverlay(lastTransmitBodyData.Body, true);                    
                }
            }
            catch (Exception e)
            {
                OSPGlobal.Log("Exception on science callback:");
                OSPGlobal.Log(e.Message);       
                if (sd == null) OSPGlobal.Log("ScienceData parameter is null!");
                if (v == null) OSPGlobal.Log("Vessel parameter is null!");
                if (lastTransmitBodyData == null) OSPGlobal.Log("Body scan data for transmission is null!");
            }
            finally
            {
                //reset transmit stuff
                lastTransmitBodyData = null;
                SetAllOSPModulesTransmitting(false);
                UpdateScanInfoAll(true);
            }
            
        }

        private IScienceDataTransmitter FindBestTransmitter()
        {
            List<IScienceDataTransmitter> list = vessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (list.Count == 0) return null;

            float score_best = 0;
            IScienceDataTransmitter t_best = null;
            foreach(IScienceDataTransmitter t in list)
            {
                float score = ScienceUtil.GetTransmitterScore(t);

                if (t_best == null)
                {
                    t_best = t;
                    continue;
                }

                if (score > score_best)
                {
                    t_best = t;
                }
            }

            return t_best;
        }

        private void SetAllOSPModulesTransmitting(bool isTransmitting)
        {
            List<ModuleOrbitalSurveyorPlus> ospList = vessel.FindPartModulesImplementing<ModuleOrbitalSurveyorPlus>();
            foreach(ModuleOrbitalSurveyorPlus osp in ospList)
            {
                osp.transmitting = isTransmitting;
                osp.SetUIVisibility();
            }
        } 

        private void UpdateScanInfoAll(bool forceUpdate)
        {
            List<ModuleOrbitalSurveyorPlus> ospList = vessel.FindPartModulesImplementing<ModuleOrbitalSurveyorPlus>();
            foreach (ModuleOrbitalSurveyorPlus osp in ospList)
            {
                osp.UpdateScanInfo(forceUpdate);
            }
        }
    }
}

