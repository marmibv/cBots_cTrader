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

        [Parameter("SL", DefaultValue = -10, Step = 1)]
        public double SL { get; set; }

        [Parameter("TP", DefaultValue = 20, Step = 1)]
        public double TP { get; set; }
        
        public double Volume = 0;
        public double SymbolMultiple = 0;
        public double LockedStopLoss;

        protected override void OnStart()
        {
            // Put your initialization logic here
        }

        protected override void OnTick()
        {
            //Fechar posição caso atinja SL ou TP financeiro definido
            foreach (var position in Positions)
            {
                //Carrega dados caracteristicos de cada paridade que esta sendo negociada
                if (position.SymbolName == "EURJPY")
                {
                    Volume = 44000;
                    SymbolMultiple = 0.01;
                }
                else if (position.SymbolName == "GBPUSD")
                {
                    Volume = 32000;
                    SymbolMultiple = 0.0001;
                }
               
                //Rotina de teste e trava de entrada no gain
                if(position.StopLoss != position.EntryPrice && position.Pips > TP / 4)
                {                    
                    ModifyPosition(position, position.EntryPrice, position.TakeProfit);
                    Print("SL locked: ", position.SymbolName);                    
                }
                   
                //Trava que paga os comissão + spread
                if(position.Pips < 1 && position.StopLoss == position.EntryPrice )
                    {
                        ClosePosition(position);
                        Print("Fechada na trava: ", position.SymbolName);
                    }
                
                //Fechamento parcial da posição quando metade do take for atingido
                if (position.Pips > TP / 2 && position.VolumeInUnits == Volume)
                {
                    ModifyPosition(position, position.VolumeInUnits / 2);
                    Print("Primeiro TP: ", position.SymbolName);
                }

                //Encerramento da operação por TP
                if (position.Pips > TP)
                {
                    ClosePosition(position);
                    Print("Fechada por Take Profit: ", position.SymbolName);
                }

                //Encerramento da operação por SL
                if (position.Pips < SL)
                {
                    ClosePosition(position);
                    Print("Fechada por Stop Loss: ", position.SymbolName);
                }
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
