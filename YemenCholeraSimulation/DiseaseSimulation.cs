using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;

namespace YemenCholeraSimulation
{
    class DiseaseSimulation
    {
        //Disease parameters:
        public int minETime = 1; //Source CDC
        public int maxETime = 5;

        const int minITime = 4; //Source Wikipedia -> Article
        const int maxITime = 7;

        public int minBacLife = 7;
        public int rangeBacLife = 7;

        public double distantContaminationProb = 8.0e-04;

        public double halfSaturationK = 20;
        public double infectionProb = 0.08;
        public double humanWaterContaminationProb = 0.065;
        public double waterWaterContaminationMultiplier = 0.06;

        readonly int maxBacLife = 14;
        double waterWaterContaminationProb = 0.2 / (14 * 2 * Math.PI);

        //Network parameters:
        public List<Node> startingNodes;
        public Dictionary<(int x, int y, NodeCategory category), Node> nodesByCoordAndCat;
        public Dictionary<int, List<Node>> nodesByAdmin1;
        public Dictionary<int, (double xAverage, double yAverage, double pop)> statsByAdmin1;
        public Dictionary<int, double[]> nodeProbsByAdmin1;

        //Intervention parameters
        public Dictionary<Node, int> vaccinatedPeople = new Dictionary<Node, int>();
        public HashSet<Node> blockedWaterNodes = new HashSet<Node>();

        //Simulation parameters
        public string simulationMode = "standard";

        public int maxSimulationLength = 496; //length of simulation in days
        public long maxSimulationTime = 2 * 60 * 1000;//allowed time for each simulation

        public DiseaseSimulation(ConnectionNetwork network)
        {
            startingNodes = network.startingNodes;
            nodesByCoordAndCat = network.nodesByCoordAndCat;
            nodesByAdmin1 = network.nodesByAdmin1;
            statsByAdmin1 = network.statsByAdmin1;
            nodeProbsByAdmin1 = network.nodeProbsByAdmin1;

            RecalculateDiseaseParameters();
        }

        public DiseaseSimulation(ConnectionNetwork network, Dictionary<Node, int> newVacinatedPeople, HashSet<Node> newBlockedWaterNodes):this(network)
        {
            IntroduceIntervention(newVacinatedPeople, newBlockedWaterNodes);
        }

        public void IntroduceIntervention(Dictionary<Node, int> newVacinatedPeople, HashSet<Node> newBlockedWaterNodes)
        {
            vaccinatedPeople = newVacinatedPeople;
            blockedWaterNodes = newBlockedWaterNodes;
        }

        public void RecalculateDiseaseParameters()
        {
            waterWaterContaminationProb = waterWaterContaminationMultiplier / (maxBacLife * 2 * Math.PI);
        }

        public void SetSimulationParameters(List<double> parameters)
        {
            distantContaminationProb = parameters[0];
            halfSaturationK = parameters[1];
            infectionProb = parameters[2];
            humanWaterContaminationProb = parameters[3];
            waterWaterContaminationMultiplier = parameters[4];

            RecalculateDiseaseParameters();
        }

        public List<int>[] RunSimulation()
        {
            Node node = new Node(0, 0, NodeCategory.WaterSource, 0);

            var startingNodeGenerated = false;

            while (!startingNodeGenerated)
            {
                node = startingNodes.ElementAt(DiscreteUniform.Sample(0, startingNodes.Count - 1));
                if (vaccinatedPeople.ContainsKey(node))
                {
                    if (vaccinatedPeople[node] < node.count)
                    {
                        startingNodeGenerated = true;
                    }
                }
                else
                {
                    startingNodeGenerated = true;
                }
            }

            return RunSimulation(node);
        }

        public List<int>[] RunSimulation(Node node)
        {

            var bacteriaByWaterNode = new Dictionary<Node, int[]>();
            var infectionsByPeopleNode = new Dictionary<Node, (int[] E, int[] I, int R)>
            {
                { node, (new int[maxETime], new int[maxITime], 0) }
            };

            infectionsByPeopleNode[node].I[DiscreteUniform.Sample(minITime, maxITime) - 1] = 1;

            return RunSimulation(bacteriaByWaterNode, infectionsByPeopleNode);
        }

