using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class NewcBot : Robot
    {
        [Parameter(DefaultValue = 0.0)]
        public double Parameter { get; set; }

        [Parameter("Time Start", DefaultValue = 4, MinValue = 0, Step = 1)]
        public double TimeStart { get; set; }

        [Parameter("Time Stop", DefaultValue = 17, MinValue = 0, Step = 1)]
        public double TimeStop { get; set; }

        [Parameter("Take Profit", DefaultValue = 100, Step = 10)]
        public double TP { get; set; }

        [Parameter("Risco", DefaultValue = 0.1, Step = 0.05)]
        public double risco { get; set; }

        [Parameter("RR", DefaultValue = 1, Step = 0.5)]
        public double RR { get; set; }

        [Parameter("Trava Entradas", DefaultValue = 5, Step = 1)]
        public double TravaEntrada { get; set; }

        [Parameter("Volatitility Detection", DefaultValue = 1, Step = 0.5)]
        public double VolDetector { get; set; }

        private const string label = "Sup Hand Op";
        public bool WaitNextCandle;
        public bool TimeAllowedTrade = false;
        public bool LockedEntry = false;
        public double Qtty;

        protected override void OnStart()
        {
            // Put your initialization logic here
        }

        protected override void OnBar()
        {

            //Registrar horario de inicio e horario de encerramento das operações do dia.
            if (TimeInUtc.Hour >= TimeStart && TimeInUtc.Hour < TimeStop && TimeAllowedTrade == false)
            {
                Print("Robô dentro do horario Ativo - Autorizado a realizar operações");
                TimeAllowedTrade = true;
            }
            if (((TimeInUtc.Hour >= TimeStop && TimeInUtc.Hour <= 23 && TimeInUtc.Minute <= 59) || (TimeInUtc.Hour < TimeStart && TimeInUtc.Hour > 0)) && TimeAllowedTrade == true)
            {
                Print("Robô inativo, resultado das operações de hoje: € ");
                TimeAllowedTrade = false;
            }


            var positioning = Positions.Find(label, SymbolName);

            if (positioning == null && WaitNextCandle == true)
            {
                WaitNextCandle = false;
            }

            // Analise de dados para tomada de decisão      
            if (WaitNextCandle == false && TimeAllowedTrade == true)
            {
                //Calcula o TRUE RANGE do ultimo candle
                var highlow = Math.Round((Bars.HighPrices.Last(1) - Bars.LowPrices.Last(1)) / Symbol.PipSize, 0);
//                var atr = Indicators.AverageTrueRange(15, MovingAverageType.Simple);
//                var atrIndicator = Math.Round((atr.Result.LastValue / Symbol.PipSize), 0);
                if (highlow > VolDetector)
                {
                    var LowClose = Bars.ClosePrices.Last(1) - Bars.LowPrices.Last(1);
                    var HighClose = Bars.HighPrices.Last(1) - Bars.ClosePrices.Last(1);
                    var direction = Math.Round(HighClose / LowClose, 2);
                    Print("Alta volatilidade encontrada! De intensidade: ", direction);
                    if (direction > 3)
                    {
                        LockedEntry = false;
                        Print("Temos uma Venda");

                        var StopLoss = Math.Round(((Bars.HighPrices.Last(1) - Symbol.Ask) / Symbol.PipSize), 0) + 1;
                        Print("O StopLoss para esse trade é: ", StopLoss);

                        Qtty = (Math.Round((Account.Balance * risco) / StopLoss / Symbol.PipValue / 100000, 1)) * 100000;
                        Print("Volume desse trade: {0} ", Qtty);

                        var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, Qtty, label, TravaEntrada, TravaEntrada * RR);
                        if (result.IsSuccessful)
                        {
                            var position = result.Position;
                            Print("Vendido em: {0}", position.EntryPrice);
                            WaitNextCandle = true;
                        }
                    }
                    else if (direction <= 0.3)
                    {
                        LockedEntry = false;
                        Print("Temos uma Compra");

                        var StopLoss = Math.Round(((Symbol.Bid - Bars.LowPrices.Last(1)) / Symbol.PipSize), 0) - 1;
                        Print("O StopLoss para esse trade é: ", StopLoss);

                        Qtty = (Math.Round((Account.Balance * risco) / StopLoss / Symbol.PipValue / 100000, 1)) * 100000;
                        Print("Volume desse trade: {0} ", Qtty);

                        var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, Qtty, label, TravaEntrada, TravaEntrada * RR);
                        if (result.IsSuccessful)
                        {
                            var position = result.Position;
                            Print("Comprado em: {0}", position.EntryPrice);
                            WaitNextCandle = true;
                        }
                    }
                }
            }
        }

        protected override void OnTick()
        {
            foreach (var position in Positions)
            {
                if (position.Pips > TravaEntrada && LockedEntry == false)
                {
                    if (position.TradeType == TradeType.Sell)
                    {
                        ModifyPosition(position, position.EntryPrice - (2 * Symbol.PipSize), position.TakeProfit);
                    }
                    else if (position.TradeType == TradeType.Buy)
                    {
                        ModifyPosition(position, position.EntryPrice + (2 * Symbol.PipSize), position.TakeProfit);
                    }
                    ClosePosition(position, Qtty / 2);
                    LockedEntry = true;
                }
            }
        }
        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
