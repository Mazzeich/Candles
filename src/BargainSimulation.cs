﻿using System;
using System.Collections.Generic;
using NLog;
using NLog.Fluent;

namespace FlatTraderBot
{
	public class BargainSimulation
	{
		private BargainSimulation() { }

		public BargainSimulation(List<_CandleStruct> candles, ref List<FlatIdentifier> flatList) : this()
		{
			globalCandles = candles;
			this.flatList = flatList;
			flatsOverall = flatList.Count;
			balanceAccount = 100000;
		}

		public void StartBargainSimulation()
		{
			logger.Trace($"\n[balanceAccount] = {balanceAccount} рублей");
			for (int i = 0; i < flatsOverall - 1;)
			{
				Direction leavingDirection = flatList[i].leavingDirection;
				switch (leavingDirection)
				{
					case Direction.Down:
					{
						ShortDeal(ref i, leavingDirection);
						break;
					}
					case Direction.Up:
					{
						LongDeal(ref i, openDirection);
						logger.Trace($"После лонга [balanceAccount] = {balanceAccount} рублей");
						break;
					}
					case Direction.Neutral:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			int commitedDeals = profitDeals + lossDeals;
			double profitPercentage = profitDeals * 100 / commitedDeals;
			double lossPercentage 	= lossDeals * 100 / commitedDeals;
			logger.Trace($"[DEALS]: {profitDeals}/{lossDeals} <=> {profitPercentage}%/{lossPercentage}%");
			logger.Trace($"Most profitable deal [{mostProfitDeal.OpenDealCandle.date} {mostProfitDeal.OpenDealCandle.time} " +
			             $"- {mostProfitDeal.CloseDealCandle.date} {mostProfitDeal.CloseDealCandle.time}] = {mostProfitDeal.delta} рублей");
			logger.Trace($"Least profitable deal [{leastProfitDeal.OpenDealCandle.date} {leastProfitDeal.OpenDealCandle.time} " +
			             $"- {leastProfitDeal.CloseDealCandle.date} {leastProfitDeal.CloseDealCandle.time}] = {leastProfitDeal.delta} рублей");
			logger.Trace($"[balance] = {balanceAccount} рублей");
		}

		private void ShortDeal(ref int currentFlatIndex, Direction openDirection)
		{
			SellOnPrice(flatList[currentFlatIndex].closingCandle.close);
			LogOperation(flatList[currentFlatIndex], "Продажа на шорт");
			Direction newDirection = flatList[currentFlatIndex + 1].closingDirection;
			while (newDirection == openDirection && currentFlatIndex + 1 < flatsOverall - 1)
			{
				currentFlatIndex++;
				newDirection = flatList[currentFlatIndex + 1].leavingDirection;
			}
			BuyOnPrice(flatList[currentFlatIndex + 1].closingCandle.close);
			LogOperation(flatList[currentFlatIndex], "Закрытие шорта");
			currentFlatIndex++;
		}

		private void LongDeal(ref int currentFlatIndex, Direction openDirection)
		{
			BuyOnPrice(flatList[currentFlatIndex].closingCandle.close);
			LogOperation(flatList[currentFlatIndex], "Покупка на лонг");
			Direction newDirection = flatList[currentFlatIndex + 1].leavingDirection;
			while (newDirection == openDirection && currentFlatIndex + 1 < flatsOverall - 1)
			{
				currentFlatIndex++;
				newDirection = flatList[currentFlatIndex + 1].leavingDirection;
			}
			SellOnPrice(flatList[currentFlatIndex+1].closingCandle.close);
			LogOperation(flatList[currentFlatIndex], "Закрытие лонга");
			currentFlatIndex++;
		}

		private void SellOnPrice(double price)
		{
			try
			{
				balanceAccount += price;
			}
			catch (Exception exception)
			{
				logger.Trace(exception);
				throw;
			}
		}

		private void BuyOnPrice(double price)
		{
			try
			{
				balanceAccount -= price;
			}
			catch (Exception exception)
			{
				logger.Trace(exception);
				throw;
			}
		}

		/// <summary>
		/// Вывод границ текущего флета
		/// </summary>
		/// <param name="flat"></param>
		/// <param name="operation"></param>
		private void LogOperation(FlatIdentifier flat, string operation)
		{
			logger.Trace($"[{flat.flatBounds.left.date}] [{flat.flatBounds.left.time} {flat.flatBounds.right.time}]: " +
			             $"{operation} по {flat.leavingCandle.close} в {flat.leavingCandle.time} [balance] = {balanceAccount}");
		}

		private readonly Logger logger = LogManager.GetCurrentClassLogger();
		private readonly List<_CandleStruct> globalCandles;
		private readonly List<FlatIdentifier> flatList;
		private readonly int flatsOverall;
		private double balanceAccount;
	}
}