        public List<int>[] RunSimulation(Dictionary<Node, int[]> bacteriaByWaterNode, Dictionary<Node, (int[] E, int[] I, int R)> infectionsByPeopleNode)
        {
            var currentlyInfectious = 0;
            var currentlySick = 0;
            var livingBacteria = 0;

            foreach (var (E, I, R) in infectionsByPeopleNode.Values)
            {
                currentlyInfectious += I.Sum();
                currentlySick += E.Sum() + I.Sum();
            }
            foreach (var bacteria in bacteriaByWaterNode.Values)
            {
                livingBacteria += bacteria.Sum();
            }
            
            var infectionsByDistrict = new List<int>[nodesByAdmin1.Count + 1];
            InitializeInfectionsByDistric(infectionsByPeopleNode, infectionsByDistrict);

            var day = 0;
            //var stopwatch = new Stopwatch();
            //stopwatch.Start();
            //long elapsedTime = 0;

            while (ContinueSimulation(currentlySick, livingBacteria, day, currentlyInfectious))// && elapsedTime < maxSimulationTime)
            {
                day++;

                //elapsedTime = stopwatch.ElapsedMilliseconds;

                //Console.WriteLine("Simulating day {0}", day);

                var newlyExposedPeople = new Dictionary<Node, int>();
                var newlyContaminatedWater = new Dictionary<Node, int>();

                GenerateNewInfections(bacteriaByWaterNode, infectionsByPeopleNode, newlyContaminatedWater, newlyExposedPeople, ref livingBacteria);

                UpdateOldInfections(bacteriaByWaterNode, infectionsByPeopleNode, newlyContaminatedWater, newlyExposedPeople, ref currentlySick, ref livingBacteria, ref currentlyInfectious);

                AddNewInfections(bacteriaByWaterNode, infectionsByPeopleNode, newlyContaminatedWater, newlyExposedPeople, ref currentlySick);

                UpdateResults(newlyExposedPeople, infectionsByDistrict, day);
            }

            //if (elapsedTime > maxSimulationTime)
            //{
            //    for (int i = 0; i < infectionsByDistrict.Count(); i++)
            //    {
            //        infectionsByDistrict[i].Add(-1);
            //    }
            //}

            //stopwatch.Stop();

            return infectionsByDistrict;
        }

        private bool ContinueSimulation(int currentlySick, int livingBacteria, int day, int currentlyInfectious)
        {
            if (simulationMode == "standard")
            {
                return ((currentlySick > 0 || livingBacteria > 0) && day < maxSimulationLength);
            }
            else if (simulationMode == "calculateR0")
            {
                return (currentlyInfectious > 0 || livingBacteria > 0);
            }
            else
            {
                return false;
            }

        }

        private void InitializeInfectionsByDistric(Dictionary<Node, (int[] E, int[] I, int R)> infectionsByPeopleNode, List<int>[] infectionsByDistrict)
        {

            for (int i = 0; i < infectionsByDistrict.Length; i++)
            {
                infectionsByDistrict[i] = new List<int>() { 0 };
            }

            foreach (var node in infectionsByPeopleNode.Keys)
            {
                var infectionsInNode = infectionsByPeopleNode[node].E.Sum() + infectionsByPeopleNode[node].I.Sum() + infectionsByPeopleNode[node].R;

                infectionsByDistrict[node.admin1][0] += infectionsInNode;

                infectionsByDistrict[0][0] += infectionsInNode;
            }
        }

