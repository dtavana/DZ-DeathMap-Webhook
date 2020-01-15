using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Globalization;

namespace DZ_DeathMap_Webhook
{
    class Program {
        static void Main( string[] args ) 
        {
            Dictionary<string, string> settings = SettingsManager.GetSettings();
            // process world info (so we know relative positions of data in admlog)
            int finalMapSize;
            if (!settings.ContainsKey("MapSize")) 
            {
                switch (settings["MapName"].ToLower())
                {
                    case "livonia":
                        finalMapSize = 12800;
                        break;
                    case "enoch":
                        finalMapSize = 12800;
                        break;
                    case "chernarus":
                        finalMapSize = 15360;
                        break;
                    case "chernarusplus":
                        finalMapSize = 15360;
                        break;
                    case "namalsk":
                        finalMapSize = 12800;
                        break;
                    case "deerisle":
                        finalMapSize = 16384;
                        break;
                    case "exclusionzone":
                        finalMapSize = 20480;
                        break;
                    default:
                        finalMapSize = -1;
                        Console.WriteLine("[DeathMap] unrecognized world name");
                        Console.WriteLine("[DeathMap] press any key to abort...");
                        Console.ReadKey();
                        break;
                }
            } 
            else 
            {
                finalMapSize = int.Parse(settings["MapSize"]);
            }

            if (finalMapSize != -1 ) {
                Console.WriteLine("[DeathMap] worldSize is " + finalMapSize);

                // process all input admin log for player death positions
                List<float> positionArray = new List<float>();
                int numberOfDeaths = 0, outsideBounds = 0;
                bool stopLineSearch = false;
                char[] wantedSeq = { 'p', 'o', 's', '=', '<' };

                // these strings will be used to search lines we want
                string file_test1, file_test2;
                switch (settings["OutputContent"]) 
                {
                    case "all":
                        file_test1 = ">) killed by ";
                        file_test2 = "died. Stats>";
                        break;
                    case "infected":
                        file_test1 = ">) killed by ZmbF";
                        file_test2 = ">) killed by ZmbM";
                        break;
                    default:
                        file_test1 = ">) killed by ";
                        file_test2 = "died. Stats>";
                        break;
                }

                // go through ALL *.ADM files from specified directory
                string[] files = Directory.GetFiles( args[0], "*", SearchOption.AllDirectories );
                foreach (string fileName in files) 
                {
                    if (fileName.Contains(".ADM")) 
                    {
                        using (StreamReader reader = new StreamReader(File.Open(fileName, FileMode.Open))) 
                        {
                            while (reader.Peek() >= 0) 
                            {
                                string line = reader.ReadLine();
                                if (line.Contains( file_test1 ) || line.Contains( file_test2 )) 
                                {
                                    stopLineSearch = false;
                                    string positionString = "";
                                    int j = 0;
                                    for (int i = 0; i < line.Length; i++) 
                                    {
                                        if (stopLineSearch)
                                            break;

                                        if ( j < 5 ) 
                                        {
                                            // find pos=<
                                            if ( line[i] == wantedSeq[j] ) 
                                            {
                                                j++;
                                            } 
                                            else 
                                            {
                                                j = 0;
                                            }
                                        } 
                                        else 
                                        {
                                            // get position
                                            if ( line[i] != '>' ) 
                                            {
                                                positionString += line[i];
                                            } else 
                                            {
                                                stopLineSearch = true;
                                                numberOfDeaths++;
                                            }
                                        }
                                    }

                                    // parse position
                                    string[] positionStringSplit = positionString.Split(',');
                                    float posParsedX = float.Parse(positionStringSplit[0], CultureInfo.InvariantCulture.NumberFormat), posParsedY = float.Parse(positionStringSplit[1], CultureInfo.InvariantCulture.NumberFormat);
                                    if (((posParsedX >= 0 ) && (posParsedX < finalMapSize)) && ((posParsedY >= 0) && (posParsedY < finalMapSize))) 
                                    {
                                        positionArray.Add( posParsedX );
                                        positionArray.Add( posParsedY );
                                    } 
                                    else 
                                    {
                                        outsideBounds++;
                                    }
                                }
                            }
                            reader.Close();

                            Console.WriteLine("[DeathMap] " + fileName + " processed");
                        }
                    }
                }
                Console.WriteLine("[DeathMap] total number of registered deaths is " + numberOfDeaths + " (outside of bounds " + outsideBounds + ")");
                    
                // generate heat or pixel map
                int posX, posY;
                int outputSize = int.Parse(settings["OutputSize"]);
                if (outputSize < 100)
                    outputSize = 100;

                switch (settings["OutputType"]) 
                {
                    case "heat":
                        // initialize intensityGrid with given size
                        float[,] intensityGrid = new float[outputSize, outputSize];
                        for (int i = 0; i < outputSize; i++)
                            for (int j = 0; j < outputSize; j++)
                                intensityGrid[i,j] = 0.0f;

                        for (int i = 0; i < (positionArray.Count - 1); i = i + 2 ) 
                        {
                            // 2D position within intensityGrid based on the input death coords
                            posX = (int) ((positionArray[i] / finalMapSize ) * outputSize);
                            posY = (outputSize - 1) - (int) ((positionArray[i + 1] / finalMapSize) * outputSize);

                            // relative size of "dot" to the output image size
                            int sizeOfDot = (int) (0.075 * outputSize);
                            if (sizeOfDot % 2 != 0) 
                            {
                                sizeOfDot -= 1; 
                            }
                        
                            // border conditions
                            int bxmax = Math.Min(posX + (sizeOfDot / 2) + 1, outputSize);
                            int bxmin = Math.Max(0, posX - (sizeOfDot / 2));
                            int bymax = Math.Min(posY + (sizeOfDot / 2) + 1, outputSize);
                            int bymin = Math.Max(0, posY - (sizeOfDot / 2));

                            // https://en.wikipedia.org/wiki/Normal_distribution
                            float sigma = sizeOfDot / 7.0f;
                            for (int x = bxmin; x < bxmax; x++) 
                            {
                                for (int y = bymin; y < bymax; y++) 
                                {
                                    intensityGrid[x, y] += (float) (Math.Exp( -1 * ((x - posX) * (x - posX) + (y - posY) * (y - posY)) / 2.0 / sigma / sigma) / (Math.Sqrt(sigma * sigma / 2.0 / Math.PI)));
                                }
                            }
                        }

                        // find maximum in intensityGrid (to use for colorizing later)
                        float max = 0.0f;
                        foreach (float e in intensityGrid) {
                            if ( e > max )
                                max = e;
                        }
                            
                        // load colors from color pallete
                        Color[] pallete = new Color[256];
                        using (Bitmap colorPallete = new Bitmap(configColorPallete)) 
                        {
                            for (int i = 0; i < 256; i++) {
                                pallete[i] = colorPallete.GetPixel(i, 0);
                            }
                        }

                        // draw intensityGrid into bitmap
                        bool linear = true;
                        if (settings["ColorScale"] == "log")
                            linear = false;
                        using (Bitmap outputHeatMap = new Bitmap( outputSize, outputSize)) {
                            for (int i = 0; i < outputSize; i++) 
                            {
                                for (int j = 0; j < outputSize; j++) 
                                {
                                    int colorIndex = 1;
                                    if (linear)
                                        colorIndex = (int) ( ( intensityGrid[i,j] / max ) * 255 );
                                    else
                                        colorIndex = (int) ( ( Math.Log(intensityGrid[i,j] + 1) / Math.Log( max + 1) ) * 255 );
                                    Color pixelColor = pallete[colorIndex];
                                    outputHeatMap.SetPixel( i, j, pixelColor );
                                }
                            }
                            outputHeatMap.Save("DeathMap_HeatMap.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                        }
                        break;

                    case "pixel":
                        // initialize dataGrid (fixed size of 100, which for chernarus means roughly each 150 meters)
                        int dataGridSize = 100;
                        int[] dataGrid = new int[dataGridSize * dataGridSize];
                        for ( int i = 0; i < dataGrid.Length; i++ )
                            dataGrid[i] = 0;
                            
                        // go through all positions and fill dataGrid with number of deaths per grid tile (increment)
                        for (int i = 0; i < (positionArray.Count - 1); i = i + 2) {
                            posX = (int) ((positionArray[i] / finalMapSize) * dataGridSize);
                            posY = (dataGridSize - 1) - (int) ((positionArray[i + 1] / finalMapSize) * dataGridSize);
                            dataGrid[posY * dataGridSize + posX]++;
                        }

                        // find max value in dataGrid (to be used later for colorizing pixel map)
                        int maxx = 0;
                        foreach (int e in dataGrid) 
                        {
                            if (e > maxx)
                                maxx = e;
                        }

                        // draw dataGrid into bitmap
                        using (Bitmap outputPixelMap = new Bitmap(outputSize, outputSize)) 
                        {
                            int truePixelSize = outputSize / dataGridSize;
                            for (int i = 0; i < dataGrid.Length; i++) 
                            {
                                int picPosX = i % dataGridSize, picPosY = i / dataGridSize;

                                Color pixelColor;
                                if ( dataGrid[i] != 0 ) {
                                    pixelColor = Color.Brown;
                                    if ( dataGrid[i] > (maxx / 6))
                                        pixelColor = Color.Orange;
                                    if ( dataGrid[i] > (maxx / 3))
                                        pixelColor = Color.Yellow;
                                    if ( dataGrid[i] >= (maxx))
                                        pixelColor = Color.White;
                                } else {
                                    pixelColor = Color.Black;
                                }

                                for ( int j = 0; j < truePixelSize; j++ ) {
                                    for ( int k = 0; k < truePixelSize; k++ ) {
                                        outputPixelMap.SetPixel( ( picPosX * truePixelSize ) + j, ( picPosY * truePixelSize ) + k, pixelColor );
                                    }
                                }
                            }

                            outputPixelMap.Save("DeathMap_PixelMap.bmp", System.Drawing.Imaging.ImageFormat.Bmp);
                        }
                        break;

                    default:
                        break;
                }
            }
        }
    }
}