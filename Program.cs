﻿using System;
using System.Collections.Generic;
using NLog;
// ReSharper disable CommentTypo

namespace Lua
{
    internal static class Program
    {
        /// <summary>
        /// Инициализация логгера
        /// В документации указано, что это делают в каждом классе программы
        /// </summary>
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        static void Main()
        {
            logger.Trace("\nProgram has started...");
            
            List<_CandleStruct> candles = new List<_CandleStruct>();
            Reader reader = new Reader(candles);
            candles = reader.GetHistoricalData();
            HistoricalFlatFinder historicalFlatFinder = new HistoricalFlatFinder(candles);
            Printer printer = new Printer(historicalFlatFinder);
            printer.OutputHistoricalInfo();
            
            logger.Trace("Main() completed successfully.");
            LogManager.Shutdown();
        }
    }
}

