using System;
using System.Text;

namespace OrbitalSurveyPlus
{
    class ScanDataBody
    {
        private static readonly char TAG_100PERCENT = 'X';
        private static readonly char TAG_COMPRESSION_DELIMITER = ',';

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

        //private globals for calculations
        private double totalArea = 0;

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

            //get total psuedo-area for coverage calculations
            totalArea = GetTotalGlobeArea(Width, Height);
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
                ScanPercent = 1;
            }
            else
            {
                LoadArrayFromString(ref ScannedMap, serializedHex);
                UpdateScanPercent();
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
            if (radius > max_radius) radius = max_radius;

            //get half width and half height for calculations (useful values)
            int half_width = Width / 2;
            int half_height = Height / 2;

            /*****************************************************************************************
                Form ellipse from scan radius, based on latitude, which will be our field of view.
                Since the width of the cells effectively shrink as you near the poles but the height
                stays the same, the scan area in the x-direction (longetudenal) needs to bloat to
                keep the scan area on the globe consistent. At the equator it is perfectly circular.
            *****************************************************************************************/

            //calculate some useful values
            int distFromEquator = Math.Abs(point_y - half_height);
            double dEq2 = distFromEquator * distFromEquator;
            double halfH2 = half_height * half_height;

            //semi minor axis (y radius of scan area) is always simply the radius, it doesn't undergo distrotion
            int semiMinorAxis = radius;

            //semi major axis (x radius of scan area) gets distorted as latitude gets higher (it appears to shrink)
            //we use Mercator projection scaling to correct for this 
            int semiMajorAxis = half_width;
            if (Math.Abs(lat) < 90) //to avoid division by zero, only use scaling if latitude is less than exactly 90
            {
                double scaleFactor = OSPGlobal.MercatorScaleFactor(lat);
                semiMajorAxis = (int)Math.Round(radius * scaleFactor);
                if (semiMajorAxis > half_width) semiMajorAxis = half_width; //clamp to width of map
            }

            //calculate squares of these values, to be used later
            double semiMinorAxis2 = semiMinorAxis * semiMinorAxis;
            double semiMajorAxis2 = semiMajorAxis * semiMajorAxis;

            //search in a rectangle that encompasses the ellipse and check to see if each point is inside the ellipse
            for (int j = -semiMinorAxis; j <= semiMinorAxis; j++)
            {
                for (int i = -semiMajorAxis; i <= semiMajorAxis; i++)
                {                   
                    //check to see if in bounds of scan ellipse
                    //if val <= 1, the point is inside the ellipse

                    /********************************************************************************************
                        The actual equation for checking if a point is bounded by an ellipse is this:

                        ((x - center_x)*(x - center_x) / Rx*Rx) + ((y - center_y)*(y-center_y) / Ry*Ry) <= 1

                        Due to the nature of how x and point_x (our center_x) are related to i in this way:
                        x - point_x = i
                        We can do a nice substitution to keep things fast and simple. Same goes with j.
                    *********************************************************************************************/

                    double val = ((i * i) / semiMajorAxis2) + ((j* j) / semiMinorAxis2);

                    if (val <= 1)
                    {
                        //get actual point that has been scanned
                        int x = point_x + i;
                        int y = point_y + j;

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
                            y = Height + (Height - (y + 1));
                            x += half_width;
                            if (x >= Width) x -= Width;
                        }

                        //sanity check
                        if (x >= 0 && x < Width && y >= 0 && y < Height)
                        {
                            if (ScannedMap[x, y] != isScanned)
                            {
                                //update point in array
                                ScannedMap[x, y] = isScanned;

                                //update scan percent as we go
                                double area = GetCellGlobeArea(y, Height);
                                double pctChange = isScanned ? (area / totalArea) : -(area / totalArea);
                                ScanPercent += pctChange;               
                            }
                        }
                        else
                        {
                            OSPGlobal.Log(String.Format("ERROR: Index out of bounds for UpdateScanData! Width={0}, Height={1}, x={2}, y={3}", Width, Height, x, y));
                        }
                    }
                }
            }            

