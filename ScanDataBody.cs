using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbitalSurveyPlus
{
    class ScanDataBody
    {
        private static readonly char TAG_100PERCENT = 'X';

        public bool[,] ScannedMap;  //how much of the planet has been scanned
        public bool[,] RevealedMap; //how much of the overlays are revealed (scanned + transmitted)

        public CelestialBody Body   //body this data set is attached to
        {
            get;
            private set;
        }

        public int Width            //width of scan data arrays
        {
            get;
            private set;
        }

        public int Height           //height of scan data arrays
        {
            get;
            private set;
        }

        public double ScanPercent   //percent of the surface scanned 
        {
            get;
            private set;
        }

        public float TotalMits      //total mits available for transmission from this planet's scan data
        {
            get;
            private set;
        }

        public float MitsGleaned    //mits that have already been claimed (transmitted home)
        {
            get;
            set;
        }

        //---------------------------------------------------------------------------------------------
        //Constructor, Load, Save, etc. ---------------------------------------------------------------
        //---------------------------------------------------------------------------------------------

        public ScanDataBody(CelestialBody body)
        {
            Body = body;
            int w;
            int h;
            OSPGlobal.GetScanDataArraySize(body, out w, out h);
            Width = w;
            Height = h;
            Initialize();
        }

        public ScanDataBody(CelestialBody body, int width, int height)
        {
            Body = body;
            Width = width;
            Height = height;
            Initialize();
        }

        private void Initialize()
        {
            /*
                represent the scan data as a 2D boolean array (false = unscanned, true = scanned)
                instead of representing each pixel, data is divided into sections so that save/load won't take 15 minutes
                basically, it's a low-resolution represenation of what is scanned and what isn't

                additionally, a "revealed" data map is stored to represent what data has been
                transmitted back the the KSC - a map that isn't updated at the KSC will still
                be shrouded even if it has been scanned (for default OSP settings)
            */

            //create data maps
            ScannedMap = new bool[Width, Height];
            RevealedMap = new bool[Width, Height];

            //I believe bool defaults to "false", but just to be safe...
            FillArray(ref ScannedMap, false);
            FillArray(ref RevealedMap, false);

            //set initial values for how much total data this planet's surface is and how much has been claimed
            TotalMits = OSPGlobal.GetScanDataTotalMits(Body);
            MitsGleaned = 0;
        }

        public void LoadScannedMap(string serializedHex)
        {
            //------Legacy function so that old saves won't lose their data ----------------
            if (serializedHex.Length == Width * Height)
            {
                //read string as a flat array
                for (int j = 0; j < Height; j++)
                {
                    for (int i = 0; i < Width; i++)
                    {
                        if (serializedHex[i + (j * Width)] == '1')
                        {
                            ScannedMap[i, j] = true;
                        }
                        else
                        {
                            ScannedMap[i, j] = false;
                        }
                    }
                }
            }//------------------------------------------------------------------------------
            else if (serializedHex[0] == TAG_100PERCENT)
            {
                FillArray(ref ScannedMap, true);
            }
            else
            {
                LoadArrayFromString(ref ScannedMap, serializedHex);
            }
        }

        public void LoadRevealedMap(string serializedHex)
        {
            if (serializedHex[0] == TAG_100PERCENT)
            {
                FillArray(ref RevealedMap, true);
            }
            else
            {
                LoadArrayFromString(ref RevealedMap, serializedHex);
            }
        }

        public string GetSerializedScannedMap() //used for saving to file
        {
            return SerializeArray(ScannedMap);
        }

        public string GetSerializedRevealedMap() //used for saving to file
        {
            return SerializeArray(RevealedMap);
        }

        //---------------------------------------------------------------------------------------------
        //Public Methods for manipulating and getting scan information --------------------------------
        //---------------------------------------------------------------------------------------------

        public void UpdateScanData(bool isScanned, double lon, double lat, int radius)
        {
            //find the data point that matches the given lon and lat
            int point_x = OSPGlobal.longitudeToX(lon, Width);
            int point_y = OSPGlobal.latitudeToY(lat, Height);

            //clamp the scan radius to be at max a factor of the data width
            int max_radius = Width / 7;
            if (radius*2 > max_radius) radius = max_radius;

            //get half width and half height for calculations
            int half_width = Width / 2;
            int half_height = Height / 2;

            //calculate stretch offset (the scan area tends to get skinnier as it nears the poles, this helps mitigate that)
            double stretchFactor = Math.Abs((double)point_y - (double)half_height) / (double)half_height;
            int stretchOffset = (int)Math.Round((double)radius * stretchFactor);
            int point_x_west = point_x - stretchOffset;
            int point_x_east = point_x + stretchOffset;

            /*
                the stretch offset basically creates two more circles of scan east and west of the
                actual scan point to try to mitigate width shrinkage as you approach the poles
            */

            //update data point and surrounding data points in radius
            for (int j = -radius; j <= radius; j++)
            {
                for (int i = -radius-stretchOffset; i <= radius+stretchOffset; i++)
                {
                    int x = point_x + i;
                    int y = point_y + j;

                    //check total distance to see if it's within radius (or stretch points)
                    if (OSPGlobal.withinDistance(point_x, point_y, x, y, radius) ||
                        OSPGlobal.withinDistance(point_x_west, point_y, x, y, radius) ||
                        OSPGlobal.withinDistance(point_x_east, point_y, x, y, radius))
                    {
                        //wrap east-west
                        if (x < 0)
                        {
                            x = Width + x;
                        }
                        else if (x >= Width)
                        {
                            x -= Width;
                        }

                        //wrap over poles
                        if (y < 0)
                        {
                            y = 0 - y;
                            x += half_width;
                            if (x >= Width) x -= Width;
                        }
                        else if (y >= Height)
                        {
                            y = Height + (Height - (y+1));
                            x += half_width;
                            if (x >= Width) x -= Width;
                        }

                        //sanity check
                        if (x >= 0 && x < Width && y >= 0 && y < Height)
                        {
                            //update point
                            ScannedMap[x, y] = isScanned;
                        }
                        else
                        {
                            OSPGlobal.Log(String.Format("ERROR: Index out of bounds for UpdateScanData! Width={0}, Height={1}, x={2}, y={3}", Width, Height, x, y));
                        }
                    }
                }
            }

            //update scan percent field
            ScanPercent = GetScanPercent();

            //if scan percent is >=95% (default), give 'em the rest!
            if (ScanPercent >= OSPGlobal.ScanAutoCompleteThreshold)
            {
                FillArray(ref ScannedMap, true);
                ScanPercent = 1;
            }

        }

        public void SetScannedMap(bool[,] array)
        {
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    ScannedMap[i, j] = array[i, j];
                }
            }
        }

        public void SetRevealedMap(bool[,] array)
        {
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    RevealedMap[i, j] = array[i, j];
                }
            }
        }

        public double GetScanPercent()
        {
            return GetCoveragePercent(ScannedMap);
        }

        public float GetAvailableMits()
        {
            //if 100% scan, give all that's left
            if (ScanPercent == 1) return TotalMits - MitsGleaned;

            //otherwise reserve 1 mit for a 'bonus' when 100% scan is done
            float availableMits = (float)((ScanPercent * (TotalMits-1)) - MitsGleaned);
            return availableMits;
        }

        public float GetTotalMits()
        {
            return OSPGlobal.GetScanDataTotalMits(Body);
        }

        public bool IsPointScanned(int x, int y)
        {
            return ScannedMap[x, y];
        }

        public bool IsPointRevealed(int x, int y)
        {
            if (OSPGlobal.OverlayRequiresTransmit) return RevealedMap[x, y];
            return ScannedMap[x, y];
        }

        //---------------------------------------------------------------------------------------------
        //private "utility" methods -------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------

        private void ResizeDataMaps(int width, int height)
        {
            Width = width;
            Height = height;

            ScannedMap = new bool[Width, Height];
            RevealedMap = new bool[Width, Height];
        }

        private void LoadArrayFromString(ref bool[,] arrayToLoad, string serializedHex)
        {
            int w = arrayToLoad.GetLength(0);
            int h = arrayToLoad.GetLength(1);

            //convert seralized hex string to flat bool array
            bool[] flatBool = new bool[w * h];
            int pos = 0;
            for (int i = 0; i < serializedHex.Length; i++)
            {
                string bin = OSPGlobal.HexToBinary(serializedHex[i]);
                for (int j = 0; j < bin.Length; j++)
                {
                    flatBool[pos] = bin[j] == '1' ? true : false;
                    pos++;
                    if (pos >= flatBool.Length) break;
                }
                if (pos >= flatBool.Length) break;
            }

            //convert flat array to 2D data array
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    arrayToLoad[i, j] = flatBool[(j * w) + i];
                }
            }
        }

        private string SerializeArray(bool[,] array)
        {
            //if 100% complete, create shortut tag
            double pct = GetCoveragePercent(array);
            if (pct >= 1)
            {
                //100% scan, just put in the shortcut tag to save time and space
                return TAG_100PERCENT.ToString();
            }

            string serializedHex = "";
            string bin = "";
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    bin += array[i, j] ? "1" : "0";

                    if (bin.Length > 3)
                    {
                        serializedHex += OSPGlobal.BinaryToHex(bin);
                        bin = "";
                    }
                }
            }

            if (bin.Length > 0)
            {
                while (bin.Length < 3) bin += "0";
                serializedHex += OSPGlobal.BinaryToHex(bin);
            }

            return serializedHex;
        }

        private void FillArray(ref bool[,] arrayToFill, bool value)
        {
            for (int j = 0; j < Height; j++)
            {
                for (int i = 0; i < Width; i++)
                {
                    arrayToFill[i, j] = value;
                }
            }
        }

        private double GetCoveragePercent(bool[,] array)
        {
            int w = array.GetLength(0);
            int h = array.GetLength(1);

            double total = w * h;
            double totalCovered = 0;

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    if (array[i, j]) totalCovered += 1;
                }
            }

            return totalCovered / total;
        }
    }
}
