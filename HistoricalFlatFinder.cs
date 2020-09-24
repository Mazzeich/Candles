using System;
using System.Collections.Generic;
using System.Runtime;

// ReSharper disable CommentTypo

namespace Lua
{
    public class HistoricalFlatFinder
    {
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

        public HistoricalFlatFinder(List<_CandleStruct> _candles)
        {
            Console.WriteLine("[HistoricalFlatFinder()]");
            globalCandles = _candles;

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

            for (int i = 0; i < globalCandles.Count - _Constants.NAperture - 1; i += _Constants.NAperture + overallAdded)
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
                    aperture = MoveAperture(overallAdded, step);
                    continue;
                }
                
                while (flatIdentifier.IsFlat == true)
                {                
                    Printer printer  = new Printer(flatIdentifier);
                    localAddedCandles++;
                    // Расширяем окно
                    aperture.Add(globalCandles[(_Constants.NAperture * step) - 1 + localAddedCandles]);
                    Console.WriteLine("Aperture expanded...\t[aperture.Count] = {0}", aperture.Count);
                    flatIdentifier.Identify();

                    
                    if (flatIdentifier.IsFlat == false)
                    {
                        flatsFound++;
                        // Нужно отсечь последнюю добавленную свечу,
                        // так как на ней `isFlat == false`
                        overallAdded += localAddedCandles - 1;

                        Console.WriteLine("+1 боковик!");
                        aperture.RemoveAt(aperture.Count - 1);
                        Console.WriteLine("[overallAdded] = {0}", overallAdded);
                        // bounds.left = flatIdentifier.FlatBounds.left;
                        // bounds.right = flatIdentifier.FlatBounds.right;
                        Bounds bounds = flatIdentifier.SetBounds(aperture[0], aperture[^1]);
                        apertureBounds.Add(bounds);
                        flatIdentifier.Identify();
                        printer.OutputApertureInfo();
                        // Двигаем окно в следующую позицию
                        aperture = MoveAperture(overallAdded, step);
                    }
                }

            }
        }

        /// <summary>
        /// Функция перемещения окна в следующую позицию
        /// </summary>
        /// <param name="_candlesToAdd">Всего свечей, которые были добавлены ранее</param>
        /// <param name="_step">Текущий шаг прохода алгоритма</param>
        /// <returns>Новое окно свечей</returns>
        private List<_CandleStruct> MoveAperture(int _candlesToAdd, int _step)
        {
            Console.WriteLine("[MoveAperture()]");
            aperture.Clear();
            
            int startPosition = (_Constants.NAperture * _step) + _candlesToAdd;
            for (int i = startPosition; i < startPosition + _Constants.NAperture; i++)
            {
                aperture.Add(globalCandles[i]);
            }
            
            return aperture;
        }
    }
}