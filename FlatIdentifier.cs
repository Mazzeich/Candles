using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NLog;

namespace Lua
{
    /// <summary>
    /// Класс, реализующий определение бокового движения в заданном интервале свечей
    /// </summary>
    [SuppressMessage("ReSharper", "CommentTypo")]
    public class FlatIdentifier
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Массив структур свечей
        /// </summary>
        public  List<_CandleStruct> candles = new List<_CandleStruct>();
        /// <summary>
        /// Минимум, его индекс, среднее по лоу
        /// </summary>
        private (double, int, double) lowInfo;
        /// <summary>
        /// Максимум, его индекс, среднее по хай
        /// </summary>
        private (double, int, double) highInfo;
        private double gMin;      // Глобальный минимум
        private double gMax;      // Глобальный максимум 
        private int idxGmin;      // Индекс гМина
        private int idxGmax;      // Индекс гМакса
        private double median;    // Скользящая средняя
        private double k;         // Угловой коэффициент апп. прямой
        private double sdLow;     // СКО по лоу
        private double sdHigh;    // СКО по хай
        private int exsNearSDL;   // Разворотов на уровне СКО-лоу
        private int exsNearSDH;   // Разворотов на уровне СКО-хай
        private Bounds flatBounds;// Границы начала и конца найденного боковика

        public  double flatWidth; // Ширина коридора текущего периода

        public double GMin 
        {
            get => gMin;
            set => this.gMin = value;
        }
        public double GMax
        {
            get => gMax;
            set => this.GMax = value;
        }
        public int IdxGmin
        {
            get => idxGmin;
            set => this.idxGmin = value;
        }
        public int IdxGmax
        {
            get => idxGmax;
            set => this.idxGmax = value;
        }
        public double Median
        {
            get => median;
            set => this.median = value;
        }
        public double K
        {
            get => k;
            set => this.k = value;
        }
        public double SDL
        {
            get => sdLow;
            set => this.sdLow = value;
        }   
        public double SDH
        {
            get => sdHigh;
            set => this.sdHigh = value;
        }
        public int ExsNearSDL
        {
            get => exsNearSDL;
            set => this.exsNearSDL = value;
        }
        public int ExsNearSDH
        {
            get => exsNearSDH;
            set => this.exsNearSDH = value;
        }

        public Bounds FlatBounds
        {
            get => flatBounds;
            set => this.flatBounds = value;
        }

        /// <summary>
        /// Действительно ли мы нашли боковик в заданном окне
        /// </summary>
        public bool IsFlat { get; private set; }

        /// <summary>
        /// Какой тренд имеет текущее окно (-1/0/1 <=> Down/Neutral/Up)
        /// </summary>
        public Enum trend;
        
        public FlatIdentifier(List<_CandleStruct> candles)
        {
            logger.Trace("\n[FlatIdentifier] initialized");
            this.candles  = candles;
            IsFlat = false;
        }

        public void Identify()
        {
            logger.Trace("[Identify] started");
            IsFlat = false;
            
            lowInfo  = GlobalExtremumsAndMedian(false);
            highInfo = GlobalExtremumsAndMedian(true);
            gMin = lowInfo.Item1;
            gMax = highInfo.Item1;
            idxGmin = lowInfo.Item2;
            idxGmax = highInfo.Item2;
            median = (highInfo.Item3 + lowInfo.Item3) * 0.5;
            flatWidth = gMax - gMin;

            k = FindK();

            (double low, double high) = StandartDeviation(median);
            sdLow  = low;
            sdHigh = high;

            exsNearSDL = ExtremumsNearSD(median, sdLow , false);
            exsNearSDH = ExtremumsNearSD(median, sdHigh, true);
            
            if (Math.Abs(k) < _Constants.KOffset)
            {
                trend = Trend.Neutral;
                if ((exsNearSDL > 1) && (exsNearSDH > 1) && (flatWidth > (_Constants.MinWidthCoeff * median)))
                {
                    IsFlat = true;
                }
            } else if (k < 0)
            {
                trend = Trend.Down;
                IsFlat = false;
            }
            else
            {
                trend = Trend.Up;
                IsFlat = false;
            }
            logger.Trace("[Identify] finished");
        }

