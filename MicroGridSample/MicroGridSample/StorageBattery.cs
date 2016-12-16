using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASYST.ver2
{
    class StorageBattery
    {
        private double[] ChargeCapacity = new double[24];   //(正の数想定)
        private double[] DischargeCapacity = new double[24];    //(負の数想定)
        private double batteryCapacity;
        private double chargeSpeedUpper = 8.55;//DC 19A, 450V
        private double dischargeSpeedUpper = 6.0;//AC 30A, 100V *2
        //充・給電速度は今後

        public object Clone()    //オブジェクトのコピー
        {
            return MemberwiseClone();
        }

        //コンストラクタ
        /// <summary>
        /// 蓄電池のバッテリー
        /// </summary>
        /// <param name="batteryCapacity">蓄電池容量</param>
        public StorageBattery(double batteryCapacity)
        {
            this.batteryCapacity = batteryCapacity;

            for (int i = 0; i < 24; i++)
            {
                ChargeCapacity[i] = batteryCapacity; //初期化時には蓄電池は空
                DischargeCapacity[i] = 0;
            }
        }
        public double getChargeCapacity(int time)
        {
            return ChargeCapacity[time];
        }
        public double getDischargeCapacity(int time)
        {
            return DischargeCapacity[time];
        }

        public override string ToString()
        {
            string str = "StorageBattery\r\n";
            str += "Time, ChargeCapacity, DischargeCapacity \r\n";
            for (int i = 0; i < 24; i++) { str += i + ":00, " + ChargeCapacity[i] + ", " + DischargeCapacity[i] + "\r\n"; }
            return str;
        }


        public double Charge(int time, double Energy)//引数は負の数、返り値は充電できなかった量(負の数)で充電できなければ引数がそのまま戻される
        {
            double retEnergy = Energy;
            bool retTrue = true;
            for (int i = time; i < 24; i++)
            {
                    //求充電量、充電速度、キャパの一番小さいものによって充電量が変わる
                    if (ChargeCapacity[i] >= Math.Abs(Energy) && Math.Abs(Energy) <= chargeSpeedUpper)//求充電量
                    {
                        if (retTrue)
                        {
                            retEnergy = 0;
                        }
                        ChargeCapacity[i] += Energy;
                        DischargeCapacity[i] += Energy;
                    }
                    //最大まで充電
                    else if (Math.Abs(Energy) >= ChargeCapacity[i] && ChargeCapacity[i] <= chargeSpeedUpper)//キャパ最小
                    {
                        if (retTrue)
                        {
                            retEnergy = ChargeCapacity[i] + Energy;
                        }
                        ChargeCapacity[i] = 0;
                        DischargeCapacity[i] = -batteryCapacity;
                    }
                    else if (chargeSpeedUpper <= ChargeCapacity[i] && chargeSpeedUpper < Math.Abs(Energy))//速度最小
                    {
                        if (retTrue)
                        {
                            retEnergy += chargeSpeedUpper;
                        }
                        ChargeCapacity[i] -= chargeSpeedUpper;
                        DischargeCapacity[i] -= chargeSpeedUpper;
                    }
                    retTrue = false;
            }
            return retEnergy;
        }

        public double Discharge(int time, double Energy)//引数は正の数、返り値は給電できなかった量(正の数)
        {
            double retEnergy = Energy;
            bool retTrue = true;
            for (int i = time; i < 24; i++)
            {
                    //求給電量、給電速度、キャパの一番小さいものによって給電量が変わる
                    if (Math.Abs(DischargeCapacity[i]) >= Energy && Energy <= dischargeSpeedUpper) //求給電量最小
                    {
                        if (retTrue)
                        {
                            retEnergy = 0;
                        }
                        ChargeCapacity[i] += Energy;
                        DischargeCapacity[i] += Energy;
                    }
                    else if (Math.Abs(DischargeCapacity[i]) <= Energy && Math.Abs(DischargeCapacity[i]) <= dischargeSpeedUpper)//キャパ最小
                    {
                        if (retTrue)
                        {
                            retEnergy = Energy + DischargeCapacity[i];
                        }
                        ChargeCapacity[i] = batteryCapacity;
                        DischargeCapacity[i] = 0;
                    }
                    else if (Math.Abs(DischargeCapacity[i]) >= dischargeSpeedUpper && Energy >= dischargeSpeedUpper)//速度最小
                    {
                        if (retTrue)
                        {
                            retEnergy -= dischargeSpeedUpper;
                        }
                        ChargeCapacity[i] += dischargeSpeedUpper;
                        DischargeCapacity[i] += dischargeSpeedUpper;
                    }

                    retTrue = false;
            }
            return retEnergy;
        }

    }
}
