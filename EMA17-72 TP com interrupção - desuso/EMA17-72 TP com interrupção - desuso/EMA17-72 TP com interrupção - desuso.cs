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

        [Parameter("Slow MA", DefaultValue = 12)]
        public int SlowPeriods { get; set; }

        [Parameter("Fast MA", DefaultValue = 24)]
        public int FastPeriods { get; set; }

        [Parameter("Quantity (Lots)", DefaultValue = 10000, MinValue = 10000, Step = 1000)]
        public int Qtty { get; set; }

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
        private int trigger;
        private const string label = "goldmine";

        //Inicializa os indicadores quando aciona o robo
        protected override void OnStart()
        {
            fastMa = Indicators.MovingAverage(SourceSeries, FastPeriods, MAType);
            slowMa = Indicators.MovingAverage(SourceSeries, SlowPeriods, MAType);

            Positions.Closed += PositionsOnClosed;

            trigger = 0;
        }

        //Para cada nova barra executa essa rotina
        protected override void OnBar()
        {
            var longPosition = Positions.Find(label, Symbol.Name, TradeType.Buy);
            var shortPosition = Positions.Find(label, Symbol.Name, TradeType.Sell);

            var currentSlowMa = slowMa.Result.Last(0);
            var currentFastMa = fastMa.Result.Last(0);
            var previousSlowMa = slowMa.Result.Last(1);
            var previousFastMa = fastMa.Result.Last(1);

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


            if (currentFastMa > currentSlowMa && trigger == 1 && Bars.LowPrices.Last(1) <= previousSlowMa && longPosition == null && shortPosition == null)
            {
                var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, Qtty * VolPartialClose, "leftover", SL, TP);
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    Print("Comprado em: {0}", position.EntryPrice);
                }
                result = ExecuteMarketOrder(TradeType.Buy, SymbolName, Qtty * VolPartialClose, label, SL, TP * PipsPartialClose);
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    Print("Comprado em: {0}", position.EntryPrice);
                }
                trigger = 0;
            }


            if (currentFastMa < currentSlowMa && trigger == 2 && Bars.HighPrices.Last(1) >= previousSlowMa && shortPosition == null && longPosition == null)
            {
                var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, Qtty * VolPartialClose, "leftover", SL, TP);
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    Print("Vendido em: {0}", position.EntryPrice);
                }
                result = ExecuteMarketOrder(TradeType.Sell, SymbolName, Qtty * VolPartialClose, label, SL, TP * PipsPartialClose);
                if (result.IsSuccessful)
                {
                    var position = result.Position;
                    Print("Vendido em: {0}", position.EntryPrice);
                }
                trigger = 0;
            }

        }
        //Para cada atualização de tick executa essa rotina
        private void PositionsOnClosed(PositionClosedEventArgs args)
        {

            // the reason for closing can be captured. 
            switch (args.Reason)
            {
                case PositionCloseReason.TakeProfit:
                    var position = Positions.Find("leftover", SymbolName);
                    if (position != null)
                    {
                        //                       var stopLoss = position.EntryPrice;
                        //                       ModifyPosition(position, stopLoss, TP);
                        var stopLoss = position.EntryPrice - 1 * Symbol.PipSize;
                        ModifyPosition(position, stopLoss, position.TakeProfit);
                        Print("First TP hit, SL locked");
                    }
                    break;
                case PositionCloseReason.StopLoss:
                    Print("Position closed as stop loss was hit");
                    break;
                case PositionCloseReason.StopOut:
                    Print("Position closed as it was stopped out");
                    break;

            }
        }
    }
}
