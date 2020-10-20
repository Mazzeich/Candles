using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NLog;

namespace FlatTraderBot
{
    /// <summary>
    /// Класс, реализующий определение бокового движения в заданном интервале свечей
    /// </summary>
    public class FlatIdentifier
    {
        public FlatIdentifier()
        {
            logger.Trace("\n[FlatIdentifier] initialized");
            isFlat = false;
        }

        /// <summary>
        /// Функция устанавливает поле candles
        /// </summary>
        /// <param name="aperture">Рассматриваемое окно</param>
        public void AssignAperture(List<_CandleStruct> aperture)
        {
            candles = new List<_CandleStruct>(aperture);
        }

        public void Identify()
        {
            logger.Trace("[Identify] started");
            logger.Trace("[{0}]: Окно с {1} по {2}", candles[0].date, candles[0].time, candles[^1].time);
            
            CalculateFlatProperties();
            
            if (Math.Abs(k) < _Constants.KOffset)
            {
                trend = Trend.Neutral;
                
                bool isEnoughExtremumsNearSDL = exsNearSDL > _Constants.MinExtremumsNearSD;
                bool isEnoughExtremumsNearSDH = exsNearSDH > _Constants.MinExtremumsNearSD;
                bool isEnoughFlatWidth        = flatWidth  > _Constants.MinWidthCoeff * candles[^1].close;
                
                if (isEnoughExtremumsNearSDL && isEnoughExtremumsNearSDH && isEnoughFlatWidth)
                {
                    isFlat = true;
                    flatBounds = SetBounds(candles[0], candles[^1]);
                }
                else
                {
                    reasonsOfApertureHasNoFlat = ReasonsWhyIsNotFlat();
                }
            } 
            else if (k < 0)
            {
                trend = Trend.Down;
                CutAperture();
            }
            else
            {
                trend = Trend.Up;
                CutAperture();
            }

            logger.Trace("isFlat = {0}\n[Identify] finished\n------------------------------------", isFlat);
        }

        /// <summary>
        /// Функция вычислет все поля класса
        /// </summary>
        public void CalculateFlatProperties()
        {
            logger.Trace("Calculating flat properties...");
            
            gMin = GetGlobalMinimum(candles);
            gMax = GetGlobalMaximum(candles);
            mean = GetMean(candles);
            flatWidth = gMax - gMin;
            SDMean = GetStandartDeviationMean(candles);
            SDL = mean - SDMean;
            SDH = mean + SDMean;
            k = FindK(candles);
            exsNearSDL = EstimateExtremumsNearSDL(candles);
            exsNearSDH = EstimateExtremumsNearSDH(candles);
            
            LogFlatProperties();
        }

        /// <summary>
        /// Функция находит глобальный минимум окна
        /// </summary>
        /// <param name="candleStructs">Список свечей окна</param>
        private double GetGlobalMinimum(IReadOnlyList<_CandleStruct> candleStructs)
        {
            double result = double.PositiveInfinity;
            for (int i = 0; i < candleStructs.Count; i++)
            {
                if (result > candleStructs[i].low)
                {
                    result = candleStructs[i].low;
                    idxGmin = i;
                }
            }

            return result;
        }

        /// <summary>
        /// Функция находит глобальный максимум окна
        /// </summary>
        /// <param name="candleStructs">Список свечей окна</param>
        private double GetGlobalMaximum(IReadOnlyList<_CandleStruct> candleStructs)
        {
            double result = double.NegativeInfinity;
            for (int i = 0; i < candleStructs.Count; i++)
            {
                if (result < candleStructs[i].high)
                {
                    result = candleStructs[i].high;
                    idxGmax = i;
                }
            }

            return result;
        }

        /// <summary>
        /// Функция находит мат. ожидание окна
        /// </summary>
        /// <param name="candleStructs">Список свечей окна</param>
        private double GetMean(IReadOnlyList<_CandleStruct> candleStructs)
        {
            double result = 0;
            for (int i = 0; i < candleStructs.Count; i++)
            {
                result += candleStructs[i].avg;
            }
            result /= candleStructs.Count;
            return result;
        }

        /// <summary>
        /// Функция поиска угла наклона аппроксимирующей прямой
        /// </summary>
        /// <returns>Угловой коэффициент аппроксимирующей прямой</returns>
        private double FindK(List<_CandleStruct> candleStructs)
        {
            // https://prog-cpp.ru/mnk/
            k = 0;
            int n = candleStructs.Count; 

            double sumX = 0;
            double sumY = 0;
            double sumXsquared = 0;
            double sumXY = 0;
            
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += candleStructs[i].avg;
                sumXsquared += i * i;
                sumXY += i * candleStructs[i].avg;
            }
            // Точка пересечения с осью ординат

