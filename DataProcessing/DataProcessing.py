import csv
import os
import numpy as np
import matplotlib.pyplot as plt
import pandas as pd
import geopandas as gpd
import scipy.stats

dirpath = os.path.normpath(os.getcwd() + os.sep + os.pardir) + "\\YemenCholeraSimulation"

def mean_confidence_interval(data, confidence=0.95):
    a = 1.0 * np.array(data)
    n = len(a)
    m, se = np.mean(a), scipy.stats.sem(a)
    h = se * scipy.stats.t.ppf((1 + confidence) / 2., n-1)
    return h

def runFinalValidation():	

	realData = {"Total" : 1063786,
				"Ibb" : 67129,
				"Abyan" : 28243, 
				"Amanat Al Asimah" : 103184,
				"Al Bayda" : 30568,
				"Taizz" : 63696,
				"Al Dhale'e" : 47136,
				"Al Jawf" : 16018,
				"Hajjah" : 121287,
				"Al Hudaydah" : 155908,
				"Hadramaut" : 591,
				"Dhamar" : 103214,
				"Shabwah" : 1399,
				"Sa'ada" : 10706,
				"Sana'a" : 76250,
				"Aden" : 20966,
				"Lahj" : 24342,
				"Marib" : 7288,
				"Al Mahwit" : 62887,
				"Al Maharah" : 1168,
				"Amran" : 103965,
				"Raymah" : 17841,
				"Socotra" : 0}

	realTotalPop = {"Total" : 28177859,
				"Ibb" : 2837000,
				"Abyan" : 568000, 
				"Amanat Al Asimah" : 2948472,
				"Al Bayda" : 760000,
				"Taizz" : 3182000,
				"Al Dhale'e" : 589000,
				"Al Jawf" : 2129000,
				"Hajjah" : 3189000,
				"Al Hudaydah" : 1424036,
				"Hadramaut" : 1913000,
				"Dhamar" : 632000,
				"Shabwah" : 1078000,
				"Sa'ada" : 1435528,
				"Sana'a" : 925000,
				"Aden" : 983000,
				"Lahj" : 336859,
				"Marib" : 695000,
				"Al Mahwit" : 150000,
				"Al Maharah" : 1052000,
				"Amran" : 720000,
				"Raymah" : 566000,
				"Socotra" : 64964}

	simResults = {key : [] for key in realData.keys() }

	with open(dirpath + "\\simResultsUnscaled.csv", mode='r') as csv_file:
		csv_reader = csv.DictReader(csv_file)
		for row in csv_reader:
			for key in simResults.keys():
				simResults[key].append(int(row[key]))


	simResultsDoubled = {key : [] for key in realData.keys() }
	with open(dirpath + "\\simResultsUnscaledDoubled.csv", mode='r') as csv_file:
		csv_reader = csv.DictReader(csv_file)
		for row in csv_reader:
			for key in simResultsDoubled.keys():
				simResultsDoubled[key].append(int(row[key]))


	governorate = "Total"
	resultsNormalized = [x/realTotalPop[governorate] for x in simResults[governorate]]
	totalResultsDoubledNormalized = [x/realTotalPop["Total"] for x in simResultsDoubled["Total"]]
	bins = np.linspace(min(resultsNormalized + totalResultsDoubledNormalized), max(resultsNormalized + totalResultsDoubledNormalized) ,50)
	#bins = np.linspace(min(resultsNormalized), max(resultsNormalized) ,50)
	plt.hist(resultsNormalized, bins, alpha=0.9, label = '$R_0 = 3.05$', color = '#0a5a77', density=True)
	plt.hist(totalResultsDoubledNormalized, bins, alpha=0.9, label = '$R_0 = 3.06$', color = '#81b5d1', density=True)
	realInfected = realData[governorate]/realTotalPop[governorate]
	#plt.axvline(realInfected, linestyle = ':', color = "g")
	plt.legend(loc='upper right')
	plt.ylabel("Probability density")
	plt.xlabel("Fraction of the population infected")#3.33, 3.35
	# epsilon = 0.1
	# print(realInfected)
	# print( sum(1 for x in resultsNormalized if (x>= realInfected*(1 - epsilon) and x <= realInfected*(1 + epsilon)) ) )
	# epsilon = 0.5
	# print( sum(1 for x in resultsNormalized if (x>= realInfected*(1 - epsilon) and x <= realInfected*(1 + epsilon)) ) )
	# 	# plt.figure(1)

	# index = 1
	# for key in realData.keys():
	# 	if key != "Total":
	# 		plt.subplot(5,5,index)
	# 		plt.hist([x/realTotalPop[key] for x in simResults[key]], 50)
	# 		#plt.axvline(np.mean(simResults[key]), color = "g")
	# 		plt.axvline(realData[key]/realTotalPop[key], linestyle = ':', color = "g")
	# 		plt.title(key)
	# 		index += 1

	# plt.subplots_adjust(top=0.92, bottom=0.08, left=0.10, right=0.95, hspace=0.6, wspace=0.5)
	plt.show()

