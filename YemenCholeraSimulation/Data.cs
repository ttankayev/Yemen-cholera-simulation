using System;
using System.Collections.Generic;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace YemenCholeraSimulation
{
    static class Data
    {
        public const double leftCenter = 41.8164639814;
        public const double topCenter = 18.9995675476;
        public const double rightCenter = leftCenter + 12.7136581;
        public const double bottomCenter = topCenter - 6.8880578;

        public const int nXcells = 15258;
        public const int nYcells = 8267;

        public const double sqSize = 0.0008333;
        public const string proj = "Geographic, WGS84";

        static Data()
        {
            Gdal.AllRegister();
            Ogr.RegisterAll();
        }

        public static float[,] GenPopData()
        {
            string path = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.FullName;
            var yemData = Gdal.Open(path+"\\Population.tif", Access.GA_ReadOnly);
            var yemPopData = yemData.GetRasterBand(1);

            int width = yemPopData.XSize;
            int height = yemPopData.YSize;

            float[] buffer = new float[width * height];
            yemPopData.ReadRaster(0, 0, width, height, buffer, width, height, 0, 0);

            var popArray = new float[width, height];

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    popArray[i, j] = buffer[i + j * width];
                }
            }

            Console.WriteLine("Pop Data Generated");
            return popArray;
        }

        public static int[,] GenAdminData()
        {
            string path = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.FullName;
            var adminData = Gdal.Open(path + "\\Admin1.tif", Access.GA_ReadOnly);
            var admin1Data = adminData.GetRasterBand(1);

            int width = admin1Data.XSize;
            int height = admin1Data.YSize;

            int[] buffer = new int[width * height];

            admin1Data.ReadRaster(0, 0, width, height, buffer, width, height, 0, 0);

            var admin1Array = new int[width, height];

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    admin1Array[i, j] = buffer[i + j * width];
                }
            }

            Console.WriteLine("Admin Data Generated");
            return admin1Array;
        }

        public static List<(int, int)>[,] GenWaterData()
        {
            var waterAdjacencyTable = new List<(int, int)>[nXcells, nYcells];

            string path = System.IO.Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.FullName;
            var ymnWaterData = Ogr.Open(path+"\\Wadies.shp", 0);
            var waterLayer = ymnWaterData.GetLayerByIndex(0);

            for (long i = 0; i < waterLayer.GetFeatureCount(0); i++)
            {
                var geom = waterLayer.GetFeature(i).GetGeometryRef();

                var oldPoint = (-1.0, -1.0);
                var oldIntPoint = (-1, -1);

                for (int j = 0; j < geom.GetPointCount(); j++)
                {
                    var pointAr = new double[2];
                    geom.GetPoint(j, pointAr);

                    var newPoint = (pointAr[0], pointAr[1]);
                    var newIntPoint = (Convert.ToInt32((pointAr[0] - leftCenter) / sqSize), Convert.ToInt32((pointAr[1] - topCenter) / (-1.0 * sqSize)));

                    if (waterAdjacencyTable[newIntPoint.Item1, newIntPoint.Item2] == null)
                    {
                        waterAdjacencyTable[newIntPoint.Item1, newIntPoint.Item2] = new List<(int, int)> { newIntPoint };
                    }

                    if (j > 0)
                    {
                        var interpolatedLine = RasterizeLine(oldPoint.Item1, oldPoint.Item2, newPoint.Item1, newPoint.Item2);
                        var startIntPoint = oldIntPoint;
                        foreach (var endIntPoint in interpolatedLine)
                        {
                            if (!waterAdjacencyTable[startIntPoint.Item1, startIntPoint.Item2].Contains(endIntPoint))
                            {
                                waterAdjacencyTable[startIntPoint.Item1, startIntPoint.Item2].Add(endIntPoint);
                            }
                            if (waterAdjacencyTable[endIntPoint.Item1, endIntPoint.Item2] == null)
                            {
                                waterAdjacencyTable[endIntPoint.Item1, endIntPoint.Item2] = new List<(int, int)> { endIntPoint };
                            }
                            startIntPoint = endIntPoint;
                        }
                    }

                    oldPoint = newPoint;
                    oldIntPoint = newIntPoint;
                }
            }

            Console.WriteLine("Water Data Generated");
            return waterAdjacencyTable;
        }

        public static List<(int, int)> RasterizeLine(double x1, double y1, double x2, double y2)
        {
            var returnList = new List<(int, int)>();

            int iX = Convert.ToInt32((x1 - leftCenter) / sqSize);
            int iY = Convert.ToInt32((y1 - topCenter) / (-1.0 * sqSize));

            int iXend = Convert.ToInt32((x2 - leftCenter) / sqSize);
            int iYend = Convert.ToInt32((y2 - topCenter) / (-1.0 * sqSize));

            int dix = (x1 < x2) ? 1 : -1;
            int diy = (y1 < y2) ? -1 : 1;

            int nSteps = Math.Abs(iXend - iX) + Math.Abs(iYend - iY);

            for (int i = 0; i < nSteps; i++)
            {
                var d1 = DistanceLinePoint(leftCenter + sqSize * Convert.ToDouble(iX + dix), topCenter - sqSize * Convert.ToDouble(iY));
                var d2 = DistanceLinePoint(leftCenter + sqSize * Convert.ToDouble(iX), topCenter - sqSize * Convert.ToDouble(iY + diy));

                if (d1 < d2)
                {
                    iX += dix;
                }
                else
                {
                    iY += diy;
                }

                returnList.Add((iX, iY));
            }

            double DistanceLinePoint(double x, double y)
            {
                var d = Math.Abs((y2 - y1) * x - (x2 - x1) * y + x2 * y1 - y2 * x1) / Math.Sqrt((y2 - y1) * (y2 - y1) + (x2 - x1) * (x2 - x1));
                return d;
            }
            return returnList;
        }
    }
}