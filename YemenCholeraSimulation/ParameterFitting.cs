using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

namespace YemenCholeraSimulation
{

    static class ParameterFitting
    {
        const int estimateRepetitions = 10;
        const int maxIters = 100;
        const double epsilon = 10;
        const double acceleration = 1.25;
        readonly static double[] candidate = { -acceleration, 0, acceleration}; 

        const double bigNumber = 1e20;
        const int maxInfectionAttempts = 100;

        //From humDataExchange:
        readonly static int[] infectionsByGovernorate = { 959810, 59932, 28103, 91799, 26793, 58223, 47004, 14689, 106933, 139145, 587, 90560, 1396, 9722, 68453, 20286, 22596, 6897, 56447, 1167, 94581, 14497, 0 };

        public static double ObjectiveFunction(ConnectionNetwork network)
        {
            var simulation = new DiseaseSimulation(network);

            return ObjectiveFunction(simulation);
        }

        public static double ObjectiveFunction(ConnectionNetwork network, List<double> parameters)
        {
            var simulation = new DiseaseSimulation(network);

            return ObjectiveFunction(simulation, parameters);
        }

        public static double ObjectiveFunction(DiseaseSimulation simulation, List<double> parameters)
        {
            foreach (var value in parameters)
            {
                if (value < 0)
                {
                    return bigNumber;
                }
            }

            simulation.SetSimulationParameters(parameters);

            return ObjectiveFunction(simulation);
        }

        public static double ObjectiveFunction(DiseaseSimulation simulation)
        {
            var simResults = simulation.RunSimulation();

            //var infectionAttempts = 0;

            while (simResults[0].Last() < 100)
            {
                simResults = simulation.RunSimulation();
            }

            if (simResults[0].Last() == -1)
            {
                return bigNumber;
            }

            //if (simResults[0].Count < simulation.maxSimulationLength)
            //{
            //    return bigNumber;
            //}

            //Console.WriteLine("Difference in total infected: " + (simResults[0].Last() - infectionsByGovernorate[0]));
            //Console.WriteLine("Difference in infections in the capital: " + (simResults[3].Last() - infectionsByGovernorate[3]));

            //var objValue = Math.Abs(simResults[0].Last() - infectionsByGovernorate[0]) + Math.Abs(simResults[3].Last() - infectionsByGovernorate[3]);
            var objValue = simResults[0].Last();
            //var objValue = 0; //simResults[0].Last();


            //foreach (var governorate in simulation.nodeProbsByAdmin1.Keys)
            //{
            //    objValue += Math.Abs(simResults[governorate].Last() - infectionsByGovernorate[governorate]);
            //}

            return Convert.ToDouble(objValue);
        }

        public static double SimpleLocalSearch(ConnectionNetwork network)
        {
            List<double> startingVector = new List<double>() { 1.0 / 30000, 20, 0.2, 0.2, 0.2 };
            List<double> stepVector = new List<double>();
            foreach (var value in startingVector)
            {
                stepVector.Add(Math.Min(Math.Abs(value - 1), value) * 0.25);
            }

            return SimpleLocalSearch(network, startingVector, stepVector);
        }

        public static double SimpleLocalSearch(ConnectionNetwork network, List<double> startingVector, List<double> stepVector)
        {
            var prevBestObjV = bigNumber;
            var newBestObjV = bigNumber;
            var difference = bigNumber;
            var iter = 0;


            while (iter < maxIters)
            {
                Console.WriteLine("Iteration: {0}. Objective value: {1}", iter, newBestObjV);
                prevBestObjV = newBestObjV;
                for (int i = 0; i < startingVector.Count; i++)
                {
                    newBestObjV = LocalSearchStep(network, startingVector, stepVector, i);
                }
                difference = Math.Abs(newBestObjV - prevBestObjV);
                iter++;                
            }

            string pathDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = pathDesktop + "\\OptimalParameters" + ".csv";

            if (!System.IO.File.Exists(filePath))
            {
                System.IO.File.Create(filePath).Close();
            }

            string delimter = ";";

            using (System.IO.TextWriter writer = System.IO.File.CreateText(filePath))
            {
                foreach (var num in startingVector)
                {
                    writer.WriteLine(string.Join(delimter, num));
                }
                writer.WriteLine(string.Join(delimter, newBestObjV));
            }

            return newBestObjV;
        }

        private static double LocalSearchStep(ConnectionNetwork network, List<double> startingVector, List<double> stepVector, int coordiante)
        {
            Console.WriteLine("Optimizing over coordinate {0}", coordiante);
            var best = -1;
            var bestObjV = bigNumber;

            for (int i = 0; i < candidate.Length; i++)
            {
                startingVector[coordiante] = startingVector[coordiante] + stepVector[coordiante] * candidate[i];
                var temp = EstimateObjective(network, startingVector);
                startingVector[coordiante] = startingVector[coordiante] - stepVector[coordiante] * candidate[i];
                if (temp < bestObjV)
                {
                    bestObjV = temp;
                    best = i;
                }
            }

            if (candidate[best] == 0)
            {
                stepVector[coordiante] = stepVector[coordiante] / acceleration;
            }
            else
            {
                startingVector[coordiante] = startingVector[coordiante] + stepVector[coordiante] * candidate[best];
                stepVector[coordiante] = stepVector[coordiante] * candidate[best];
            }

            Console.WriteLine("Best candidate: " + best.ToString() + ". Objective: " + bestObjV);

            return bestObjV;
        }

        private static double EstimateObjective(ConnectionNetwork network, List<double> startingVector)
        {
            var obj = new double[estimateRepetitions];
            Parallel.For(0, estimateRepetitions, (i, state) =>
            {
                Console.WriteLine("Simulation: {0}", i);
                obj[i] = ObjectiveFunction(network, startingVector);
            });

            return obj.Average();
        }
    }
}