def runTimeSeriesValidation():
	
	simResultsNInfected = []
	with open(dirpath + "\\simResultsTime.csv", mode='r') as csv_file:
		csv_reader = csv.reader(csv_file)
		simResultsTimeStamps = list(map(int, next(csv_reader)))
		for row in csv_reader:
			convertedRow = []
			lastValue = 1
			for x in row:
				if x != "":
					convertedRow.append(int(x))
					lastValue = int(x)
				else:
					convertedRow.append(lastValue)

			simResultsNInfected.append(convertedRow)

	realTimeStamps = []
	realNInfected = []

	with open(dirpath + "\\realResultsTimes.csv", mode='r') as csv_file:
		csv_reader = csv.reader(csv_file)
		for row in csv_reader:
			realTimeStamps.append(int(row[0]))
			realNInfected.append(int(row[1]))
			
	#for	nInfected in simResultsNInfected:
	#	plt.scatter(simResultsTimeStamps, nInfected, c = "b", alpha = 0.5, s = 1)
	
	simResultsMedian = np.median(simResultsNInfected, axis = 0)
	simResultsMean = np.average(simResultsNInfected, axis = 0)
	simResultsVariance = np.var(simResultsNInfected, axis = 0)
	simResultsSD = [ simResultsMean[i] + np.sqrt(simResultsVariance[i]) for i in range(len(simResultsMean)) ]
	simResultsNSD = [ max(0 , simResultsMean[i] - np.sqrt(simResultsVariance[i])) for i in range(len(simResultsMean)) ]
	simResultsTSD = [ simResultsMean[i] + 2*np.sqrt(simResultsVariance[i]) for i in range(len(simResultsMean)) ]
	simResultsTNSD = [ max(0, simResultsMean[i] - 2*np.sqrt(simResultsVariance[i])) for i in range(len(simResultsMean)) ]

	plt.scatter(simResultsTimeStamps, simResultsMedian, c = 'g', s = 1)
	plt.scatter(simResultsTimeStamps, simResultsMean, c= 'b', s = 1)
	plt.scatter(simResultsTimeStamps, simResultsSD, c= 'b', s = 1)
	plt.scatter(simResultsTimeStamps, simResultsNSD, c= 'b', s = 1)
	plt.scatter(simResultsTimeStamps, simResultsTSD, c= 'b', s = 1)
	plt.scatter(simResultsTimeStamps, simResultsTNSD, c= 'b', s = 1)

	plt.scatter(realTimeStamps, realNInfected, c = "r", s = 1)
	plt.yscale("log")
	plt.show()

def createBarPlots():

	realData = {"Total" : 1063786,
				"Ibb" : 67129,
				"Abyan" : 28243, 
				"Amanat Al Asimah" : 103184,
				"Al Bayda" : 30568,
				"Taizz" : 63696,
				"Al Dhale'e" : 47136,
				"Al Jawf" : 16018,
				"Hajjah" : 121287,
				"Al Hudaydah" : 155908,
				"Hadramaut" : 591,
				"Dhamar" : 103214,
				"Shabwah" : 1399,
				"Sa'ada" : 10706,
				"Sana'a" : 76250,
				"Aden" : 20966,
				"Lahj" : 24342,
				"Marib" : 7288,
				"Al Mahwit" : 62887,
				"Al Maharah" : 1168,
				"Amran" : 103965,
				"Raymah" : 17841,
				"Socotra" : 0}

	realTotalPop = {"Total" : 28177859,
				"Ibb" : 2837000,
				"Abyan" : 568000, 
				"Amanat Al Asimah" : 2948472,
				"Al Bayda" : 760000,
				"Taizz" : 3182000,
				"Al Dhale'e" : 589000,
				"Al Jawf" : 2129000,
				"Hajjah" : 3189000,
				"Al Hudaydah" : 1424036,
				"Hadramaut" : 1913000,
				"Dhamar" : 632000,
				"Shabwah" : 1078000,
				"Sa'ada" : 1435528,
				"Sana'a" : 925000,
				"Aden" : 983000,
				"Lahj" : 336859,
				"Marib" : 695000,
				"Al Mahwit" : 150000,
				"Al Maharah" : 1052000,
				"Amran" : 720000,
				"Raymah" : 566000,
				"Socotra" : 64964}

	simResults = {key : [] for key in realData.keys() }

	with open(dirpath + "\\simResultsUnscaled.csv", mode='r') as csv_file:
		csv_reader = csv.DictReader(csv_file)
		for row in csv_reader:
			for key in simResults.keys():
				simResults[key].append(int(row[key]))

	
	numsToKeys = list(realData.keys())
	numsToKeys.remove("Total")
	numsToKeys.sort(reverse = True, key = lambda x: realData[x])
	simMeans = [np.median(simResults[key]) for key in numsToKeys]
	simError = [mean_confidence_interval(simResults[key]) for key in numsToKeys]
	realMeans = [realData[key] for key in numsToKeys]

	ind = np.arange(1, 3*len(numsToKeys)+1, 3)
	width = 0.35

	indShifted = [x + 2*width for x in ind]

	plt.bar(ind, simMeans, width, color = (10.0/255, 90.0/255, 119.0/255, 1.0))
	plt.bar(indShifted, realMeans, width, color = (129.0/255, 181.0/255, 209.0/255, 1.0))
	plt.errorbar(ind, simMeans, yerr=simError, fmt='.', capsize = 5)

	numsToKeys = ["Al Hudaydah", "Hajjah*", "Amran*", "Dhamar*", "Amanat\n Al Asimah", "Sana'a", "Ibb", "Taizz", "Al Mahwit**", "Al Dhale'e**", "Al Bayda**", "Abyan**", "Lahj**", "Aden**", "Raymah", "Al Jawf", "Sa'ada", "Marib", "Shabwah", "Al Maharah", "Hadramaut", "Socotra"]
	plt.xticks(ind, numsToKeys)
	plt.ylabel("Infected people")
	plt.xlabel("Governorate")

	plt.show()

createBarPlots()