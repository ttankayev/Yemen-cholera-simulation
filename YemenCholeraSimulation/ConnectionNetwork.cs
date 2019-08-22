using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YemenCholeraSimulation
{
    enum NodeCategory { People, River, WaterSource };

    class Node
    {
        public int xPos;
        public int yPos;
        public NodeCategory category;
        public int count;
        public HashSet<(Node node, double weight)> successorNodes = new HashSet<(Node node, double weight)>();
        public int admin1 = 0;

        public Node(int x, int y, NodeCategory nodeCategory, int memberCount)
        {
            xPos = x;
            yPos = y;
            category = nodeCategory;
            count = memberCount;
        }
    }

    class ConnectionNetwork
    {
        //tolerance for float comparisons
        const double tol = 1e-6;

        //possible starting locations for the epidemic
        (int x, int y) sanaaCenterCoords = (2867, 4382);
        const double cityRadius = 100;
        const int maxWaterSpreadRadius = 10;
        const int maxRiverSpreadRadius = 10;
        const int numOfAdmin1Areas = 22;

        public long numberOfPeople = 0;
        public List<Node> startingNodes = new List<Node>();
        public Dictionary<(int x, int y, NodeCategory category), Node> nodesByCoordAndCat = new Dictionary<(int x, int y, NodeCategory category), Node>();
        public Dictionary<int, List<Node>> nodesByAdmin1 = new Dictionary<int, List<Node>>();
        public Dictionary<int, (double xAverage, double yAverage, double pop)> statsByAdmin1 = new Dictionary<int, (double xAverage, double yAverage, double pop)>();
        public Dictionary<int, double[]> nodeProbsByAdmin1 = new Dictionary<int, double[]>();

        //public List<double> popByGovernorateMultiplier = new List<double> {0.0, 1.2815880, 1.6739312, 0.7583185, 1.8315813, 0.8929471, 2.0930241, 8.1854078, 2.5351474, 0.6516146, 2.5259061, 0.3696455, 2.6095124, 2.4418725, 0.3794134, 0.8456809, 0.5074431, 2.7452452, 0.3479132, 9.4752580, 1.2416598, 1.0408411, 0.8399467};
        public List<double> popByGovernorateMultiplier = new List<double> { 0.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0};


        public ConnectionNetwork(float[,] popData, List<(int, int)>[,] waterData, int[,] adminData)
        {
            InitializeAdmin1Areas();
            InitializeRivers(waterData);
            InitializePeople(popData, adminData);

            Console.WriteLine("Initialized rivers and pops");
            Console.WriteLine("Creating network");

            var numberOfNodes = nodesByCoordAndCat.Count;
            double currentNode = 0;
            double percent = 0;

            var waterSpreadRadiusSquared = maxWaterSpreadRadius * maxWaterSpreadRadius;
            var riverSpreadRadiusSquared = maxRiverSpreadRadius * maxRiverSpreadRadius;

            foreach (var node in nodesByCoordAndCat.Values)
            {
                currentNode++;

                switch (node.category)
                {
                    case NodeCategory.WaterSource:
                        GenWaterConnections(node, waterSpreadRadiusSquared);
                        break;
                    case NodeCategory.River:
                        GenRiverConnections(node, riverSpreadRadiusSquared);
                        break;
                    case NodeCategory.People:
                        GenPeopleConnections(node);
                        break;
                    default:
                        break;
                }

                if (currentNode / numberOfNodes > percent)
                {
                    Console.WriteLine("Finished connecting {0}% of nodes", Convert.ToInt32(percent * 100));
                    percent += 0.01;
                }
            }

            Console.WriteLine("Finished connecting nodes");

            GenAdmin1Stats();

            GenAdmin1Probs();
        }

        private void InitializeAdmin1Areas()
        {
            for (int i = 1; i <= numOfAdmin1Areas; i++)
            {
                nodesByAdmin1.Add(i, new List<Node>());
            }
        }

        private void InitializeRivers(List<(int, int)>[,] waterData)
        {
            for (var i = 0; i < waterData.GetLength(0); i++)
            {
                for (int j = 0; j < waterData.GetLength(1); j++)
                {
                    if (waterData[i, j] != null)
                    {
                        if (!nodesByCoordAndCat.ContainsKey((i, j, NodeCategory.River)))
                        {
                            nodesByCoordAndCat.Add((i, j, NodeCategory.River), new Node(i, j, NodeCategory.River, 1));
                        }

                        var riverNode = nodesByCoordAndCat[(i, j, NodeCategory.River)];

                        foreach (var (ii, jj) in waterData[i, j].Skip(1))
                        {
                            if (!nodesByCoordAndCat.ContainsKey((ii, jj, NodeCategory.River)))
                            {
                                nodesByCoordAndCat.Add((ii, jj, NodeCategory.River), new Node(ii, jj, NodeCategory.River, 1));
                            }

                            var successorNode = nodesByCoordAndCat[(ii, jj, NodeCategory.River)];

                            riverNode.successorNodes.Add((successorNode, 1));
                        }
                    }
                }
            }
        }

        private void InitializePeople(float[,] popData, int[,] adminData)
        {
            var accumulationError = 0.0;
            for (int i = 0; i < popData.GetLength(0); i++)
            {
                for (int j = 0; j < popData.GetLength(1); j++)
                {
                    if (popData[i, j] > 0)
                    {
                        var governorate = 0;

                        if (adminData[i, j] > 0)
                        {
                            governorate = adminData[i, j];
                        }
                        else
                        {
                            var flag = false;
                            for (int a = 0; a < 10; a++)
                            {
                                if (flag) { break; };
                                for (int b = 0; b < a; b++)
                                {
                                    if (flag) { break; };
                                    foreach (var (x, y) in new HashSet<(int, int)> { (a, b), (-a, b), (a, -b), (-a, -b), (b, a), (b, -a), (-b, a), (-b, -a) })
                                    {
                                        if (adminData[i + x, j + y] > 0)
                                        {
                                            governorate = adminData[i + x, j + y];
                                            flag = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (governorate == 0)
                            {
                                flag = true;
                            }
                        }

                        int roundedPop = (int)(popData[i, j] * popByGovernorateMultiplier[governorate]);
                        accumulationError += popData[i, j] * popByGovernorateMultiplier[governorate] - roundedPop;

                        if (accumulationError > 1)
                        {
                            roundedPop++;
                            accumulationError -= 1;
                        }

                        if (roundedPop > 0)
                        {
                            Node waterNode;
                            if (nodesByCoordAndCat.ContainsKey((i, j, NodeCategory.River)))
                            {
                                waterNode = nodesByCoordAndCat[(i, j, NodeCategory.River)];
                            }
                            else
                            {
                                nodesByCoordAndCat.Add((i, j, NodeCategory.WaterSource), new Node(i, j, NodeCategory.WaterSource, 1));
                                waterNode = nodesByCoordAndCat[(i, j, NodeCategory.WaterSource)];
                            }

                            numberOfPeople += roundedPop;
                            var peopleNode = new Node(i, j, NodeCategory.People, roundedPop);
                            nodesByCoordAndCat.Add((i, j, NodeCategory.People), peopleNode);
                            
                            peopleNode.successorNodes.Add((waterNode, 1));

                            waterNode.successorNodes.Add((peopleNode, 1));

                            if (Math.Sqrt((i - sanaaCenterCoords.x) * (i - sanaaCenterCoords.x) + (j - sanaaCenterCoords.y) * (j - sanaaCenterCoords.y)) < cityRadius)
                            {
                                startingNodes.Add(peopleNode);
                            }

                            if (governorate > 0)
                            {
                                peopleNode.admin1 = governorate;
                                nodesByAdmin1[governorate].Add(peopleNode);
                            }
                        }
                    }
                }
            }
        }

        private void GenWaterConnections(Node node, int wrSQ)
        {
            if (node.xPos == 1218 && node.yPos == 4315)
            {
                Console.WriteLine("Checking Here");
            }


            var coveredCircle = new List<(double, double)>();
            var circleIsClosed = false;

            for (int i = 1; i <= maxWaterSpreadRadius; i++)
            {
                if (circleIsClosed) { break; }

                for (int j = 0; j <= i; j++)
                {
                    if (circleIsClosed) { break; }

                    if (i*i + j*j <= wrSQ)
                    {
                        foreach (var (x,y) in new HashSet<(int, int)> { (i, j), (-i, j), (i, -j), (-i, -j), (j, i), (j, -i), (-j, i), (-j, -i) })
                        {
                            if (circleIsClosed) { break; }

                            if (nodesByCoordAndCat.ContainsKey((node.xPos + x, node.yPos + y, NodeCategory.WaterSource)) || nodesByCoordAndCat.ContainsKey((node.xPos + x, node.yPos + y, NodeCategory.River)))
                            {
                                (var minAngle, var maxAngle) = GenArc(x, y);

                                var arcIsCovered = CheckArcCoverage(minAngle, maxAngle, coveredCircle);

                                if (arcIsCovered)
                                {
                                    continue;
                                }
                                else
                                {
                                    if (nodesByCoordAndCat.ContainsKey((node.xPos + x, node.yPos + y, NodeCategory.WaterSource)))
                                    {
                                        node.successorNodes.Add((nodesByCoordAndCat[(node.xPos + x, node.yPos + y, NodeCategory.WaterSource)], 1 / Math.Sqrt(i * i + j * j)));
                                    }
                                    else
                                    {
                                        node.successorNodes.Add((nodesByCoordAndCat[(node.xPos + x, node.yPos + y, NodeCategory.River)], 1 / Math.Sqrt(i * i + j * j)));
                                    }

                                    InsertArc(minAngle, maxAngle, coveredCircle);

                                    if (coveredCircle.Count == 1)
                                    {
                                        if (coveredCircle[0].Item1 < -Math.PI + tol && coveredCircle[0].Item2 > Math.PI - tol) { circleIsClosed = true; }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GenRiverConnections(Node node, int rrSQ)
        {
            var coveredCircle = new List<(double, double)>();
            var circleIsClosed = false;

            for (int i = 1; i <= maxRiverSpreadRadius; i++)
            {
                if (circleIsClosed) { break; }

                for (int j = 0; j <= i; j++)
                {
                    if (circleIsClosed) { break; }

                    if (i * i + j * j <= rrSQ)
                    {
                        foreach (var (x, y) in new HashSet<(int, int)> { (i, j), (-i, j), (i, -j), (-i, -j), (j, i), (j, -i), (-j, i), (-j, -i) })
                        {
                            if (circleIsClosed) { break; }

                            if (nodesByCoordAndCat.ContainsKey((node.xPos + x, node.yPos + y, NodeCategory.WaterSource)) || nodesByCoordAndCat.ContainsKey((node.xPos + x, node.yPos + y, NodeCategory.River)))
                            {
                                (var minAngle, var maxAngle) = GenArc(x, y);

                                var arcIsCovered = CheckArcCoverage(minAngle, maxAngle, coveredCircle);

                                if (arcIsCovered)
                                {
                                    continue;
                                }
                                else
                                {
                                    if (nodesByCoordAndCat.ContainsKey((node.xPos + x, node.yPos + y, NodeCategory.WaterSource)))
                                    {
                                        node.successorNodes.Add((nodesByCoordAndCat[(node.xPos + x, node.yPos + y, NodeCategory.WaterSource)], 1 / Math.Sqrt(i * i + j * j)));
                                    }

                                    InsertArc(minAngle, maxAngle, coveredCircle);

                                    if (coveredCircle.Count == 1)
                                    {
                                        if (coveredCircle[0].Item1 < -Math.PI + tol && coveredCircle[0].Item2 > Math.PI - tol) { circleIsClosed = true; }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GenPeopleConnections(Node node)
        {

        }

        private void GenAdmin1Stats()
        {
            foreach (var district in nodesByAdmin1.Keys)
            {
                var xAv = nodesByAdmin1[district].Select(node => node.xPos).Average();
                var yAv = nodesByAdmin1[district].Select(node => node.yPos).Average();
                var pop = nodesByAdmin1[district].Select(node => node.count).Sum();
                statsByAdmin1.Add(district, (xAv, yAv, pop));
            }
        }

        private void GenAdmin1Probs()
        {
            foreach (var district in nodesByAdmin1.Keys)
            {
                var total = statsByAdmin1[district].pop;
                var cumList = nodesByAdmin1[district].Select(node => node.count / total).ToArray();
                nodeProbsByAdmin1.Add(district, cumList);
            }
        }

        private (double minAngle, double maxAngle) GenArc(int x, int y)
        {
            var vertexRads = new List<double> { Math.Atan2(-(y + 0.5), (x + 0.5)), Math.Atan2(-(y - 0.5), (x + 0.5)), Math.Atan2(-(y - 0.5), (x - 0.5)), Math.Atan2(-(y + 0.5), (x - 0.5)) };
            var minAngle = vertexRads.Min();
            var maxAngle = vertexRads.Max();

            return (minAngle, maxAngle);
        }

        private bool CheckArcCoverage(double minAngle, double maxAngle, List<(double, double)> coveredCircle)
        {
            var arcIsCovered = false;

            if (maxAngle - minAngle < Math.PI)
            {
                foreach (var (a1, a2) in coveredCircle)
                {
                    if (minAngle > a1 - tol && maxAngle < a2 + tol)
                    {
                        arcIsCovered = true;
                        break;
                    }
                }
            }
            else
            {
                var check1 = false;
                var check2 = false;
                foreach (var (a1, a2) in coveredCircle)
                {
                    if (maxAngle > a1 - tol && a2 > Math.PI - tol) { check1 = true; }
                    if (minAngle < a2 + tol && a1 < -Math.PI + tol) { check2 = true; }
                }
                arcIsCovered = check1 && check2;
            }
            
            return arcIsCovered;
        }

        private void InsertArc(double minAngle, double maxAngle, List<(double, double)> coveredCircle)
        {
            if (maxAngle - minAngle < Math.PI)
            {
                var deletionList = new List<(double, double)>();
                double newMax = maxAngle;
                double newMin = minAngle;
                foreach (var (a1, a2) in coveredCircle)
                {
                    if (maxAngle > a1 - tol && maxAngle < a2 + tol)
                    {
                        newMax = a2;
                        deletionList.Add((a1, a2));
                    }
                    if (minAngle > a1 - tol && minAngle < a2 + tol)
                    {
                        newMin = a1;
                        deletionList.Add((a1, a2));
                    }
                }
                foreach (var angles in deletionList)
                {
                    coveredCircle.Remove(angles);
                }
                coveredCircle.Add((newMin, newMax));
            }
            else
            {
                var deletionList = new List<(double, double)>();
                double newMax = maxAngle;
                double newMin = minAngle;
                foreach (var (a1, a2) in coveredCircle)
                {
                    if (maxAngle > a1 - tol && a2 > Math.PI - tol)
                    {
                        newMax = a1;
                        deletionList.Add((a1, a2));
                    }
                    if (minAngle < a2 + tol && a1 < -Math.PI + tol)
                    {
                        newMin = a2;
                        deletionList.Add((a1, a2));
                    }
                }
                if (newMax < -Math.PI + tol || newMin > Math.PI - tol)
                {
                    coveredCircle = new List<(double, double)> { (-Math.PI, Math.PI) };
                }
                else
                {
                    foreach (var angles in deletionList)
                    {
                        coveredCircle.Remove(angles);
                    }
                    coveredCircle.Add((newMax, Math.PI));
                    coveredCircle.Add((-Math.PI, newMin));
                }
            }
        }
    }
}
