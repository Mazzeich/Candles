using System;
using System.Collections.Generic;
using System.Runtime;

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
        
        public int FlatsFound
        {
            get => flatsFound;
            set => this.flatsFound = value;
        }

        public List<Bounds> ApertureBounds
        {
            get => apertureBounds;
            set => apertureBounds = value;
        }

        public HistoricalFlatFinder(List<_CandleStruct> candles)
        {
            Console.WriteLine("[HistoricalFlatFinder()]");
            globalCandles = candles;

            for (int i = 0; i < _Constants.NAperture; i++) // Формируем стартовое окно
            {
                aperture.Add(globalCandles[i]);
            }
            Console.WriteLine("Стартовое окно: globalCandles.Count = {0}\taperture.Count = {1}", globalCandles.Count, aperture.Count);

            FindAllFlats();
        }

        private void FindAllFlats()
        {
            int overallAdded = 0;
            int localAddedCandles = 0;
            int step = 0;

            for (int i = 0; i < globalCandles.Count - _Constants.NAperture; i += _Constants.NAperture + localAddedCandles)
            {
                step++;
                localAddedCandles = 0;
                Console.WriteLine("[i] = {0}\t\t[aperture.Count] = {1}", i, aperture.Count);
                
                // Если в конце осталось меньше свечей, чем вмещает окно
                if (globalCandles.Count - (_Constants.NAperture * step) + overallAdded <= _Constants.NAperture)
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
                    aperture = MoveAperture(overallAdded, step);
                    continue;
                }
                
                while (flatIdentifier.IsFlat == true)
                {                
                    Printer printer  = new Printer(flatIdentifier);
                    localAddedCandles++;
                    // Расширяем окно
                    aperture.Add(globalCandles[(_Constants.NAperture * step) + overallAdded + localAddedCandles + 1]);
                    Console.WriteLine("Aperture expanded...\t[aperture.Count] = {0}", aperture.Count);
                    flatIdentifier.Identify();

                    
                    if (flatIdentifier.IsFlat == false)
                    {
                        printer.WhyIsNotFlat(aperture[0], aperture[^1]);
                        flatsFound++;
                        overallAdded += localAddedCandles;

                        Console.WriteLine("+1 боковик!");
                        aperture.RemoveAt(aperture.Count - 1);
                        Bounds bounds = flatIdentifier.SetBounds(aperture[0], aperture[^1]);
                        apertureBounds.Add(bounds);
                        flatIdentifier.candles = aperture;
                        flatIdentifier.Identify();
                        printer.OutputApertureInfo();
                        // Двигаем окно в следующую позицию
                        aperture = MoveAperture(overallAdded - 1, step);
                    }
                }

            }
        }

        /// <summary>
        /// Функция перемещения окна в следующую позицию
        /// </summary>
        /// <param name="candlesToAdd">Всего свечей, которые были добавлены ранее</param>
        /// <param name="step">Текущий шаг прохода алгоритма</param>
        /// <returns>Новое окно свечей</returns>
        private List<_CandleStruct> MoveAperture(int candlesToAdd, int step)
        {
            Console.WriteLine("[MoveAperture()]");
            aperture.Clear();
            
            int startPosition = (_Constants.NAperture * step) + candlesToAdd + 1;
            for (int i = startPosition; i < startPosition + _Constants.NAperture; i++)
            {
                aperture.Add(globalCandles[i]);
            }
            
            return aperture;
        }
    }
}