            double result = ((n * sumXY) - (sumX * sumY)) / ((n * sumXsquared) - (sumX * sumX));
            double b = (sumY - result * sumX)/n;
            
            return result;
        }

        /// <summary>
        /// Функция вычисляет СКО окна по avg всех свечей
        /// </summary>
        /// <returns></returns>
        private double GetStandartDeviationMean(IReadOnlyList<_CandleStruct> candleStructs)
        {
            double sumMean = 0;
            for (int i = 0; i < candleStructs.Count - 1; i++)
            {
                sumMean += Math.Pow(mean - candleStructs[i].avg, 2);
            }
            double result = Math.Sqrt(sumMean / candleStructs.Count);
            return result;
        }

        /// <summary>
        /// Функция находит среднеквадратическое отклонение свечей тех, что ниже среднего, 
        /// и тех, что выше внутри коридора
        /// </summary>
        /// <returns>double SDLow, double SDHigh</returns>
        [Obsolete("Method was used to calculate standart deviations way extreme than it should have been")]
        private (double, double) GetStandartDeviations()
        {
            double sumLow = 0;
            double sumHigh = 0;

            int lowsCount = 0;
            int highsCount = 0;

            for (int i = 0; i < candles.Count - 1; i++)
            {
                if ((candles[i].low) <= mean) 
                {
                    sumLow += Math.Pow(mean - candles[i].low, 2);
                    lowsCount++;
                }
                else if ((candles[i].high) >= mean)
                {
                    sumHigh += Math.Pow(candles[i].high - mean, 2);
                    highsCount++; 
                }
            }
            double SDLow = Math.Sqrt(sumLow / lowsCount);
            double SDHigh = Math.Sqrt(sumHigh / highsCount);
            logger.Trace("Standart deviations calculated. [mean] - [SDL] = {0} | [mean] + [SDH] = {1}", mean - SDLow, mean + SDHigh);

            return (mean - SDLow, mean + SDHigh);
        }

        /// <summary>
        /// Функция, подсчитывающая количество экстремумов, находящихся поблизости СКО по лоу
        /// </summary>
        /// <param name="candleStructs"></param>
        /// <returns>Количество экстремумов возле СКО лоу</returns>
        private int EstimateExtremumsNearSDL(List<_CandleStruct> candleStructs)
        {
            int result = 0;
            double distanceToSD = mean * _Constants.SDOffset;
            logger.Trace("Near [SDL]: ");
            for (int i = 2; i < candleStructs.Count - 2; i++)
            {
                if (Math.Abs(candles[i].low - SDL) <= distanceToSD &&
                    candleStructs[i].low <= candleStructs[i-1].low && candleStructs[i].low <= candleStructs[i-2].low &&
                    candleStructs[i].low <= candleStructs[i+1].low && candleStructs[i].low <= candleStructs[i+2].low)
                {
                    logger.Trace(candleStructs[i].time);
                    result++;
                    _CandleStruct temp = candleStructs[i];
                    temp.low -= 0.01;
                    candleStructs[i] = temp; // Костыль, чтобы следующая(соседняя) свеча более вероятно не подошла
                }
            }

            logger.Trace("[distanceToSD] = {0}", distanceToSD);
            logger.Trace("[SDL] offset = {0}|{1}", SDL - distanceToSD, SDL = distanceToSD);
            return result;
        }
        
        /// <summary>
        /// Функция, подсчитывающая количество экстремумов, находящихся поблизости СКО по хай
        /// </summary>
        /// <param name="candleStructs"></param>
        /// <returns>Количество экстремумов возле СКО хай</returns>
        private int EstimateExtremumsNearSDH(List<_CandleStruct> candleStructs)
        {
            int result = 0;
            double distanceToSD = mean * _Constants.SDOffset;
            logger.Trace("Near [SDH]: ");
            for (int i = 2; i < candleStructs.Count - 2; i++)
            {
                if (Math.Abs(candleStructs[i].high - SDH) <= distanceToSD &&
                    candleStructs[i].high >= candleStructs[i-1].high && candleStructs[i].high >= candleStructs[i-2].high &&
                    candleStructs[i].high >= candleStructs[i+1].high && candleStructs[i].high >= candleStructs[i+2].high)
                {
                    logger.Trace(candleStructs[i].time);
                    result++;
                    _CandleStruct temp = candleStructs[i];
                    temp.high += 0.01;
                    candleStructs[i] = temp;
                }
            }

            logger.Trace("[distanceToSD] = {0}", distanceToSD);
            logger.Trace("[SDH] offset = {0}|{1}", SDH - distanceToSD, SDH = distanceToSD);
            return result;
        }

