using System;
using System.IO;
using System.Globalization;
using CsvHelper;
using System.Data;
using System.Collections.Generic;

namespace Lua
{
    public class Reader
    {
        List<_CandleStruct> candleStruct;

        private static string currentDirectory;
        
        private string pathHigh;
        private string pathLow;
        private string pathAvg;
        private string pathOpen;
        private string pathClose;
        private string pathVolume;
        private string pathHistoricalData;


        private string[] readHeights;
        private string[] readLows;
        private string[] readAvgs;
        private string[] readCloses;
        private string[] readOpens;
        private string[] readVolumes;
        private string[] readAllData;

        public Reader(List<_CandleStruct> candleStruct)
        {
            this.candleStruct = candleStruct;
            currentDirectory = Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Считать все строки из файлов распарсенных данных
        /// </summary>
        public List<_CandleStruct> GetSeparatedData()
        {
            pathHigh   = Path.Combine(currentDirectory, @"..\..\..\Data\dataHigh.txt");
            pathLow    = Path.Combine(currentDirectory, @"..\..\..\Data\dataLow.txt");
            pathAvg    = Path.Combine(currentDirectory, @"..\..\..\Data\dataAvg.txt");
            pathOpen   = Path.Combine(currentDirectory, @"..\..\..\Data\dataOpen.txt");
            pathClose  = Path.Combine(currentDirectory, @"..\..\..\Data\dataClose.txt");
            pathVolume = Path.Combine(currentDirectory, @"..\..\..\Data\dataVolume.txt");

            readHeights  = File.ReadAllLines(pathHigh);
            readLows     = File.ReadAllLines(pathLow);
            readAvgs     = File.ReadAllLines(pathAvg);
            readOpens    = File.ReadAllLines(pathOpen);
            readCloses   = File.ReadAllLines(pathClose);
            readVolumes  = File.ReadAllLines(pathVolume);
        
            for (int i = 0; i < readHeights.Length; i++) //readHeights.Length = readLows.Length
            {
                _CandleStruct temp;
                temp.low   = double.Parse(readLows[i], CultureInfo.InvariantCulture);
                temp.high  = double.Parse(readHeights[i], CultureInfo.InvariantCulture);
                temp.close = double.Parse(readCloses[i], CultureInfo.InvariantCulture);
                temp.avg   = double.Parse(readAvgs[i], CultureInfo.InvariantCulture);
                temp.date  = "";

                candleStruct.Add(temp);
            }

            return candleStruct;
        }

        public List<_CandleStruct> GetHistoricalData()
        {
            // TODO: Сделать нормальное считывание свечей
            pathHistoricalData = Path.Combine(currentDirectory, @"..\..\..\Data\dataRTS.csv");

            using (StreamReader reader = new StreamReader(pathHistoricalData))
            {
                using CsvReader csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                using (CsvDataReader dr = new CsvDataReader(csv))
                {
                    // readAllData[0] - "<DATE>,<TIME>,<OPEN>,<HIGH>,<LOW>,<CLOSE>,<VOL>"
                    readAllData = File.ReadAllLines(pathHistoricalData);
                    string[] row = new string[7];

                    for (int i = 1; i < readAllData.Length; i++)
                    {
                        row = readAllData[i].Split(",");
                        _CandleStruct temp;
                        temp.low   = double.Parse(row[4], CultureInfo.InvariantCulture);
                        temp.high  = double.Parse(row[3], CultureInfo.InvariantCulture);
                        temp.close = double.Parse(row[5], CultureInfo.InvariantCulture);
                        temp.avg   = (temp.high + temp.low) * 0.5;
                        temp.date  = row[0] + " " + row[1];
                        //temp.date = i.ToString();

                        candleStruct.Add(temp);
                    }
                }
            }

            return candleStruct;
        }
    }
}