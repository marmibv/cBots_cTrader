// -------------------------------------------------------------------------------------------------
//
//    Código de analise de mercado, abertura e fechamento de operações conforme segue:
//
//    O codigo aguarda a ruptura de máxima ou minima do candle anterior para entrar com Compra na máxima e Venda na minima.
//    
//    Sistema usa duas médias móveis para filtrar as entradas. Rapida sobre a Lenta indica compra. Lenta sobre a rápida indica venda.
//    
//    Código permite a configuração do horário diário de trade. Start and Finish.
//
//    Código permite o fechamento parcial da order aberta, com % de pips até o TP e % de volume da OP para definir o fechamento parcial
//
//    As otimizações e backtests que rodei indicam:
//    Valores proximos de TP e SL, ou TP>SL costumam trazer a probabilidade de retorno pro positivo
//    Pares diferentes se comportam de formas diferente, em horários diferentes, com volatilidade diferente.
//    
//    Notificações para abertura das ordens ainda estão pendentes, mas as de fechamento o proprio sistema já faz.
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
    public class BreakoutH1comTPparcial : Robot
    {
        [Parameter("MA Type", DefaultValue = "Exponential")]
        public MovingAverageType MAType { get; set; }

        [Parameter()]
        public DataSeries SourceSeries { get; set; }

        [Parameter("Slow MA", DefaultValue = 24)]
        public int SlowPeriods { get; set; }

        [Parameter("Fast MA", DefaultValue = 12)]
        public int FastPeriods { get; set; }

        [Parameter("Quantity (Lots)", DefaultValue = 10000, MinValue = 10000, Step = 10000)]
        public double Quantity { get; set; }

        [Parameter("Stop Loss", DefaultValue = 15, Step = 1)]
        public double SL { get; set; }

        [Parameter("RR", DefaultValue = 2, Step = 0.1)]
        public double RR { get; set; }

        [Parameter("Time Start", DefaultValue = 5, MinValue = 0, Step = 1)]
        public double TimeStart { get; set; }

        [Parameter("Time Stop", DefaultValue = 15, MinValue = 0, Step = 1)]
        public double TimeStop { get; set; }

        [Parameter("VolPartialClose", DefaultValue = 0.6)]
        public double VolPartialClose { get; set; }

        [Parameter("PipsPartialClose", DefaultValue = 0.6)]
        public double PipsPartialClose { get; set; }

        [Parameter("Daily Limit Loss", DefaultValue = -100, Step = 100)]
        public double DailyLimitLoss { get; set; }

        [Parameter("Daily Limit Gain", DefaultValue = 300, Step = 100)]
        public double DailyLimitGain { get; set; }

        [Parameter("CF Size", DefaultValue = 15, MinValue = 5, Step = 1)]
        public double CFsize { get; set; }

        private const string label = "Goldmine";

        private MovingAverage slowMa;
        private MovingAverage fastMa;

        public bool TimeAllowedTrade = false;
        public bool WaitNextCandle = true;
        public bool LimitGainLoss = false;
        public double DailyNet = 0;
        public string direction = "";
        public int BarCounter = 0;

        //Inicializa os indicadores quando aciona o robo
        protected override void OnStart()
        {
            Print("Programa iniciado: ", TimeInUtc);

            fastMa = Indicators.MovingAverage(SourceSeries, FastPeriods, MAType);
            slowMa = Indicators.MovingAverage(SourceSeries, SlowPeriods, MAType);
        }


//Para cada nova barra executa essa rotina
        protected override void OnBar()
        {
            var longPosition1 = Positions.Find(label, SymbolName, TradeType.Buy);
            var shortPosition1 = Positions.Find(label, SymbolName, TradeType.Sell);

            //Registrar horario de inicio e horario de encerramento das operações do dia.
            if (TimeInUtc.Hour >= TimeStart && TimeInUtc.Hour < TimeStop && TimeAllowedTrade == false)
            {
                Print("Robô dentro do horario Ativo - Autorizado a realizar operações");
                TimeAllowedTrade = true;
            }
            if (((TimeInUtc.Hour >= TimeStop && TimeInUtc.Hour <= 23 && TimeInUtc.Minute <= 59) || (TimeInUtc.Hour < TimeStart && TimeInUtc.Hour > 0)) && TimeAllowedTrade == true)
            {
                Print("Robô inativo, resultado das operações de hoje: € {0}", DailyNet);
                TimeAllowedTrade = false;
            }

            if (LimitGainLoss == true)
            {
                if (TimeInUtc.Hour == 0 && TimeInUtc.Minute == 0)
                {
                    Print("Reset dos limites do dia anterior");
                    LimitGainLoss = false;
                }
            }
            //libera p abrir operação no maximo 2 candles apos o CF
            if (BarCounter > 0)
            {
                BarCounter--;
            }
            else if (BarCounter <= 0)
            {
                WaitNextCandle = true;
                direction = "";
            }

            if (longPosition1 == null && shortPosition1 == null && TimeAllowedTrade == true && WaitNextCandle == true && LimitGainLoss == false)
            {
                var difmaxmin = Math.Round(((Bars.ClosePrices.Last(1) - Bars.OpenPrices.Last(1)) / Symbol.PipSize), 0);
                Print("Procurando por candle de força!");
                if (difmaxmin < 0)
                {
                    difmaxmin = -difmaxmin;
                    direction = "venda";
                }
                else
                    direction = "compra";

                if (difmaxmin > CFsize)
                {
                    WaitNextCandle = false;
                    Print("Candle de força encontrado, aguardando operação no rompimento!");
                    BarCounter = 2;
                }

            }

        }

//Para cada novo tick de mudança do preço executa essa rotina    
        protected override void OnTick()
        {
            var longPosition = Positions.Find(label, SymbolName, TradeType.Buy);
            var shortPosition = Positions.Find(label, SymbolName, TradeType.Sell);

            var currentSlowMa = slowMa.Result.Last(1);
            var currentFastMa = fastMa.Result.Last(1);

//Rotina que define se o preço parcial de saida foi atingido fechar parte da posição e travar a entrada
            if (longPosition != null && longPosition.Pips > (SL * RR) * PipsPartialClose && longPosition.Quantity == (Quantity / 100000))
            {
                ClosePosition(longPosition, Quantity * VolPartialClose);
                var stopLoss = longPosition.EntryPrice + 1 * Symbol.PipSize;
                ModifyPosition(longPosition, stopLoss, longPosition.TakeProfit);
                Print("First TP hit, SL locked");
            }

            if (shortPosition != null && shortPosition.Pips > (SL * RR) * PipsPartialClose && shortPosition.Quantity == (Quantity / 100000))
            {
                ClosePosition(shortPosition, Quantity * VolPartialClose);
                var stopLoss = shortPosition.EntryPrice - 1 * Symbol.PipSize;
                ModifyPosition(shortPosition, stopLoss, shortPosition.TakeProfit);
                Print("First TP hit, SL locked");
            }

//Rotina de calculos dos resultados do dia, define limite de perdas e ganhos e encerra as operações para o dia.
            double DailyNet1 = Positions.Sum(p => p.NetProfit);
            double DailyNet2 = History.Where(x => x.ClosingTime.Date == Time.Date).Sum(x => x.NetProfit);
            DailyNet = DailyNet1 + DailyNet2;

            if ((DailyNet < DailyLimitLoss || DailyNet > DailyLimitGain) && LimitGainLoss == false)
            {
                LimitGainLoss = true;
                var openposition = Positions.Find(label, SymbolName);
                if (openposition != null)
                {
                    ClosePosition(openposition);
                    Print("Posições fechadas, CHEGA POR HOJE");
                }
                if (DailyNet < DailyLimitLoss)
                    Print("Estourou limite diario de perdas!!! {0}", DailyNet);
                if (DailyNet > DailyLimitGain)
                    Print("Estourou limite diario de ganhos!!! {0}", DailyNet);
            }

//Teste do sinal de entrada para compra e venda
            if (longPosition == null && shortPosition == null && WaitNextCandle == false && TimeAllowedTrade == true && LimitGainLoss == false)
            {

                //
                if (Bars.LowPrices.Last(1) > Symbol.Ask && currentFastMa < currentSlowMa && direction == "venda")
                {
                    var result = ExecuteMarketOrder(TradeType.Sell, SymbolName, Quantity, label, SL, SL * RR);
                    if (result.IsSuccessful)
                    {
                        var position = result.Position;
                        Print("Vendido em: {0}", position.EntryPrice);
                        WaitNextCandle = true;
                    }

                }

                //
                if (Bars.HighPrices.Last(1) < Symbol.Bid && currentFastMa > currentSlowMa && direction == "compra")
                {
                    var result = ExecuteMarketOrder(TradeType.Buy, SymbolName, Quantity, label, SL, SL * RR);
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
}