        /// <summary>
        /// Функция поиска глобальных экстремумов в массиве структур свечей
        /// </summary>
        /// <param name="onHigh">true - ищем по high, false - по low</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        private (double, int, double) GlobalExtremumsAndMedian(bool onHigh)
        {
            logger.Trace("Calculating global extremums and median of current aperture. [onHigh] = {0}", onHigh);
            double globalExtremum;
            double med = 0;
            int index = 0;

            if (onHigh)
            {
                globalExtremum = double.NegativeInfinity;
                for (int i = 0; i < candles.Count; i++)
                {
                    med += candles[i].high;
                    if (globalExtremum < candles[i].high)
                    {
                        globalExtremum = candles[i].high;
                        index = i;
                    }
                }
            }
            else
            {
                globalExtremum = double.PositiveInfinity;
                for (int i = 0; i < candles.Count; i++)
                {
                    med += candles[i].low;
                    if (globalExtremum > candles[i].low)
                    {
                        globalExtremum = candles[i].low;
                        index = i;
                    }
                }
            }
            med /= candles.Count;
            logger.Trace("GEaM found. [onHigh] = {0}", onHigh);

            return (globalExtremum, index, med);
        }

        /// <summary>
        /// Функция поиска угла наклона аппроксимирующей прямой
        /// </summary>
        /// <returns>Угловой коэффициент аппроксимирующей прямой</returns>
        private double FindK()
        {
            logger.Trace("Finding k...");
            double k = 0;
            int n = candles.Count; 

            double sumX = 0;
            double sumY = 0;
            double sumXsquared = 0;
            double sumXY = 0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += candles[i].avg;
                sumXsquared += i * i;
                sumXY += i * candles[i].avg;
            }
            k = ((n * sumXY) - (sumX * sumY)) / ((n * sumXsquared) - (sumX * sumX));
            logger.Trace("k found. k = {0}", k);

