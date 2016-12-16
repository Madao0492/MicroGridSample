using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroGridSample
{
    class MicroGridOneday
    {
        //需要電力量、予測発電量、需要発電差、EV充給電後の需要、電力供給元実績
        private double[] powerLog = new double[24];
        private double[] generation = new double[24];
        private double[] differencePG = new double[24];
        private double[] differencePGEV = new double[24];
        private int[] demand = new int[24];
        //ピーク時間算出用
        private Dictionary<int, int> demandTime = new Dictionary<int, int>(); //ピーク時間の需要を入れると、時間を返す
        int peakPattern;

        //屋上総面積[m^2]
        private double square;

        //ピーク時間かどうか(ピーク時間±1時間)、ピーク時の需要発電差の総量
        private bool[] peakTime = new bool[24];

        //バッテリー充電給電前後
        private MicroGridBattery mgbBef;
        private MicroGridBattery mgbAft;

        #region getset
        //getset
        public double[] PowerLog
        {
            set { this.powerLog = value; }
            get { return this.powerLog; }
        }
        public double[] Generation
        {
            set { this.generation = value; }
            get { return this.generation; }
        }
        public double[] DifferencePG
        {
            set { this.differencePG = value; }
            get { return this.differencePG; }
        }
        public double[] DifferencePGEV
        {
            set { this.differencePGEV = value; }
            get { return this.differencePGEV; }
        }
        public int[] Demand
        {
            set { this.demand = value; }
            get { return this.demand; }
        }
        public Boolean[] Peaktime
        {
            set { this.peakTime = value; }
            get { return this.peakTime; }
        }

        public double Square
        {
            set { this.square = value; }
            get { return this.square; }
        }
        public MicroGridBattery MicroGridBatteryBefore
        {
            set { this.mgbBef = value; }
            get { return this.mgbBef; }
        }
        public MicroGridBattery MicroGridBatteryAfter
        {
            set { this.mgbAft = value; }
            get { return this.mgbAft; }
        }
        #endregion

        //コンストラクタ
        /// <summary>
        /// マイクログリッドの需要電力量・発電量・需要発電差・供給元実績・EV充給電推移のデータセットを引数として、グリッドのシミュレート結果を導出する
        /// </summary>
        /// <param name="square">太陽光発電システムの面積。計算には未使用</param>
        /// <param name="powerLog">需要電力量</param>
        /// <param name="generation">発電量</param>
        /// <param name="differencePG">需要発電差</param>
        /// <param name="demand">供給元実績</param>
        /// <param name="mgb">EV充給電推移のデータセット</param>
        public MicroGridOneday(double square, double[] powerLog, double[] generation, double[] differencePG, int[] demand, MicroGridBattery mgb, int peakPattern)
        {
            this.square = square;
            this.powerLog = powerLog;
            this.generation = generation;
            this.differencePG = differencePG;
            this.demand = demand;
            int j = 1;
            for (int i = 0; i < 24; i++)
            {
                try
                {
                    this.differencePGEV[i] = differencePG[i];
                    this.demandTime.Add(demand[i], i);
                }
                catch (System.ArgumentException e)
                {
                    this.demandTime.Add(demand[i] + j, i);
                    j++;
                }
            }



            this.peakPattern = peakPattern;

            for (int i = 0; i < 24; i++)
            {
                peakTime[i] = IsPeakTime(i, peakPattern);
            }
            this.mgbBef = (MicroGridBattery)mgb.Clone();
            this.mgbAft = (MicroGridBattery)mgb.Clone();

            UpdateDifferencePGEV();
        }

        public override string ToString()
        {
            string str = "MicroGridOneday, PeakPattern:" + peakPattern + " \r\n";
            str += "DateTime, Square, PowerLogEnergy, Generation, DifferencePG, DifferencePGEV, Demand, PeakOrNot \r\n";
            for (int i = 0; i < 24; i++)
            {
                str += i + ":00, " + square + ", " + powerLog[i] + ", " + generation[i] + ", " + differencePG[i] + ", " + differencePGEV[i] + ", " + demand[i] + ", " + peakTime[i] + " \r\n";
            }
            return str;
        }

        //ピーク時間かどうか
        public bool IsPeakTime(int time, int peakPattern)
        {
            //ピーク時間ソート
            int[] demandtemp = new int[24];
            for (int i = 0; i < 24; i++) { demandtemp[i] = demand[i]; }
            Array.Sort(demandtemp);
            Array.Reverse(demandtemp);

            switch (peakPattern)
            {
                #region パターンごとのピーク判定
                case 0:
                    { //最大Demandの時間±1時間
                        int peakTime = demandTime[demandtemp[0]];
                        if (Math.Abs(time - peakTime) <= 1)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                case 1:
                    { //最大Demandの時間±2時間
                        int peakTime = demandTime[demandtemp[0]];
                        if (Math.Abs(time - peakTime) <= 2)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                case 2: //供給元実績降順2時間
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            if (time == demandTime[demandtemp[i]])
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                case 3: //供給元実績降順3時間
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (time == demandTime[demandtemp[i]])
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                case 4: //供給元実績降順4時間
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (time == demandTime[demandtemp[i]])
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                default:
                    return false;
                    #endregion
            }
        }

        //ある1時間の余剰量
        public double GetOverGenerationOnehour(int time)
        {
            if (differencePG[time] < 0) { return differencePG[time]; }
            else return 0;
        }

        //1時間の求給電量
        public double GetDischargeEnergy(int time, double DischargeCapacity)
        {
            //ピーク時間でなければ0
            if (!peakTime[time]) { return 0; }

            //残りのピーク時間、ピーク時differencePGの合計
            int peakTimeCount = 0;
            double peakDifferencePGEnergy = 0;
            for (int i = time; i < 24; i++)
            {
                if (peakTime[i])
                {
                    Console.WriteLine("TIME : " + i + " dif : " + differencePG[i]);
                    peakTimeCount++;
                    peakDifferencePGEnergy += differencePG[i];
                    Console.WriteLine("TIME : " + i + " peak: " + peakDifferencePGEnergy);
                    //次のループへ
                    continue;
                }
            }
            Console.WriteLine(DischargeCapacity);

            //目標differencePGEV、求給電量
            double aimDifferencePGEV = (peakDifferencePGEnergy + DischargeCapacity) / peakTimeCount;
            if (aimDifferencePGEV > 0) { return differencePG[time] - aimDifferencePGEV; }
            else return differencePG[time];
        }

        //differencePGEVの更新
        private void UpdateDifferencePGEV()
        {
            //余剰電力・ピーク時間の求給電量を取得、EVBatteryの計算、グリッド更新
            for (int i = 0; i < 24; i++)
            {
                double overGeneration = GetOverGenerationOnehour(i);
                double peakTimeDischarge = GetDischargeEnergy(i, mgbAft.GetAllDischargeCapacity(i));

                //Console.WriteLine("TIME : " + i + " OG : " + overGeneration + " PD : " + peakTimeDischarge);

                //充電出来なかった量・給電出来なかった量
                double notcharge = overGeneration;
                double notdischarge = peakTimeDischarge;

                if (overGeneration < 0)
                {
                    notcharge = mgbAft.ChargeBatteries(i, overGeneration);
                    //充電できなかった量が、充電後のPGEV
                    differencePGEV[i] = notcharge;
                    //Console.WriteLine("TIME : " + i + " NC : " + notcharge + " diff : " + differencePGEV[i]);
                }
                if (peakTimeDischarge > 0)
                {
                    notdischarge = mgbAft.DischargeBatteries(i, peakTimeDischarge);
                    //differencePG - 求給電量 + 給電できなかった量 = 給電後のPGEV
                    differencePGEV[i] = differencePG[i] - peakTimeDischarge + notdischarge;
                    //Console.WriteLine("TIME : " + i + " NDC : " + notdischarge + " diff : " + differencePGEV[i]);
                }
            }
        }

        /////////////////////////////////////
        //ピーク時充給電前電力量
        public double GetBeforePeakEnergy()
        {
            double peakBeforeEnergy = 0;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i])
                    peakBeforeEnergy += powerLog[i];
                //peakBeforeEnergy += differencePG[i];
            }
            if (peakBeforeEnergy < 0) return 0;
            return peakBeforeEnergy;
        }
        //ピーク時充給電後電力量
        public double GetAfterPeakEnergy()
        {
            double peakAfterEnergy = 0;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i])
                    peakAfterEnergy += differencePGEV[i];
            }
            if (peakAfterEnergy < 0) return 0;
            return peakAfterEnergy;
        }

        //ピーク時間最大電力W
        public double GetPeakBeforeMaxWatt()
        {
            double peakWatt = 0;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i] && peakWatt < powerLog[i])
                    peakWatt = powerLog[i];
            }
            return peakWatt;
        }
        public double GetPeakAfterMaxWatt()
        {
            double peakWatt = 0;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i] && peakWatt < differencePGEV[i])
                    peakWatt = differencePGEV[i];
            }
            return peakWatt;
        }
        public double GetPeakDifferencePGMaxWatt()
        {
            double peakWatt = double.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i] && peakWatt > differencePG[i])
                    peakWatt = differencePG[i];
            }
            return peakWatt;
        }
        public double GetPeakDifferencePGEVMaxWatt()
        {
            double peakWatt = double.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i] && peakWatt > differencePGEV[i])
                    peakWatt = differencePGEV[i];
            }
            return peakWatt;
        }

        //太陽光のみでのピークカット効果
        public double GetBeforePeakDifferencePGEnergy()
        {
            double peakBeforeDifferencePGEnergy = 0;
            for (int i = 0; i < 24; i++)
            {
                if (peakTime[i])
                    peakBeforeDifferencePGEnergy += differencePG[i];
                //peakBeforeEnergy += differencePG[i];
            }

            if (peakBeforeDifferencePGEnergy < 0)
                return 0;

            return peakBeforeDifferencePGEnergy;
        }



        //余剰発電総量
        public double GetBeforeOverGeneration()
        {
            double overGeneration = 0;
            for (int i = 0; i < 24; i++)
            {
                if (differencePG[i] < 0)
                    overGeneration += differencePG[i];
            }
            return overGeneration;
        }
        public double GetAfterOverGeneration()
        {
            double overGeneration = 0;
            for (int i = 0; i < 24; i++)
            {
                if (differencePGEV[i] < 0)
                    overGeneration += differencePGEV[i];
            }
            return overGeneration;
        }
    }
}