            //if scan percent is >=95% (default), give 'em the rest!
            if (ScanPercent < 1 && ScanPercent >= OSPGlobal.ScanAutoCompleteThreshold)
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

        public void UpdateScanPercent()
        {
            ScanPercent = GetCoveragePercent(ScannedMap);
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
            //decompress hex string in case it is compressed
            serializedHex = UncompressHexString(serializedHex);

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

            return CompressHexString(serializedHex);
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

        /*
         * returns the amount of scanned globe area of an array that represents a globe map, where cells are 1x1 at the equator and scale at higher latitudes
         */
        private double GetCoveragePercent(bool[,] array)
        {
            int w = array.GetLength(0);
            int h = array.GetLength(1);

            double total = 0;
            double totalCovered = 0;

            for (int j = 0; j < h; j++)
            {
                double area = GetCellGlobeArea(j, h); //each area is "1x1", but the width must be scaled
                for (int i = 0; i < w; i++)
                {
                    total += area;
                    if (array[i, j]) totalCovered += area;
                }
            }

            return totalCovered / total;
        }
        
        /*
         * returns the total area of an array that represents a gobe map, where the cells are 1x1 at the equator and scale at high latitudes
         */
        private double GetTotalGlobeArea(int width, int height)
        {
            double total = 0;

            for (int j = 0; j < height; j++)
            {
                double area = GetCellGlobeArea(j, height); //each area is "1x1", but the width must be scaled
                for (int i = 0; i < width; i++)
                {
                    total += area;
                }
            }

            return total;
        }   
        
        /*
         * returns the total unit area of a grid cell on an array representing a globe map
         */
        private double GetCellGlobeArea(int y, int totalHeight)
        {
            //the width of cells must be scaled at different latitudes, cells at the equator are 1x1 (unit cells)
            double lat = OSPGlobal.YToLatitude(y, totalHeight);
            double scale = OSPGlobal.MercatorScaleFactor(lat);
            return (1 / scale);
        }     

        private string CompressHexString(string uncompressed)
        {
            if (uncompressed.Length == 0) return uncompressed;

            StringBuilder sb = new StringBuilder();

            char c = uncompressed[0];
            int num = 1;
            for (int i = 1; i < uncompressed.Length; i++)
            {
                char next = uncompressed[i];
                if (next.Equals(c))
                {
                    num++;
                }
                else
                {                   
                    sb.Append(num.ToString("X")); //number of times character appears in a row      
                    sb.Append(TAG_COMPRESSION_DELIMITER);                    
                    sb.Append(c); //the character
                    sb.Append(TAG_COMPRESSION_DELIMITER);

                    c = next;
                    num = 1;
                }
            }

            //get the last letter
            sb.Append(num.ToString("X"));      
            sb.Append(TAG_COMPRESSION_DELIMITER);
            sb.Append(c);

            return sb.ToString();
        }

        private string UncompressHexString(string compressed)
        {
            const byte MODE_NUM = 0;
            const byte MODE_CHAR = 1;

            if (compressed.Length == 0) return compressed;
            if (!compressed.Contains(TAG_COMPRESSION_DELIMITER.ToString())) return compressed;           

            string[] array = compressed.Split(TAG_COMPRESSION_DELIMITER);

            StringBuilder sb = new StringBuilder();
            byte mode = MODE_NUM;
            int num = 0;
            for (int i = 0; i < array.Length; i++)
            {
                switch(mode)
                {
                    case MODE_NUM:
                        num = int.Parse(array[i], System.Globalization.NumberStyles.HexNumber);
                        mode = MODE_CHAR;
                        break;

                    case MODE_CHAR:
                        for (int j = 0; j < num; j++) sb.Append(array[i]);
                        mode = MODE_NUM;
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
