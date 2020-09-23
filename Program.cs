﻿using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;

namespace Lua
{
    static class Program
    {
        static void Main(string[] args)
        {
            List<_CandleStruct> candles = new List<_CandleStruct>();
            Reader reader = new Reader(candles);
            candles = reader.GetHistoricalData();
            HistoricalFlatFinder historicalFlatFinder = new HistoricalFlatFinder(candles);
            Printer printer = new Printer(historicalFlatFinder);
            printer.OutputHistoricalInfo();

            Console.WriteLine("End of Main();");
            Console.ReadKey();
            return;
        }
    }
}

