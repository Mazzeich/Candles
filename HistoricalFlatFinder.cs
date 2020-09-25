using System;
using System.Collections.Generic;

// ReSharper disable CommentTypo

namespace Lua
{
    public class HistoricalFlatFinder
    {
        // TODO: Коллекция окон, чтобы можно было итерироваться по каждому и выводить информацию адекватнее
        private readonly List<_CandleStruct> globalCandles;
        
        private List<_CandleStruct> aperture = new List<_CandleStruct>(_Constants.NAperture);
        private List<Bounds> apertureBounds = new List<Bounds>();

        /// <summary>
        /// Сколько боковиков было найдено
        /// </summary>
        private int flatsFound;
        /// <summary>
        /// Сколько всего было добавлено свечей к окну
        /// </summary>
        private int overallAddedCandles;
        /// <summary>
        /// На каком шаге сейчас находимся
        /// </summary>
        private int step;

        
        public int FlatsFound => flatsFound;

        public List<Bounds> ApertureBounds => apertureBounds;

        public HistoricalFlatFinder(List<_CandleStruct> candles)
        {
            globalCandles = candles;

            for (int i = 0; i < _Constants.NAperture; i++) // Формируем стартовое окно
            {
                aperture.Add(globalCandles[i]);
            }

            FindAllFlats();
        }

        private void FindAllFlats()
        {
            overallAddedCandles = 0;
            step = 0;

            int localAddedCandles;

            for (int i = 0; i < globalCandles.Count - _Constants.NAperture; i += _Constants.NAperture + localAddedCandles)
            {
                step++;
                localAddedCandles = 0;
                // TODO: Log-файл. Асинхронный String.Append-ер, чтобы наконец забыть про консоль
                Console.WriteLine("[i] = {0}\t\t[aperture.Count] = {1}", i, aperture.Count);
                
                // Если в конце осталось меньше свечей, чем вмещает окно
                if (globalCandles.Count - (_Constants.NAperture * step) + overallAddedCandles <= _Constants.NAperture)
                {
                    break;
                }

                FlatIdentifier flatIdentifier = new FlatIdentifier(aperture);

                flatIdentifier.Identify();
                // Если не нашли боковик сходу
                if (flatIdentifier.IsFlat == false)
                {
                    // Двигаем окно в следующую позицию
                    Printer printer = new Printer(flatIdentifier);
                    printer.WhyIsNotFlat(aperture[0], aperture[^1]);
                    MoveAperture(overallAddedCandles, step);
                    continue;
                }
                
                while (flatIdentifier.IsFlat)
                {                
                    Printer printer  = new Printer(flatIdentifier);
                    localAddedCandles++;
                    // Расширяем окно
                    ExpandAperture(localAddedCandles);
                    flatIdentifier.Identify();
                    
                    if (!flatIdentifier.IsFlat)
                    {
                        printer.WhyIsNotFlat(aperture[0], aperture[^1]);
                        flatsFound++;
                        overallAddedCandles += localAddedCandles;

                        Console.WriteLine("+1 боковик!");
                        aperture.RemoveAt(aperture.Count - 1);
                        Bounds bounds = flatIdentifier.SetBounds(aperture[0], aperture[^1]);
                        apertureBounds.Add(bounds);
                        flatIdentifier.candles = aperture;
                        flatIdentifier.Identify();
                        printer.OutputApertureInfo();
                        // Двигаем окно в следующую позицию
                        MoveAperture(overallAddedCandles - 1, step);
                    }
                }

            }
        }

        /// <summary>
        /// Функция перемещения окна в следующую позицию
        /// </summary>
        /// <param name="candlesToAdd">Всего свечей, которые были добавлены ранее</param>
        /// <param name="step">Текущий шаг прохода алгоритма</param>
        private void MoveAperture(int candlesToAdd, int step)
        {
            Console.WriteLine("[MoveAperture()]");
            aperture.Clear();
            
            int startPosition = (_Constants.NAperture * step) + candlesToAdd + 1;
            for (int i = startPosition; i < startPosition + _Constants.NAperture; i++)
            {
                aperture.Add(globalCandles[i]);
            }
        }

        /// <summary>
        /// Функция расширения окна на 
        /// </summary>
        /// <param name="addedCandlesToAperture">Количество свечей, добавленных на текущем шаге</param>
        private void ExpandAperture(int addedCandlesToAperture)
        {
            aperture.Add(globalCandles[_Constants.NAperture * step + overallAddedCandles + addedCandlesToAperture + 1]);
            Console.WriteLine("Aperture expanded...\t[aperture.Count] = {0}", aperture.Count);
        }
    }
}