// -------------------------------------------------------------------------------------------------
//
//    This code is a cAlgo API sample.
//
//    This cBot is intended to be used as a sample and does not guarantee any particular outcome or
//    profit of any kind. Use it at your own risk.
//
//    The "Sample Trend cBot" will buy when fast period moving average crosses the slow period moving average and sell when 
//    the fast period moving average crosses the slow period moving average. The orders are closed when an opposite signal 
//    is generated. There can only by one Buy or Sell order at any time.
//
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SampleTrendcBot : Robot
    {
        [Parameter("MA Type", DefaultValue = "Exponential")]
        public MovingAverageType MAType { get; set; }

        [Parameter()]
        public DataSeries SourceSeries { get; set; }

        [Parameter("Slow MA", DefaultValue = 72)]
        public int SlowPeriods { get; set; }

        [Parameter("Fast MA", DefaultValue = 17)]
        public int FastPeriods { get; set; }

        [Parameter("Quantity (Lots)", DefaultValue = 10000, MinValue = 10000, Step = 10000)]
        public double Qtty { get; set; }

        [Parameter("Stop Loss", DefaultValue = 20)]
        public double SL { get; set; }

        [Parameter("Take Profit", DefaultValue = 30)]
        public double TP { get; set; }

        [Parameter("VolPartialClose", DefaultValue = 0.5)]
        public double VolPartialClose { get; set; }

        [Parameter("PipsPartialClose", DefaultValue = 0.5)]
        public double PipsPartialClose { get; set; }

        private MovingAverage slowMa;
        private MovingAverage fastMa;
        private const string label = "goldmine";
        private int trigger;

        //Inicializa os indicadores quando aciona o robo
        protected override void OnStart()
        {
            fastMa = Indicators.MovingAverage(SourceSeries, FastPeriods, MAType);
            slowMa = Indicators.MovingAverage(SourceSeries, SlowPeriods, MAType);

            trigger = 0;
        }

        //Para cada nova barra executa essa rotina
        protected override void OnBar()
        {
            var longPosition = Positions.Find(label, Symbol.Name, TradeType.Buy);
            var shortPosition = Positions.Find(label, Symbol.Name, TradeType.Sell);

            var currentSlowMa = slowMa.Result.Last(1);
            var currentFastMa = fastMa.Result.Last(1);
            var previousSlowMa = slowMa.Result.Last(2);
            var previousFastMa = fastMa.Result.Last(2);


            if (previousSlowMa > previousFastMa && currentSlowMa <= currentFastMa)
            {
                trigger = 1;
                Print("Trigger de compra: " + Bars.LowPrices.Last(1));
            }

            if (previousSlowMa < previousFastMa && currentSlowMa >= currentFastMa)
            {
                trigger = 2;
                Print("Trigger de venda: " + Bars.HighPrices.Last(1));
            }


            if (currentFastMa > currentSlowMa && trigger == 1 && Bars.ClosePrices.Last(0) <= previousSlowMa && longPosition == null && shortPosition == null)
            {
                var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, Qtty, label, SL, TP);
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    Print("Comprado em: {0}", position.EntryPrice);
                }
                trigger = 0;
            }


            if (currentFastMa < currentSlowMa && trigger == 2 && Bars.ClosePrices.Last(0) >= previousSlowMa && shortPosition == null && longPosition == null)
            {
                var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, Qtty, label, SL, TP);
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    Print("Vendido em: {0}", position.EntryPrice);
                }
                trigger = 0;
            }

        }

        //Para cada atualização de tick executa essa rotina
        protected override void OnTick()
        {
            var longPosition1 = Positions.Find(label, Symbol.Name, TradeType.Buy);
            var shortPosition1 = Positions.Find(label, Symbol.Name, TradeType.Sell);


            if (longPosition1 != null && longPosition1.Pips > TP * PipsPartialClose && longPosition1.Quantity == (Qtty / 100000))
            {
                ClosePosition(longPosition1, Qtty * VolPartialClose);
                var stopLoss = longPosition1.EntryPrice + 1 * Symbol.PipSize;
                ModifyPosition(longPosition1, stopLoss, longPosition1.TakeProfit);
                Print("First TP hit, SL locked");
            }


            if (shortPosition1 != null && shortPosition1.Pips > TP * PipsPartialClose && shortPosition1.Quantity == (Qtty / 100000))
            {
                ClosePosition(shortPosition1, Qtty * VolPartialClose);
                var stopLoss = shortPosition1.EntryPrice - 1 * Symbol.PipSize;
                ModifyPosition(shortPosition1, stopLoss, shortPosition1.TakeProfit);
                Print("First TP hit, SL locked");
            }

        }
    }
}