        /// <summary>
        /// Логгирует все поля объекта
        /// </summary>
        private void LogFlatProperties()
        {
            logger.Trace("[gMin] = {0} [gMax] = {1} [mean] = {2}", gMin, gMax, mean);
            logger.Trace("[Standart Deviation on mean] = {0}", SDMean);
            logger.Trace("[flatWidth] = {0}", flatWidth);
            logger.Trace("[k] = {0}", k);
            logger.Trace("Extremums near SDL = {0}\tExtremums near SDH = {1}", exsNearSDL, exsNearSDH);
        }

        /// <summary>
        /// Функция устанавливает поле flatBounds
        /// </summary>
        /// <param name="left">Левая граница боковика (свеча)</param>
        /// <param name="right">Правая граница боковика (свеча)</param>
        /// <returns></returns>
        public _Bounds SetBounds(_CandleStruct left, _CandleStruct right)
        {
            _Bounds result = flatBounds;
            result.left = left;
            result.right = right;
            return result;
        }
        
        /// <summary>
        /// Функция для отладки, позволяющая вывести возможные причины отсутстивия нужного бокового движения 
        /// </summary>
        /// <returns>Строка, содержащая возможные причины</returns>
        private string ReasonsWhyIsNotFlat()
        {
            string result = "";

            if ((flatWidth) < (_Constants.MinWidthCoeff * mean))
            {
                result += "Недостаточная ширина коридора. ";
            }
            if (exsNearSDL < _Constants.MinExtremumsNearSD)
            {
                result += "Недостаточно вершин снизу возле СКО.  ";
            }
            if (exsNearSDH < _Constants.MinExtremumsNearSD)
            {
                result += "Недостаточно вершин сверху возле СКО. ";
            }
            
            switch (trend)
            {
                case Trend.Down:
                {
                    result += "Нисходящий тренд. ";
                    break;
                }
                case Trend.Up:
                {
                    result += "Восходящий тренд. ";
                    break;
                }
                case Trend.Neutral:
                {
                    break;
                }
            }
            return result;
        }
        
        /// <summary>
        /// Функция подрезает окн после того, как боковик не определился
        /// </summary>
        private void CutAperture()
        {
            isFlat = false;
            candles.RemoveRange(candles.Count - _Constants.ExpansionRate, _Constants.ExpansionRate);
            reasonsOfApertureHasNoFlat = ReasonsWhyIsNotFlat();
        }

        /// <summary>
        /// Логгер в logFlatIdentifier.txt
        /// </summary>
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Массив структур свечей
        /// </summary>
        public List<_CandleStruct> candles { get; private set; }
        /// <summary>
        /// Границы начала и конца найденного боковика
        /// </summary>
        public _Bounds flatBounds { get;  set; }
        /// <summary>
        /// Ширина текущего коридора
        /// </summary>
        public  double flatWidth { get; private set; }
        /// <summary>
        /// Глобальный минимум в боковике
        /// </summary>
        public double gMin { get; private set; }
        /// <summary>
        /// Глобальный максимум в боковике
        /// </summary>
        public double gMax { get; private set; }
        /// <summary>
        /// Индекс глобального минимума относительно окна
        /// </summary>
        public int idxGmin { get; private set; }
        /// <summary>
        /// Индекс глобального максимума относительно окна
        /// </summary>
        public int idxGmax { get; private set; }
        /// <summary>
        /// Значение математического ожидания в боковике
        /// </summary>
        public double mean { get; private set; }
        /// <summary>
        /// Угловой коэффициент аппроксимирущей прямой
        /// </summary>
        public double k { get; private set; }
        /// <summary>
        /// Среднеквадратическое отклонение снизу
        /// </summary>
        public double SDL { get; private set; }
        /// <summary>
        /// Среднеквадратическое отклонение сверху
        /// </summary>
        public double SDH { get; private set; }
        /// <summary>
        /// Среднеквадратическое отклонение в боковике
        /// </summary>
        public double SDMean { get; private set; }
        /// <summary>
        /// Количество экстремумов возле среднеквадратического отклонения снизу
        /// </summary>
        public int exsNearSDL { get; private set; }
        /// <summary>
        /// Количество экстремумов возле среднеквадратического отклонения сверху
        /// </summary>
        public int exsNearSDH { get; private set; }
        /// <summary>
        /// Действительно ли мы нашли боковик в заданном окне
        /// </summary>
        public bool isFlat { get; private set; }
        /// <summary>
        /// Какой тренд имеет текущее окно (-1/0/1 <=> Down/Neutral/Up)
        /// </summary>
        public Trend trend;
        /// <summary>
        /// Возможные причины того, что в текущем объекте не обнаружился нужный боковик
        /// </summary>
        public string reasonsOfApertureHasNoFlat { get; private set; }
        /// <summary>
        /// Снизу или сверху сформировался боковик
        /// </summary>
        public FormedFrom formedFrom { get; set; }
    }
}