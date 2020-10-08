using System;
using System.Collections.Generic;
using NLog;

// ReSharper disable CommentTypo

namespace Lua
{
    public class HistoricalFlatFinder
    {
        /// <summary>
        /// Инициализация логгера
        /// </summary>
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Основной, глобальный список свечей
        /// </summary>
        private readonly List<_CandleStruct> globalCandles;
        
        private List<_CandleStruct> aperture = new List<_CandleStruct>(_Constants.NAperture);

        /// <summary>
        /// Сколько боковиков было найдено
        /// </summary>
        public int flatsFound { get; private set; }
        
        [Obsolete("The field used to contain bounds of all founded flats")]
        public List<_Bounds> flatsBounds { get; private set; } = new List<_Bounds>();

        /// <summary>
        /// Список всех найденных боковиков
        /// </summary>
        public List<FlatIdentifier> flats { get; private set; } = new List<FlatIdentifier>();

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

        public void FindAllFlats()
        {
            // Как правило, globalIterator хранит в себе индекс начала окна во всём датасете
            for (int globalIterator = 0; globalIterator < globalCandles.Count;) 
            {
                if (globalIterator + _Constants.NAperture > globalCandles.Count - 1)
                    return;

                FlatIdentifier flatIdentifier = new FlatIdentifier(ref aperture);
                flatIdentifier.Identify(); // Определяем начальное окно
                
                // Если не определили боковик сходу
                if (!flatIdentifier.isFlat)
                {
                    Printer printer = new Printer(flatIdentifier);
                    printer.ReasonsApertureIsNotFlat();
                    globalIterator++;
                    MoveAperture(globalIterator);
                    continue;
                }

                while (flatIdentifier.isFlat)
                {
                    for (int j = 0; j < _Constants.ExpansionRate; j++)
                    {
                        try
                        {
                            ExpandAperture(globalIterator);
                        }
                        catch (Exception exception)
                        {
                            logger.Trace(exception);
                            return;
                        }
                    }
                    
                    flatIdentifier.Identify(); // Identify() вызывает SetBounds() сам

                    if (flatIdentifier.isFlat) 
                        continue; // Райдер предложил
                    
                    Printer printer = new Printer(flatIdentifier);
                    printer.ReasonsApertureIsNotFlat();
                    //flatsBounds.Add(flatIdentifier.flatBounds);
                    flats.Add(flatIdentifier);
                    flatsFound++;
                    logger.Trace("Боковик определён в [{0}] с [{1}] по [{2}]", 
                        flatIdentifier.flatBounds.leftBound.date, 
                        flatIdentifier.flatBounds.leftBound.time,
                        flatIdentifier.flatBounds.rightBound.time);
                    
                    
                    globalIterator += aperture.Count; // Переместить i на следующую после найденного окна свечу

                    try
                    {
                        MoveAperture(globalIterator); // Записать в окно новый лист с i-го по (i + _Constants.NAperture)-й в aperture
                    }
                    catch (Exception exception)
                    {
                        logger.Trace(exception);
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// Перемещает окно в следующую позицию (переинициализирует в следующем интервале)
        /// </summary>
        /// <param name="i">Начальный индекс, с которого будет начинать новое окно на + _Constants.NAperture</param>
        private void MoveAperture(int i)
        {
            logger.Trace("[MoveAperture()]");
            
            aperture.Clear();
            for (int j = i; j < i + _Constants.NAperture; j++)
            {
                aperture.Add(globalCandles[j]);
            }
        }

        /// <summary>
        /// Расширяет окно на 1 свечу
        /// </summary>
        /// <param name="i">Начальный индекс, к которому добавить (aperture.Count + 1)</param>
        private void ExpandAperture(int i)
        {
            int indexOfAddingCandle = i + aperture.Count + 1;
            aperture.Add(globalCandles[indexOfAddingCandle]);
            logger.Trace("Aperture expanded...\t[aperture.Count] = {0}", aperture.Count);
        }

        /// <summary>
        /// Функция склеивает находящиеся близко друг к другу боковики
        /// </summary>
        /// <param name="_flats">Список границ всех найденных боковиков</param>
        private List<FlatIdentifier> UniteFlats(List<FlatIdentifier> _flats)
        {
            logger.Trace("Uniting flats...");
            
            

            return _flats;
        }
    }
}