            return k;
        }
        
        /// <summary>
        /// Функция поиска угла наклона аппроксимирующей прямой
        /// без учёта последних нескольких свечей (фаза)
        /// Нужно для определения текущего тренда по инструменту
        /// </summary>
        /// <returns>Угловой коэффициент аппроксимирующей прямой</returns>
        private double FindKWithoutPhase()
        {
            logger.Trace("Finding k without phase candles...");
            double k = 0;

            // Не учитывать первые и последние несколько свечей
            int phaseCandlesNum = (int)(candles.Count * _Constants.PhaseCandlesCoeff);
            int n = candles.Count - phaseCandlesNum;

            double sx = 0;
            double sy = 0;
            double sx2 = 0;
            double sxy = 0;

            for (int i = 0; i < n; i++)
            {
                sx += i;
                sy += candles[i].avg;
                sx2 += i * i;
                sxy += i * candles[i].avg;
            }
            k = ((n * sxy) - (sx * sy)) / ((n * sx2) - (sx * sx)); 
            logger.Trace("k found");

            return k;
        }

        /// <summary>
        /// Функция находит среднеквадратическое отклонение свечей тех, что выше среднего, 
        /// и тех, что ниже внутри коридора
        /// </summary>
        /// <param name="_median">Скользящая средняя</param>
        /// <returns></returns>
        private (double, double) StandartDeviation(double _median)
        {
            logger.Trace("Calculation standart deviations in current aperture...");
            double sumLow = 0;
            double sumHigh = 0;

            // Количество low и high, находящихся шире минимальной ширины коридора
            int lowsCount = 0;
            int highsCount = 0;

            for (int i = 0; i < candles.Count - 1; i++)
            {
                if ((candles[i].low) <= (_median - _Constants.KOffset)) // `_median - _Constants.KOffset` ??? 
                {
                    sumLow += Math.Pow(_median - candles[i].low, 2);
                    lowsCount++;
                }
                else if ((candles[i].high) >= (_median + _Constants.KOffset))
                {
                    sumHigh += Math.Pow(candles[i].high - _median, 2);
                    highsCount++;
                }
            }
            double SDLow = Math.Sqrt(sumLow / lowsCount);
            double SDHigh = Math.Sqrt(sumHigh / highsCount);
            logger.Trace("Standart deviations calculated. SDlow = {0} | SDhigh = {1}", _median - SDLow, _median + SDHigh);

            return (_median - SDLow, _median + SDHigh);
        }

        /// <summary>
        /// Функция, подсчитывающая количество экстремумов, находящихся поблизости СКО
        /// </summary>
        /// <param name="_median">Скользящая средняя</param>
        /// <param name="standartDeviation">Среднеквадратическое отклонение</param>
        /// <param name="onHigh">true - ищем по high, false - по low</param>
        /// <returns></returns>
        private int ExtremumsNearSD(double _median, double standartDeviation, bool onHigh)
        {
            logger.Trace("Counting extremums near standart deviations. [onHigh] - {0}", onHigh);
            int extremums = 0;
            double rangeToReachSD = _median * _Constants.SDOffset;

            if (!onHigh)
            {
                //Console.Write("[Попавшие в low индексы]: ");
                for (int i = 2; i < candles.Count - 2; i++) // Кажется, здесь есть проблема индексаций Lua и C#
                {
                    if ((Math.Abs(candles[i].low - standartDeviation) <= (rangeToReachSD)) &&
                        (candles[i].low <= candles[i-1].low) &&
                        (candles[i].low <= candles[i-2].low) &&
                        (candles[i].low <= candles[i+1].low) &&
                        (candles[i].low <= candles[i+2].low))
                    {
                        //Console.Write("{0}({1}) ", cdls[i].low, i + 1);
                        extremums++;
                        _CandleStruct temp;
                        temp = candles[i];
                        temp.low -= 0.01;
                        candles[i] = temp; // Костыль, чтобы следующая(соседняя) свеча более вероятно не подошла
                    }
                }
                //Console.WriteLine("\n[rangeToReachSD] =  {0}", rangeToReachSD);
                //Console.WriteLine("[rangeToReachSD + standartDeviation] = {0}", rangeToReachSD + standartDeviation);
            }
            else
            {
                //Console.Write("[Попавшие в high индексы]: ");
                for (int i = 2; i < candles.Count - 2; i++)
                {
                    if ((Math.Abs(candles[i].high - standartDeviation) <= (rangeToReachSD)) &&
                        (candles[i].high >= candles[i-1].high) &&
                        (candles[i].high >= candles[i-2].high) &&
                        (candles[i].high >= candles[i+1].high) &&
                        (candles[i].high >= candles[i+2].high))
                    {
                        //Console.Write("{0}({1}) ", cdls[i].high, i + 1);
                        extremums++;
                        _CandleStruct temp;
                        temp = candles[i];
                        temp.high += 0.01;
                        candles[i] = temp;
                    }
                }
                
                //Console.WriteLine("\n[rangeToReachSD] =  {0}", rangeToReachSD);
                //Console.WriteLine("[rangeToReachSD + standartDeviation] = {0}", rangeToReachSD + standartDeviation);
            }
            logger.Trace("Extremums near SD = {0} | [onHigh] = {1}", extremums, onHigh);

            return extremums;
        }

        public Bounds SetBounds(_CandleStruct left, _CandleStruct right)
        {
            logger.Trace("Setting bounds...");
            flatBounds.left = left;
            flatBounds.right = right;
            logger.Trace("Bounds set");
            return FlatBounds;
        }
    }
}