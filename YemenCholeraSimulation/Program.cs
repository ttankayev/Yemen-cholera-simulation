using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using System.Threading.Tasks;
using System.IO;

namespace YemenCholeraSimulation
{
    class Program
    {
        static void Main(string[] args)
        {

            var diseaseNetwork = new ConnectionNetwork(Data.GenPopData(), Data.GenWaterData(), Data.GenAdminData());
            var simulation = new DiseaseSimulation(diseaseNetwork);

            Console.WriteLine(diseaseNetwork.numberOfPeople);
            Console.WriteLine(diseaseNetwork.nodesByCoordAndCat.Count(x => x.Key.category == NodeCategory.People));
            Console.WriteLine(diseaseNetwork.nodesByCoordAndCat.Count(x => x.Key.category == NodeCategory.WaterSource));
            Console.WriteLine(diseaseNetwork.nodesByCoordAndCat.Count(x => x.Key.category == NodeCategory.River));
            Console.WriteLine(diseaseNetwork.nodesByCoordAndCat.Values.Sum(x => x.successorNodes.Count ) );
            //Intervention.CalculateR0(diseaseNetwork, simulation);

            //RunSimulations();

            Console.WriteLine("Press any key to continue.");
            Console.ReadLine();
        }

        static void RunSimulations()
        {
            var diseaseNetwork = new ConnectionNetwork(Data.GenPopData(), Data.GenWaterData(), Data.GenAdminData());

            string file = Directory.GetCurrentDirectory();
            file = Path.GetFullPath(Path.Combine(file, @"..\..\")) + "simResultsTimeUnscaled.csv";
            string header = string.Join(",",Enumerable.Range(0, 496).Select(x => x.ToString()).ToArray()) + "\n";


            for (int i = 0; i < 999; i++)
            {
                //Console.WriteLine(i);

                var simulation = new DiseaseSimulation(diseaseNetwork);

                var simResults = simulation.RunSimulation();

                while (simResults[0].Last() < 1000)
                {
                    simResults = simulation.RunSimulation();
                }

                if (!File.Exists(file))
                {
                    File.WriteAllText(file, header);
                }

                Console.WriteLine(i.ToString() + ": " + simResults[0].Last().ToString());
                //var line = string.Concat(simResults.Select(l => l.Last().ToString() + ","));
                var line = string.Concat(simResults[0].Select(dInfectious => dInfectious.ToString() + ","));  
                line = line.Remove(line.Length - 1) + "\n";

                File.AppendAllText(file, line);
            }
        }

        static void ManualCalibration(ConnectionNetwork diseaseNetwork, DiseaseSimulation simulation)
        {
            var numSimulations = 1;

            var tPop = diseaseNetwork.numberOfPeople;

            string input = "R";

            while (input != "S")
            {
                if (input == "N")
                {
                    Console.WriteLine("Input new parametes separated by spaces: distContProb, K, infProb, hwProb, wwProb");
                    var newParams = Console.ReadLine();
                    var tokens = newParams.Split(' ');
                    var parameters = Array.ConvertAll(tokens, double.Parse).ToList();
                    simulation.SetSimulationParameters(parameters);
                }

                if (input == "C")
                {
                    Console.WriteLine("Input new number of simulations: ");
                    var n = Console.ReadLine();
                    numSimulations = Convert.ToInt32(n);
                }

                var results = new double[numSimulations];

                for (int i = 0; i < numSimulations; i++)
                {
                    Console.WriteLine("Running simulation: " + (i + 1));
                    results[i] = ParameterFitting.ObjectiveFunction(simulation);
                }

                Console.WriteLine("Objective values: " + results.Average());

                Console.WriteLine("Run another simulation (R), input new parameters (N), change number of simulations (C) or stop (S)");
                input = Console.ReadLine();
            }
        }

    }
}