using System;
using System.Collections.Generic;
using NLog;
using NUnit.Framework;

// ReSharper disable CommentTypo

namespace FlatTraderBot
{
    public class HistoricalFlatFinder
    {
        private HistoricalFlatFinder()
        {
            logger.Trace("\n[HistoricalFlatFinder] initialized");
        }

        public HistoricalFlatFinder(List<_CandleStruct> candles) : this()
        {
            globalCandles = candles;

            for (int i = 1; i < _Constants.NAperture; i++) // Формируем стартовое окно
            {
                aperture.Add(globalCandles[i]);
            }
        }

        /// <summary>
        /// Основная функция, выполняющая поиск всех боковиков в глобальном списке свечей
        /// </summary>
        public void FindAllFlats()
        {
            // Как правило, globalIterator хранит в себе индекс начала окна во всём датасете

            for (int globalIterator = 0; globalIterator < globalCandles.Count - _Constants.NAperture - 1;)
            {
                FlatIdentifier flatIdentifier = new FlatIdentifier();
                flatIdentifier.AssignAperture(aperture);
                flatIdentifier.Identify(); // Определяем начальное окно
                
                // Если не определили боковик сходу
                if (!flatIdentifier.isFlat)
                {
                    Printer printer = new Printer(flatIdentifier);
                    printer.PrintReasonsApertureIsNotFlat();
                    globalIterator++;
                    MoveAperture(ref globalIterator);
                    continue;
                }

                while (flatIdentifier.isFlat)
                {
                    // ExpansionRate раз...
                    for (int j = 0; j < _Constants.ExpansionRate; j++)
                    {
                        try
                        {
                            // ... расширяем окно на 1 свечу
                            ExtendAperture(globalIterator, ref aperture);
                        }
                        catch (Exception exception)
                        {
                            logger.Trace(exception);
                            return;
                        }
                    }
                    flatIdentifier.AssignAperture(aperture);
                    flatIdentifier.Identify(); // Identify() вызывает SetBounds() сам

                    if (flatIdentifier.isFlat) 
                        continue; // Райдер предложил
                    
                    Printer printer = new Printer(flatIdentifier);
                    printer.PrintReasonsApertureIsNotFlat();
                    flatList.Add(flatIdentifier);
                    flatsFound++;
                    logger.Trace("Боковик определён в [{0}] с [{1}] по [{2}]", 
                        flatIdentifier.flatBounds.left.date, 
                        flatIdentifier.flatBounds.left.time,
                        flatIdentifier.flatBounds.right.time);

                    globalIterator += aperture.Count; // Переместить i на следующую после найденного окна свечу

                    try
                    {
                        MoveAperture(ref globalIterator); // Записать в окно новый лист с i-го по (i + _Constants.NAperture)-й в aperture
                    }
                    catch (ArgumentOutOfRangeException exception)
                    {
                        logger.Warn("Argument out of range");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Перемещает окно в следующую позицию (переинициализирует в следующем интервале)
        /// </summary>
        /// <param name="i">Начальный индекс, с которого будет начинать новое окно на + _Constants.NAperture</param>
        private void MoveAperture(ref int i)
        {
            aperture.Clear();
            // Если первая и последняя свечи будущего окна находятся в пределах одного дня
            if (globalCandles[i].date == globalCandles[i + _Constants.NAperture].date)
            {
                for (int j = i; j < i + _Constants.NAperture; j++)
                {
                    aperture.Add(globalCandles[j]);
                }
            }
            else
            {
                logger.Trace("Начало и конец предполагаемого окна находятся в разных днях.");
                int indexOfTheNextDay = 0;
                // Находим начало следующего дня, где дата свечи не совпадает с датой свечи самого начала окна
                for (int j = i + 1; j < i + _Constants.NAperture; j++)
                {
                    indexOfTheNextDay = globalCandles[j].index;

                    if (globalCandles[j].date != globalCandles[i].date)
                    {
                        break;
                    }
                }

                i = indexOfTheNextDay + 1;

                for (int j = i; j < i + _Constants.NAperture; j++)
                {
                    aperture.Add(globalCandles[j]);
                }
            }
        }

        /// <summary>
        /// Расширяет окно на 1 свечу
        /// </summary>
        /// <param name="i">Начальный индекс, к которому добавить (aperture.Count + 1)</param>
        private void ExtendAperture(int i, ref List<_CandleStruct> _aperture)
        {
            int indexOfAddingCandle = i + _aperture.Count + 1;
            _aperture.Add(globalCandles[indexOfAddingCandle]);
        }

        /// <summary>
        /// Функция склеивает находящиеся близко друг к другу боковики
        /// </summary>
        public void UniteFlats()
        {
            for (int i = 1; i < flatsFound; i++)
            {
                FlatIdentifier currentFlat = flatList[i];
                FlatIdentifier prevFlat = flatList[i-1];

                // ЕСЛИ левая граница предыдущего и левая граница текущего находятся в пределах одного дня
                // И ЕСЛИ разница в свечах между левой границей текущего и правой границей предыдущего меьше ГАПА
                // И ЕСЛИ разница в цене между мат. ожиданиями текущего и предыдущего <= ОФФСЕТ * среднее между мат. ожиданиями обоих боковиков
                if (currentFlat.flatBounds.left.date == prevFlat.flatBounds.left.date &&
                    currentFlat.flatBounds.left.index - prevFlat.flatBounds.right.index <= _Constants.MinFlatGap &&
                    Math.Abs(currentFlat.mean - prevFlat.mean) <= _Constants.flatsMeanOffset * ((currentFlat.mean + prevFlat.mean) * 0.5))
                {
                    logger.Trace("{0}: Uniting [{1}] [{2}] and [{3}] [{4}]",
                        prevFlat.candles[0].date,
                        prevFlat.flatBounds.left.time,
                        prevFlat.flatBounds.right.time,
                        currentFlat.flatBounds.left.time,
                        currentFlat.flatBounds.right.time);
                    
                    List<_CandleStruct> newAperture = new List<_CandleStruct>(currentFlat.flatBounds.right.index - prevFlat.flatBounds.left.index);
                    for (int j = prevFlat.flatBounds.left.index; j <= currentFlat.flatBounds.right.index; j++)
                    {
                        newAperture.Add(globalCandles[j]);
                    }
                    FlatIdentifier newFlat = new FlatIdentifier();
                    newFlat.AssignAperture(newAperture);
                    newFlat.CalculateFlatProperties();
                    newFlat.flatBounds = newFlat.SetBounds(newFlat.candles[0], newFlat.candles[^1]);
                    newFlat.SetBounds(newFlat.candles[0], newFlat.candles[^1]);
                    
                    flatList.RemoveRange(i-1, 2);
                    flatList.Insert(i-1, newFlat);
                    flatsFound--;
                    i++;
                }
            }
        }
        
        /// <summary>
        /// Инициализация логгера
        /// </summary>
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Основной, глобальный список свечей
        /// </summary>
        private readonly List<_CandleStruct> globalCandles;
        /// <summary>
        /// Маленький список свечей, формирующий окно
        /// </summary>
        private List<_CandleStruct> aperture = new List<_CandleStruct>(_Constants.NAperture);
        /// <summary>
        /// Сколько боковиков было найдено
        /// </summary>
        public int flatsFound { get; private set; }
        /// <summary>
        /// Список всех найденных боковиков
        /// </summary>
        public readonly List<FlatIdentifier> flatList = new List<FlatIdentifier>();
    }
}