        private void GenerateNewInfections(Dictionary<Node, int[]> bacteriaByWaterNode, Dictionary<Node, (int[] E, int[] I, int R)> infectionsByPeopleNode, Dictionary<Node,int> newlyContaminatedWater, Dictionary<Node, int> newlyExposedPeople, ref int livingBacteria )
        {
            foreach (var waterNode in bacteriaByWaterNode.Keys)
            {
                foreach (var (potentialNode, weight) in waterNode.successorNodes)
                {
                    if (potentialNode.category == NodeCategory.WaterSource || potentialNode.category == NodeCategory.River)
                    {
                        if (!blockedWaterNodes.Contains(potentialNode))
                        {
                            var nBacteria = Binomial.Sample(weight * waterWaterContaminationProb, bacteriaByWaterNode[waterNode].Sum());

                            if (nBacteria > 0)
                            {
                                livingBacteria += nBacteria;

                                if (newlyContaminatedWater.ContainsKey(potentialNode))
                                {
                                    newlyContaminatedWater[potentialNode] += nBacteria;
                                }
                                else
                                {
                                    newlyContaminatedWater.Add(potentialNode, nBacteria);
                                }
                            }
                        }
                    }
                    else if (potentialNode.category == NodeCategory.People)
                    {
                        int nSusceptible = potentialNode.count;

                        if (vaccinatedPeople.ContainsKey(potentialNode))
                        {
                            nSusceptible -= vaccinatedPeople[potentialNode];
                        }

                        if (infectionsByPeopleNode.ContainsKey(potentialNode))
                        {
                            nSusceptible -= (infectionsByPeopleNode[potentialNode].E.Sum() + infectionsByPeopleNode[potentialNode].I.Sum() + infectionsByPeopleNode[potentialNode].R);
                        }

                        int nBacteria = bacteriaByWaterNode[waterNode].Sum();

                        double pInfect = infectionProb * nBacteria / (halfSaturationK + nBacteria);

                        int nExposed = Binomial.Sample(pInfect, nSusceptible);

                        if (nExposed > 0)
                        {
                            newlyExposedPeople.Add(potentialNode, nExposed);
                        }
                    }
                }
            }

            foreach (var personNode in infectionsByPeopleNode.Keys)
            {
                if (infectionsByPeopleNode[personNode].I.Sum() > 0)
                {
                    var distInfectors = InfectDistantWaterSources(personNode, newlyContaminatedWater, infectionsByPeopleNode[personNode].I.Sum());

                    int possibleInfectors = infectionsByPeopleNode[personNode].I.Sum() - distInfectors;

                    if (possibleInfectors > 0)
                    {
                        foreach (var (exposedWaterNode, edgeStrength) in personNode.successorNodes)
                        {
                            if (!blockedWaterNodes.Contains(exposedWaterNode))
                            {
                                if (exposedWaterNode.category == NodeCategory.WaterSource || exposedWaterNode.category == NodeCategory.River)
                                {
                                    int nBacteria = Binomial.Sample(humanWaterContaminationProb * edgeStrength, possibleInfectors);

                                    if (nBacteria > 0)
                                    {
                                        livingBacteria += nBacteria;

                                        if (newlyContaminatedWater.ContainsKey(exposedWaterNode))
                                        {
                                            newlyContaminatedWater[exposedWaterNode] += nBacteria;
                                        }
                                        else
                                        {
                                            newlyContaminatedWater.Add(exposedWaterNode, nBacteria);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateOldInfections(Dictionary<Node, int[]> bacteriaByPeopleNode, Dictionary<Node, (int[] E, int[] I, int R)> infectionsByPeopleNode, Dictionary<Node, int> newlyContaminatedWater, Dictionary<Node, int> newlyExposedPeople, ref int currentlySick, ref int livingBacteria, ref int currentlyInfectious )
        {
            var waterKeys = new List<Node>(bacteriaByPeopleNode.Keys);

            foreach (var waterNode in waterKeys)
            {
                int tempOld = 0;
                int tempNew = 0;

                for (int i = bacteriaByPeopleNode[waterNode].Length - 1; i > -1; i--)
                {
                    tempNew = bacteriaByPeopleNode[waterNode][i];
                    bacteriaByPeopleNode[waterNode][i] = tempOld;
                    tempOld = tempNew;
                }

                livingBacteria -= tempOld;
            }

            var peopleKeys = new List<Node>(infectionsByPeopleNode.Keys);

            foreach (var peopleNode in peopleKeys)
            {
                int tempOld = 0;
                int tempNew = 0;

                for (int i = infectionsByPeopleNode[peopleNode].E.Length - 1; i > -1; i--)
                {
                    tempNew = infectionsByPeopleNode[peopleNode].E[i];
                    infectionsByPeopleNode[peopleNode].E[i] = tempOld;
                    tempOld = tempNew;
                }

                
                int newInfected = tempOld;
                currentlyInfectious += newInfected;

                tempOld = 0;
                tempNew = 0;

                for (int i = infectionsByPeopleNode[peopleNode].I.Length - 1; i > -1; i--)
                {
                    tempNew = infectionsByPeopleNode[peopleNode].I[i];
                    infectionsByPeopleNode[peopleNode].I[i] = tempOld;
                    tempOld = tempNew;
                }

                int newR = tempOld;

                currentlySick -= newR;
                currentlyInfectious -= newR;

                for (int i = 0; i < newInfected; i++)
                {
                    infectionsByPeopleNode[peopleNode].I[DiscreteUniform.Sample(minITime, maxITime) - 1]++;
                }

                newR += infectionsByPeopleNode[peopleNode].R;
                infectionsByPeopleNode[peopleNode] = (infectionsByPeopleNode[peopleNode].E, infectionsByPeopleNode[peopleNode].I, newR);
            }
        }

        private void AddNewInfections(Dictionary<Node, int[]> bacteriaByPeopleNode, Dictionary<Node, (int[] E, int[] I, int R)> infectionsByPeopleNode, Dictionary<Node, int> newlyContaminatedWater, Dictionary<Node, int> newlyExposedPeople, ref int currentlySick)
        {
            foreach (var waterNode in newlyContaminatedWater.Keys)
            {
                var newConatmArray = new int[maxBacLife];
                for (int i = 0; i < newlyContaminatedWater[waterNode]; i++)
                {
                    newConatmArray[DiscreteUniform.Sample(minBacLife, maxBacLife) - 1]++;
                }

                if (bacteriaByPeopleNode.ContainsKey(waterNode))
                {
                    for (int i = 0; i < bacteriaByPeopleNode[waterNode].Length; i++)
                    {
                        bacteriaByPeopleNode[waterNode][i] += newConatmArray[i];
                    }
                }
                else
                {
                    bacteriaByPeopleNode.Add(waterNode, newConatmArray);
                }
            }

            foreach (var peopleNode in newlyExposedPeople.Keys)
            {
                var newExposedPeopleArray = new int[maxETime];
                for (int i = 0; i < newlyExposedPeople[peopleNode]; i++)
                {
                    newExposedPeopleArray[DiscreteUniform.Sample(minETime, maxETime) - 1]++;
                }

                if (infectionsByPeopleNode.ContainsKey(peopleNode))
                {
                    for (int i = 0; i < infectionsByPeopleNode[peopleNode].E.Length; i++)
                    {
                        infectionsByPeopleNode[peopleNode].E[i] += newExposedPeopleArray[i];
                    }
                }
                else
                {
                    infectionsByPeopleNode.Add(peopleNode, (newExposedPeopleArray, new int[maxITime], 0));
                }

                currentlySick += newlyExposedPeople[peopleNode];
            }
        }

        private void UpdateResults(Dictionary<Node, int> newlyExposedPeople, List<int>[] infectionsByDistrict, int day)
        {
            for (int i = 0; i < infectionsByDistrict.Length; i++)
            {
                infectionsByDistrict[i].Add(infectionsByDistrict[i].Last());
            }

            foreach (var node in newlyExposedPeople.Keys)
            {
                var newInfectionsInNode = newlyExposedPeople[node];

                infectionsByDistrict[node.admin1][day] += newInfectionsInNode;

                infectionsByDistrict[0][day] += newInfectionsInNode;
            }
        }

        private int InfectDistantWaterSources(Node personNode, Dictionary<Node, int> newlyContaminatedWater, int nInfectious)
        {
            var distantInfectors = Binomial.Sample(distantContaminationProb, nInfectious);

            if (distantInfectors > 0)
            {
                var probWeights = new Dictionary<int, double>();
                foreach (var district in statsByAdmin1.Keys)
                {
                    if (district != personNode.admin1 )
                    probWeights.Add( district, Math.Sqrt((personNode.xPos - statsByAdmin1[district].xAverage) * (personNode.xPos - statsByAdmin1[district].xAverage) + (personNode.yPos - statsByAdmin1[district].yAverage) * (personNode.yPos - statsByAdmin1[district].yAverage)));
                }

                var tot = probWeights.Values.Sum();
                var probs = probWeights.Values.Select(x => x / tot).ToArray();

                for (int i = 0; i < distantInfectors; i++)
                {
                    var district = Categorical.Sample(probs) + 1;

                    if (district >= personNode.admin1)
                    {
                        district++;
                    }

                    var exposedNode = nodesByAdmin1[district][Categorical.Sample(nodeProbsByAdmin1[district])];

                    Node exposedWaterNode = exposedNode;

                    if (nodesByCoordAndCat.ContainsKey((exposedNode.xPos, exposedNode.yPos, NodeCategory.River)))
                    {
                        exposedWaterNode = nodesByCoordAndCat[(exposedNode.xPos, exposedNode.yPos, NodeCategory.River)];
                    }
                    else if (nodesByCoordAndCat.ContainsKey((exposedNode.xPos, exposedNode.yPos, NodeCategory.WaterSource)))
                    {
                        exposedWaterNode = nodesByCoordAndCat[(exposedNode.xPos, exposedNode.yPos, NodeCategory.WaterSource)];
                    }

                    if (exposedWaterNode.category == NodeCategory.WaterSource || exposedWaterNode.category == NodeCategory.River)
                    {
                        if (!blockedWaterNodes.Contains(exposedWaterNode))
                        {
                            if (newlyContaminatedWater.ContainsKey(exposedWaterNode))
                            {
                                newlyContaminatedWater[exposedWaterNode] += 1;
                            }
                            else
                            {
                                newlyContaminatedWater.Add(exposedWaterNode, 1);
                            }
                        }
                    }

                }
            }

            return distantInfectors;
        }
    }
}
